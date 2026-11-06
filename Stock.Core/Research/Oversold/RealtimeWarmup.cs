namespace Stock.Core.Research.Oversold;

public static class RealtimeWarmup
{
    public static int RequiredBars(OversoldConfig c)
    {
        var periods=new List<int>{c.CrashLookbackDays,c.ShortCrashLookbackDays,c.AverageTradingValueLookbackDays+1};
        if(c.UseRsiFilter)periods.Add(c.RsiPeriod+1);
        if(c.UseVolumeFilter)periods.Add(c.VolumeLookbackDays+1);
        if(c.UseMovingAverageSupport)periods.AddRange(c.MaPeriods());
        if(c.UsePivotSupport)periods.Add(c.PivotLeftBars+c.PivotRightBars+1);
        if(c.UseApproximateVolumeZone)periods.Add(c.VolumeZoneLookbackDays);
        return periods.Max()+2;
    }
    public static int SourceInterval(int target,Func<int,bool> supported)
        =>Enumerable.Range(1,Math.Min(60,target)).Where(n=>target%n==0&&supported(n)).DefaultIfEmpty(1).Max();
    public static int SourceBars(int required,int targetMinutes,int sourceMinutes)
    {
        const int sessionMinutes=390;
        var completePerSession=sessionMinutes/targetMinutes;
        if(completePerSession==0)throw new InvalidOperationException("선택한 봉 길이가 국내 정규장보다 길어 완성 봉을 만들 수 없습니다.");
        var sourcePerSession=(sessionMinutes+sourceMinutes-1)/sourceMinutes;
        return (int)Math.Min(20000,((long)required+completePerSession-1)/completePerSession*sourcePerSession+sourcePerSession);
    }
}
