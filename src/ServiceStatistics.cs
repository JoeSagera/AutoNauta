using System;
namespace NautaReconnect {
    internal sealed class ServiceStatistics {
        internal DateTime StartedUtc;
        internal int Drops,Recoveries,AutoAttempts,AutoSuccesses;
        bool online,outage,acceptedAuto;
        internal bool Begin(){if(StartedUtc!=DateTime.MinValue)return false;StartedUtc=DateTime.UtcNow;return true;}
        internal bool Drop(){if(!online||outage)return false;online=false;outage=true;Drops++;return true;}
        internal bool Healthy(){bool changed=false;online=true;if(outage){Recoveries++;outage=false;changed=true;}if(acceptedAuto){AutoSuccesses++;acceptedAuto=false;changed=true;}return changed;}
        internal bool Attempt(){AutoAttempts++;acceptedAuto=false;return true;}
        internal bool Accepted(){acceptedAuto=true;return false;}
        internal bool ForgetAuto(){acceptedAuto=false;return false;}
        internal bool Suspend(){online=outage=acceptedAuto=false;return false;}
        internal void Apply(Telemetry value){value.ServiceStartedUtc=StartedUtc;value.ConnectionDrops=Drops;value.ConnectionRecoveries=Recoveries;value.AutoLoginAttempts=AutoAttempts;value.AutoLoginSuccesses=AutoSuccesses;}
    }
}
