namespace Stock.Desktop;

/// <summary>UI-only translation of WPF CandleChart's discrete candle navigation.
/// Source bars/execution timestamps are never rounded or reduced.</summary>
public sealed class CandleViewport
{
    public int Total {get;private set;}
    public int Visible {get;private set;}
    public int Offset {get;private set;}
    public bool Review {get;private set;}
    public bool Focused {get;private set;}
    public int FocusMaximum {get;private set;}
    public bool Enabled=>!Review||Focused;
    public int End=>Total-Offset;
    public int Start=>Math.Max(0,End-Visible);
    public void Reset(int total,bool review,int displayCount=0)
    {
        Total=total;Review=review;Offset=0;FocusMaximum=FocusCount(total);
        Focused=review&&FocusMaximum==total&&total>0;
        Visible=review?total:Math.Min(total,Math.Clamp(displayCount>0?displayCount:total,25,2000));
    }
    public void UpdateCount(int total)
    {if(Review&&!Focused){Reset(total,true);return;}if(Offset>0&&total>Total)Offset+=total-Total;Total=total;Visible=Math.Min(Visible,total);Offset=Math.Clamp(Offset,0,Math.Max(0,total-Visible));}
    public static int FocusCount(int total)=>total<=25?Math.Max(0,total):Math.Min(500,Math.Max(25,(int)Math.Ceiling(total/4d)));
    public void Toggle(double ratio)
    {
        if(!Review||Total==0)return;
        if(Focused){Reset(Total,true);return;}
        var index=Start+Math.Clamp((int)(ratio*(End-Start)),0,Math.Max(0,End-Start-1));
        Visible=FocusMaximum;Offset=Total-(Math.Clamp(index-Visible/2,0,Total-Visible)+Visible);Focused=true;
    }
    public void Zoom(double ratio,double delta)
    {
        if(!Enabled||Total==0||delta==0)return;
        var minimum=Math.Min(Review&&Focused?5:25,Total);
        var maximum=Review&&Focused?FocusMaximum:Math.Min(2000,Math.Max(25,Total));
        var next=Math.Clamp(Visible+(delta>0?-15:15),minimum,maximum);
        ratio=Math.Clamp(ratio,0,1);
        var anchor=Start+ratio*Math.Max(0,End-Start-1);
        var shown=Math.Min(next,Total);
        var start=Math.Clamp((int)Math.Round(anchor-ratio*Math.Max(0,shown-1)),0,Math.Max(0,Total-shown));
        Visible=shown;Offset=Total-(start+shown);
    }
    public void Pan(int initialOffset,double delta,double width)
    {
        if(!Enabled)return;
        if(Math.Abs(delta)<=4){Offset=Math.Clamp(initialOffset,0,Math.Max(0,Total-Visible));return;}
        var unit=Math.Min(12,Math.Max(.25,width/Math.Max(1,Visible)));
        Offset=Math.Clamp(initialOffset+Math.Sign(delta)*(int)Math.Ceiling((Math.Abs(delta)-4)/unit),0,Math.Max(0,Total-Visible));
    }
    public void Focus(int first,int last)
    {
        if(!Enabled||Total==0)return;
        var minimum=Math.Min(Review?5:25,Total);var maximum=Review?FocusMaximum:Math.Min(Total,2000);
        Visible=Math.Clamp(last-first+1,minimum,Math.Max(minimum,maximum));
        Offset=Total-(Math.Clamp(first,0,Total-Visible)+Visible);
    }
}
