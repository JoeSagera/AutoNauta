using System;
using System.IO;
using System.Text.RegularExpressions;

namespace NautaReconnect {
    // Untrusted HTML must have both a size bound and a regex execution bound.
    internal static class HtmlRegex {
        static readonly TimeSpan Limit=TimeSpan.FromMilliseconds(250);
        internal static Match Match(string input,string pattern){return Match(input,pattern,RegexOptions.None);}
        internal static Match Match(string input,string pattern,RegexOptions options){return Regex.Match(input,pattern,options,Limit);}
        internal static MatchCollection Matches(string input,string pattern){return Matches(input,pattern,RegexOptions.None);}
        internal static MatchCollection Matches(string input,string pattern,RegexOptions options){return Regex.Matches(input,pattern,options,Limit);}
        internal static bool IsMatch(string input,string pattern){return IsMatch(input,pattern,RegexOptions.None);}
        internal static bool IsMatch(string input,string pattern,RegexOptions options){return Regex.IsMatch(input,pattern,options,Limit);}
        internal static string Replace(string input,string pattern,string replacement){return Replace(input,pattern,replacement,RegexOptions.None);}
        internal static string Replace(string input,string pattern,string replacement,RegexOptions options){return Regex.Replace(input,pattern,replacement,options,Limit);}
    }
    internal static class BoundedInput {
        internal const int MaximumBytes=1024*1024;
        internal static string ReadPage(string path){
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var copy=new MemoryStream()){
                if(stream.CanSeek&&stream.Length>MaximumBytes)throw TooLarge();
                byte[] buffer=new byte[8192];int count;
                while((count=stream.Read(buffer,0,Math.Min(buffer.Length,MaximumBytes+1-(int)copy.Length)))>0){
                    if(copy.Length+count>MaximumBytes)throw TooLarge();copy.Write(buffer,0,count);
                }
                copy.Position=0;using(var reader=new StreamReader(copy))return reader.ReadToEnd();
            }
        }
        static ArgumentException TooLarge(){return new ArgumentException("La página guardada es demasiado grande.");}
    }
}
