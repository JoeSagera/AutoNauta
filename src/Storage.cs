using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Linq;
using System.Text.RegularExpressions;
namespace NautaReconnect {
    public sealed class SettingsStore {
        public static string DataDirectory(){
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);string current=Path.Combine(local,"AutoNauta");Directory.CreateDirectory(current);
            string destination=Path.Combine(current,"config.json"),legacy=Path.Combine(local,"ReconectorNauta","config.json");
            if(!File.Exists(destination)&&File.Exists(legacy))File.Copy(legacy,destination,false);
            return current;
        }
        public readonly string DirectoryPath;
        public readonly string ConfigPath;
        readonly object fileGate=new object();
        string savedJson;
        DateTime savedWriteTime;
        internal Action<string,string> ReplaceFile=(source,destination)=>File.Replace(source,destination,null);
        public SettingsStore(string path) { DirectoryPath=path; ConfigPath=Path.Combine(path,"config.json"); Directory.CreateDirectory(path); }
        public AppSettings Load() {
            if(!File.Exists(ConfigPath)) return new AppSettings();
            try { var c=new JavaScriptSerializer().Deserialize<AppSettings>(File.ReadAllText(ConfigPath)); if(c==null) throw new Exception(); c.IntervalSeconds=Math.Max(10,Math.Min(120,c.IntervalSeconds)); return c; }
            catch { throw new InvalidDataException("La configuración guardada no se puede leer. Conserva config.json y revisa el archivo antes de continuar."); }
        }
        public void Save(AppSettings value) {
            lock(fileGate){
            string json=new JavaScriptSerializer().Serialize(value);
            if(json==savedJson&&File.Exists(ConfigPath)&&File.GetLastWriteTimeUtc(ConfigPath)==savedWriteTime)return;
            string temp=ConfigPath+".tmp";
            File.WriteAllText(temp,json,Encoding.UTF8);
            if(File.Exists(ConfigPath)){
                for(int attempt=0;;attempt++){
                    try{ReplaceFile(temp,ConfigPath);break;}
                    catch(IOException error){int code=error.HResult&0xffff;if(attempt>=4||(code!=32&&code!=33&&code!=5&&code!=1175))throw;System.Threading.Thread.Sleep(25*(attempt+1));}
                }
            }else File.Move(temp,ConfigPath);
            savedJson=json;savedWriteTime=File.GetLastWriteTimeUtc(ConfigPath);
            }
        }
        public static string Protect(string password) {
            byte[] bytes=Encoding.UTF8.GetBytes(password);
            try { return Convert.ToBase64String(ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser)); }
            finally { Array.Clear(bytes,0,bytes.Length); }
        }
        public static string Unprotect(string data) {
            if(String.IsNullOrEmpty(data)) return "";
            byte[] bytes=ProtectedData.Unprotect(Convert.FromBase64String(data),null,DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); } finally { Array.Clear(bytes,0,bytes.Length); }
        }
        public void Log(Status status) {
            try {
                lock(fileGate){
                string path=Path.Combine(DirectoryPath,"eventos.log");
                if(File.Exists(path)&&new FileInfo(path).Length>512*1024){
                    File.Move(path,Path.Combine(DirectoryPath,"eventos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".log"));
                    var archives=Directory.GetFiles(DirectoryPath,"eventos-*.log").Where(file=>Regex.IsMatch(Path.GetFileName(file),@"^eventos-\d{8}-\d{9}\.log$")).OrderByDescending(file=>Path.GetFileName(file),StringComparer.Ordinal).Skip(4);
                    foreach(string archive in archives)File.Delete(archive);
                }
                File.AppendAllText(path,status.Time.ToString("yyyy-MM-dd HH:mm:ss")+" | "+status.Light+" | "+status.Message+" | "+status.Detail.Replace("\r"," ").Replace("\n"," ")+Environment.NewLine,Encoding.UTF8);
                }
            } catch { }
        }
    }
}
