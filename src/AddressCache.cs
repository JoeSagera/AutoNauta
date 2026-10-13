using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
namespace NautaReconnect {
    public sealed class AddressCache {
        sealed class Entry{public IPAddress[] Addresses;public double Expires;}
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        readonly object gate=new object();
        readonly Func<string,CancellationToken,IPAddress[]> resolve;
        readonly Func<double> clock;
        int generation;
        public AddressCache(Func<string,CancellationToken,IPAddress[]> resolver,Func<double> now){resolve=resolver;clock=now;}
        public IPAddress[] Get(string host,CancellationToken token){
            token.ThrowIfCancellationRequested();int version;
            lock(gate){Entry entry;if(entries.TryGetValue(host,out entry)&&entry.Expires>clock())return entry.Addresses;version=generation;}
            var result=resolve(host,token);token.ThrowIfCancellationRequested();
            lock(gate){if(version==generation&&result.Length>0){if(entries.Count>=8)entries.Clear();entries[host]=new Entry{Addresses=result,Expires=clock()+120};}}
            return result;
        }
        public void Invalidate(string host){lock(gate){entries.Remove(host);generation++;}}
        public void Clear(){lock(gate){entries.Clear();generation++;}}
    }
}
