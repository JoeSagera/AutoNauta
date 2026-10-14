using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NautaReconnect {
    // Event-driven ordering on the current desktop. No game hooks or focus changes.
    internal sealed class OverlayOrderGuard : IDisposable {
        readonly Form window;
        readonly Func<bool> suspended;
        readonly WinEventProc callback;
        readonly Timer settle=new Timer{Interval=350};
        IntPtr foregroundHook,orderHook;
        bool disposed,queued;
        internal bool Listening{get{return foregroundHook!=IntPtr.Zero&&orderHook!=IntPtr.Zero;}}
        internal bool Pending{get{return queued||settle.Enabled;}}
        internal int Reassertions{get;private set;}
        internal bool TestOwnWindows=false;
        internal OverlayOrderGuard(Form window,Func<bool> suspended){
            this.window=window;this.suspended=suspended;callback=Changed;
            // OUTOFCONTEXT callbacks arrive on this UI thread; keep the delegate rooted.
            foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,2);
            orderHook=SetWinEventHook(0x8002,0x8004,IntPtr.Zero,callback,0,0,2);
            settle.Tick+=delegate{settle.Stop();Raise();};
        }
        void Changed(IntPtr hook,uint kind,IntPtr target,int objectId,int childId,uint thread,uint time){
            if(disposed||target==IntPtr.Zero)return;
            if(kind==3){Request();return;}
            // Desktop child Z-order changes use OBJID_CLIENT on Windows 11.
            if(kind==0x8004&&target==GetDesktopWindow()&&(objectId==0||objectId==-4)&&childId==0){Request();return;}
            if(objectId!=0||childId!=0)return;
            if(kind==0x8004){
                if(target!=GetDesktopWindow()&&target!=GetForegroundWindow()&&!TopLevelTopmost(target))return;
            }else if(kind==0x8002){
                if(!TopLevelTopmost(target))return;
            }else return;
            Request();
        }
        static bool TopLevelTopmost(IntPtr target){return GetAncestor(target,2)==target&&IsWindowVisible(target)&&(GetWindowLongPtr(target,-20).ToInt64()&8)!=0;}
        internal void Request(){
            if(disposed||queued||!window.IsHandleCreated||window.IsDisposed||window.Disposing)return;
            if(!window.Visible||suspended()){settle.Stop();return;}
            queued=true;
            try{window.BeginInvoke((MethodInvoker)delegate{
                queued=false;if(disposed)return;
                if(!window.Visible||suspended()){settle.Stop();return;}
                Raise();settle.Stop();settle.Start();
            });}catch(InvalidOperationException){queued=false;}
        }
        void Raise(){
            if(disposed||!window.IsHandleCreated||window.IsDisposed||window.Disposing||!window.Visible||IsIconic(window.Handle)||suspended())return;
            if(!Covered())return;
            // Never show, restore, resize, activate or redraw the surface here.
            IntPtr order=(GetWindowLongPtr(window.Handle,-20).ToInt64()&8)!=0?IntPtr.Zero:new IntPtr(-1);
            if(SetWindowPos(window.Handle,order,0,0,0,0,0x0001|0x0002|0x0010|0x0200))Reassertions++;
        }
        bool Covered(){
            if((GetWindowLongPtr(window.Handle,-20).ToInt64()&8)==0)return true;
            RECT own;if(!GetWindowRect(window.Handle,out own))return false;
            IntPtr next=GetWindow(window.Handle,3);
            uint currentProcess=GetCurrentProcessId();
            // Own menus/tooltips stay above the indicator. Inspect only on events.
            for(int i=0;i<512&&next!=IntPtr.Zero;i++,next=GetWindow(next,3)){
                uint process;GetWindowThreadProcessId(next,out process);
                RECT other;if((TestOwnWindows||process!=currentProcess)&&IsWindowVisible(next)&&!IsIconic(next)&&GetWindowRect(next,out other)&&other.Left<own.Right&&other.Right>own.Left&&other.Top<own.Bottom&&other.Bottom>own.Top)return true;
            }
            return false;
        }
        internal void TestSettle(){settle.Stop();Raise();}
        public void Dispose(){
            if(disposed)return;disposed=true;queued=false;settle.Stop();settle.Dispose();
            if(foregroundHook!=IntPtr.Zero){UnhookWinEvent(foregroundHook);foregroundHook=IntPtr.Zero;}
            if(orderHook!=IntPtr.Zero){UnhookWinEvent(orderHook);orderHook=IntPtr.Zero;}
            GC.KeepAlive(callback);
        }
        delegate void WinEventProc(IntPtr hook,uint kind,IntPtr target,int objectId,int childId,uint thread,uint time);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out RECT rectangle);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [StructLayout(LayoutKind.Sequential)] struct RECT{internal int Left,Top,Right,Bottom;}
    }
}
