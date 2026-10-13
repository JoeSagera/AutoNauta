using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;
namespace NautaReconnect {
    public sealed class PageResponse { public Uri Uri; public int Code; public string Body; }
    public sealed class NetworkFailure : Exception {
        public bool Blocked, Certificate;
        public NetworkFailure(string message,Exception inner):base(message,inner) {
            bool timeout=false;
            for(Exception e=inner;e!=null;e=e.InnerException) {
                if(e is RequestTimeoutException)timeout=true;
                var socket=e as SocketException; if(socket!=null && socket.SocketErrorCode==SocketError.AccessDenied) Blocked=true;
                var web=e as WebException; if(web!=null && (web.Status==WebExceptionStatus.TrustFailure||web.Status==WebExceptionStatus.SecureChannelFailure)) Certificate=true;
                if(e is AuthenticationException) Certificate=true;
            }
            if(timeout)Certificate=false;
        }
    }
    public static class Networks {
        public static readonly AddressCache Addresses=new AddressCache(ResolveFresh,RateMonitor.NowSeconds);
        static bool Tunnel(NetworkInterface nic) {
            string s=(nic.Name+" "+nic.Description).ToLowerInvariant();
            return s.Contains("wireguard")||s.Contains("wintun")||nic.NetworkInterfaceType==NetworkInterfaceType.Tunnel;
        }
        public static NetworkLink Router() {
            var found=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up && !Tunnel(n) && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback);
            foreach(var nic in found.OrderBy(n=>n.NetworkInterfaceType==NetworkInterfaceType.Ethernet?0:1)) {
                var ip=nic.GetIPProperties(); var local=ip.UnicastAddresses.Select(x=>x.Address).FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork && !x.ToString().StartsWith("169.254."));
                var gateway=ip.GatewayAddresses.Select(x=>x.Address).FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork&&!x.Equals(IPAddress.Any));
                if(local!=null&&gateway!=null) return new NetworkLink {Name=nic.Name,LocalIp=local,Gateway=gateway,Index=ip.GetIPv4Properties().Index};
            }
            throw new InvalidOperationException("No se encontró una conexión activa con el router. Comprueba Ethernet o Wi-Fi.");
        }
        public static NetworkLink Vpn() {
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&Tunnel(n))) {
                var ip=nic.GetIPProperties(); var local=ip.UnicastAddresses.Select(x=>x.Address).FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork&&!x.ToString().StartsWith("169.254."));
                if(local!=null) return new NetworkLink {Name=nic.Name,LocalIp=local,Index=ip.GetIPv4Properties().Index};
            }
            return null;
        }
        public static IPAddress[] Resolve(string host,CancellationToken token) {
            return Addresses.Get(host,token);
        }
        static IPAddress[] ResolveFresh(string host,CancellationToken token){
            token.ThrowIfCancellationRequested();
            var task=Task<IPAddress[]>.Factory.FromAsync((callback,state)=>Dns.BeginGetHostAddresses(host,callback,state),Dns.EndGetHostAddresses,null);
            try{if(!task.Wait(3000,token))throw new IOException("La resolución DNS no respondió a tiempo.");return task.Result;}
            catch(AggregateException error){throw new IOException("No se pudo resolver el sitio.",error.GetBaseException());}
            finally{
                if(task.IsCompleted)task.Dispose();
                else task.ContinueWith(completed=>{var ignored=completed.Exception;completed.Dispose();},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            }
        }
        static int Read16(byte[] b,int pos) { return (b[pos]<<8)|b[pos+1]; }
        static int SkipName(byte[] b,int pos) {
            while(pos<b.Length) { int len=b[pos++]; if(len==0)return pos; if((len&192)==192)return pos+1; if(len>63||pos+len>b.Length)throw new IOException("DNS inválido"); pos+=len; }
            throw new IOException("DNS incompleto");
        }
        public static IPAddress PortalAddress(NetworkLink link,string cached,CancellationToken token) {
            return RouterAddress(link,"secure.etecsa.net",cached,token);
        }
        public static IPAddress RouterAddress(NetworkLink link,string host,string cached,CancellationToken token) {
            token.ThrowIfCancellationRequested();
            try {
                ushort id=(ushort)new Random().Next(1,65535); var data=new MemoryStream();
                data.Write(new byte[]{(byte)(id>>8),(byte)id,1,0,0,1,0,0,0,0,0,0},0,12);
                foreach(string label in host.Split('.')) { byte[] bytes=Encoding.ASCII.GetBytes(label); data.WriteByte((byte)bytes.Length); data.Write(bytes,0,bytes.Length); }
                data.Write(new byte[]{0,0,1,0,1},0,5);
                using(var sock=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)) {
                    SocketPath.Select(sock,link);
                    sock.ReceiveTimeout=2000; sock.SendTimeout=2000; sock.Bind(new IPEndPoint(link.LocalIp,0));
                    using(token.Register(delegate{try{sock.Close();}catch{}})) {
                        sock.Connect(new IPEndPoint(link.Gateway,53)); sock.Send(data.ToArray()); byte[] answer=new byte[4096]; int length=sock.Receive(answer);
                        if(length<12||Read16(answer,0)!=id||(answer[3]&15)!=0)throw new IOException("DNS sin respuesta válida");
                        int p=12; int questions=Read16(answer,4),count=Read16(answer,6);
                        for(int i=0;i<questions;i++){p=SkipName(answer,p)+4;if(p>length)throw new IOException();}
                        for(int i=0;i<count;i++){p=SkipName(answer,p);if(p+10>length)throw new IOException();int type=Read16(answer,p),cls=Read16(answer,p+2),size=Read16(answer,p+8);p+=10;if(p+size>length)throw new IOException();if(type==1&&cls==1&&size==4)return new IPAddress(new byte[]{answer[p],answer[p+1],answer[p+2],answer[p+3]});p+=size;}
                    }
                }
            } catch { token.ThrowIfCancellationRequested(); }
            IPAddress fallback;
            if(IPAddress.TryParse(cached,out fallback)&&fallback.AddressFamily==AddressFamily.InterNetwork)return fallback;
            throw new IOException("No se pudo resolver el portal mediante el router.");
        }
    }
    public sealed class PortalClient {
        readonly CookieContainer cookies=new CookieContainer();
        readonly NetworkLink router;
        readonly IPAddress portal;
        public static readonly Uri Home=new Uri("https://secure.etecsa.net:8443/");
        public static bool InformationEndpoint(Uri uri){return PortalParser.Trusted(uri)&&uri.AbsolutePath.Trim('/').Equals("EtecsaQueryServlet",StringComparison.Ordinal);}
        public PortalClient(NetworkLink link,IPAddress target) { router=link; portal=target; }
        public PageResponse Get(CancellationToken token) { return Send(Home,null,token); }
        public PageResponse Login(PortalPage page,string user,string password,CancellationToken token) {
            if(!page.Login||!PortalParser.Trusted(page.Action)) throw new InvalidOperationException("Formulario no autorizado.");
            var fields=new System.Collections.Generic.Dictionary<string,string>(page.Fields); fields[page.UserField]=user; fields[page.PasswordField]=password;
            token.ThrowIfCancellationRequested();
            return Send(page.Action,PortalParser.Encode(fields),token);
        }
        public PageResponse QueryAccount(PortalPage page,string user,string password,CancellationToken token){
            if(!page.Login)throw new InvalidOperationException("No se reconoció el formulario para consultar la cuenta.");
            var fields=new System.Collections.Generic.Dictionary<string,string>(page.Fields);fields[page.UserField]=user;fields[page.PasswordField]=password;
            token.ThrowIfCancellationRequested();return Send(new Uri(Home,"/EtecsaQueryServlet"),PortalParser.Encode(fields),token,true);
        }
        public PageResponse GetLeftTime(NautaSession session,CancellationToken token){
            if(session==null||!session.ValidFor(session.Username))throw new InvalidOperationException("No hay un contador de sesión disponible.");
            var fields=new System.Collections.Generic.Dictionary<string,string>();fields["username"]=session.Username;fields["CSRFHW"]=session.Csrf;fields["ATTRIBUTE_UUID"]=session.Uuid;fields["op"]="getLeftTime";
            token.ThrowIfCancellationRequested();return Send(new Uri(Home,"/EtecsaQueryServlet"),PortalParser.Encode(fields),token,true);
        }
        PageResponse Send(Uri uri,string body,CancellationToken token,bool informationOnly=false) {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
                deadline.CancelAfter(15000);
                try{return SendRedirects(uri,body,deadline.Token,informationOnly);}
                catch(Exception error){token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new NetworkFailure("El portal no respondió a tiempo.",new RequestTimeoutException(error));throw;}
            }
        }
        PageResponse SendRedirects(Uri uri,string body,CancellationToken token,bool informationOnly){
            for(int redirects=0;redirects<6;redirects++) {
                token.ThrowIfCancellationRequested();
                if(!PortalParser.Trusted(uri))throw new InvalidOperationException("Se bloqueó una redirección externa del portal.");
                if(informationOnly&&!InformationEndpoint(uri))throw new InvalidOperationException("La consulta de tiempo no puede iniciar ni cerrar una sesión de navegación.");
                try {
                    string location; var response=PinnedHttp.Send(uri,portal,router,body,cookies,token,out location);
                    if(response.Code>=300&&response.Code<=399&&!String.IsNullOrEmpty(location)){
                        uri=new Uri(uri,location);if(response.Code!=307&&response.Code!=308)body=null;continue;
                    }
                    return response;
                }catch(Exception ex){token.ThrowIfCancellationRequested();if(ex is InvalidOperationException)throw;throw new NetworkFailure("No se pudo acceder al portal.",ex);}
            }
            throw new InvalidOperationException("El portal redirigió demasiadas veces.");
        }
    }
    public static class Connectivity {
        public static bool Verify(NetworkLink vpn,CancellationToken token) {
            return Verify(vpn,token,null);
        }
        public static bool Verify(NetworkLink vpn,CancellationToken token,System.Collections.Generic.List<string> trace) {
            if(vpn==null)return false;
            string[] urls={"https://www.gstatic.com/generate_204","https://www.msftconnecttest.com/connecttest.txt"};
            for(int i=0;i<urls.Length;i++) {
                token.ThrowIfCancellationRequested();
                try{
                    Uri uri=new Uri(urls[i]); var addresses=Networks.Resolve(uri.Host,token).Where(a=>a.AddressFamily==AddressFamily.InterNetwork).ToList();
                    if(addresses.Count==0||addresses.Any(a=>SocketPath.BestInterface(a)!=(uint)vpn.Index)){Networks.Addresses.Invalidate(uri.Host);if(trace!=null)trace.Add("Prueba "+i+": DNS sin IPv4 o ruta fuera de la interfaz VPN "+vpn.Index);continue;}
                    foreach(var address in addresses.Take(2)){
                        try{
                            string location;var response=PinnedHttp.Send(uri,address,vpn,null,new CookieContainer(),token,out location,5000);
                            if(IsExpected(i,response.Code,response.Body)){if(trace!=null)trace.Add("Prueba "+i+": correcta");return true;}
                            if(trace!=null)trace.Add("Prueba "+i+": HTTP "+response.Code+" no coincide con la respuesta esperada.");
                        }catch(Exception ex){token.ThrowIfCancellationRequested();if(trace!=null)trace.Add("Prueba "+i+": "+ex.GetType().Name);}
                    }
                    Networks.Addresses.Invalidate(uri.Host);
                }catch(Exception ex){token.ThrowIfCancellationRequested();if(trace!=null)trace.Add("Prueba "+i+": "+ex.GetType().Name+" "+ex.Message);}
            }
            return false;
        }
        public static bool IsExpected(int endpoint,int code,string body){return endpoint==0?code==204&&body.Length==0:code==200&&body.Trim()=="Microsoft Connect Test";}
    }
}
