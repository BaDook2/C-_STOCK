using Stock.Core.Trading;
namespace Stock.Core.Research;

public sealed class ChartStrategySettings
{
    [Setting("이동평균 추세 전략")] public bool TrendEnabled {get;set;}=true;
    [Setting("단기 이동평균 (일)",2,120)] public int Fast {get;set;}=5;
    [Setting("장기 이동평균 (일)",3,250)] public int Slow {get;set;}=20;
    [Setting("박스권 전략")] public bool BoxEnabled {get;set;}
    [Setting("박스 관측 기간 (일)",5,250)] public int BoxDays {get;set;}=20;
    [Setting("박스 최대 폭 (%)",.1,50)] public decimal BoxWidth {get;set;}=12;
    [Setting("박스 하단 매수 영역 (%)",1,40)] public decimal EntryZone {get;set;}=20;
    [Setting("박스 최대 평균 거래량 (주, 0은 제한 없음)",0)] public decimal BoxMaxVolume {get;set;}
    [Setting("박스 거래량 감소 확인")] public bool BoxDecliningVolume {get;set;}
    [Setting("박스 장기 이동평균 지지 확인")] public bool BoxMaSupport {get;set;}
    [Setting("박스 지지선 허용 거리 (%)",1,20)] public decimal SupportTolerance {get;set;}=3;
    [Setting("박스 최소 독립 지지 터치",1,30)] public int BoxTouches {get;set;}=1;
    [Setting("박스 상단 매도 영역 (%)",1,40)] public decimal BoxExitZone {get;set;}=10;
    [Setting("급락 후 반등 전략")] public bool ReboundEnabled {get;set;}
    [Setting("급락 관측 기간 (일)",2,120)] public int DropDays {get;set;}=10;
    [Setting("고점 대비 하락 (%)",1,80)] public decimal DropPercent {get;set;}=10;
    [Setting("저점 대비 반등 확인 (%)",.1,30)] public decimal BouncePercent {get;set;}=2;
    [Setting("반등 진입 전 종목별 악재 확인 필요")] public bool RequireRiskReview {get;set;}=true;
    public bool RiskReviewed {get;set;}
    public bool Excluded {get;set;}
    [Setting("진입 시 일목 구름 위 조건")] public bool CloudFilter {get;set;}
    [Setting("최소 일 거래량 (주)",0)] public decimal MinimumVolume {get;set;}=0;
    [Setting("거래량 평균 대비 배수",0,20)] public decimal VolumeMultiple {get;set;}=0;
    [Setting("매수에 사용할 가상 자산 (%)",1,100)] public decimal AllocationPercent {get;set;}=20;
    [Setting("손절 (%)",.1,80)] public decimal StopPercent {get;set;}=5;
    [Setting("익절 (%)",.1,500)] public decimal TakePercent {get;set;}=15;
    [Setting("가상 수수료 · 편도 (%)",0,5)] public decimal FeePercent {get;set;}=.05m;
    [Setting("가상 슬리피지 · 편도 (%)",0,5)] public decimal SlippagePercent {get;set;}=.05m;
    [Setting("가상 체결 최대 일 거래량 비중 (%)",.001,10)] public decimal ParticipationPercent {get;set;}=1;
    public void Validate()
    {foreach(var p in GetType().GetProperties()){var a=(SettingAttribute?)Attribute.GetCustomAttribute(p,typeof(SettingAttribute));if(a==null||p.PropertyType==typeof(bool))continue;var n=Convert.ToDouble(p.GetValue(this));if(n<a.Min||n>a.Max)throw new InvalidOperationException(a.Label+" 범위를 확인하세요.");}if(Fast>=Slow)throw new InvalidOperationException("단기 이동평균은 장기보다 짧아야 합니다.");}
    public ChartStrategySettings Clone()=>Serialization.Unpack<ChartStrategySettings>(Serialization.Pack(this));
}
public static class ChartIndicators
{
    public static decimal? Sma(IReadOnlyList<PriceBar> b,int index,int period)=>index+1<period?null:b.Skip(index-period+1).Take(period).Average(x=>x.Close);
    public static decimal? Mid(IReadOnlyList<PriceBar> b,int index,int period)=>index+1<period?null:(b.Skip(index-period+1).Take(period).Max(x=>x.High)+b.Skip(index-period+1).Take(period).Min(x=>x.Low))/2;
    // Visible cloud at index uses values computed 26 bars EARLIER. No future prices.
    public static (decimal? A,decimal? B) Cloud(IReadOnlyList<PriceBar> b,int index)=>index<26?(null,null):((Mid(b,index-26,9)+Mid(b,index-26,26))/2,Mid(b,index-26,52));
}
public sealed record ChartSignal(bool Buy,bool Sell,string Reason);
/// <summary>스크리닝·백테스트·가상 재생에서 같은 확정 일봉 조건을 평가합니다.</summary>
public static class ChartStrategies
{
    public static ChartSignal Evaluate(IReadOnlyList<PriceBar> bars,int index,ChartStrategySettings s,string owner="")
    {
        if(index<1)return new(false,false,"데이터 준비 중");var last=bars[index];var fast=ChartIndicators.Sma(bars,index,s.Fast);var slow=ChartIndicators.Sma(bars,index,s.Slow);
        if(s.Excluded)return new(false,false,"사용자 악재 제외");
        if(owner=="이동평균 추세"&&fast<slow)return new(false,true,"이동평균 추세 종료");
        if(owner=="박스권"&&index>=s.BoxDays){var prior=bars.Skip(index-s.BoxDays).Take(s.BoxDays).ToArray();var lo=prior.Min(b=>b.Low);var hi=prior.Max(b=>b.High);if(last.Close>=hi-(hi-lo)*s.BoxExitZone/100||last.Close<lo)return new(false,true,"박스권 상단 / 하단 이탈");}
        if(last.Volume<s.MinimumVolume)return new(false,false,"최소 거래량 미달");
        if(s.VolumeMultiple>0){if(index<20)return new(false,false,"거래량 준비 중");if(last.Volume<bars.Skip(index-20).Take(20).Average(b=>b.Volume)*s.VolumeMultiple)return new(false,false,"거래량 배수 미달");}
        if(s.CloudFilter){var cloud=ChartIndicators.Cloud(bars,index);if(cloud.A==null||cloud.B==null)return new(false,false,"일목 78봉 준비 중");if(last.Close<=Math.Max(cloud.A.Value,cloud.B.Value))return new(false,false,"일목 구름 아래");}
        if(s.TrendEnabled&&fast>slow&&last.Close>fast)return new(true,false,"이동평균 추세");
        if(s.BoxEnabled&&index>=s.BoxDays)
        {var prior=bars.Skip(index-s.BoxDays).Take(s.BoxDays).ToArray();var low=prior.Min(b=>b.Low);var high=prior.Max(b=>b.High);var avg=prior.Average(b=>b.Volume);int touches=0;bool inside=false;foreach(var b in prior){var hit=b.Low<=low*(1+s.SupportTolerance/100);if(hit&&!inside)touches++;inside=hit;}if((s.BoxMaxVolume==0||avg<=s.BoxMaxVolume)&&(!s.BoxDecliningVolume||last.Volume<avg)&&(!s.BoxMaSupport||slow.HasValue&&Math.Abs(last.Close/slow.Value-1)*100<=s.SupportTolerance)&&touches>=s.BoxTouches&&(high-low)/low*100<=s.BoxWidth&&last.Close>=low&&last.Close<=low+(high-low)*s.EntryZone/100)return new(true,false,"박스권");}
        if(s.ReboundEnabled&&(!s.RequireRiskReview||s.RiskReviewed)&&index>=s.DropDays)
        {var prior=bars.Skip(index-s.DropDays).Take(s.DropDays).ToArray();var high=prior.Max(b=>b.High);var low=prior.Min(b=>b.Low);if((high-low)/high*100>=s.DropPercent&&last.Close>=low*(1+s.BouncePercent/100)&&last.Close>bars[index-1].Close&&bars[index-1].Close<=low*(1+s.BouncePercent/100))return new(true,false,"급락 후 반등");}
        return new(false,false,"설정 조건 미충족");
    }
}
public sealed record BarFill(DateTimeOffset Time,string Side,int Quantity,decimal Price,decimal Fee,decimal Realized,string Reason,string OrderId="")
{public string KoreaTime=>Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm");}
public sealed record BarEquity(DateTimeOffset Time,decimal Value);
// Market-on-next-open, no synthetic ticks and no invented within-bar path.
/// <summary>종가 판단 뒤 다음 시가 체결을 사용하는 가상 계좌로, 실제 주문을 전송하지 않습니다.</summary>
public sealed class BarPaperAccount
{
    public decimal Initial {get;}
    public decimal Cash {get;private set;}
    public int Quantity {get;private set;}
    public decimal Cost {get;private set;}
    public decimal Last {get;private set;}
    public string Owner {get;private set;}="";
    public bool HasPending=>pending!=null;
    public string PendingDescription=>pending is {} p?$"{(p.Buy?"매수":"매도")} {p.Quantity:N0}주 · 다음 거래일 시가 대기":"대기 주문 없음";
    private (bool Buy,int Quantity,string Reason)? pending;
    public List<BarFill> Fills {get;}=[];
    public List<BarEquity> Equity {get;}=[];
    public decimal Value=>Cash+Quantity*Last;
    public decimal Profit=>Value-Initial;
    public decimal Fees=>Fills.Sum(f=>f.Fee);
    public BarPaperAccount(decimal capital){if(capital<=0)throw new InvalidOperationException("가상 시작 금액은 0보다 커야 합니다.");Initial=Cash=capital;}
    public void Queue(bool buy,int quantity,string reason){if(pending!=null)throw new InvalidOperationException("다음 거래일에 처리할 주문이 있습니다.");if(quantity<=0||!buy&&quantity>Quantity)throw new InvalidOperationException("주문 수량을 확인하세요.");pending=(buy,quantity,reason);}
    public void Cancel()=>pending=null;
    public void Advance(PriceBar bar,ChartStrategySettings s)
    {
        if(pending is {} p)
        {
            var price=bar.Open*(1+(p.Buy?1:-1)*s.SlippagePercent/100);var qty=Math.Min(p.Quantity,(int)Math.Min(int.MaxValue,decimal.Floor(bar.Volume*s.ParticipationPercent/100)));
            qty=p.Buy?Math.Min(qty,(int)Math.Min(int.MaxValue,decimal.Floor(Cash/(price*(1+s.FeePercent/100))))):Math.Min(qty,Quantity);
            if(qty>0)
            {var fee=price*qty*s.FeePercent/100;decimal realized=0;if(p.Buy){Cash-=price*qty+fee;Cost=(Cost*Quantity+price*qty+fee)/(Quantity+qty);Quantity+=qty;Owner=p.Reason;}else{realized=(price-Cost)*qty-fee;Cash+=price*qty-fee;Quantity-=qty;if(Quantity==0){Cost=0;Owner="";}}Fills.Add(new(bar.Time,p.Buy?"매수":"매도",qty,price,fee,realized,p.Reason));}
            pending=null; // Unfilled remainder expires at this open; never carried silently.
        }
        Last=bar.Close;Equity.Add(new(bar.Time,Value));
    }
    public void AutoDecide(IReadOnlyList<PriceBar> bars,int index,ChartStrategySettings s)
    {
        if(HasPending)return;var signal=ChartStrategies.Evaluate(bars,index,s,Owner);
        if(Quantity>0){if(Last<=Cost*(1-s.StopPercent/100)||Last>=Cost*(1+s.TakePercent/100)||signal.Sell)Queue(false,Quantity,signal.Sell?signal.Reason:Last<Cost?"손절":"익절");}
        else if(signal.Buy){var qty=(int)Math.Min(int.MaxValue,decimal.Floor(Value*s.AllocationPercent/100/(bars[index].Close*(1+(s.FeePercent+s.SlippagePercent)/100))));if(qty>0)Queue(true,qty,signal.Reason);}
    }
    public decimal MaxDrawdown{get{decimal peak=Initial,dd=0;foreach(var p in Equity){peak=Math.Max(peak,p.Value);if(peak>0)dd=Math.Max(dd,(peak-p.Value)/peak*100);}return dd;}}
    public static BarPaperAccount Backtest(IReadOnlyList<PriceBar> bars,ChartStrategySettings settings,decimal capital,int start,CancellationToken token)
    {settings.Validate();var account=new BarPaperAccount(capital);for(int i=Math.Max(0,start);i<bars.Count;i++){token.ThrowIfCancellationRequested();account.Advance(bars[i],settings);if(i<bars.Count-1)account.AutoDecide(bars,i,settings);}account.Cancel();return account;}
}
