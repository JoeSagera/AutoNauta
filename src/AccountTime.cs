using System;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace NautaReconnect {
    public sealed class NautaSession {
        public string Username="",Csrf="",Uuid="";
        public static NautaSession Capture(PortalPage form,PageResponse response,string username){
            if(!PortalParser.Trusted(response.Uri)||!PortalParser.Parse(response.Body,response.Uri).Connected)return null;
            string csrf;if(!form.Fields.TryGetValue("CSRFHW",out csrf)||String.IsNullOrEmpty(csrf))return null;
            Match match=Regex.Match(response.Body+" "+response.Uri.Query,@"ATTRIBUTE_UUID\s*(?:=|:)\s*[""']?([A-Za-z0-9_-]{8,160})");
            return match.Success?new NautaSession{Username=username,Csrf=csrf,Uuid=match.Groups[1].Value}:null;
        }
        public bool ValidFor(string username){return String.Equals(Username,username,StringComparison.OrdinalIgnoreCase)&&!String.IsNullOrEmpty(Csrf)&&Regex.IsMatch(Uuid??"",@"^[A-Za-z0-9_-]{8,160}$");}
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
            string clean=Regex.Replace(html??"",@"<(script|style)\b[^>]*>[\s\S]*?</\1>|<!--[\s\S]*?-->"," ",RegexOptions.IgnoreCase);
            return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(clean,@"<[^>]+>"," ")),@"\s+"," ").Trim();
        }
        public static TimeSpan? ParseDuration(string text){
            Match match=Regex.Match((text??"").Trim(),@"^(\d{1,6}):(\d{2}):(\d{2})$");if(!match.Success)return null;
            long hours;int minutes,seconds;
            if(!Int64.TryParse(match.Groups[1].Value,out hours)||!Int32.TryParse(match.Groups[2].Value,out minutes)||!Int32.TryParse(match.Groups[3].Value,out seconds)||minutes>59||seconds>59)return null;
            return TimeSpan.FromSeconds(hours*3600+minutes*60+seconds);
        }
        public static AccountReading Parse(string html){
            var result=new AccountReading();
            foreach(Match row in Regex.Matches(html??"",@"<tr\b[^>]*>([\s\S]*?)</tr>",RegexOptions.IgnoreCase)){
                var cells=new List<string>();foreach(Match cell in Regex.Matches(row.Groups[1].Value,@"<t[dh]\b[^>]*>([\s\S]*?)</t[dh]>",RegexOptions.IgnoreCase))cells.Add(Text(cell.Groups[1].Value));
                if(cells.Count<2)continue;string label=cells[0].ToLowerInvariant();string value=cells[cells.Count-1];
                if(label.Contains("tiempo disponible")||label.Contains("tiempo restante")){
                    Match duration=Regex.Match(value,@"\b\d{1,6}:\d{2}:\d{2}\b");if(duration.Success)result.Remaining=ParseDuration(duration.Value);
                }
                if(label.Contains("crédito")||label.Contains("credito")){
                    Match amount=Regex.Match(value,@"^(\d+(?:[.,]\d{1,2})?)\s*CUP$",RegexOptions.IgnoreCase);decimal credit;
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
