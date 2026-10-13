using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Globalization;
using System.Threading;
namespace NautaReconnect {
    public sealed class TrafficCounter {
        public string Id;
        public long Received,Sent;
    }
    public sealed class RateMonitor:IDisposable {
        readonly Dictionary<string,TrafficCounter> previous=new Dictionary<string,TrafficCounter>();
        double previousTime;
        bool initialized;
        NetworkInterface[] adapters=new NetworkInterface[0];
        readonly List<TrafficCounter> counters=new List<TrafficCounter>();
        double nextScan;
        int dirty=1;
        bool watching,disposed;
        public event EventHandler TopologyChanged;
        void AddressChanged(object sender,EventArgs args){MarkChanged();}
        void AvailabilityChanged(object sender,NetworkAvailabilityEventArgs args){MarkChanged();}
        void MarkChanged(){Interlocked.Exchange(ref dirty,1);var changed=TopologyChanged;if(changed!=null)changed(this,EventArgs.Empty);}
        public List<TrafficCounter> ReadCached(){
            if(disposed)throw new ObjectDisposedException("RateMonitor");
            if(!watching){NetworkChange.NetworkAddressChanged+=AddressChanged;NetworkChange.NetworkAvailabilityChanged+=AvailabilityChanged;watching=true;}
            double now=NowSeconds();
            if(Interlocked.Exchange(ref dirty,0)!=0||now>=nextScan){
                var selected=new List<NetworkInterface>();foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())if(nic.OperationalStatus==OperationalStatus.Up&&IsPhysical(nic.NetworkInterfaceType,nic.Description))selected.Add(nic);
                adapters=selected.ToArray();nextScan=now+30;
            }
            counters.Clear();foreach(var nic in adapters){try{var stats=nic.GetIPv4Statistics();counters.Add(new TrafficCounter{Id=nic.Id,Received=stats.BytesReceived,Sent=stats.BytesSent});}catch(NetworkInformationException){Interlocked.Exchange(ref dirty,1);}}
            return counters;
        }
        public Telemetry Sample(IList<TrafficCounter> counters,double nowSeconds){
            var result=new Telemetry();double elapsed=nowSeconds-previousTime;
            if(initialized&&elapsed>0){
                foreach(var counter in counters){TrafficCounter prior;if(previous.TryGetValue(counter.Id,out prior)){
                    result.DownloadBytesPerSecond+=Math.Max(0,counter.Received-prior.Received)/elapsed;
                    result.UploadBytesPerSecond+=Math.Max(0,counter.Sent-prior.Sent)/elapsed;
                }}
                result.RatesReady=true;
            }
            var active=new HashSet<string>();foreach(var counter in counters){active.Add(counter.Id);TrafficCounter entry;if(!previous.TryGetValue(counter.Id,out entry)){entry=new TrafficCounter{Id=counter.Id};previous[counter.Id]=entry;}entry.Received=counter.Received;entry.Sent=counter.Sent;}
            if(previous.Count>active.Count){var removed=new List<string>();foreach(var id in previous.Keys)if(!active.Contains(id))removed.Add(id);foreach(var id in removed)previous.Remove(id);}
            previousTime=nowSeconds;initialized=true;return result;
        }
        public static bool IsPhysical(NetworkInterfaceType type,string description){
            if(type!=NetworkInterfaceType.Ethernet&&type!=NetworkInterfaceType.Wireless80211)return false;
            string value=(description??"").ToLowerInvariant();
            return !value.Contains("wireguard")&&!value.Contains("wintun")&&!value.Contains("virtual")&&!value.Contains("hyper-v")&&!value.Contains("tap-")&&!value.Contains("vpn");
        }
        public static List<TrafficCounter> Read(){
            var counters=new List<TrafficCounter>();
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()){
                if(nic.OperationalStatus!=OperationalStatus.Up||!IsPhysical(nic.NetworkInterfaceType,nic.Description))continue;
                try{var stats=nic.GetIPv4Statistics();counters.Add(new TrafficCounter{Id=nic.Id,Received=stats.BytesReceived,Sent=stats.BytesSent});}catch(NetworkInformationException){}
            }
            return counters;
        }
        public static string Speed(double bytesPerSecond){
            double bits=Math.Max(0,bytesPerSecond)*8;
            if(bits>=1000000000)return (bits/1000000000).ToString("0.00",CultureInfo.InvariantCulture)+" Gb/s";
            if(bits>=1000000)return (bits/1000000).ToString("0.00",CultureInfo.InvariantCulture)+" Mb/s";
            return (bits/1000).ToString("0",CultureInfo.InvariantCulture)+" Kb/s";
        }
        public static string TimeText(TimeSpan? value){return value.HasValue?((long)value.Value.TotalHours).ToString(CultureInfo.InvariantCulture)+"h "+value.Value.Minutes.ToString("00",CultureInfo.InvariantCulture)+"m":"—h —m";}
        public static double NowSeconds(){return (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;}
        public void Dispose(){if(disposed)return;disposed=true;if(watching){NetworkChange.NetworkAddressChanged-=AddressChanged;NetworkChange.NetworkAvailabilityChanged-=AvailabilityChanged;watching=false;}adapters=new NetworkInterface[0];counters.Clear();previous.Clear();}
    }
}
