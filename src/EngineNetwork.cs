using System;
using System.Net;
using System.Threading;
namespace NautaReconnect {
    internal sealed class EngineNetwork {
        internal Func<bool> PhysicalReady=Networks.PhysicalReady;
        internal Func<CancellationToken,ConnectionCheck> Internet=token=>Connectivity.CheckCurrent(token);
        internal Func<NetworkLink> Router=Networks.Router;
        internal Func<NetworkLink,CancellationToken,CaptiveState> Captive=CaptiveProbe.Check;
        internal Func<NetworkLink,CancellationToken,bool> RouterInternet=Connectivity.CheckRouter;
        internal Func<NetworkLink,string,CancellationToken,IPAddress> PortalAddress=Networks.PortalAddress;
        internal Func<NetworkLink,IPAddress,IPortalClient> Client=(router,address)=>new PortalClient(router,address);
        internal static bool InspectPortal(CaptiveState state,Func<bool> directInternet){
            return state!=CaptiveState.Internet&&(state==CaptiveState.LoginRequired||!directInternet());
        }
    }
}
