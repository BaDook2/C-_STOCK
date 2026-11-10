using Stock.Core;
using Stock.Desktop;

internal static partial class Program
{
    private static void HistoryTimezoneChecks()
    {
        using var services=new AppServices(Path.Combine(output,"history-timezone-"+Guid.NewGuid().ToString("N")),new MarketFixture());
        using var model=new MainViewModel(services);
        var today=KoreaClock.Today.ToDateTime(TimeOnly.MinValue);
        Check(model.HistoryFrom==today.AddMonths(-1)&&model.HistoryTo==today,"history default dates follow Korean display dates independently from the host time zone");
        var midnight=new DateTimeOffset(today,TimeSpan.FromHours(9));
        foreach(var (id,at) in new[]{("previous-day",midnight.AddTicks(-1)),("current-day",midnight), ("next-day",midnight.AddDays(1))})
            services.Store.State("desktop-history-index:"+id,new DesktopHistoryEntry(id,at,DesktopRunKind.Screening,"격리 날짜 검사","합성 종목","fixture","0",id));
        model.HistoryFrom=today;model.LoadHistoryCommand.Execute(null);
        Pump(()=>model.History.Count==1);
        Check(model.History.Single().Entry.Id=="current-day","history includes Korean midnight and excludes the adjacent Korean dates");
        Check(model.History.Single().At==today.ToString("yyyy.MM.dd")+" 00:00:00","the visible history date matches the date used to select the row");
        using var reopened=new MainViewModel(services);
        Check(reopened.History.Select(r=>r.Entry.Id).Order().SequenceEqual(new[]{"current-day","previous-day"}),"rebuilding the view includes saved current-day records and excludes tomorrow's fixture");
    }
}
