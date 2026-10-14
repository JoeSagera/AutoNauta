using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using NautaReconnect;

internal static class StartupUiProbe {
    static void Assert(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    static object Entry(){using(var key=Registry.CurrentUser.OpenSubKey(WindowsStartup.RunPath))return key==null?null:key.GetValue(WindowsStartup.ValueName);}
    [STAThread]
    static int Main(){
        string folder=Path.Combine(Path.GetTempPath(),"AutoNautaStartupUi-"+Guid.NewGuid().ToString("N"));
        try {
            Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            object before=Entry();
            using(var context=new UiContext(new Engine(new SettingsStore(folder),true),true,true)) {
                Application.DoEvents();
                var main=(Form)typeof(UiContext).GetField("main",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(context);
                var indicator=(Form)typeof(UiContext).GetField("indicator",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(context);
                Assert(!main.Visible&&indicator.Visible,"El inicio en segundo plano abre solo el indicador.");
                Assert(main.Text.Contains("AutoNauta "+AppVersion.Text),"El título incluye la versión real del ejecutable.");
                Assert(Object.Equals(before,Entry()),"La vista previa no modifica el arranque personal.");
                typeof(UiContext).GetMethod("ShowMain",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(context,new object[]{false});Application.DoEvents();
                Assert(main.Visible&&indicator.Visible,"El programa en segundo plano permite recuperar la ventana.");
                context.ExitThread();
            }
            Assert(Object.Equals(before,Entry()),"Cerrar la prueba conserva la entrada personal.");
            Console.WriteLine("PASS inicio oculto, indicador visible, versión, recuperación y registro personal intacto");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
        finally {if(Directory.Exists(folder)){foreach(string file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder);}}
    }
}
