using System;
using System.Net;
namespace NautaReconnect {
    public enum Light { Gray, Green, Yellow, Red }
    public sealed class Status {
        public Light Light;
        public string Message;
        public string Detail;
        public DateTime Time = DateTime.Now;
        public Status(Light light, string message, string detail) { Light=light; Message=message; Detail=detail; }
    }
    public sealed class AppSettings {
        public string Username = "";
        public string ProtectedPassword = "";
        public int IndicatorX = -1, IndicatorY = -1;
        public int IntervalSeconds = 15;
        public string LastPortalIp = "10.180.0.30";
        public bool AutoLoginEnabled = true;
        public bool StartWithWindows = true;
        public bool IndicatorPinned = false;
        public string ProtectedSession = "";
    }
    public sealed class NetworkLink {
        public string Name;
        public int Index;
        public IPAddress LocalIp, Gateway;
    }
    public interface IController {
        AppSettings Settings { get; }
        Status Current { get; }
        bool Running { get; }
        Telemetry Metrics {get;}
        event EventHandler StatusChanged;
        event EventHandler MetricsChanged;
        void SetAutoLogin(bool enabled);
        void Start(); void Pause(); void CheckNow();
        void SaveAccount(string user,string password); void SaveSettings();
        void ClearAccount(); void Shutdown();
        void ImportSessionFile(string path);
    }
    public sealed class Telemetry {
        public DateTime ServiceStartedUtc=DateTime.MinValue;
        public int ConnectionDrops,ConnectionRecoveries,AutoLoginAttempts,AutoLoginSuccesses;
        public double DownloadBytesPerSecond,UploadBytesPerSecond;
        public TimeSpan? Remaining;
        public decimal? Credit;
        public DateTime AccountCheckedUtc=DateTime.MinValue;
        public string AccountNote="Consultando tiempo…";
        public bool RatesReady;
        public Telemetry Copy(){return (Telemetry)MemberwiseClone();}
    }
}
