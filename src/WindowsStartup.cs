using System;
using System.IO;
using Microsoft.Win32;

namespace NautaReconnect {
    internal static class AppVersion {
        internal static string Text {
            get { return typeof(AppVersion).Assembly.GetName().Version.ToString(3); }
        }
    }

    // One per-user entry, refreshed when a portable copy is moved and opened.
    // Preview, diagnostics and tests never reconcile the real startup entry.
    internal sealed class WindowsStartup {
        internal const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "AutoNauta";
        readonly string keyPath;
        internal readonly string Command;
        internal WindowsStartup(string executable,string keyPath = RunPath) {
            if(String.IsNullOrWhiteSpace(executable)||executable.IndexOf('"')>=0)throw new ArgumentException("Ruta del programa no válida.");
            Command="\""+Path.GetFullPath(executable)+"\" --startup";
            this.keyPath=keyPath;
        }
        internal void Apply(bool enabled) {
            if(enabled) {
                using(var key=Registry.CurrentUser.CreateSubKey(keyPath)) {
                    if(key==null)throw new IOException("No se pudo abrir el inicio de Windows.");
                    if(!String.Equals(key.GetValue(ValueName) as string,Command,StringComparison.Ordinal))key.SetValue(ValueName,Command,RegistryValueKind.String);
                }
            } else {
                using(var key=Registry.CurrentUser.OpenSubKey(keyPath,true))if(key!=null)key.DeleteValue(ValueName,false);
            }
        }
        internal void ChangePreference(AppSettings settings,bool enabled,Action save) {
            object previous=null; RegistryValueKind kind=RegistryValueKind.String;
            using(var key=Registry.CurrentUser.OpenSubKey(keyPath))if(key!=null) {
                previous=key.GetValue(ValueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                if(previous!=null)kind=key.GetValueKind(ValueName);
            }
            bool old=settings.StartWithWindows;
            Apply(enabled);
            try { settings.StartWithWindows=enabled; save(); }
            catch {
                settings.StartWithWindows=old;
                using(var key=Registry.CurrentUser.CreateSubKey(keyPath)) {
                    if(previous==null)key.DeleteValue(ValueName,false);else key.SetValue(ValueName,previous,kind);
                }
                throw;
            }
        }
    }
}
