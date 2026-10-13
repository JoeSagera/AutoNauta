using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Reflection;
namespace NautaReconnect {
    public static class CoreTests {
        static int passed,failed;
        static void Check(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch(Exception e) { failed++;Console.WriteLine("FAIL " + name + ": " +e.GetBaseException().GetType().Name+" 0x"+e.GetBaseException().HResult.ToString("X8")+" "+ e.GetBaseException().Message); } }
        static void Assert(bool value, string message) { if(!value) throw new Exception(message); }
        static readonly Uri Origin = new Uri("https://secure.etecsa.net:8443/nauta/login.jsp");
        sealed class SharingFailure:IOException{internal SharingFailure(int code=32):base("fake-sensitive-session-data"){HResult=unchecked((int)0x80070000)|code;}}
        static string LoginHtml(string target) {
            return "<html><form id='formulario' method='post' action='"+target+"'><input type=hidden name=CSRFHW value='a&amp;b'><input name=wlanuserip type=hidden value='192.0.2.1'><input type=text name=username><input type=password name=password><input type=submit name=submit value=Aceptar></form></html>";
        }
        static string OnlineHtml(){
            return "<div>Usted está conectado</div><input value='Cerrar sesión'>"+
                "<script>function updateAvailableTime(){var urlParam=\"op=getLeftTime&ATTRIBUTE_UUID=12345678abcdef&CSRFHW=online-csrf\""+
                "+\"&wlanuserip=192.0.2.20\"+\"&ssid=nauta_hogar\"+\"&loggerId=logger+test%40nauta.com.cu\""+
                "+\"&domain=\"+\"&username=test@nauta.com.cu\"+\"&wlanacname=\"+\"&wlanmac=\";}</script>";
        }
        static string SavedOnline(){return "<!-- saved from url=(0085)https://secure.etecsa.net:8443/web/online.do?CSRFHW=online-csrf& -->"+OnlineHtml();}
        sealed class FakePortal:IPortalClient{
            internal int Logins,Queries,TimeQueries;
            internal string LoginAnswer=OnlineHtml(),TimeAnswer="125:09:30";
            internal NautaSession LastSession;
            internal Func<PortalPage,string,string,CancellationToken,PageResponse> OnLogin;
            public PageResponse Get(CancellationToken token){return new PageResponse{Code=200,Uri=Origin,Body=LoginHtml("/LoginServlet")};}
            public PageResponse Login(PortalPage form,string user,string password,CancellationToken token){Logins++;if(OnLogin!=null)return OnLogin(form,user,password,token);return new PageResponse{Code=200,Uri=new Uri(Origin,"/web/online.do?CSRFHW=online-csrf"),Body=LoginAnswer};}
            public PageResponse QueryAccount(PortalPage form,string user,string password,CancellationToken token){Queries++;return new PageResponse{Code=200,Uri=new Uri(Origin,"/EtecsaQueryServlet"),Body="<tr><td>Crédito</td><td>250.00 CUP</td></tr>"};}
            public PageResponse GetLeftTime(NautaSession session,CancellationToken token){TimeQueries++;LastSession=session;return new PageResponse{Code=200,Body=TimeAnswer,Uri=new Uri(Origin,"/EtecsaQueryServlet")};}
        }
        sealed class Scenario:IDisposable{
            internal readonly string DirectoryPath=Path.Combine(Path.GetTempPath(),"AutoNautaReconnect-"+Guid.NewGuid().ToString("N"));
            internal readonly FakePortal Portal=new FakePortal();
            internal readonly Engine Engine;
            internal bool Internet,RouterInternet,PhysicalReady=true;
            internal CaptiveState Captive=CaptiveState.Unknown;
            internal int DirectChecks,InternetChecks;
            internal Scenario(){
                var link=new NetworkLink{Name="Router de prueba",Index=10,LocalIp=IPAddress.Parse("192.0.2.20")};
                var store=new SettingsStore(DirectoryPath);store.Save(new AppSettings{Username="test@nauta.com.cu",ProtectedPassword=SettingsStore.Protect("fake-password")});
                Engine=new Engine(store,true,new EngineNetwork{
                    PhysicalReady=()=>PhysicalReady,
                    Internet=token=>{InternetChecks++;return new ConnectionCheck{Connected=Internet,Interface=link};},Router=()=>link,Captive=(router,token)=>Captive,
                    RouterInternet=(router,token)=>{DirectChecks++;return RouterInternet;},PortalAddress=(router,cached,token)=>IPAddress.Parse("192.0.2.30"),Client=(router,address)=>Portal});
                Engine.Start();
            }
            internal void Cycle(){typeof(Engine).GetMethod("Cycle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(Engine,new object[]{CancellationToken.None});}
            public void Dispose(){Engine.Shutdown();foreach(string file in Directory.GetFiles(DirectoryPath))File.Delete(file);Directory.Delete(DirectoryPath);}
        }
        sealed class WorkerScenario:IDisposable{
            readonly string path=Path.Combine(Path.GetTempPath(),"AutoNautaNetworkEvents-"+Guid.NewGuid().ToString("N"));
            internal readonly Engine Engine;
            internal readonly FakePortal Portal=new FakePortal();
            internal volatile bool Physical=true,Connected=true;
            internal int InternetChecks;
            internal Func<CancellationToken,bool> OnInternet;
            internal WorkerScenario(bool allowPortal=false){
                var link=new NetworkLink{Name="Red de prueba",Index=10,LocalIp=IPAddress.Parse("192.0.2.20")};
                var store=new SettingsStore(path);if(allowPortal)store.Save(new AppSettings{Username="test@nauta.com.cu",ProtectedPassword=SettingsStore.Protect("fake-password")});
                Engine=new Engine(store,false,new EngineNetwork{
                    PhysicalReady=()=>Physical,
                    Internet=token=>{Interlocked.Increment(ref InternetChecks);bool online=OnInternet==null?Connected:OnInternet(token);return new ConnectionCheck{Connected=online,Interface=link};},
                    Router=()=>{if(allowPortal)return link;throw new Exception("No debe consultar el portal en esta prueba");},
                    Captive=(router,token)=>CaptiveState.LoginRequired,PortalAddress=(router,cached,token)=>IPAddress.Parse("192.0.2.30"),Client=(router,address)=>Portal
                });
            }
            internal void WaitFor(Func<bool> condition,int milliseconds=2000){
                var watch=System.Diagnostics.Stopwatch.StartNew();while(!condition()&&watch.ElapsedMilliseconds<milliseconds)Thread.Sleep(10);
                Assert(condition(),"No respondió a tiempo a la condición de red");
            }
            internal void WaitIdle(){WaitFor(()=>typeof(Engine).GetField("cycle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Engine)==null);}
            public void Dispose(){Engine.Shutdown();foreach(string file in Directory.GetFiles(path))File.Delete(file);Directory.Delete(path);}
        }
        public static int Run() {
            Check("Estadísticas cuentan episodios y AUTO solo tras confirmar Internet",delegate{
                using(var test=new Scenario()){
                    test.Internet=true;test.Cycle();test.Internet=false;test.Cycle();Assert(test.Engine.Metrics.ConnectionDrops==0,"Cuenta una sola sonda fallida como caída");test.Cycle();
                    Assert(test.Engine.Metrics.ConnectionDrops==1&&test.Engine.Metrics.AutoLoginAttempts==1&&test.Engine.Metrics.AutoLoginSuccesses==0,"Confunde aceptación de Nauta con Internet disponible");
                    string session=test.Engine.Settings.ProtectedSession;test.Cycle();test.Cycle();Assert(test.Portal.Logins==1&&test.Engine.Settings.ProtectedSession==session,"Repite login o descarta contador mientras espera la salida de Internet");
                    test.Internet=true;test.Cycle();test.Cycle();var data=test.Engine.Metrics;
                    Assert(data.ConnectionDrops==1&&data.ConnectionRecoveries==1&&data.AutoLoginAttempts==1&&data.AutoLoginSuccesses==1,"Duplica estados o no atribuye recuperación confirmada");
                    test.Internet=false;test.Cycle();test.Cycle();Assert(test.Engine.Metrics.ConnectionDrops==2&&test.Portal.Logins==1,"Un login ya recuperado oculta una nueva caída o elimina la espera entre autenticaciones");
                }
            });
            Check("Estadísticas excluyen pausa e inicio offline y conservan el comienzo",delegate{
                var stats=new ServiceStatistics();stats.Begin();var start=stats.StartedUtc;stats.Drop();stats.Attempt();stats.Accepted();stats.Healthy();stats.Suspend();stats.Drop();stats.Begin();
                Assert(stats.Drops==0&&stats.Recoveries==0&&stats.AutoAttempts==1&&stats.AutoSuccesses==1&&stats.StartedUtc==start,"Cuenta inicio offline/pausa o reinicia el periodo al reanudar");
                stats.Healthy();stats.Drop();stats.Drop();stats.Healthy();Assert(stats.Drops==1&&stats.Recoveries==1,"Cuenta cada aviso dentro de una misma caída");
            });
            Check("Login rechazado o importación manual no cuentan como AUTO efectivo",delegate{
                var stats=new ServiceStatistics();stats.Begin();stats.Healthy();stats.Drop();stats.Attempt();stats.ForgetAuto();stats.Healthy();stats.Drop();stats.Attempt();stats.Accepted();stats.ForgetAuto();stats.Healthy();
                Assert(stats.AutoAttempts==2&&stats.AutoSuccesses==0&&stats.Recoveries==2,"Atribuye recuperación manual o rechazo a un AUTO efectivo");
            });
            Check("Fallo del contador tras login no impide recuperar Internet",delegate{
                using(var test=new Scenario()){
                    var services=(EngineNetwork)typeof(Engine).GetField("network",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(test.Engine);services.CaptureSession=(form,response,user)=>{throw new IOException("fake-secret-counter");};
                    test.Portal.OnLogin=(form,user,password,token)=>{test.Internet=true;return new PageResponse{Code=200,Uri=new Uri(Origin,"/web/online.do"),Body=OnlineHtml()};};
                    test.Cycle();test.Cycle();Assert(test.Engine.Current.Light==Light.Green&&test.Engine.Metrics.AutoLoginSuccesses==1,"Un error auxiliar deja rojo un login efectivo");
                    string log=File.ReadAllText(Path.Combine(test.DirectoryPath,"eventos.log"));Assert(log.Contains("IOException")&&log.Contains("guardar contador")&&!log.Contains("fake-secret-counter"),"Oculta fase/tipo o filtra el secreto del error");
                }
            });
            Check("Error después del POST vuelve a verde sin Comprobar ahora",delegate{
                using(var test=new WorkerScenario(true)){
                    test.Connected=false;test.Portal.OnLogin=(form,user,password,token)=>{test.Connected=true;throw new IOException("fake-sensitive-url-CSRF");};
                    bool sawCheck=false;test.Engine.StatusChanged+=delegate{if(test.Engine.Current.Message=="Comprobando Internet tras login")sawCheck=true;};
                    test.Engine.Start();test.WaitFor(()=>test.Engine.Current.Light==Light.Green,6500);
                    Assert(sawCheck&&test.Portal.Logins==1&&test.Engine.Metrics.AutoLoginAttempts==1&&test.Engine.Metrics.AutoLoginSuccesses==0,"No recupera por sí solo o atribuye un POST sin confirmación como efectivo");
                }
            });
            Check("Error de verificación conserva AUTO aceptado para la siguiente comprobación",delegate{
                using(var test=new WorkerScenario(true)){
                    test.Connected=false;bool probeFailed=false;test.OnInternet=token=>{if(test.Portal.Logins==0)return false;if(!probeFailed){probeFailed=true;throw new InvalidDataException("fake-secret");}return true;};
                    test.Engine.Start();test.WaitFor(()=>test.Engine.Current.Light==Light.Green,6500);
                    Assert(test.Portal.Logins==1&&test.Engine.Metrics.AutoLoginSuccesses==1,"Pierde la confirmación de Nauta o necesita un clic manual tras error de sonda");
                }
            });
            Check("Guardar configuración reintenta bloqueo temporal sin perder datos",delegate{
                string dir=Path.Combine(Path.GetTempPath(),"AutoNautaSharing-"+Guid.NewGuid().ToString("N"));try{
                    var store=new SettingsStore(dir);var value=new AppSettings{ProtectedPassword=SettingsStore.Protect("fake-password")};store.Save(value);int calls=0;
                    store.ReplaceFile=(source,destination)=>{if(++calls<3)throw new SharingFailure();File.Replace(source,destination,null);};value.IndicatorX=77;store.Save(value);
                    Assert(calls==3&&store.Load().IndicatorX==77&&SettingsStore.Unprotect(store.Load().ProtectedPassword)=="fake-password","Pierde actualización o credenciales ante bloqueo transitorio");
                }finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
            });
            Check("Guardar configuración limita reintentos y conserva original si falla",delegate{
                string dir=Path.Combine(Path.GetTempPath(),"AutoNautaSharing-"+Guid.NewGuid().ToString("N"));try{
                    var store=new SettingsStore(dir);var value=new AppSettings{IndicatorX=10};store.Save(value);int calls=0;store.ReplaceFile=(source,destination)=>{calls++;throw new SharingFailure(1175);};value.IndicatorX=11;bool stopped=false;
                    try{store.Save(value);}catch(IOException){stopped=true;}
                    Assert(stopped&&calls==5&&store.Load().IndicatorX==10,"Reintenta indefinidamente o borra la configuración anterior");
                }finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
            });
            Check("Guardar configuración no reintenta fallo de disco no transitorio",delegate{
                string dir=Path.Combine(Path.GetTempPath(),"AutoNautaSharing-"+Guid.NewGuid().ToString("N"));try{
                    var store=new SettingsStore(dir);var value=new AppSettings();store.Save(value);int calls=0;store.ReplaceFile=(source,destination)=>{calls++;throw new SharingFailure(112);};value.IndicatorX=4;try{store.Save(value);}catch(IOException){}
                    Assert(calls==1&&store.Load().IndicatorX==-1,"Oculta un fallo permanente de disco o sustituye el original");
                }finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
            });
            Check("Sin enlace físico no espera DNS ni envía login aunque la VPN figure activa",delegate{
                using(var test=new Scenario()){
                    test.PhysicalReady=false;test.Internet=true;test.Cycle();
                    Assert(test.Engine.Current.Light==Light.Yellow&&test.Engine.Current.Message=="Sin conexión de red"&&test.InternetChecks==0&&test.DirectChecks==0&&test.Portal.Logins==0&&test.Portal.TimeQueries==0,"Espera sondas o confunde la VPN con el cable conectado");
                    test.PhysicalReady=true;test.Internet=false;test.Cycle();test.PhysicalReady=false;test.Cycle();
                    var retry=(RetryPolicy)typeof(Engine).GetField("retry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(test.Engine);
                    Assert(retry.Failures==0&&test.InternetChecks==1,"Un enlace ausente deja pendiente para siempre la segunda comprobación rápida");
                }
            });
            Check("Cambio de red cancela petición antigua y no publica su verde tardío",delegate{
                using(var entered=new ManualResetEvent(false))using(var canceled=new ManualResetEvent(false))using(var test=new WorkerScenario()){
                    int greens=0;test.Engine.StatusChanged+=delegate{if(test.Engine.Current.Light==Light.Green)Interlocked.Increment(ref greens);};
                    test.OnInternet=token=>{entered.Set();token.WaitHandle.WaitOne(5000);if(token.IsCancellationRequested)canceled.Set();return true;};
                    test.Engine.Start();Assert(entered.WaitOne(2000),"No inició la petición simulada");test.Physical=false;
                    var watch=System.Diagnostics.Stopwatch.StartNew();test.Engine.NotifyNetworkChange();test.WaitFor(()=>test.Engine.Current.Message=="Sin conexión de red");
                    Assert(canceled.WaitOne(0)&&greens==0&&watch.ElapsedMilliseconds<1500,"Espera el timeout antiguo o deja verde después de quitar el enlace");
                }
            });
            Check("Reconectar enlace despierta vigilancia sin esperar quince segundos",delegate{
                using(var test=new WorkerScenario()){
                    test.Engine.Start();test.WaitFor(()=>test.Engine.Current.Light==Light.Green);test.WaitIdle();test.Physical=false;test.Engine.NotifyNetworkChange();test.WaitFor(()=>test.Engine.Current.Message=="Sin conexión de red");test.WaitIdle();
                    test.Physical=true;var watch=System.Diagnostics.Stopwatch.StartNew();test.Engine.NotifyNetworkChange();test.WaitFor(()=>test.Engine.Current.Light==Light.Green);
                    Assert(watch.ElapsedMilliseconds<1500&&test.InternetChecks>=2,"Reconexionar sigue esperando el intervalo normal");
                }
            });
            Check("Recuperación breve reintenta en dos segundos sin necesitar otro evento",delegate{
                using(var test=new WorkerScenario()){
                    test.Engine.Start();test.WaitFor(()=>test.Engine.Current.Light==Light.Green);test.WaitIdle();test.Connected=false;test.Engine.NotifyNetworkChange();test.WaitFor(()=>test.Engine.Current.Message=="Verificando caída");test.WaitIdle();
                    test.Connected=true;var watch=System.Diagnostics.Stopwatch.StartNew();test.WaitFor(()=>test.Engine.Current.Light==Light.Green,4000);
                    Assert(watch.ElapsedMilliseconds<3500&&test.InternetChecks>=3,"La red lista después del evento espera quince segundos más");
                }
            });
            Check("Cambio de red no inicia vigilancia pausada",delegate{
                using(var test=new WorkerScenario()){
                    test.Engine.NotifyNetworkChange();Thread.Sleep(100);
                    Assert(!test.Engine.Running&&test.InternetChecks==0&&test.Engine.Current.Light==Light.Gray,"Un cambio de red enciende la vigilancia pausada");
                }
            });
            Check("Cambio de cable conserva bloqueo y límite de autenticación",delegate{
                using(var test=new WorkerScenario()){
                    test.Engine.Start();test.WaitFor(()=>test.Engine.Current.Light==Light.Green);test.WaitIdle();
                    var retry=(RetryPolicy)typeof(Engine).GetField("retry",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(test.Engine);var login=DateTime.UtcNow;
                    retry.AuthBlocked=true;retry.AuthFailures=3;retry.LastLogin=login;test.Physical=false;test.Engine.NotifyNetworkChange();test.WaitFor(()=>test.Engine.Current.Message=="Sin conexión de red");
                    Assert(retry.AuthBlocked&&retry.AuthFailures==3&&retry.LastLogin==login&&!retry.CanLogin(login),"Desenchufar el cable elimina protecciones de la cuenta");
                }
            });
            Check("Motor cerrado ignora avisos de red tardíos",delegate{
                using(var test=new WorkerScenario()){test.Engine.Shutdown();test.Engine.NotifyNetworkChange();Assert(!test.Engine.Running&&test.InternetChecks==0,"Un aviso tardío reactiva un motor cerrado");}
            });
            Check("Contador Hogar conserva todo el contexto y el CSRF de online",delegate{
                var session=NautaSession.Capture(PortalParser.Parse(LoginHtml("/LoginServlet"),Origin),new PageResponse{Uri=new Uri(Origin,"/web/online.do"),Body=OnlineHtml()},"test@nauta.com.cu");
                Assert(session!=null&&session.Csrf=="online-csrf"&&session.Context.Count==6&&session.Context["ssid"]=="nauta_hogar"&&session.Context["loggerId"]=="logger test@nauta.com.cu"&&session.QueryFields().Count==10,"Pierde campos reales del contador o usa el CSRF anterior");
            });
            Check("Contador no admite otra cuenta ni expresiones ejecutables",delegate{
                Assert(NautaSession.ReadSavedPage(SavedOnline(),"otra@nauta.com.cu")==null,"Importa otra cuenta");
                Assert(NautaSession.ReadSavedPage(SavedOnline().Replace("+\"&domain=\"","+steal()+\"&domain=\""),"test@nauta.com.cu")==null,"Interpreta código de la página");
                Assert(NautaSession.ReadSavedPage(SavedOnline().Replace("&username=test@nauta.com.cu","&username=test@nauta.com.cu&username=otra@nauta.com.cu"),"test@nauta.com.cu")==null,"Admite cuenta duplicada");
            });
            Check("Importación exige página de sesión del origen HTTPS Nauta",delegate{
                Assert(NautaSession.ReadSavedPage(SavedOnline(),"test@nauta.com.cu")!=null&&NautaSession.ReadSavedPage(OnlineHtml(),"test@nauta.com.cu")==null&&NautaSession.ReadSavedPage(SavedOnline().Replace("secure.etecsa.net","evil.test"),"test@nauta.com.cu")==null,"Admite página ajena o sin origen");
            });
            Check("Consulta de contador no admite operaciones extra",delegate{var session=NautaSession.ReadSavedPage(SavedOnline(),"test@nauta.com.cu");session.Context["op"]="logout";Assert(!session.ValidFor("test@nauta.com.cu"),"Admite operar sobre la sesión desde el contador");});
            Check("Caída sin popup HTTP recupera sesión y contador",delegate{
                using(var test=new Scenario()){
                    test.Portal.OnLogin=(form,user,password,token)=>{Assert(user=="test@nauta.com.cu"&&password=="fake-password"&&form.Login,"Envía otro usuario o un formulario no válido");test.Internet=true;return new PageResponse{Code=200,Uri=new Uri(Origin,"/web/online.do"),Body=OnlineHtml()};};
                    test.Cycle();Assert(test.Portal.Logins==0,"Autentica ante un solo fallo");test.Cycle();
                    Assert(test.DirectChecks==1&&test.Portal.Logins==1&&test.Engine.Current.Light==Light.Green&&test.Engine.Metrics.Remaining==AccountTime.ParseDuration("125:09:30")&&test.Portal.LastSession.Csrf=="online-csrf","La ausencia de popup sigue impidiendo recuperar login o contador");
                }
            });
            Check("Fallo VPN con Internet físico no inicia sesión",delegate{using(var test=new Scenario()){test.RouterInternet=true;test.Cycle();test.Cycle();Assert(test.Portal.Logins==0&&test.Engine.Current.Light==Light.Yellow&&test.DirectChecks==1,"Reautentica cuando el router ya tiene Internet");}});
            Check("Internet funcional no consulta login ni sonda física",delegate{using(var test=new Scenario()){test.Internet=true;test.Cycle();test.Cycle();Assert(test.Portal.Logins==0&&test.DirectChecks==0&&test.Engine.Current.Light==Light.Green,"Hace reconexiones innecesarias");}});
            Check("MAN sigue detectando portal sin enviar login",delegate{using(var test=new Scenario()){test.Engine.SetAutoLogin(false);test.Cycle();test.Cycle();Assert(test.Portal.Logins==0&&test.Engine.Current.Message=="Login automático desactivado","MAN envía credenciales de login");}});
            Check("Usuario ya conectado no bloquea permanentemente AUTO",delegate{using(var test=new Scenario()){test.Captive=CaptiveState.LoginRequired;test.Portal.LoginAnswer="<script>alert('El usuario ya está conectado');</script>";test.Cycle();test.Cycle();var retry=(RetryPolicy)typeof(Engine).GetField("retry",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(test.Engine);Assert(test.Portal.Logins==1&&!retry.AuthBlocked&&test.Engine.Current.Message=="Sesión Nauta en uso","Un estado transitorio desactiva AUTO");test.Cycle();Assert(test.Portal.Logins==1,"Repite login sin esperar");}});
            Check("Vincular contador verifica Nauta y guarda solo contexto protegido",delegate{using(var test=new Scenario()){test.Engine.ImportSessionHtml(SavedOnline());Assert(test.Portal.Logins==0&&test.Portal.TimeQueries==1&&test.Engine.Metrics.Remaining.HasValue,"Importar inicia login o no consulta contador");string file=File.ReadAllText(Path.Combine(test.DirectoryPath,"config.json"));Assert(!file.Contains("online-csrf")&&!file.Contains("12345678abcdef")&&test.Engine.Settings.ProtectedSession.Length>0,"Guarda tokens sin protección");}});
            Check("Importación fallida conserva el contador existente",delegate{using(var test=new Scenario()){test.Engine.ImportSessionHtml(SavedOnline());string previous=test.Engine.Settings.ProtectedSession;test.Portal.TimeAnswer="errorop";bool rejected=false;try{test.Engine.ImportSessionHtml(SavedOnline());}catch(ArgumentException){rejected=true;}Assert(rejected&&test.Engine.Settings.ProtectedSession==previous&&test.Engine.Metrics.Remaining.HasValue&&test.Portal.Logins==0,"Una importación inválida pierde la sesión o autentica");}});
            Check("Sin VPN verifica Internet por la ruta física del sistema",delegate{
                var lan=new NetworkLink{Name="Ethernet",Index=10,LocalIp=System.Net.IPAddress.Parse("192.0.2.10")};int calls=0;
                var result=Connectivity.CheckCurrent((host,token)=>new[]{System.Net.IPAddress.Parse("198.51.100.1")},address=>lan,(uri,address,link,token)=>{calls++;Assert(link==lan&&uri.Scheme=="https"&&uri.Host=="www.gstatic.com","No sigue la salida directa HTTPS");return new PageResponse{Code=204,Body=""};},CancellationToken.None);
                Assert(result.Connected&&result.Interface==lan&&calls==1,"Exige una VPN aunque haya Internet directo");
            });
            Check("Con VPN conserva la ruta del sistema sin forzar la LAN",delegate{
                var tunnel=new NetworkLink{Name="Tunel",Index=42};var result=Connectivity.CheckCurrent((host,token)=>new[]{System.Net.IPAddress.Parse("198.51.100.2")},address=>tunnel,(uri,address,link,token)=>{Assert(link==tunnel,"Evita la salida seleccionada por Windows");return new PageResponse{Code=204,Body=""};},CancellationToken.None);Assert(result.Connected&&result.Interface==tunnel,"No comprueba la salida del túnel");
            });
            Check("Sin ruta no envía sondas ni declara conexión",delegate{int sent=0;var result=Connectivity.CheckCurrent((host,token)=>new[]{System.Net.IPAddress.Parse("198.51.100.3")},address=>null,(uri,address,link,token)=>{sent++;return new PageResponse{Code=204,Body=""};},CancellationToken.None);Assert(!result.Connected&&sent==0,"Envia por una interfaz desconocida");});
            Check("Ruta directa con portal cautivo no da verde",delegate{var result=Connectivity.CheckCurrent((host,token)=>new[]{System.Net.IPAddress.Parse("198.51.100.4")},address=>new NetworkLink{Index=10},(uri,address,link,token)=>new PageResponse{Code=200,Body="<form>login</form>"},CancellationToken.None);Assert(!result.Connected,"Confunde un portal con Internet");});
            Check("Cancelar la comprobación actual impide trabajo y resultados tardíos",delegate{
                using(var stop=new CancellationTokenSource()){stop.Cancel();bool canceled=false;try{Connectivity.CheckCurrent((host,token)=>{throw new Exception("No debe resolver");},address=>null,(uri,address,link,token)=>null,stop.Token);}catch(OperationCanceledException){canceled=true;}Assert(canceled,"Ignora cancelación inicial");}
                using(var stop=new CancellationTokenSource()){bool canceled=false;try{Connectivity.CheckCurrent((host,token)=>new[]{System.Net.IPAddress.Parse("198.51.100.5")},address=>new NetworkLink{Index=10},(uri,address,link,token)=>{stop.Cancel();return new PageResponse{Code=204,Body=""};},stop.Token);}catch(OperationCanceledException){canceled=true;}Assert(canceled,"Publica verde después de cancelar");}
            });
            Check("Fallo temporal del contador no invalida la sesión",delegate{Assert(!AccountTime.SessionExpired(503,"FAILURE")&&!AccountTime.SessionExpired(500,"error temporal")&&!AccountTime.SessionExpired(200,"respuesta inesperada")&&AccountTime.SessionExpired(200,"FAILURE")&&AccountTime.SessionExpired(401,""),"Confunde fallo temporal con sesión terminada");});
            Check("DNS cache evita consultas repetidas y caduca",delegate{int calls=0;double now=0;var cache=new AddressCache((host,token)=>{calls++;return new System.Net.IPAddress[]{System.Net.IPAddress.Parse("192.0.2.1")};},()=>now);cache.Get("example.test",CancellationToken.None);now=119;cache.Get("example.test",CancellationToken.None);Assert(calls==1,"Repite DNS en estado estable");now=121;cache.Get("example.test",CancellationToken.None);Assert(calls==2,"No renueva DNS caducado");cache.Invalidate("example.test");cache.Get("example.test",CancellationToken.None);Assert(calls==3,"Un fallo no invalida el cache");});
            Check("Cambio de red durante DNS no conserva resultado viejo",delegate{int calls=0;AddressCache cache=null;cache=new AddressCache((host,token)=>{calls++;if(calls==1)cache.Clear();return new System.Net.IPAddress[]{System.Net.IPAddress.Parse("192.0.2.1")};},()=>0);cache.Get("example.test",CancellationToken.None);cache.Get("example.test",CancellationToken.None);Assert(calls==2,"Guarda DNS de la red anterior");});
            Check("Cancelación se respeta incluso con DNS cacheado",delegate{var cache=new AddressCache((host,token)=>new System.Net.IPAddress[]{System.Net.IPAddress.Loopback},()=>0);cache.Get("test",CancellationToken.None);using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{cache.Get("test",cancel.Token);}catch(OperationCanceledException){stopped=true;}Assert(stopped,"Una lectura cacheada ignora la pausa");}});
            Check("Timeout TLS no bloquea la cuenta como certificado inválido",delegate{var timed=new NetworkFailure("test",new RequestTimeoutException(new System.Security.Authentication.AuthenticationException("handshake abortado")));var invalid=new NetworkFailure("test",new System.Security.Authentication.AuthenticationException("certificado inválido"));Assert(!timed.Certificate&&invalid.Certificate,"Clasifica una espera agotada como error permanente de certificado");});
            Check("Configuración sin cambios no vuelve a escribir disco",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaSave-"+Guid.NewGuid().ToString("N"));try{var store=new SettingsStore(dir);var settings=new AppSettings();store.Save(settings);var stamp=File.GetLastWriteTimeUtc(store.ConfigPath);Thread.Sleep(25);store.Save(settings);Assert(File.GetLastWriteTimeUtc(store.ConfigPath)==stamp,"Reescribe configuración idéntica");settings.IndicatorPinned=true;store.Save(settings);Assert(store.Load().IndicatorPinned,"El cache pierde una preferencia modificada");}finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}});
            Check("Registros acotados conservan archivos ajenos",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaLogs-"+Guid.NewGuid().ToString("N"));try{var store=new SettingsStore(dir);for(int i=1;i<=6;i++)File.WriteAllText(Path.Combine(dir,"eventos-2024010"+i+"-000000000.log"),"old");string unrelated=Path.Combine(dir,"eventos-mis-notas.log");File.WriteAllText(unrelated,"keep");File.WriteAllText(Path.Combine(dir,"eventos.log"),new string('x',513*1024));store.Log(new Status(Light.Gray,"test","test"));int count=0;foreach(string file in Directory.GetFiles(dir,"eventos-*.log"))if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file),@"^eventos-\d{8}-\d{9}\.log$"))count++;Assert(count<=4&&File.ReadAllText(unrelated)=="keep","Registro ilimitado o borrado de archivo ajeno");}finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}});
            Check("Cerrar motor libera el evento y es idempotente",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaClose-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);try{var handle=(AutoResetEvent)typeof(Engine).GetField("wake",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);engine.Shutdown();engine.Shutdown();bool closed=false;try{handle.WaitOne(0);}catch(ObjectDisposedException){closed=true;}Assert(closed,"Deja abierto un recurso nativo al cerrar");}finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}});
            Check("Comprobar ahora no espera por el ciclo de red",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaWake-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);var gate=typeof(Engine).GetField("cycleGate",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false)){var thread=new Thread(()=>{lock(gate){entered.Set();release.WaitOne(2000);}});try{engine.Start();thread.Start();entered.WaitOne();var watch=System.Diagnostics.Stopwatch.StartNew();engine.CheckNow();Assert(watch.ElapsedMilliseconds<250,"La acción queda bloqueada por una petición de red");}finally{release.Set();thread.Join();engine.Shutdown();foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}}});
            Check("Handshake lento tiene límite total sin omitir HTTPS",delegate{
                var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,8443);listener.Start();
                using(var stop=new ManualResetEvent(false)){
                    var server=new Thread(()=>{try{using(var accepted=listener.AcceptTcpClient())stop.WaitOne(2000);}catch(System.Net.Sockets.SocketException){}});server.IsBackground=true;server.Start();
                    try{int index=0;foreach(var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())if(nic.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Loopback)index=nic.GetIPProperties().GetIPv4Properties().Index;
                        string redirect;bool timed=false;var watch=System.Diagnostics.Stopwatch.StartNew();try{PinnedHttp.Send(PortalClient.Home,System.Net.IPAddress.Loopback,new NetworkLink{Index=index,LocalIp=System.Net.IPAddress.Loopback},null,new System.Net.CookieContainer(),CancellationToken.None,out redirect,350);}catch(RequestTimeoutException){timed=true;}
                        Assert(timed&&watch.ElapsedMilliseconds<1800,"La negociación TLS puede quedarse bloqueada");
                    }finally{stop.Set();listener.Stop();server.Join(2500);}
                }
            });
            Check("Guardar misma cuenta conserva contador; cambiarla lo elimina",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaTests-"+Guid.NewGuid().ToString("N"));var store=new SettingsStore(dir);string session=SettingsStore.Protect("session-test");store.Save(new AppSettings{Username="test@nauta.com.cu",ProtectedPassword=SettingsStore.Protect("test-password"),ProtectedSession=session});var engine=new Engine(store,true);try{engine.SaveAccount("test@nauta.com.cu","");Assert(engine.Settings.ProtectedSession==session,"Guardar sin cambiar cuenta perdió el contador");engine.SaveAccount("nueva@nauta.com.cu","new-test-password");Assert(engine.Settings.ProtectedSession==""&&engine.Metrics.Remaining==null,"Una cuenta nueva conservaría datos de la anterior");}finally{engine.Shutdown();foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}});
            Check("Velocidad real usa deltas y segundos transcurridos",delegate{var sample=new RateMonitor();sample.Sample(new List<TrafficCounter>{new TrafficCounter{Id="eth",Received=1000,Sent=2000}},10);var rate=sample.Sample(new List<TrafficCounter>{new TrafficCounter{Id="eth",Received=5000,Sent=3000}},12);Assert(rate.RatesReady&&rate.DownloadBytesPerSecond==2000&&rate.UploadBytesPerSecond==500,"Velocidad incorrecta");});
            Check("Reinicio de contador e interfaz nueva no disparan velocidad",delegate{var sample=new RateMonitor();sample.Sample(new List<TrafficCounter>{new TrafficCounter{Id="eth",Received=10000,Sent=5000}},1);var rate=sample.Sample(new List<TrafficCounter>{new TrafficCounter{Id="eth",Received=10,Sent=5},new TrafficCounter{Id="wifi",Received=9000000,Sent=1000000}},2);Assert(rate.DownloadBytesPerSecond==0&&rate.UploadBytesPerSecond==0,"Muestra tráfico inventado al reiniciar o conectar una interfaz");});
            Check("No suma la VPN junto a la interfaz física",delegate{Assert(RateMonitor.IsPhysical(System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,"Realtek PCIe GbE")&&!RateMonitor.IsPhysical(System.Net.NetworkInformation.NetworkInterfaceType.Ethernet,"WireGuard Tunnel")&&!RateMonitor.IsPhysical(System.Net.NetworkInformation.NetworkInterfaceType.Tunnel,"VPN"),"Cuenta tráfico del túnel dos veces");});
            Check("Horas de Nauta pueden superar un día",delegate{var time=AccountTime.ParseDuration("125:09:30");Assert(time.HasValue&&time.Value.TotalHours>125&&RateMonitor.TimeText(time)=="125h 09m"&&AccountTime.ParseDuration("02:60:00")==null,"Duración inválida o truncada");});
            Check("Cuenta distingue tiempo restante del historial",delegate{var reading=AccountTime.Parse("<table><tr><td>Crédito:</td><td>250.5 CUP</td></tr><tr><th>Desde</th><th>Tiempo</th></tr><tr><td>2026/10/12</td><td>03:30:31</td></tr></table>");Assert(reading.Remaining==null&&reading.Credit==250.5m,"Confunde una conexión pasada con el tiempo restante");});
            Check("Tiempo explícito de cuenta se reconoce",delegate{var reading=AccountTime.Parse("<tr><td>Tiempo disponible:</td><td>125:09:30</td></tr>");Assert(reading.Remaining.HasValue&&reading.Remaining.Value.TotalHours>125,"No reconoce dato oficial");});
            Check("Consulta de tiempo no puede iniciar ni cerrar sesión",delegate{Assert(PortalClient.InformationEndpoint(new Uri(Origin,"/EtecsaQueryServlet"))&&!PortalClient.InformationEndpoint(new Uri(Origin,"/LoginServlet"))&&!PortalClient.InformationEndpoint(new Uri(Origin,"/LogoutServlet")),"La consulta permitiría cambiar una sesión");});
            Check("Captura de contador requiere sesión HTTPS confirmada",delegate{var form=PortalParser.Parse(LoginHtml("/LoginServlet"),Origin);var response=new PageResponse{Uri=new Uri(Origin,"/online.do"),Body="Usted está conectado <a>Cerrar sesión</a> ATTRIBUTE_UUID=12345678abcdef"};var session=NautaSession.Capture(form,response,"test@nauta.com.cu");Assert(session!=null&&session.ValidFor("test@nauta.com.cu")&&!session.ValidFor("otra@nauta.com.cu"),"Sesión no vinculada a su cuenta");response.Uri=new Uri("https://evil.test/online.do");Assert(NautaSession.Capture(form,response,"test@nauta.com.cu")==null,"Captura token de origen externo");});
            Check("Desactivar login cancela intento y conserva vigilancia",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaTests-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);var field=typeof(Engine).GetField("cycle",BindingFlags.NonPublic|BindingFlags.Instance);try{engine.Start();using(var token=new CancellationTokenSource()){field.SetValue(engine,token);engine.SetAutoLogin(false);Assert(token.IsCancellationRequested&&engine.Running&&!engine.Settings.AutoLoginEnabled&&!new SettingsStore(dir).Load().AutoLoginEnabled,"Apagar login no cancela o apaga la vigilancia");field.SetValue(engine,null);}}finally{field.SetValue(engine,null);engine.Shutdown();foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}});
            Check("Modo de barra y login sobreviven al reinicio",delegate{string dir=Path.Combine(Path.GetTempPath(),"AutoNautaTests-"+Guid.NewGuid().ToString("N"));try{var store=new SettingsStore(dir);store.Save(new AppSettings{IndicatorPinned=true,AutoLoginEnabled=false,ProtectedSession=SettingsStore.Protect("session-test-secret")});var settings=store.Load();Assert(settings.IndicatorPinned&&!settings.AutoLoginEnabled&&SettingsStore.Unprotect(settings.ProtectedSession)=="session-test-secret"&&!File.ReadAllText(store.ConfigPath).Contains("session-test-secret"),"No conserva preferencias o expone el token");}finally{foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}});
            Check("Sondas públicas HTTPS solo admiten lectura",delegate{var uri=new Uri("https://www.gstatic.com/generate_204");Assert(PinnedHttp.TrustedRequest(uri,null)&&!PinnedHttp.TrustedRequest(uri,"password=secret")&&!PinnedHttp.TrustedRequest(new Uri("https://www.gstatic.com/otra-ruta"),null)&&!PinnedHttp.TrustedRequest(new Uri("/relativo",UriKind.Relative),null),"Sonda permitiría enviar credenciales o cambiar de destino");});
            Check("Socket selecciona interfaz IPv4 con orden correcto",delegate{
                foreach(var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())if(nic.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Loopback){
                    int index=nic.GetIPProperties().GetIPv4Properties().Index;
                    using(var socket=new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork,System.Net.Sockets.SocketType.Stream,System.Net.Sockets.ProtocolType.Tcp)){
                        SocketPath.Select(socket,new NetworkLink{Index=index,LocalIp=System.Net.IPAddress.Loopback});
                        Assert((int)socket.GetSocketOption(System.Net.Sockets.SocketOptionLevel.IP,(System.Net.Sockets.SocketOptionName)31)==index,"La interfaz configurada en Windows no coincide");
                    }
                    return;
                }
                throw new Exception("No se encontró la interfaz de bucle local de Windows");
            });
            Check("Inicio sin elevación conserva controles de pausa",delegate{
                string dir=Path.Combine(Path.GetTempPath(),"NautaTests-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);
                try{engine.Start();Assert(engine.Running&&engine.Current.Light!=Light.Red,"No inicia la vigilancia");engine.Pause();Assert(!engine.Running,"No detiene la vigilancia");}
                finally{engine.Shutdown();foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}
            });
            Check("Internet directo evita autenticar por fallo VPN",delegate{Assert(CaptiveProbe.Classify("HTTP/1.1 200 OK\r\nContent-Length: 22\r\n\r\nMicrosoft Connect Test")==CaptiveState.Internet,"No reconoce Internet existente");});
            Check("Redirección Nauta confirma necesidad de acceso",delegate{Assert(CaptiveProbe.Classify("HTTP/1.1 302 Found\r\nLocation: https://secure.etecsa.net:8443/\r\n\r\n")==CaptiveState.LoginRequired,"No reconoce petición de Nauta");});
            Check("Formulario aislado no confirma caída Nauta",delegate{Assert(CaptiveProbe.Classify("HTTP/1.1 200 OK\r\n\r\n"+LoginHtml("/LoginServlet"))==CaptiveState.Unknown,"Autenticaría ante un formulario ambiguo");});
            Check("No autentica ante redirección externa o ambigua",delegate{Assert(CaptiveProbe.Classify("HTTP/1.1 302 Found\r\nLocation: https://evil.test/\r\n\r\n")==CaptiveState.Unknown&&CaptiveProbe.Classify("HTTP/1.1 302 Found\r\nLocation: https://secure.etecsa.net:8443/\r\nLocation: https://evil.test/\r\n\r\n")==CaptiveState.Unknown,"Admite contexto no confirmado");});
            Check("HTTPS exacto y puerto correctos",delegate { Assert(PortalParser.Trusted(Origin),"No reconoce origen autorizado"); });
            Check("Rechaza HTTP y dominio parecido",delegate { Assert(!PortalParser.Trusted(new Uri("http://secure.etecsa.net:8443/")) && !PortalParser.Trusted(new Uri("https://secure.etecsa.net.evil.test:8443/")),"Origen inseguro aceptado"); });
            Check("Rechaza usuario embebido y otro puerto",delegate { Assert(!PortalParser.Trusted(new Uri("https://user@secure.etecsa.net:8443/")) && !PortalParser.Trusted(new Uri("https://secure.etecsa.net/")),"Origen incorrecto aceptado"); });
            Check("Formulario conserva CSRF y contexto",delegate { var p=PortalParser.Parse(LoginHtml("/LoginServlet"),Origin); Assert(p.Login && p.Action.AbsolutePath=="/LoginServlet" && p.Fields["CSRFHW"]=="a&b" && p.UserField=="username" && p.PasswordField=="password", "Formulario/contexto incorrectos"); });
            Check("Rechaza envío de credenciales a otro host",delegate { var p=PortalParser.Parse(LoginHtml("https://evil.test/LoginServlet"),Origin); Assert(!p.Login && p.Error.Length>0,"No bloquea destino externo"); });
            Check("Rechaza acción HTTP",delegate { var p=PortalParser.Parse(LoginHtml("http://secure.etecsa.net:8443/LoginServlet"),Origin); Assert(!p.Login && p.Error.Length>0,"No bloquea HTTP"); });
            Check("Rechaza formulario sin contexto Nauta",delegate { var p=PortalParser.Parse("<form method=post action=/LoginServlet><input name=username><input type=password name=password></form>",Origin); Assert(!p.Login && p.Error.Length>0,"Formulario desconocido admitido"); });
            Check("Comentarios y scripts no forman login",delegate { var p=PortalParser.Parse("<!--"+LoginHtml("/LoginServlet")+"--><script>var x=\""+LoginHtml("/LoginServlet")+"\";</script>",Origin); Assert(!p.Login,"Login falso detectado"); });
            Check("Sesión ya abierta no pide autenticar",delegate { var p=PortalParser.Parse("<div>Usted está conectado</div><a href=/LogoutServlet>Cerrar sesión</a>",Origin); Assert(p.Connected && !p.Login,"Sesión activa no reconocida"); });
            Check("Error de contraseña detiene intentos",delegate { var p=PortalParser.Parse("<script>alert(\"Usuario o contraseña incorrectos\");</script>",Origin); Assert(p.Fatal && p.Error.Length>0,"No identifica error permanente"); });
            Check("Saldo insuficiente detiene intentos",delegate { var p=PortalParser.Parse("<script>alert('Saldo insuficiente');</script>",Origin); Assert(p.Fatal,"No bloquea saldo insuficiente"); });
            Check("CAPTCHA necesita intervención",delegate { var p=PortalParser.Parse(LoginHtml("/LoginServlet")+"<img src=captcha.png>",Origin); Assert(!p.Login && p.Fatal,"CAPTCHA no bloqueado"); });
            Check("Codificación protege caracteres especiales",delegate { var f=new Dictionary<string,string>(); f["username"]="a+b@nauta.com.cu"; f["password"]="x&y=z"; string data=PortalParser.Encode(f); Assert(data.Contains("a%2Bb%40nauta.com.cu")&&data.Contains("x%26y%3Dz"),"Datos mal codificados"); });
            Check("Confirma múltiples fallos",delegate { var p=new RetryPolicy(); Assert(!p.ConfirmFailure()&&p.ConfirmFailure(),"Reintenta ante un único fallo"); });
            Check("Internet sano reinicia fallos",delegate { var p=new RetryPolicy(); p.ConfirmFailure(); p.ConfirmFailure(); p.Healthy(); Assert(p.Failures==0,"Fallos no reiniciados"); });
            Check("Espacia autenticaciones",delegate { var p=new RetryPolicy(); var now=DateTime.UtcNow; Assert(p.CanLogin(now),"Primer intento prohibido"); p.LastLogin=now; Assert(!p.CanLogin(now.AddSeconds(30))&&p.CanLogin(now.AddSeconds(91)),"Repite muy pronto"); });
            Check("Bloqueo de cuenta conserva estado",delegate { var p=new RetryPolicy(); p.AuthBlocked=true; p.Healthy(); Assert(p.AuthBlocked&&!p.CanLogin(DateTime.UtcNow),"Borra bloqueo de autenticación"); });
            Check("Espera creciente y limitada",delegate { var p=new RetryPolicy(); p.Failures=2; int first=p.DelaySeconds(15); p.Failures=30; Assert(first>=15&&p.DelaySeconds(15)<=120&&p.DelaySeconds(15)>first,"Espera incorrecta"); });
            Check("Formulario duplicado no se envía",delegate { string h=LoginHtml("/LoginServlet").Replace("</form>","<input type=hidden name=CSRFHW value=evil></form>"); Assert(!PortalParser.Parse(h,Origin).Login,"Admite parámetros ambiguos"); });
            Check("Contraseña cifrada recuperable y no literal",delegate { string encrypted=SettingsStore.Protect("test-no-real-secret-ñ&"); Assert(encrypted!="test-no-real-secret-ñ&" && SettingsStore.Unprotect(encrypted)=="test-no-real-secret-ñ&","Falla almacenamiento protegido"); });
            Check("Configuración conserva posición y secreto protegido",delegate { string dir=Path.Combine(Path.GetTempPath(),"NautaTests-"+Guid.NewGuid().ToString("N")); try { var store=new SettingsStore(dir); var c=new AppSettings { Username="test@nauta.com.cu", ProtectedPassword=SettingsStore.Protect("test-no-real-secret"), IndicatorX=42,IndicatorY=84 }; store.Save(c); var loaded=store.Load(); Assert(loaded.IndicatorX==42 && loaded.IndicatorY==84 && SettingsStore.Unprotect(loaded.ProtectedPassword)=="test-no-real-secret","Configuración incorrecta"); Assert(!File.ReadAllText(store.ConfigPath).Contains("test-no-real-secret"),"Contraseña en texto plano"); } finally { if(Directory.Exists(dir)) { foreach(string f in Directory.GetFiles(dir)) File.Delete(f); Directory.Delete(dir); } } });
            Check("Configuración dañada no se sobrescribe",delegate { string dir=Path.Combine(Path.GetTempPath(),"NautaTests-"+Guid.NewGuid().ToString("N")); try { var store=new SettingsStore(dir); File.WriteAllText(store.ConfigPath,"invalid"); bool blocked=false; try { store.Load(); } catch(InvalidDataException) { blocked=true; } Assert(blocked&&File.ReadAllText(store.ConfigPath)=="invalid","Sobrescribe configuración dañada"); } finally { if(Directory.Exists(dir)) { foreach(string f in Directory.GetFiles(dir)) File.Delete(f); Directory.Delete(dir); } } });
            Check("Tres fallos de autenticación frenan reintentos",delegate { var p=new RetryPolicy(); Assert(!p.AuthenticationFailed()&&!p.AuthenticationFailed()&&p.AuthenticationFailed()&&!p.CanLogin(DateTime.UtcNow),"Reintentos ilimitados"); });
            Check("Reconoce sesión en uso sin volver a autenticar",delegate { var p=PortalParser.Parse("<script>alert('La sesión se encuentra activa en otro equipo');</script>",Origin); Assert(p.SessionInUse&&!p.Login,"No reconoce sesión en uso"); });
            Check("HTTP 200 de portal no confirma Internet",delegate { Assert(!Connectivity.IsExpected(0,200,"<form>login</form>")&&!Connectivity.IsExpected(1,200,"<form>login</form>"),"Falso positivo de Internet"); });
            Check("Valida respuesta exacta de conexión",delegate { Assert(Connectivity.IsExpected(0,204,"")&&Connectivity.IsExpected(1,200,"Microsoft Connect Test"),"Respuesta legítima rechazada"); });
            Check("HTTP fragmentado conserva UTF8",delegate { byte[] data=Encoding.UTF8.GetBytes("3\r\nabc\r\n2\r\nñ\r\n0\r\n\r\n"); Assert(Encoding.UTF8.GetString(PinnedHttp.DecodeChunks(data))=="abcñ","Pierde caracteres en HTTP fragmentado"); });
            Check("Rechaza HTTP fragmentado incompleto",delegate { bool rejected=false; try{PinnedHttp.DecodeChunks(Encoding.ASCII.GetBytes("4\r\nabc"));}catch(IOException){rejected=true;}Assert(rejected,"Admite respuesta truncada"); });
            Check("Pausa impide publicar verde desde un ciclo cancelado",delegate {
                string dir=Path.Combine(Path.GetTempPath(),"NautaTests-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);
                FieldInfo worker=typeof(Engine).GetField("worker",BindingFlags.Instance|BindingFlags.NonPublic), cycle=typeof(Engine).GetField("cycle",BindingFlags.Instance|BindingFlags.NonPublic);
                try{using(var token=new CancellationTokenSource()){worker.SetValue(engine,Thread.CurrentThread);cycle.SetValue(engine,token);engine.Pause();
                    typeof(Engine).GetMethod("Set",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(engine,new object[]{Light.Green,"Internet conectado","Solicitud completada justo antes de pausar"});
                    Assert(engine.Current.Light==Light.Gray,"Ciclo cancelado cambió el indicador a verde");}}
                finally{worker.SetValue(engine,null);cycle.SetValue(engine,null);engine.Shutdown();foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}
            });
            Check("POST con respuesta perdida consume el límite",delegate { var p=new RetryPolicy(); for(int i=0;i<3;i++){try{p.LoginAttempt<int>(delegate{throw new IOException("Respuesta perdida");});}catch(IOException){}} Assert(p.AuthFailures==3&&p.AuthBlocked,"POST sin respuesta se reintentaría indefinidamente"); });
            Check("Cancelar una autenticación no consume el límite",delegate { var p=new RetryPolicy();try{p.LoginAttempt<int>(delegate{throw new OperationCanceledException();});}catch(OperationCanceledException){}Assert(p.AuthFailures==0,"Pausar consume intentos de cuenta"); });
            Check("Guardar cuenta cancela el intento anterior",delegate { string dir=Path.Combine(Path.GetTempPath(),"NautaTests-"+Guid.NewGuid().ToString("N"));var engine=new Engine(new SettingsStore(dir),true);FieldInfo cycle=typeof(Engine).GetField("cycle",BindingFlags.NonPublic|BindingFlags.Instance);var retry=(RetryPolicy)typeof(Engine).GetField("retry",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine);try{using(var cancel=new CancellationTokenSource()){cycle.SetValue(engine,cancel);retry.AuthBlocked=true;engine.SaveAccount("test@nauta.com.cu","test-no-real-secret");Assert(cancel.IsCancellationRequested&&!retry.AuthBlocked&&retry.AuthFailures==0,"No cancela el ciclo antiguo ni desbloquea la cuenta nueva");}}finally{cycle.SetValue(engine,null);engine.Shutdown();foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);} });
            Console.WriteLine("RESULTADO: "+passed+" correctas, "+failed+" fallidas"); return failed==0?0:1;
        }
    }
    public static class TestEntry { public static int Main() { return CoreTests.Run(); } }
}
