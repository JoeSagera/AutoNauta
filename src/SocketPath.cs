using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace NautaReconnect {
    public sealed class RequestTimeoutException:IOException{public RequestTimeoutException(Exception error):base("El servidor excedió el tiempo de respuesta.",error){}}
    public static class SocketPath {
        const int UnicastInterface=31;
        public static int InterfaceValue(int index){
            if(index<=0||index>0xFFFFFF)throw new ArgumentOutOfRangeException("index","La interfaz de red no es válida.");
            return IPAddress.HostToNetworkOrder(index);
        }
        public static void Select(Socket socket,NetworkLink link){
            if(socket==null)throw new ArgumentNullException("socket");
            if(link==null||link.LocalIp==null||link.LocalIp.AddressFamily!=AddressFamily.InterNetwork)throw new ArgumentException("Se necesita una interfaz IPv4 concreta.","link");
            socket.SetSocketOption(SocketOptionLevel.IP,(SocketOptionName)UnicastInterface,InterfaceValue(link.Index));
        }
        public static void Connect(TcpClient client,IPAddress destination,int port,CancellationToken token,int timeout){
            token.ThrowIfCancellationRequested();
            var task=Task.Factory.FromAsync((callback,state)=>client.BeginConnect(destination,port,callback,state),client.EndConnect,null);
            try{if(!task.Wait(timeout,token))throw new RequestTimeoutException(null);}
            catch(AggregateException error){throw new IOException("No se pudo conectar con el servidor.",error.GetBaseException());}
            finally{if(task.IsCompleted)task.Dispose();else task.ContinueWith(completed=>{var ignored=completed.Exception;completed.Dispose();},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
        }
        [DllImport("iphlpapi.dll")] static extern uint GetBestInterface(uint destination,out uint index);
        public static uint BestInterface(IPAddress address){
            uint index;uint code=GetBestInterface(BitConverter.ToUInt32(address.GetAddressBytes(),0),out index);
            if(code!=0)throw new Win32Exception((int)code);return index;
        }
    }
}
