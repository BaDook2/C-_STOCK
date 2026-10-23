using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
/// <summary>One feed subscription's contiguous trade segment; cumulative quantities prove continuity, never supply size.</summary>
public sealed class TickCoverageTracker
{
    private MarketEvent? prior;private DateTimeOffset start;private long firstSequence;
    public BarCoverage? Current {get;private set;}
    public void Observe(MarketEvent e)
    {
        if(e.Kind!=EventKind.Trade)return;
        try{e.Validate();}catch{prior=null;Current=null;return;}
        if(e.CumulativeVolume==null||e.TradeSize<=0){prior=null;Current=null;return;}
        if(prior!=null&&e==prior)return;
        bool continuous=prior!=null&&prior.Key==e.Key&&prior.Source==e.Source&&e.ExchangeTime>=prior.ExchangeTime&&e.Sequence>prior.Sequence&&e.CumulativeVolume-prior.CumulativeVolume==e.TradeSize;
        if(!continuous){start=e.ExchangeTime;firstSequence=e.Sequence;Current=null;}
        prior=e;
        if(e.ExchangeTime>start)Current=new(start,e.ExchangeTime,true,e.Source,firstSequence,e.Sequence);
    }
}
