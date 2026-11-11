namespace Stock.Core.Trading;

// Shared descriptive analytics for existing charts and manual research, independent of automatic strategy selection.
public static class SupportResistanceService
{
    public static int Touches(IReadOnlyList<MarketEvent> history,decimal level,decimal tolerance,int gap)
    {
        var inside=false;DateTimeOffset? previous=null;var count=0;
        foreach(var t in history){var now=Math.Abs(t.TradePrice!.Value-level)<=level*tolerance/100;if(now&&!inside&&(previous==null||(t.ExchangeTime-previous.Value).TotalSeconds>=gap)){count++;previous=t.ExchangeTime;}inside=now;}return count;
    }
    public static IReadOnlyList<decimal> Levels(MarketContext c)
    {
        var s=c.Settings.Analysis;var h=c.Window(s.LookbackSeconds);var levels=new List<decimal>();
        // A pivot only becomes known after its right-hand neighbours have actually arrived.
        for(int i=s.PivotRadius;i<h.Count-s.PivotRadius;i++) {var p=h[i].TradePrice!.Value;var w=h.Skip(i-s.PivotRadius).Take(2*s.PivotRadius+1).Select(x=>x.TradePrice!.Value).ToList();if(p==w.Min()||p==w.Max())if(Touches(h,p,s.TolerancePercent,c.Settings.Box.TouchGapSeconds)>=s.MinimumTouches&&!levels.Any(l=>Math.Abs(l-p)<=p*s.TolerancePercent/100))levels.Add(p);}
        return levels;
    }
}
public static class VolumeProfileService
{
    public static IReadOnlyList<(decimal Price,decimal Volume)> Calculate(MarketContext c)
    {var s=c.Settings.Analysis;var step=c.Instrument.Tick(c.Price)*s.ProfileTicks;return c.Window(s.ProfileSeconds).GroupBy(t=>decimal.Floor(t.TradePrice!.Value/step)*step).Select(g=>(Price:g.Key,Volume:g.Sum(x=>x.TradeSize??0))).OrderByDescending(x=>x.Volume).Take(s.ProfileTop).ToList();}
}
