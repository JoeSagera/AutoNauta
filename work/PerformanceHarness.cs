using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using NautaReconnect;
public static class PerformanceHarness {
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process,uint flag);
    [STAThread]public static int Main(string[] args){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        // Match production DPI awareness where the UI version supports it.
        var native=typeof(UiContext).Assembly.GetType("NautaReconnect.Native");
        if(native!=null){var aware=native.GetMethod("SetProcessDPIAware",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);if(aware!=null)aware.Invoke(null,null);}
        string mode=args[0],report=args[1];int duration=Int32.Parse(args[2]);
        string data=Path.Combine(Path.GetTempPath(),"AutoNautaPerf-"+Guid.NewGuid().ToString("N"));var store=new SettingsStore(data);store.Save(new AppSettings{AutoLoginEnabled=false,IndicatorX=20,IndicatorY=mode=="collapsed"?200:260});
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var engine=new Engine(store,false);var context=new UiContext(engine,false);
        typeof(UiContext).GetMethod("HideMain",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(context,null);
        object indicator=typeof(UiContext).GetField("indicator",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(context);
        indicator.GetType().GetMethod("SetPinned",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(indicator,new object[]{mode=="expanded"});
        var process=Process.GetCurrentProcess();var watch=Stopwatch.StartNew();var rows=new List<object>();double startCpu=0;int[] collections=new int[3];bool measuring=false;
        var timer=new System.Windows.Forms.Timer{Interval=2000};timer.Tick+=delegate{
            process.Refresh();if(!measuring&&watch.Elapsed.TotalSeconds<20)return;
            if(!measuring){measuring=true;watch.Restart();startCpu=process.TotalProcessorTime.TotalSeconds;for(int g=0;g<3;g++)collections[g]=GC.CollectionCount(g);}
            rows.Add(new {second=watch.Elapsed.TotalSeconds,cpu=process.TotalProcessorTime.TotalSeconds,workingMiB=process.WorkingSet64/1048576.0,privateMiB=process.PrivateMemorySize64/1048576.0,handles=process.HandleCount,gdi=GetGuiResources(process.Handle,0),userObjects=GetGuiResources(process.Handle,1),managedMiB=GC.GetTotalMemory(false)/1048576.0});
            if(watch.Elapsed.TotalSeconds<duration)return;
            timer.Stop();double cpu=process.TotalProcessorTime.TotalSeconds-startCpu,wall=watch.Elapsed.TotalSeconds;var metrics=engine.Metrics;
            File.WriteAllText(report,new JavaScriptSerializer().Serialize(new {mode=mode,seconds=wall,cores=Environment.ProcessorCount,cpuSeconds=cpu,cpuPercentOneCore=100*cpu/wall,cpuPercentWholeMachine=100*cpu/wall/Environment.ProcessorCount,gen0=GC.CollectionCount(0)-collections[0],gen1=GC.CollectionCount(1)-collections[1],gen2=GC.CollectionCount(2)-collections[2],internet=engine.Current.Light==Light.Green,ratesReady=metrics.RatesReady,samples=rows}));
            context.ExitThread();
        };
        try{engine.Start();timer.Start();Application.Run(context);return 0;}finally{timer.Dispose();engine.Shutdown();context.Dispose();process.Dispose();foreach(string file in Directory.GetFiles(data))File.Delete(file);Directory.Delete(data);}
    }
}
