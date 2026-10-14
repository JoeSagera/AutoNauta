using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NautaReconnect;

// Two separate processes exercise OUTOFCONTEXT callbacks without network or account data.
internal sealed class OverlayOrderProbe : Form {
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
    [STAThread] static int Main(string[] args){
        Application.EnableVisualStyles();
        using(var form=new OverlayOrderProbe{Text="AutoNauta prueba de superposición",FormBorderStyle=FormBorderStyle.None,TopMost=true,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(180,180,160,32)}){
            if(args.Length==3&&args[0]=="--cover")return Cover(form,new IntPtr(long.Parse(args[1])),args[2]);
            string result=Path.Combine(Path.GetTempPath(),"AutoNautaOverlay-"+Guid.NewGuid().ToString("N")+".txt");
            int code=1;Process child=null;OverlayOrderGuard guard=null;
            using(var timeout=new Timer{Interval=12000})using(var poll=new Timer{Interval=100}){
                form.Shown+=delegate{
                    guard=new OverlayOrderGuard(form,delegate{return false;});
                    if(!guard.Listening){form.Close();return;}
                    child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--cover "+form.Handle.ToInt64()+" \""+result+"\""){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden});
                    poll.Start();timeout.Start();
                };
                poll.Tick+=delegate{
                    if(!File.Exists(result))return;
                    string text=File.ReadAllText(result);
                    if(text=="PASS"&&guard.Reassertions>=2){code=0;Console.WriteLine("PASS eventos reales entre procesos: ventana topmost recuperada dos veces, foco conservado");}
                    else Console.Error.WriteLine("FAIL prueba nativa: "+text+", recuperaciones="+guard.Reassertions);
                    form.Close();
                };
                timeout.Tick+=delegate{Console.Error.WriteLine("FAIL timeout de eventos nativos");form.Close();};
                try{Application.Run(form);}finally{
                    poll.Stop();timeout.Stop();if(guard!=null)guard.Dispose();
                    if(child!=null){if(!child.WaitForExit(2000))child.Kill();child.Dispose();}
                    if(File.Exists(result))File.Delete(result);
                }
            }
            return code;
        }
    }
    static int Cover(Form form,IntPtr guarded,string result){
        IntPtr focus=GetForegroundWindow();bool first=false,second=false;int stage=0;
        using(var timer=new Timer{Interval=650}){
            form.Shown+=delegate{form.BeginInvoke((MethodInvoker)delegate{
                form.TopMost=false;form.TopMost=true;
                SetWindowPos(form.Handle,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010);
                SetWindowPos(form.Handle,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);
                // The other process can recover before this thread inspects Z-order.
                first=(GetWindowLongPtr(form.Handle,-20).ToInt64()&8)!=0;timer.Start();
            });};
            timer.Tick+=delegate{
                if(stage++==0){
                    first=first&&Above(guarded,form.Handle)&&GetForegroundWindow()==focus;
                    SetWindowPos(form.Handle,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0010);second=(GetWindowLongPtr(form.Handle,-20).ToInt64()&8)!=0;return;
                }
                second=second&&Above(guarded,form.Handle)&&GetForegroundWindow()==focus;
                File.WriteAllText(result,first&&second?"PASS":"FAIL cobertura inicial="+first+", cobertura posterior="+second);form.Close();
            };
            Application.Run(form);
        }
        return 0;
    }
    static bool Above(IntPtr first,IntPtr second){IntPtr next=first;for(int i=0;i<10000&&next!=IntPtr.Zero;i++){next=GetWindow(next,2);if(next==second)return true;}return false;}
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
}
