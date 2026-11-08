namespace Stock.Desktop;
public sealed partial class MainViewModel
{
    public ScreeningWorkspace Screening {get;private set;}=null!;
    public AsyncCommand OpenDayTradingScreen {get;private set;}=null!;
    private void InitializeScreening()
    {
        Screening=new(services,Strategies,HistoryService,()=>Online,WatchScreenTargets,()=>Research.FactsJson,()=>Broker is Stock.Core.IMinuteHistoryBroker);Screening.Notice+=Notify;Screening.Progress+=message=>Status=message;
        Screening.Completed+=run=>RecordHistory(new(run.Id,run.ScreeningTime,DesktopRunKind.Screening,run.StrategyName,$"{run.Results.Length}/{run.Targets.Length}종목",run.From+" ~ "+run.To,$"후보{run.FinalCandidates.Length}","desktop-screen:"+run.Id));Screening.BrowserRequested+=model=>BrowserPopupRequested?.Invoke(model);
        OpenDayTradingScreen=new(()=>{if(Screening.CurrentTargets.Length==0){Notify("먼저 스크리닝을 실행하고 후보 종목을 확인하세요.");return Task.CompletedTask;}dayTargets=Screening.CurrentTargets.Where(s=>s.Exchange==Stock.Core.Trading.Exchange.KRX).Take(100).ToArray();DayTrading.FactsJson=Research.FactsJson;DayTrading.StartDate=Screening.From??DateTime.Today.AddDays(-7);DayTrading.EndDate=Screening.To??DateTime.Today;DayTradingPopupRequested?.Invoke(DayTrading,1);return Task.CompletedTask;});
    }
}
