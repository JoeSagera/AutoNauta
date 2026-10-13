using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Collections.Generic;
namespace NautaReconnect {
    public static class Program {
        public static readonly string DataPath=SettingsStore.DataDirectory();
        [STAThread]
        public static int Main(string[] args) {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            try {
                if(args.Length>0&&args[0]=="--diagnostic")return Diagnostic(args.Length>1?args[1]:Path.Combine(DataPath,"diagnostico.json"));
                if(args.Length>0&&args[0]=="--self-test")return CoreTests.Run();
                bool preview=args.Length>0&&(args[0]=="--preview"||args[0]=="--ui-smoke");
                string data=preview?Path.Combine(Path.GetTempPath(),"NautaUiPreview-"+Guid.NewGuid().ToString("N")):DataPath;
                using(var mutex=new Mutex(false,"Local\\ReconectorNauta-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value)){
                    bool acquired=preview;try{if(!preview)acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}
                    if(!acquired){MessageBox.Show("AutoNauta ya está abierto. Busca su indicador o el icono junto al reloj.","AutoNauta");return 0;}
                    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                    var engine=new Engine(new SettingsStore(data),preview);var context=new UiContext(engine,preview);
                    try{
                        if(args.Length>0&&args[0]=="--ui-smoke"){context.Smoke(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pruebas-visuales"));context.ExitThread();return 0;}
                        if(args.Length>0&&args[0]=="--resume")engine.Start();
                        if(Array.IndexOf(args,"--background")>=0)context.MinimizeMain();
                        Application.Run(context);
                    }finally{engine.Shutdown();if(!preview)mutex.ReleaseMutex();}
                }
                return 0;
            }catch(Exception ex){
                if(args.Length>0&&(args[0]=="--self-test"||args[0]=="--diagnostic"||args[0]=="--ui-smoke")){Console.Error.WriteLine(ex.ToString());return 1;}
                MessageBox.Show(ex is InvalidDataException?ex.Message:"No se pudo abrir AutoNauta. Los archivos y la configuración se conservan.","AutoNauta",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
            }
        }
        public static int Diagnostic(string file){
            using(var mutex=new Mutex(false,"Local\\ReconectorNauta-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value)){
                bool acquired=false;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
                if(!acquired){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));File.WriteAllText(file,new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"error","Cierra AutoNauta antes de ejecutar el diagnóstico."}}));return 1;}
                try{return DiagnosticCore(file);}finally{mutex.ReleaseMutex();}
            }
        }
        static int DiagnosticCore(string file){
            var result=new Dictionary<string,object>();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            result["fecha"]=DateTime.Now.ToString("o");
            result["administrador"]=new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            try{
                Phase(file,result,"internet_vpn");
                var vpn=Networks.Vpn();result["vpn"]=vpn==null?"Sin interfaz VPN activa":vpn.Name;var trace=new List<string>();result["internet_por_vpn"]=Connectivity.Verify(vpn,CancellationToken.None,trace);result["detalle_pruebas_vpn"]=trace;
                Phase(file,result,"detectar_router");var link=Networks.Router();result["router"]=link.Gateway.ToString();result["interfaz_router"]=link.Name;result["interfaz_sockets"]=link.Index;
                Phase(file,result,"comprobar_router");
                result["estado_nauta_por_router"]=CaptiveProbe.Check(link,CancellationToken.None).ToString();
                Phase(file,result,"resolver_portal");var target=Networks.PortalAddress(link,"10.180.0.30",CancellationToken.None);result["portal_ip"]=target.ToString();
                Phase(file,result,"consultar_portal_https");
                var page=new PortalClient(link,target).Get(CancellationToken.None);result["http"]=page.Code;var parsed=PortalParser.Parse(page.Body,page.Uri);result["formulario_reconocido"]=parsed.Login;result["sesion_abierta"]=parsed.Connected;result["error_portal"]=parsed.Error;result["campos_formulario"]=new List<string>(parsed.Fields.Keys);
            }catch(NetworkFailure ex){result["bloqueo_red"]=ex.Blocked;result["error_certificado"]=ex.Certificate;result["error"]=ex.Message;if(ex.Certificate)result["certificado_portal"]=PinnedHttp.LastCertificateInfo;}
            catch(Exception ex){result["error"]=ex.Message;}
            finally{Phase(file,result,"comprobar_vpn_despues");var after=Networks.Vpn();result["vpn_despues"]=after==null?"Sin interfaz VPN activa":after.Name;var trace=new List<string>();result["internet_por_vpn_despues"]=Connectivity.Verify(after,CancellationToken.None,trace);result["detalle_vpn_despues"]=trace;}
            result["fase"]="terminado";
            File.WriteAllText(file,new JavaScriptSerializer().Serialize(result));return result.ContainsKey("error")?1:0;
        }
        static void Phase(string file,Dictionary<string,object> result,string phase){result["fase"]=phase;File.WriteAllText(file,new JavaScriptSerializer().Serialize(result));}
    }
}
