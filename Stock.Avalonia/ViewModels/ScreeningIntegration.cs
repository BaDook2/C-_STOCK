namespace Stock.Desktop;
public sealed partial class MainViewModel
{
    public ScreeningWorkspace Screening {get;private set;}=null!;
    private void InitializeScreening()
    {
        Screening=new(services,Strategies,HistoryService,()=>Online,WatchScreenTargets,()=>Research.FactsJson,()=>Broker is Stock.Core.IMinuteHistoryBroker);Screening.Notice+=Notify;Screening.Progress+=message=>Status=message;
        Screening.Completed+=run=>RecordHistory(new(run.Id,run.ScreeningTime,DesktopRunKind.Screening,run.StrategyName,$"{run.Results.Length}/{run.Targets.Length}종목",run.From+" ~ "+run.To,$"후보{run.FinalCandidates.Length}","desktop-screen:"+run.Id));Screening.BrowserRequested+=model=>BrowserPopupRequested?.Invoke(model);
    }
}
