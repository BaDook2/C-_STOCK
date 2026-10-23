using System.Globalization;
using System.Text.RegularExpressions;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record DecimalEditResult(bool Accepted,string Text,int Caret,bool Commit,string Error="");
/// <summary>Pure fixed-decimal editing rules. List commas are delimiters, never thousands separators.</summary>
public static class FixedDecimalEdit
{
    private static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
    public const string Help="소수 둘째 자리 고정 · 정수부는 숫자를 삽입하고 소수부만 덮어씁니다. 소수점 바로 앞에서도 정수를 삽입합니다. 소수점 키로 소수부로 이동하세요. 소수 3자리 이상 붙여넣기는 거부합니다.";
    public static bool Enabled(ParameterInputKind kind)=>kind is ParameterInputKind.Decimal or ParameterInputKind.DecimalList;
    private static bool List(ParameterInputKind kind)=>kind==ParameterInputKind.DecimalList;
    private static bool TryFormat(string text,ParameterInputKind kind,bool precise,out string formatted)
    {
        formatted=text;if(text is "" or "-")return true;
        var parts=List(kind)?text.Split(','):new[]{text};var result=new List<string>();
        foreach(var part in parts)
        {
            var t=part.Trim();var pattern=List(kind)?@"^-?\d+(?:\.\d*)?$":@"^-?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d*)?$";
            if(!Regex.IsMatch(t,pattern,RegexOptions.CultureInvariant)||(!precise&&t.Contains('.')&&t.Length-t.IndexOf('.')-1>2)||!decimal.TryParse(t,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands,Culture,out var value))return false;
            var s=value.ToString(List(kind)?"0.00":"N2",Culture);if(value==0&&t.StartsWith('-'))s="-"+s.TrimStart('-');result.Add(s);
        }
        formatted=string.Join(", ",result);return true;
    }
    public static string Display(string source,ParameterInputKind kind)=>Enabled(kind)&&TryFormat(source,kind,true,out var value)?value:source;
    public static bool Rounded(string source,ParameterInputKind kind)
    {
        if(!Enabled(kind)||!TryFormat(source,kind,true,out var formatted)||source is "" or "-")return false;
        var a=List(kind)?source.Split(','):new[]{source};var b=List(kind)?formatted.Split(','):new[]{formatted};
        return a.Zip(b).Any(p=>decimal.Parse(p.First,NumberStyles.Number,Culture)!=decimal.Parse(p.Second,NumberStyles.Number,Culture));
    }
    private static DecimalEditResult Reject(string text,int caret,string why)=>new(false,text,caret,false,why);
    private static DecimalEditResult Normalize(string candidate,int caret,ParameterInputKind kind,bool internalEdit=false)
    {
        bool Count(char c)=>!char.IsWhiteSpace(c)&&(List(kind)||c!=',');
        var logical=candidate.Take(Math.Clamp(caret,0,candidate.Length)).Count(Count);
        if(internalEdit&&!List(kind))candidate=candidate.Replace(",","");
        if(!TryFormat(candidate,kind,false,out var formatted))return Reject(candidate,caret,"올바른 숫자를 입력하세요. 소수는 2자리까지, 목록은 쉼표로 구분합니다.");
        int index=0,seen=0;while(index<formatted.Length&&seen<logical){if(Count(formatted[index]))seen++;index++;}
        return new(true,formatted,index,true);
    }
    private static (int Start,int End) Segment(string text,int caret,ParameterInputKind kind)
    {
        if(!List(kind))return(0,text.Length);
        var start=caret==0?0:text.LastIndexOf(',',Math.Min(caret-1,text.Length-1))+1;
        var end=text.IndexOf(',',caret);if(end<0)end=text.Length;
        if(caret<text.Length&&text[caret]==','){start=caret+1;end=text.IndexOf(',',start);if(end<0)end=text.Length;}
        while(start<end&&char.IsWhiteSpace(text[start]))start++;return(start,end);
    }
    public static DecimalEditResult Type(string text,int caret,int selection,string input,ParameterInputKind kind)
    {
        caret=Math.Clamp(caret,0,text.Length);selection=Math.Clamp(selection,0,text.Length-caret);
        if(input.Length!=1)return Paste(text,caret,selection,input,kind);
        var character=input[0];var (start,end)=Segment(text,caret,kind);
        if(character=='.')
        {
            var dot=text.IndexOf('.',start,end-start);return dot>=0?new(true,text,dot+1,false):Reject(text,caret,"먼저 숫자를 입력하세요.");
        }
        if(character=='-')
        {
            if(selection==text.Length)return new(true,"-",1,true);
            if(text=="")return new(true,"-",1,true);
            bool negative=start<text.Length&&text[start]=='-';var candidate=negative?text.Remove(start,1):text.Insert(start,"-");
            return Normalize(candidate,Math.Clamp(caret+(negative?-1:1),start,candidate.Length),kind,true);
        }
        if(character<'0'||character>'9')return Reject(text,caret,"숫자·소수점·음수 부호만 입력할 수 있습니다. 목록 개수는 붙여넣기로 변경하세요.");
        var decimalAt=text.IndexOf('.',start,end-start);
        if(selection>0){var candidate=text.Remove(caret,selection).Insert(caret,input);return decimalAt<0||caret<=decimalAt?Normalize(candidate,caret+1,kind,true):Normalize(candidate,caret+1,kind,true);}
        if(text is "" or "-")return Normalize(text+input,text.Length+1,kind,true);
        caret=Math.Max(caret,start);if(caret<end&&text[caret]=='-')caret++;
        if(decimalAt<0||caret<=decimalAt)return Normalize(text.Insert(caret,input),caret+1,kind,true);
        if(caret>=end)return Reject(text,caret,"소수 둘째 자리까지 입력할 수 있습니다. 정수를 입력하려면 정수부로 커서를 이동하세요.");
        return Normalize(text.Remove(caret,1).Insert(caret,input),caret+1,kind,true);
    }
    public static DecimalEditResult Paste(string text,int caret,int selection,string input,ParameterInputKind kind)
    {
        caret=Math.Clamp(caret,0,text.Length);selection=Math.Clamp(selection,0,text.Length-caret);
        var candidate=text.Remove(caret,selection).Insert(caret,input.Trim());var result=Normalize(candidate,caret+input.Trim().Length,kind);
        return result.Accepted?result:Reject(text,caret,"붙여넣기를 거부했습니다. 숫자와 올바른 쉼표 형식만 허용하며 소수 3자리 이상은 입력할 수 없습니다.");
    }
    public static DecimalEditResult Delete(string text,int caret,int selection,bool backspace,ParameterInputKind kind)
    {
        caret=Math.Clamp(caret,0,text.Length);selection=Math.Clamp(selection,0,text.Length-caret);
        if(selection>0)return Normalize(text.Remove(caret,selection),caret,kind,true);
        int at=backspace?caret-1:caret;if(at<0||at>=text.Length)return new(true,text,caret,false);
        if(List(kind)&&(text[at]==','||char.IsWhiteSpace(text[at])))return new(true,text,backspace?at:at+1,false);
        while(at>=0&&at<text.Length&&(text[at]=='.'||(!List(kind)&&text[at]==',')))at+=backspace?-1:1;
        if(at<0||at>=text.Length)return new(true,text,caret,false);
        var (start,end)=Segment(text,at,kind);var dot=text.IndexOf('.',start,end-start);
        var candidate=dot>=0&&at>dot?text.Remove(at,1).Insert(at,"0"):text.Remove(at,1);
        // A deleted last integer digit leaves zero, while deleting the whole selection leaves an invalid empty draft.
        var segment=candidate.Substring(start,Math.Max(0,end-start-(dot>=0&&at>dot?0:1)));
        if(segment.StartsWith('.'))candidate=candidate.Insert(start,"0");else if(segment.StartsWith("-."))candidate=candidate.Insert(start+1,"0");
        return Normalize(candidate,backspace?at:Math.Min(caret,candidate.Length),kind,true);
    }
}
