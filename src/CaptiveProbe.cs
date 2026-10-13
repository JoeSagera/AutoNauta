using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace NautaReconnect {
    public enum CaptiveState { Unknown,Internet,LoginRequired }
    public static class CaptiveProbe {
        public static CaptiveState Check(NetworkLink router,CancellationToken token){
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
                deadline.CancelAfter(6000);return CheckWithinDeadline(router,deadline.Token,token);
            }
        }
        static CaptiveState CheckWithinDeadline(NetworkLink router,CancellationToken token,CancellationToken caller){
            try{
                IPAddress target=Networks.RouterAddress(router,"www.msftconnecttest.com","",token);
                    using(var client=new TcpClient(new IPEndPoint(router.LocalIp,0)))
                    using(token.Register(delegate{try{client.Close();}catch{}})){
                        SocketPath.Select(client.Client,router);
                        SocketPath.Connect(client,target,80,token,3000);
                        client.ReceiveTimeout=3000;client.SendTimeout=3000;
                        using(var stream=client.GetStream()){
                            byte[] request=Encoding.ASCII.GetBytes("GET /connecttest.txt HTTP/1.1\r\nHost: www.msftconnecttest.com\r\nConnection: close\r\nUser-Agent: NautaReconnect/1.0\r\n\r\n");stream.Write(request,0,request.Length);
                            using(var response=new MemoryStream()){byte[] buffer=new byte[1024];int count;
                            while(response.Length<8192&&(count=stream.Read(buffer,0,buffer.Length))>0){response.Write(buffer,0,count);token.ThrowIfCancellationRequested();}
                            return Classify(Encoding.UTF8.GetString(response.ToArray()));}
                        }
                    }
            }catch{caller.ThrowIfCancellationRequested();return CaptiveState.Unknown;}
        }
        public static CaptiveState Classify(string response){
            int end=response.IndexOf("\r\n\r\n",StringComparison.Ordinal);if(end<0)return CaptiveState.Unknown;
            string[] headers=response.Substring(0,end).Split(new string[]{"\r\n"},StringSplitOptions.None);
            string[] status=headers[0].Split(' ');int code;if(status.Length<2||!status[0].StartsWith("HTTP/1.",StringComparison.Ordinal)||!Int32.TryParse(status[1],out code))return CaptiveState.Unknown;
            if(code==200&&response.Substring(end+4).Trim()=="Microsoft Connect Test")return CaptiveState.Internet;
            if(code!=301&&code!=302&&code!=303&&code!=307&&code!=308)return CaptiveState.Unknown;
            string location=null;
            for(int i=1;i<headers.Length;i++)if(headers[i].StartsWith("Location:",StringComparison.OrdinalIgnoreCase)){if(location!=null)return CaptiveState.Unknown;location=headers[i].Substring(9).Trim();}
            Uri uri;if(location!=null&&Uri.TryCreate(location,UriKind.Absolute,out uri)&&PortalParser.Trusted(uri))return CaptiveState.LoginRequired;
            return CaptiveState.Unknown;
        }
    }
}
