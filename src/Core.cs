using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace NautaReconnect {
    public sealed class PortalPage {
        public bool Login, Connected, SessionInUse;
        public string Error = "";
        public bool Fatal;
        public Uri Action;
        public string UserField, PasswordField;
        public Dictionary<string,string> Fields = new Dictionary<string,string>();
    }
    public static class PortalParser {
        public static bool Trusted(Uri uri) {
            return uri!=null && uri.IsAbsoluteUri && uri.Scheme=="https" &&
                uri.Host.Equals("secure.etecsa.net",StringComparison.OrdinalIgnoreCase) && uri.Port==8443 && uri.UserInfo.Length==0;
        }
        static Dictionary<string,string> Attributes(string tag) {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(Match m in Regex.Matches(tag,@"([\w:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",RegexOptions.Singleline)) {
                result[m.Groups[1].Value]=WebUtility.HtmlDecode(m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Success?m.Groups[3].Value:m.Groups[4].Value);
            }
            return result;
        }
        static string Get(Dictionary<string,string> attrs,string key) { string v; return attrs.TryGetValue(key,out v)?v:""; }
        static string Normalize(string text) {
            var b=new StringBuilder(); foreach(char c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark) b.Append(c);
            return b.ToString();
        }
        static void SetError(PortalPage page,string text) {
            string normalized=Normalize(text);
            if(normalized.Contains("saldo")||normalized.Contains("tiempo disponible")||normalized.Contains("cuota")) {
                page.Error="Nauta indica un problema de saldo, tiempo o cuota. Revisa tu cuenta."; page.Fatal=true;
            } else if(normalized.Contains("contrasena")||normalized.Contains("password")||normalized.Contains("incorrect")||normalized.Contains("credencial")||normalized.Contains("bloquead")) {
                page.Error="Nauta rechazó las credenciales o bloqueó la cuenta. Revisa usuario y contraseña."; page.Fatal=true;
            } else if(normalized.Contains("sesion") && (normalized.Contains("activa")||normalized.Contains("uso")||normalized.Contains("conectad"))) {
                page.Error="La cuenta tiene una sesión en uso. Esperando sin repetir autenticaciones.";
                page.SessionInUse=true;
            } else page.Error="El portal devolvió un aviso o error. Abre el portal para revisar el mensaje.";
        }
        public static PortalPage Parse(string html, Uri pageUri) {
            var page=new PortalPage();
            if(!Trusted(pageUri)) { page.Error="El portal redirigió a un destino no autorizado."; page.Fatal=true; return page; }
            if(html==null||html.Length>1024*1024) { page.Error="Respuesta del portal vacía o demasiado grande."; return page; }
            string clean=Regex.Replace(html,@"<!--[\s\S]*?-->","");
            foreach(Match s in Regex.Matches(clean,@"<script\b[^>]*>([\s\S]*?)</script\s*>",RegexOptions.IgnoreCase)) {
                Match alert=Regex.Match(s.Groups[1].Value,@"^\s*alert\s*\(\s*(?:""([^""]+)""|'([^']+)')",RegexOptions.IgnoreCase);
                if(alert.Success) { SetError(page,alert.Groups[1].Success?alert.Groups[1].Value:alert.Groups[2].Value); return page; }
            }
            clean=Regex.Replace(clean,@"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase);
            if(Regex.IsMatch(clean,@"captcha|g-recaptcha|hcaptcha",RegexOptions.IgnoreCase)) { page.Fatal=true; page.Error="El portal requiere una comprobación manual (CAPTCHA)."; return page; }
            string text=Normalize(WebUtility.HtmlDecode(Regex.Replace(clean,"<[^>]+>"," ")));
            if((text.Contains("usted esta conectado")&&text.Contains("cerrar sesion")) ||
               (pageUri.AbsolutePath.EndsWith("online.do",StringComparison.OrdinalIgnoreCase)&&clean.Contains("ATTRIBUTE_UUID"))) {
                page.Connected=true; return page;
            }
            foreach(Match form in Regex.Matches(clean,@"<form\b([^>]*)>([\s\S]*?)</form\s*>",RegexOptions.IgnoreCase)) {
                var attrs=Attributes(form.Groups[1].Value);
                var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                string user=null,pass=null;
                bool duplicate=false;
                foreach(Match input in Regex.Matches(form.Groups[2].Value,@"<input\b([^>]*)>",RegexOptions.IgnoreCase)) {
                    var a=Attributes(input.Groups[1].Value); string name=Get(a,"name"),type=Get(a,"type").ToLowerInvariant();
                    if(name.Length==0 || Regex.IsMatch(input.Value,@"\sdisabled(?:\s|=|/?>)",RegexOptions.IgnoreCase)) continue;
                    if(type=="password") { if(pass!=null) duplicate=true; pass=name; }
                    if(name.Equals("username",StringComparison.OrdinalIgnoreCase)) user=name;
                    if(type=="hidden"||type=="text"||type=="password"||type=="submit"||type=="email"||type=="") {
                        if(fields.ContainsKey(name)) duplicate=true;
                        fields[name]=Get(a,"value");
                    }
                }
                if(pass==null) continue;
                Uri action;
                if(!Uri.TryCreate(pageUri,Get(attrs,"action"),out action)||!Trusted(action)) {
                    page.Error="Se bloqueó el envío a un destino externo o sin HTTPS."; page.Fatal=true; return page;
                }
                if(!Get(attrs,"method").Equals("post",StringComparison.OrdinalIgnoreCase) || user==null ||
                   !action.AbsolutePath.TrimEnd('/').EndsWith("/LoginServlet",StringComparison.OrdinalIgnoreCase) || duplicate ||
                   !fields.ContainsKey("CSRFHW")||fields["CSRFHW"].Length==0 || !fields.ContainsKey("wlanuserip")||fields["wlanuserip"].Length==0) {
                    page.Error="Formulario Nauta desconocido o incompleto; no se enviaron credenciales."; page.Fatal=true; return page;
                }
                page.Login=true; page.Action=action; page.Fields=fields; page.UserField=user; page.PasswordField=pass; return page;
            }
            page.Error="El portal respondió, pero no se reconoció una sesión ni su formulario.";
            return page;
        }
        public static string Encode(Dictionary<string,string> fields) {
            return String.Join("&",fields.Select(x=>Uri.EscapeDataString(x.Key)+"="+Uri.EscapeDataString(x.Value??"")));
        }
    }
    public sealed class RetryPolicy {
        public int Failures;
        public DateTime LastLogin = DateTime.MinValue;
        public bool AuthBlocked;
        public int AuthFailures;
        public T LoginAttempt<T>(Func<T> send) { try{return send();}catch(OperationCanceledException){throw;}catch{AuthenticationFailed();throw;} }
        public bool AuthenticationFailed() { AuthFailures++; if(AuthFailures>=3)AuthBlocked=true; return AuthBlocked; }
        public void Healthy() { Failures=0; AuthFailures=0; }
        public bool ConfirmFailure() { Failures=Math.Min(Failures+1,30); return Failures>=2; }
        public bool CanLogin(DateTime now) { return !AuthBlocked && now-LastLogin>=TimeSpan.FromSeconds(90); }
        public int DelaySeconds(int normal) { return Math.Min(120,Math.Max(normal,15)*(int)Math.Pow(2,Math.Min(3,Math.Max(0,Failures-2)))); }
    }
}
