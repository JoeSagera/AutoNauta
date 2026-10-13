using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Threading;
namespace NautaReconnect {
    public static class PinnedHttp {
        public static string LastCertificateInfo="";
        public static bool TrustedRequest(Uri uri,string body){
            return PortalParser.Trusted(uri)||(body==null&&uri!=null&&uri.IsAbsoluteUri&&(uri.AbsoluteUri=="https://www.gstatic.com/generate_204"||uri.AbsoluteUri=="https://www.msftconnecttest.com/connecttest.txt"));
        }
        public static PageResponse Send(Uri uri,IPAddress destination,NetworkLink link,string body,CookieContainer cookies,CancellationToken token,out string location,int timeoutMs=8000) {
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
                deadline.CancelAfter(timeoutMs);
                try{return SendWithinDeadline(uri,destination,link,body,cookies,deadline.Token,out location,timeoutMs);}
                catch(Exception error){token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new RequestTimeoutException(error);throw;}
            }
        }
        static PageResponse SendWithinDeadline(Uri uri,IPAddress destination,NetworkLink link,string body,CookieContainer cookies,CancellationToken token,out string location,int timeoutMs){
            location=null;
            LastCertificateInfo="";
            if(!TrustedRequest(uri,body))throw new InvalidOperationException("Destino HTTPS no autorizado.");
            using(var tcp=new TcpClient(new IPEndPoint(link.LocalIp,0)))using(token.Register(delegate{try{tcp.Close();}catch{}})) {
                SocketPath.Select(tcp.Client,link);
                tcp.ReceiveTimeout=timeoutMs;tcp.SendTimeout=timeoutMs;
                SocketPath.Connect(tcp,destination,uri.Port,token,timeoutMs);
                using(var tls=new SslStream(tcp.GetStream(),false,delegate(object sender,System.Security.Cryptography.X509Certificates.X509Certificate certificate,System.Security.Cryptography.X509Certificates.X509Chain chain,SslPolicyErrors errors){
                    if(errors!=SslPolicyErrors.None){
                        string detail="Errores: "+errors;
                        using(var cert=certificate==null?null:new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate)){
                            if(cert!=null)using(var hash=System.Security.Cryptography.SHA256.Create())detail+="; Emisor: "+cert.Issuer+"; Sujeto: "+cert.Subject+"; Desde: "+cert.NotBefore.ToString("o")+"; Hasta: "+cert.NotAfter.ToString("o")+"; SHA256: "+BitConverter.ToString(hash.ComputeHash(cert.RawData)).Replace("-","");
                        }
                        if(chain!=null)foreach(var status in chain.ChainStatus)detail+="; Cadena: "+status.Status+" "+status.StatusInformation.Trim();
                        LastCertificateInfo=detail;
                    }
                    return errors==SslPolicyErrors.None;
                })) {
                    tls.ReadTimeout=timeoutMs;tls.WriteTimeout=timeoutMs;
                    // SNI and platform certificate validation use the DNS name, while TCP uses the router-resolved IP.
                    // Match the normal .NET HTTPS policy: validate name, dates and trust chain without
                    // requiring an online revocation download from a portal reached before Internet is available.
                    tls.AuthenticateAsClient(uri.Host,null,SslProtocols.Tls12,false);
                    token.ThrowIfCancellationRequested();
                    byte[] payload=body==null?new byte[0]:Encoding.UTF8.GetBytes(body);
                    string headers=(body==null?"GET":"POST")+" "+uri.PathAndQuery+" HTTP/1.1\r\nHost: "+uri.Authority+"\r\nUser-Agent: AutoNauta/2.1\r\nAccept: text/html\r\nConnection: close\r\n";
                    string cookie=cookies.GetCookieHeader(uri);if(cookie.Length>0)headers+="Cookie: "+cookie+"\r\n";
                    if(body!=null)headers+="Content-Type: application/x-www-form-urlencoded\r\nContent-Length: "+payload.Length+"\r\n";
                    byte[] h=Encoding.ASCII.GetBytes(headers+"\r\n");tls.Write(h,0,h.Length);
                    try{token.ThrowIfCancellationRequested();if(payload.Length>0)tls.Write(payload,0,payload.Length);tls.Flush();}
                    finally{Array.Clear(payload,0,payload.Length);}
                    using(var raw=new MemoryStream()) {
                        byte[] buffer=new byte[4096];int read;
                        while((read=tls.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();raw.Write(buffer,0,read);if(raw.Length>1024*1024)throw new IOException("Respuesta demasiado grande.");}
                        byte[] bytes=raw.ToArray();int end=-1;
                        for(int i=0;i+3<bytes.Length;i++)if(bytes[i]==13&&bytes[i+1]==10&&bytes[i+2]==13&&bytes[i+3]==10){end=i;break;}
                        if(end<0||end>65536)throw new IOException("Respuesta HTTP no reconocida.");
                        string header=Encoding.ASCII.GetString(bytes,0,end);string[] lines=header.Split(new string[]{"\r\n"},StringSplitOptions.None);string[] status=lines[0].Split(' ');int code;
                        if(status.Length<2||!Int32.TryParse(status[1],out code))throw new IOException("Estado HTTP inválido.");
                        bool chunks=false;
                        foreach(string line in lines){int colon=line.IndexOf(':');if(colon<0)continue;string name=line.Substring(0,colon),value=line.Substring(colon+1).Trim();
                            if(name.Equals("Location",StringComparison.OrdinalIgnoreCase))location=value;
                            if(name.Equals("Set-Cookie",StringComparison.OrdinalIgnoreCase)){try{cookies.SetCookies(uri,value);}catch(CookieException){}}
                            if(name.Equals("Transfer-Encoding",StringComparison.OrdinalIgnoreCase))chunks=value.ToLowerInvariant().Contains("chunked");
                            if(name.Equals("Content-Encoding",StringComparison.OrdinalIgnoreCase)&&value.Length>0&&!value.Equals("identity",StringComparison.OrdinalIgnoreCase))throw new IOException("El portal envió una compresión no solicitada.");
                        }
                        byte[] content=new byte[bytes.Length-end-4];Buffer.BlockCopy(bytes,end+4,content,0,content.Length);
                        if(chunks)content=DecodeChunks(content);
                        return new PageResponse{Uri=uri,Code=code,Body=Encoding.UTF8.GetString(content)};
                    }
                }
            }
        }
        public static byte[] DecodeChunks(byte[] raw) {
            using(var output=new MemoryStream()){int p=0;
                while(p<raw.Length){int end=p;while(end+1<raw.Length&&!(raw[end]==13&&raw[end+1]==10))end++;
                    if(end+1>=raw.Length)throw new IOException("Respuesta fragmentada incompleta.");string line=Encoding.ASCII.GetString(raw,p,end-p).Split(';')[0];int size;
                    if(!Int32.TryParse(line,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out size)||size<0)throw new IOException("Fragmento inválido.");
                    p=end+2;if(size==0)return output.ToArray();if(p+size+2>raw.Length||output.Length+size>1024*1024)throw new IOException("Fragmento incompleto.");
                    output.Write(raw,p,size);p+=size;if(raw[p]!=13||raw[p+1]!=10)throw new IOException("Separador inválido.");p+=2;
                }throw new IOException("Respuesta fragmentada sin cierre.");
            }
        }
    }
}
