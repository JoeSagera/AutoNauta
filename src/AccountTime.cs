using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace NautaReconnect {
    public sealed class NautaSession {
        public string Username="",Csrf="",Uuid="";
        public Dictionary<string,string> Context=new Dictionary<string,string>();
        static readonly string[] ContextNames={"wlanuserip","ssid","loggerId","domain","wlanacname","wlanmac"};
        public static NautaSession Capture(PortalPage form,PageResponse response,string username){
            try{return CaptureBounded(form,response,username);}catch(RegexMatchTimeoutException){return null;}
        }
        static NautaSession CaptureBounded(PortalPage form,PageResponse response,string username){
            if(!PortalParser.Trusted(response.Uri)||!PortalParser.Parse(response.Body,response.Uri).Connected)return null;
            // The online page supplies a new CSRF and the complete query context.
            // Read literal strings only; never execute portal JavaScript.
            foreach(Match statement in HtmlRegex.Matches(response.Body,@"var\s+urlParam\s*=\s*([^;]{1,8192});")){
                string expression=statement.Groups[1].Value;if(!expression.Contains("op=getLeftTime"))continue;
                var literals=HtmlRegex.Matches(expression,@"""([^""\\]*)""|'([^'\\]*)'");
                string remainder=HtmlRegex.Replace(expression,@"""([^""\\]*)""|'([^'\\]*)'","");
                if(!HtmlRegex.IsMatch(remainder,@"^[\s+]*$"))return null;
                string raw="";foreach(Match literal in literals)raw+=literal.Groups[1].Success?literal.Groups[1].Value:literal.Groups[2].Value;
                var fields=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(string pair in raw.Split('&')){int equal=pair.IndexOf('=');if(equal<1)continue;string key=WebUtility.UrlDecode(pair.Substring(0,equal));if(fields.ContainsKey(key))return null;fields[key]=WebUtility.UrlDecode(pair.Substring(equal+1));}
                string user,csrfValue,uuid;
                if(!fields.TryGetValue("username",out user)||!fields.TryGetValue("CSRFHW",out csrfValue)||!fields.TryGetValue("ATTRIBUTE_UUID",out uuid))return null;
                var session=new NautaSession{Username=user,Csrf=csrfValue,Uuid=uuid};
                foreach(string name in ContextNames){string value;if(fields.TryGetValue(name,out value))session.Context[name]=value;}
                return session.ValidFor(username)?session:null;
            }
            string csrf=null;foreach(string pair in response.Uri.Query.TrimStart('?').Split('&')){int equal=pair.IndexOf('=');if(equal>0&&WebUtility.UrlDecode(pair.Substring(0,equal))=="CSRFHW"){csrf=WebUtility.UrlDecode(pair.Substring(equal+1));break;}}
            if(String.IsNullOrEmpty(csrf)&&(form==null||!form.Fields.TryGetValue("CSRFHW",out csrf)))return null;
            Match match=HtmlRegex.Match(response.Body+" "+response.Uri.Query,@"ATTRIBUTE_UUID\s*(?:=|:)\s*[""']?([A-Za-z0-9_-]{8,160})");
            if(!match.Success)return null;var fallback=new NautaSession{Username=username,Csrf=csrf,Uuid=match.Groups[1].Value};
            if(form!=null)foreach(string name in ContextNames){string value;if(form.Fields.TryGetValue(name,out value))fallback.Context[name]=value;}
            return fallback.ValidFor(username)?fallback:null;
        }
        public bool ValidFor(string username){
            if(!String.Equals(Username,username,StringComparison.OrdinalIgnoreCase)||String.IsNullOrEmpty(Username)||String.IsNullOrEmpty(Csrf)||Csrf.Length>256||Csrf.Contains("\r")||Csrf.Contains("\n")||!HtmlRegex.IsMatch(Uuid??"",@"^[A-Za-z0-9_-]{8,160}$"))return false;
            if(Context!=null)foreach(var pair in Context){if(Array.IndexOf(ContextNames,pair.Key)<0||pair.Value==null||pair.Value.Length>512)return false;}
            return true;
        }
        public Dictionary<string,string> QueryFields(){
            if(!ValidFor(Username))throw new InvalidOperationException("No hay un contador de sesión válido.");
            var fields=Context==null?new Dictionary<string,string>():new Dictionary<string,string>(Context);
            fields["username"]=Username;fields["CSRFHW"]=Csrf;fields["ATTRIBUTE_UUID"]=Uuid;fields["op"]="getLeftTime";return fields;
        }
        public static NautaSession ReadSavedPage(string html,string username){
            try{return ReadSavedPageBounded(html,username);}catch(RegexMatchTimeoutException){return null;}
        }
        static NautaSession ReadSavedPageBounded(string html,string username){
            if(String.IsNullOrEmpty(html)||html.Length>1024*1024)return null;
            Match origin=HtmlRegex.Match(html,@"<!--\s*saved from url=\(\d+\)(https://[^\s]+)\s*-->",RegexOptions.IgnoreCase);Uri uri;
            if(!origin.Success||!Uri.TryCreate(WebUtility.HtmlDecode(origin.Groups[1].Value),UriKind.Absolute,out uri)||!PortalParser.Trusted(uri)||!uri.AbsolutePath.EndsWith("/online.do",StringComparison.OrdinalIgnoreCase))return null;
            return Capture(null,new PageResponse{Uri=uri,Code=200,Body=html},username);
        }
    }
    public sealed class AccountReading {
        public TimeSpan? Remaining;
        public decimal? Credit;
        public string Note="Nauta no informó el tiempo restante.";
        public bool CredentialsRejected;
    }
    public static class AccountTime {
        public static bool SessionExpired(int code,string body){
            string value=(body??"").Trim();
            return code==401||code==403||(code==200&&(value.Equals("FAILURE",StringComparison.OrdinalIgnoreCase)||value.Equals("SESSION_EXPIRED",StringComparison.OrdinalIgnoreCase)));
        }
        static string Text(string html){
            string clean=HtmlRegex.Replace(html??"",@"<(script|style)\b[^>]*>[\s\S]*?</\1>|<!--[\s\S]*?-->"," ",RegexOptions.IgnoreCase);
            return HtmlRegex.Replace(WebUtility.HtmlDecode(HtmlRegex.Replace(clean,@"<[^>]+>"," ")),@"\s+"," ").Trim();
        }
        public static TimeSpan? ParseDuration(string text){
            Match match=HtmlRegex.Match((text??"").Trim(),@"^(\d{1,6}):(\d{2}):(\d{2})$");if(!match.Success)return null;
            long hours;int minutes,seconds;
            if(!Int64.TryParse(match.Groups[1].Value,out hours)||!Int32.TryParse(match.Groups[2].Value,out minutes)||!Int32.TryParse(match.Groups[3].Value,out seconds)||minutes>59||seconds>59)return null;
            return TimeSpan.FromSeconds(hours*3600+minutes*60+seconds);
        }
        public static AccountReading Parse(string html){
            try{return ParseBounded(html);}catch(RegexMatchTimeoutException){return new AccountReading{Note="La respuesta del contador no se pudo interpretar dentro del límite."};}
        }
        static AccountReading ParseBounded(string html){
            var result=new AccountReading();
            foreach(Match row in HtmlRegex.Matches(html??"",@"<tr\b[^>]*>([\s\S]*?)</tr>",RegexOptions.IgnoreCase)){
                var cells=new List<string>();foreach(Match cell in HtmlRegex.Matches(row.Groups[1].Value,@"<t[dh]\b[^>]*>([\s\S]*?)</t[dh]>",RegexOptions.IgnoreCase))cells.Add(Text(cell.Groups[1].Value));
                if(cells.Count<2)continue;string label=cells[0].ToLowerInvariant();string value=cells[cells.Count-1];
                if(label.Contains("tiempo disponible")||label.Contains("tiempo restante")){
                    Match duration=HtmlRegex.Match(value,@"\b\d{1,6}:\d{2}:\d{2}\b");if(duration.Success)result.Remaining=ParseDuration(duration.Value);
                }
                if(label.Contains("crédito")||label.Contains("credito")){
                    Match amount=HtmlRegex.Match(value,@"^(\d+(?:[.,]\d{1,2})?)\s*CUP$",RegexOptions.IgnoreCase);decimal credit;
                    if(amount.Success&&Decimal.TryParse(amount.Groups[1].Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out credit))result.Credit=credit;
                }
            }
            if(result.Remaining.HasValue)result.Note="Tiempo confirmado por Nauta.";
            else if(result.Credit.HasValue)result.Note="Nauta informó crédito en CUP. El contador exacto estará disponible cuando AutoNauta obtenga una sesión de navegación; no se convierte dinero a horas con una tarifa supuesta.";
            else{
                var error=PortalParser.Parse(html??"",PortalClient.Home);result.CredentialsRejected=error.Fatal;
                if(error.Error.Length>0)result.Note=error.Error;
            }
            return result;
        }
    }
}
