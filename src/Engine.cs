using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
namespace NautaReconnect {
    public sealed class Engine:IController {
        public AppSettings Settings {get;private set;}
        public Status Current {get;private set;}
        public bool Running {get{return running;}}
        public event EventHandler StatusChanged;
        public event EventHandler MetricsChanged;
        readonly object metricsGate=new object();
        Telemetry metrics=new Telemetry();
        public Telemetry Metrics{get{lock(metricsGate)return metrics.Copy();}}
        readonly RateMonitor rates=new RateMonitor();
        readonly Timer rateTimer;
        int sampling;
        int shutdownStarted,wakeDisposed,accountCheckRequested;
        readonly object ratesGate=new object();
        DateTime nextAccountCheck=DateTime.MinValue;
        bool accountQueryBlocked;
        readonly SettingsStore store;
        readonly RetryPolicy retry=new RetryPolicy();
        readonly object settingsGate=new object();
        readonly object tokenGate=new object();
        readonly object cycleGate=new object();
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        readonly Thread worker;
        CancellationTokenSource cycle;
        volatile bool running,closing;
        DateTime loginSucceededUntil=DateTime.MinValue;
        public Engine(SettingsStore value,bool preview) {
            store=value;Settings=store.Load();
            Current=new Status(Light.Gray,"Pausado",preview?"Vista de prueba; no se realizan conexiones.":"Configura tu cuenta y pulsa Iniciar.");
            if(!preview){rates.TopologyChanged+=NetworkChanged;worker=new Thread(Loop);worker.IsBackground=true;worker.Start();rateTimer=new Timer(SampleRates,null,0,1000);}
        }
        void NetworkChanged(object sender,EventArgs args){Networks.Addresses.Clear();Signal();}
        void Signal(){if(closing)return;try{wake.Set();}catch(ObjectDisposedException){}}
        void ReleaseWake(){if(Interlocked.Exchange(ref wakeDisposed,1)==0)wake.Dispose();}
        void SampleRates(object state){
            if(closing||Interlocked.Exchange(ref sampling,1)!=0)return;
            try{lock(ratesGate){if(closing)return;var sample=rates.Sample(rates.ReadCached(),RateMonitor.NowSeconds());lock(metricsGate){metrics.DownloadBytesPerSecond=sample.DownloadBytesPerSecond;metrics.UploadBytesPerSecond=sample.UploadBytesPerSecond;metrics.RatesReady=sample.RatesReady;}}MetricsUpdated();}catch{}finally{Interlocked.Exchange(ref sampling,0);}
        }
        void MetricsUpdated(){var changed=MetricsChanged;if(changed!=null)changed(this,EventArgs.Empty);}
        public void SetAutoLogin(bool enabled){
            lock(tokenGate){if(cycle!=null)cycle.Cancel();}
            lock(cycleGate){lock(settingsGate){Settings.AutoLoginEnabled=enabled;store.Save(Settings);}}
            var changed=StatusChanged;if(changed!=null)changed(this,EventArgs.Empty);Signal();
        }
        void ResetAccountReading(){lock(metricsGate){metrics.Remaining=null;metrics.Credit=null;metrics.AccountCheckedUtc=DateTime.MinValue;metrics.AccountNote="Tiempo disponible al iniciar sesión con AutoNauta.";}accountQueryBlocked=false;nextAccountCheck=DateTime.MinValue;MetricsUpdated();}
        void Set(Light light,string message,string detail) {
            Status updated;
            lock(tokenGate){
                if(Thread.CurrentThread==worker && cycle!=null && (cycle.IsCancellationRequested||!running||closing))return;
                if(Current.Light==light&&Current.Message==message&&Current.Detail==detail)return;
                updated=new Status(light,message,detail);Current=updated;
            }
            store.Log(updated);
            var changed=StatusChanged;if(changed!=null)changed(this,EventArgs.Empty);
        }
        public void Start() {
            if(closing)return;
            lock(tokenGate){running=true;retry.Failures=0;}
            Set(Light.Gray,"Comprobando conexión","Verificando Internet por la salida actual antes de acceder a Nauta.");Signal();
        }
        public void Pause() {
            lock(tokenGate){running=false;if(cycle!=null)cycle.Cancel();}
            Set(Light.Gray,"Pausando","Se detienen los intentos. Esto no cierra una sesión Nauta ya abierta.");Signal();
        }
        public void CheckNow() { if(running){Interlocked.Exchange(ref accountCheckRequested,1);Signal();}else Set(Light.Gray,"Pausado","Pulsa Iniciar para comprobar y reconectar. Pausar no cierra la sesión Nauta."); }
        public void SaveSettings(){lock(settingsGate)store.Save(Settings);}
        public void SaveAccount(string user,string password){
            user=(user??"").Trim(); if(user.Length<5||user.Length>254||!user.EndsWith("@nauta.com.cu",StringComparison.OrdinalIgnoreCase)||user.Contains(" "))throw new ArgumentException("Introduce tu cuenta completa de navegación, terminada en @nauta.com.cu.");
            lock(tokenGate){if(cycle!=null)cycle.Cancel();}
            lock(cycleGate){
            bool credentialsChanged=!user.Equals(Settings.Username,StringComparison.OrdinalIgnoreCase)||!String.IsNullOrEmpty(password);
            lock(settingsGate){if(!String.IsNullOrEmpty(password)){if(password.Length>256)throw new ArgumentException("Contraseña demasiado larga.");Settings.ProtectedPassword=SettingsStore.Protect(password);}else if(String.IsNullOrEmpty(Settings.ProtectedPassword))throw new ArgumentException("Introduce la contraseña para guardar la cuenta.");
                if(!user.Equals(Settings.Username,StringComparison.OrdinalIgnoreCase)&&String.IsNullOrEmpty(password))throw new ArgumentException("Al cambiar de usuario debes introducir su contraseña.");
                Settings.Username=user;if(credentialsChanged)Settings.ProtectedSession="";store.Save(Settings);retry.AuthBlocked=false;retry.AuthFailures=0;retry.LastLogin=DateTime.MinValue;}
                if(credentialsChanged)ResetAccountReading();else{accountQueryBlocked=false;nextAccountCheck=DateTime.MinValue;}
            }
            Set(Light.Gray,running?"Cuenta guardada":"Pausado","Cuenta guardada en esta computadora. La contraseña está protegida por Windows.");Signal();
        }
        public void ClearAccount(){Pause();lock(cycleGate){lock(settingsGate){Settings.Username="";Settings.ProtectedPassword="";Settings.ProtectedSession="";store.Save(Settings);retry.AuthBlocked=false;}ResetAccountReading();}Set(Light.Gray,"Pausado","La cuenta guardada se eliminó. No se cerró la sesión Nauta existente.");}
        public void Shutdown(){
            if(Interlocked.Exchange(ref shutdownStarted,1)!=0)return;
            closing=true;lock(tokenGate){running=false;if(cycle!=null)cycle.Cancel();}try{wake.Set();}catch(ObjectDisposedException){}
            if(rateTimer!=null)rateTimer.Dispose();
            lock(ratesGate){rates.TopologyChanged-=NetworkChanged;rates.Dispose();}
            if(worker!=null&&worker.IsAlive)worker.Join(2500);
            SaveSettings();if(worker==null||!worker.IsAlive)ReleaseWake();
        }
        void Loop(){
            try{
                while(!closing){
                    if(!running){
                        Set(Light.Gray,"Pausado","No se hacen conexiones. Una sesión Nauta que ya estaba abierta sigue activa.");wake.WaitOne();continue;}
                    using(var token=new CancellationTokenSource()){
                        lock(tokenGate){cycle=token;if(!running||closing)token.Cancel();}
                        lock(cycleGate){
                        try{token.Token.ThrowIfCancellationRequested();Cycle(token.Token);}catch(OperationCanceledException){}
                        catch(NetworkFailure e){if(running){if(e.Blocked)Set(Light.Red,"Acceso al portal bloqueado","Windows o el software de red rechazó el acceso por el router. Revisa el diagnóstico y los permisos de acceso a la red local.");else if(e.Certificate){retry.AuthBlocked=true;Set(Light.Red,"Certificado HTTPS no válido","No se enviaron credenciales. Revisa fecha de Windows y certificado del portal; no se desactiva su validación.");}else if(retry.AuthBlocked)Set(Light.Red,"Autenticación detenida","Tres intentos no confirmados. Revisa el portal y guarda de nuevo la cuenta para permitir otro intento.");else Set(Light.Gray,"Esperando portal","Nauta todavía no responde. Se reintentará automáticamente.");}}
                        catch(Exception e){if(running)Set(Light.Red,"No se pudo continuar",SafeError(e));}
                        finally{lock(tokenGate){cycle=null;}}
                        }
                    }
                    if(!running||closing)continue;
                    wake.WaitOne(retry.DelaySeconds(Settings.IntervalSeconds)*1000);
                }
            }finally{if(closing)ReleaseWake();}
        }
        static string SafeError(Exception error){
            if(error is InvalidOperationException||error is ArgumentException||error is System.ComponentModel.Win32Exception)return error.Message;
            return "Comprueba la conexión y la configuración. El detalle técnico se omite para no registrar datos de sesión.";
        }
        void Cycle(CancellationToken token){
            var connection=Connectivity.CheckCurrent(token);
            if(connection.Connected){retry.Healthy();Set(Light.Green,"Internet conectado","Conexión comprobada a través de "+connection.Interface.Name+". El programa permanece esperando.");RefreshAccount(token);return;}
            if(!retry.ConfirmFailure()){Set(Light.Gray,"Verificando caída","Primer fallo de conectividad; esperando una segunda comprobación.");return;}
            token.ThrowIfCancellationRequested();
            var router=Networks.Router();string cached;lock(settingsGate)cached=Settings.LastPortalIp;
            CaptiveState access=CaptiveProbe.Check(router,token);
            if(access==CaptiveState.Internet){Set(Light.Yellow,"Esperando conexión","El router tiene Internet. Se espera a que la salida del sistema vuelva a responder sin iniciar sesión en Nauta.");RefreshAccount(token);return;}
            if(access!=CaptiveState.LoginRequired){Set(Light.Gray,"Esperando confirmación de Nauta","La comprobación por el router no confirmó que Nauta solicite acceso. Se espera sin enviar la contraseña.");return;}
            IPAddress address=Networks.PortalAddress(router,cached,token);
            lock(settingsGate){if(Settings.LastPortalIp!=address.ToString()){Settings.LastPortalIp=address.ToString();store.Save(Settings);}}
            token.ThrowIfCancellationRequested();
            var client=new PortalClient(router,address);var response=client.Get(token);
            if(response.Code<200||response.Code>=300){Set(Light.Red,"Error del portal","El portal respondió HTTP "+response.Code+". Se volverá a comprobar sin enviar credenciales.");return;}
            var page=PortalParser.Parse(response.Body,response.Uri);
            if(page.Connected){Set(Light.Yellow,"Esperando conexión","Nauta informa que la sesión está abierta. Se espera a recuperar Internet sin volver a autenticar.");RefreshAccount(token);return;}
            if(page.SessionInUse){retry.AuthBlocked=true;Set(Light.Yellow,"Sesión Nauta en uso","Se espera sin volver a autenticar. Cuando se libere la cuenta, guárdala de nuevo para permitir otro intento.");return;}
            if(!page.Login){if(page.Fatal)retry.AuthBlocked=true;Set(Light.Red,"Error del portal",page.Error);return;}
            if(!String.IsNullOrEmpty(Settings.ProtectedSession)){lock(settingsGate){Settings.ProtectedSession="";store.Save(Settings);}lock(metricsGate){metrics.Remaining=null;metrics.AccountNote="La sesión anterior terminó; esperando un nuevo contador de Nauta.";}MetricsUpdated();}
            if(!Settings.AutoLoginEnabled){Set(Light.Yellow,"Login automático desactivado","Nauta solicita acceso. La vigilancia continúa sin iniciar sesión automáticamente.");return;}
            if(retry.AuthBlocked){Set(Light.Red,"Autenticación detenida","Revisa el portal y guarda nuevamente la cuenta para permitir otro intento.");return;}
            if(DateTime.UtcNow<loginSucceededUntil){Set(Light.Yellow,"Esperando recuperación","Se envió un inicio de sesión recientemente. Esperando la conexión antes de repetirlo.");return;}
            string user,protectedPassword;lock(settingsGate){user=Settings.Username;protectedPassword=Settings.ProtectedPassword;}
            if(String.IsNullOrEmpty(user)||String.IsNullOrEmpty(protectedPassword)){Set(Light.Red,"Falta configurar la cuenta","El portal está disponible. Introduce usuario y contraseña en la ventana y pulsa Guardar cuenta.");return;}
            if(!retry.CanLogin(DateTime.UtcNow)){Set(Light.Yellow,"Esperando próximo intento","El portal está disponible. Se espacian los intentos para evitar repetir autenticaciones.");return;}
            Set(Light.Yellow,"Iniciando sesión","El portal de Nauta está disponible. Intentando recuperar la sesión por el router.");
            string password=SettingsStore.Unprotect(protectedPassword);token.ThrowIfCancellationRequested();retry.LastLogin=DateTime.UtcNow;
            PageResponse logged;try{logged=retry.LoginAttempt(delegate{return client.Login(page,user,password,token);});}finally{password=null;}
            if(logged.Code<200||logged.Code>=300){bool blocked=retry.AuthenticationFailed();Set(Light.Red,"Error al iniciar sesión","Nauta devolvió HTTP "+logged.Code+(blocked?". Se alcanzaron tres fallos; guarda nuevamente la cuenta después de revisar el portal.":". Esperando antes de repetir el intento."));return;}
            var answer=PortalParser.Parse(logged.Body,logged.Uri);
            var captured=NautaSession.Capture(page,logged,user);if(captured!=null){lock(settingsGate){Settings.ProtectedSession=SettingsStore.Protect(new JavaScriptSerializer().Serialize(captured));store.Save(Settings);}nextAccountCheck=DateTime.MinValue;}
            if(answer.Fatal)retry.AuthBlocked=true;
            if(answer.SessionInUse){retry.AuthBlocked=true;Set(Light.Yellow,"Sesión Nauta en uso","Se detienen las autenticaciones mientras la cuenta está en uso. Guarda de nuevo la cuenta para reanudar los intentos.");return;}
            if(answer.Error.Length>0&&!answer.Login&&!answer.Connected){retry.AuthenticationFailed();Set(Light.Red,"Nauta devolvió un error",answer.Error+(retry.AuthBlocked?" Los intentos quedaron detenidos hasta revisar y guardar de nuevo la cuenta.":""));return;}
            if(Connectivity.CheckCurrent(token).Connected){retry.Healthy();Set(Light.Green,"Internet recuperado","Acceso a Internet comprobado después de iniciar sesión en Nauta.");RefreshAccount(token);return;}
            if(answer.Connected){loginSucceededUntil=DateTime.UtcNow.AddMinutes(3);Set(Light.Yellow,"Nauta conectado; esperando Internet","La sesión está abierta. El verde aparecerá cuando se confirme el acceso a Internet.");RefreshAccount(token);}
            else {retry.AuthenticationFailed();Set(Light.Red,"Inicio de sesión no confirmado",retry.AuthBlocked?"Tres intentos no confirmados. Revisa el portal y guarda nuevamente la cuenta antes de continuar.":"El portal devolvió el formulario de acceso. No se confirmó la reconexión; se esperará antes de reintentar.");}
        }
        void RefreshAccount(CancellationToken token){
            if(Interlocked.Exchange(ref accountCheckRequested,0)!=0)nextAccountCheck=DateTime.MinValue;
            if(DateTime.UtcNow<nextAccountCheck||accountQueryBlocked)return;nextAccountCheck=DateTime.UtcNow.AddSeconds(60);
            string user,encrypted,protectedSession;lock(settingsGate){user=Settings.Username;encrypted=Settings.ProtectedPassword;protectedSession=Settings.ProtectedSession;}
            if(String.IsNullOrEmpty(user)||String.IsNullOrEmpty(encrypted)){lock(metricsGate){metrics.AccountNote="Guarda tu cuenta para consultar el tiempo.";}MetricsUpdated();return;}
            try{
                var router=Networks.Router();var client=new PortalClient(router,Networks.PortalAddress(router,Settings.LastPortalIp,token));
                if(!String.IsNullOrEmpty(protectedSession)){
                    NautaSession session=null;try{session=new JavaScriptSerializer().Deserialize<NautaSession>(SettingsStore.Unprotect(protectedSession));}catch{}
                    if(session!=null&&session.ValidFor(user)){
                        var left=client.GetLeftTime(session,token);var duration=left.Code==200?AccountTime.ParseDuration(left.Body):null;
                        if(duration.HasValue){lock(metricsGate){metrics.Remaining=duration;metrics.AccountCheckedUtc=DateTime.UtcNow;metrics.AccountNote="Tiempo confirmado por Nauta; se actualiza cada minuto.";}MetricsUpdated();return;}
                        if(!AccountTime.SessionExpired(left.Code,left.Body))throw new IOException("No se pudo actualizar el contador de la sesión.");
                    }
                    lock(settingsGate){Settings.ProtectedSession="";store.Save(Settings);}lock(metricsGate)metrics.Remaining=null;
                }
                var home=client.Get(token);var form=PortalParser.Parse(home.Body,home.Uri);if(!form.Login)throw new InvalidOperationException("Nauta no ofreció el formulario de información de cuenta.");
                string password=SettingsStore.Unprotect(encrypted);PageResponse info;try{info=client.QueryAccount(form,user,password,token);}finally{password=null;}
                if(info.Code!=200)throw new InvalidOperationException("La consulta de cuenta no respondió correctamente.");
                var reading=AccountTime.Parse(info.Body);accountQueryBlocked=reading.CredentialsRejected;
                if(!reading.Remaining.HasValue)nextAccountCheck=DateTime.MaxValue;
                lock(metricsGate){metrics.Remaining=reading.Remaining;metrics.Credit=reading.Credit;metrics.AccountCheckedUtc=DateTime.UtcNow;metrics.AccountNote=reading.Note;}MetricsUpdated();
            }catch(OperationCanceledException){throw;}
            catch{token.ThrowIfCancellationRequested();lock(metricsGate){metrics.AccountNote="No se pudo actualizar el tiempo. Se conserva la última consulta, si existe.";}MetricsUpdated();}
        }
    }
}
