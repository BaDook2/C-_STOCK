using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    private bool ScreeningMarketIsOpen()
    {
        var time=multiClock();
        if(!IsForeignScreening)return LiveBatchSupport.RegularSession(time);
        var local=TimeZoneInfo.ConvertTime(time,TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        return local.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)&&local.TimeOfDay>=new TimeSpan(9,30,0)&&local.TimeOfDay<new TimeSpan(16,0,0);
    }
}
