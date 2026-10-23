using Stock.Core.Pipeline;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    private async Task ScreenPipeline(IReadOnlyList<StockItem> input,TradingSettings settings,string source)
    {
        var cfg=settings.Pipeline!;var ranking=RankingProviderFactory.Create(cfg.Universe.RankingSource,settings.PipelineObservations);
        var end=new DateTimeOffset(To!.Value.Date.AddDays(1),TimeSpan.FromHours(9)).AddTicks(-1);var start=new DateTimeOffset(From!.Value.Date,TimeSpan.FromHours(9));
        var ranked=await ranking.ReadAsync(end,Token);var candidates=input.ToList();
        if(cfg.Universe.UseTopSearchSymbols)foreach(var r in ranked.Where(r=>r.SearchRank<=cfg.Universe.TopRankCutoff)){var i=Instrument.Parse(r.Symbol);candidates.Add(Catalog.FirstOrDefault(s=>s.Key==r.Symbol)??new(i.Symbol,i.Symbol,i.Exchange,i.Symbol));}
        if(cfg.Universe.UseWatchlistSymbols)candidates.AddRange(WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Items).Select(i=>i.Stock));
        var unique=candidates.DistinctBy(s=>s.Key).Where(s=>(Region=="전체"||s.Region==Region)&&(Sector=="전체"||s.Sector==Sector)).ToArray();int done=0,missing=0,matched=0;var reasons=new Dictionary<string,int>();
        foreach(var item in unique)
        {
            Token.ThrowIfCancellationRequested();var data=store.ReadEvents(item.Key,"KIWOOM",start,end);
            if(data.Count==0){missing++;}
            else
            {
                var token=Token;var result=await Task.Run(()=>{var context=new MarketContext(new(item.Symbol,item.Exchange),settings);var quality=new DataQualityGate(settings.Data,settings.Execution);foreach(var e in data){token.ThrowIfCancellationRequested();if(quality.Accept(e,false,out _))context.Add(e);}return context.Last==null?null:new PipelineEvaluator().Evaluate(context,cfg,settings.PipelineObservations);},token);
                if(result!=null){ShowPipelineEvaluation(result);var threshold=cfg.Strategy==PipelineStrategy.TickScalping?cfg.TickScalping.EntryScoreThreshold:cfg.Farmer.TrendContinuationScoreThreshold;if(result.Passed&&result.Score>=threshold){matched++;Matches.Add(new(item,result.Features.Price,result.Features.DayChange??0,$"{cfg.Strategy} · {result.Score:N0}점",result.Features.At.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss")));}else{var reason=result.Stages.FirstOrDefault(s=>!s.Passed)?.Stage??"Strategy";reasons[reason]=reasons.GetValueOrDefault(reason)+1;}}
                else missing++;
            }
            ScreenInfo=$"Pipeline {++done:N0}/{unique.Length:N0} · 일치 {matched:N0} · 체결 자료 없음 {missing:N0} · "+string.Join(" / ",reasons.Select(r=>$"{r.Key} 제외 {r.Value:N0}"))+" · "+source;
        }
        Log("파이프라인 스크리닝 완료 · 진입 후보만 표시하며 실제 주문하지 않습니다.");
    }
}
