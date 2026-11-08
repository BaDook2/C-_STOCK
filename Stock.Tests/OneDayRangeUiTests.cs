using System.IO;
using System.Reflection;
using System.Windows.Controls;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void OneDayRangeUiTests(MainWindow window,WorkspaceViewModel vm,ModeCacheBroker broker,List<string> alerts,string output)
    {
        var day=(RadioButton)window.FindName("ChartOneDayButton");
        var chart=(CandleChart)window.FindName("PriceChart");
        void ClickDay()
        {
            // Exercise WPF toggle/group behavior as well as its bound command.
            typeof(RadioButton).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(day,null);
            PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        }
        void SetPeriod(string period){vm.ChartPeriod=period;PumpUntil(()=>!vm.Busy);window.UpdateLayout();}
        bool LatestDayOnly()=>vm.ChartBars.Count>0&&vm.ChartBars.All(b=>DailyQuoteMerger.Date(b.Time,Exchange.KRX)==new DateOnly(2026,11,6));
        Check(vm.ChartRangeDaySelected&&day.IsChecked==true&&vm.ChartBars.Count==120&&LatestDayOnly(),"minute selection automatically highlights one day and excludes the prior trading session");
        Check(vm.ChartWindowCount==120&&chart.DisplayBarCount==120,"one-day minute range initially fits the complete observed session");
        ClickDay();Check(day.IsChecked==true&&vm.ChartRangeDaySelected&&alerts.Count==0,"valid one-day click keeps minute candles and produces no warning");
        foreach(var minutes in new[]{5,59})
        {
            SetPeriod(minutes+"분");
            Check(vm.ChartRangeDaySelected&&LatestDayOnly()&&vm.ChartWindowCount==vm.ChartBars.Count,"one-day selection survives minute interval "+minutes+" without stale bars");
        }
        var reads=broker.MinuteReads;
        SetPeriod("1시");
        Check(!vm.ChartRangeDaySelected&&vm.ChartRange1Selected&&vm.ChartBars.Count==4,"hour selection leaves one-day mode and restores the cached two-session history");
        var hourBars=vm.ChartBars;var alertCount=alerts.Count;
        ClickDay();
        Check(alerts.Count==alertCount+1&&alerts[^1].Contains("분봉에서만")&&vm.ChartPeriod=="1시"&&ReferenceEquals(hourBars,vm.ChartBars),"one-day click from hours requests a popup without changing candles or period");
        Check(day.IsChecked==false&&vm.ChartRange1Selected&&Descendants(window).OfType<RadioButton>().Single(r=>r.GroupName=="ChartRange"&&Equals(r.Content,"1개월")).IsChecked==true&&broker.MinuteReads==reads,"rejected day click restores monthly highlight without another history request");
        SetPeriod("1분");Check(vm.ChartRangeDaySelected&&LatestDayOnly(),"returning from hours automatically selects one day again");
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var number=(ComboBox)window.FindName("ChartCustomBarNumber");
        Check(Equals(number.SelectedItem,"1")&&Equals(number.SelectionBoxItem,"1"),"returning from hours keeps the selected minute number visible");
        var unit=(ComboBox)window.FindName("ChartCustomBarUnit");
        number.SelectedItem="59";PumpUntil(()=>!vm.Busy);unit.SelectedValue="시";PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        Check(vm.ChartPeriod=="12시"&&number.Items.Count==12&&Equals(number.SelectionBoxItem,"12")&&!vm.ChartRangeDaySelected,"actual combo selection coerces 59 minutes to 12 hours without losing its visible number");
        unit.SelectedValue="분";PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        Check(vm.ChartPeriod=="12분"&&number.Items.Count==59&&Equals(number.SelectionBoxItem,"12")&&vm.ChartRangeDaySelected&&LatestDayOnly(),"actual combo selection restores 59 minute choices and automatically selects one day");
        number.SelectedItem="1";PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        Check(vm.ChartPeriod=="1분"&&Equals(number.SelectionBoxItem,"1")&&vm.ChartRangeDaySelected,"actual minute number selection keeps the one-day range and numeric display synchronized");
        Render(window,Path.Combine(output,"one-day-minute-chart.png"));
        SetPeriod("일봉");
        vm.Actions["기간3"].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        var dailyBars=vm.ChartBars;alertCount=alerts.Count;
        ClickDay();
        Check(alerts.Count==alertCount+1&&vm.DailySelected&&vm.ChartRange3Selected&&ReferenceEquals(dailyBars,vm.ChartBars)&&day.IsChecked==false,"daily invalid day click preserves the three-month range and selection");
        SetPeriod("1분");
        vm.IsOffline=true;PumpUntil(()=>!vm.Busy);window.UpdateLayout();
        Check(vm.DailySelected&&!vm.ChartRangeDaySelected&&vm.ChartRange1Selected,"switching offline from one-day minutes restores daily and one-month range");
        alertCount=alerts.Count;ClickDay();Check(alerts.Count==alertCount+1&&vm.IsOffline&&vm.DailySelected&&!vm.ChartRangeDaySelected,"offline one-day click explains the minute-only requirement without connecting");
        vm.IsOnline=true;PumpUntil(()=>!vm.Busy);SetPeriod("1분");
        Check(vm.ChartRangeDaySelected&&LatestDayOnly(),"mode roundtrip restores the minute-only one-day selection and correct session");
        Render(window,Path.Combine(output,"one-day-after-mode-switch.png"));
    }
}
