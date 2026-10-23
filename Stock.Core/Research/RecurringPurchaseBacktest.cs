using Stock.Core.Trading;
namespace Stock.Core.Research;
public enum PurchaseFrequency { Daily, Weekly, Monthly }
public enum PurchaseTiming { Open, Close }
public sealed class RecurringPurchaseConfig
{
    [Parameter("기간","설정한 시작일과 종료일 사이의 확정 일봉만 사용합니다. 거래일이 아닌 날짜에는 매수하지 않습니다.","더 늦은 시작일로 범위를 줄입니다.","더 이른 시작일로 범위를 넓힙니다.","한국시간 날짜")]
    public DateTime From {get;set;}=DateTime.Today.AddYears(-1);
    public DateTime To {get;set;}=DateTime.Today;
    [Parameter("매수 주기","매일 또는 매주·매월 첫 거래일에 일정 금액을 납입하고 매수합니다.","매수 간격을 늘리면 납입 횟수가 줄어듭니다.","매수 간격을 줄이면 납입 횟수가 늘어납니다.","매일 / 매주 / 매월")]
    public PurchaseFrequency Frequency {get;set;}
    [Parameter("매수 시점","해당 거래일의 시가 또는 종가로 체결한다고 가정합니다. 실제 주문을 보내지 않습니다.","종가는 그날 마감 가격을 사용합니다.","시가는 그날 시작 가격을 사용합니다.","시가 / 종가")]
    public PurchaseTiming Timing {get;set;}
    [Parameter("1회 매수 금액","각 회차마다 새로 납입할 금액입니다. 남는 현금은 다음 회차로 이월합니다.","회차별 납입금이 증가합니다.","회차별 납입금이 감소합니다.","종목 통화",0.0001,1000000000000)]
    public decimal Amount {get;set;}=100000;
    [Parameter("초기 투자금","첫 회차의 정기 납입금에 추가하는 초기 금액입니다.","첫 매수에 더 많은 자금을 사용합니다.","첫 매수의 초기 자금을 줄입니다.","종목 통화",0,1000000000000)]
    public decimal InitialAmount {get;set;}=3000000;
    [Parameter("수수료","각 매수 체결금액에 적용할 비율입니다. 이 비용을 포함해 살 수 있는 정수 주식 수를 계산합니다.","비용이 증가하고 매수 가능 수량이 감소할 수 있습니다.","비용이 감소합니다.","%",0,99.999)]
    public decimal CommissionPercent {get;set;}=.015m;
    public void Validate()
    {if(From>To)throw new InvalidOperationException("시작일은 종료일보다 늦을 수 없습니다.");if(Amount<=0||Amount>1_000_000_000_000||InitialAmount<0||InitialAmount>1_000_000_000_000)throw new InvalidOperationException("매수 금액은 0 초과, 초기 투자금은 0 이상이며 각각 1,000,000,000,000 이하여야 합니다.");if(CommissionPercent<0||CommissionPercent>=100)throw new InvalidOperationException("수수료는 0% 이상 100% 미만으로 입력하세요.");if(!Enum.IsDefined(Frequency)||!Enum.IsDefined(Timing))throw new InvalidOperationException("매수 주기와 시점을 선택하세요.");}
}
public sealed record RecurringEquity(DateTimeOffset Time,decimal Equity,decimal Contributions,decimal UnitValue);
public sealed class RecurringPurchaseResult
{
    public string Id {get;set;}="recurring-run:"+Guid.NewGuid().ToString("N");
    public StockItem Stock {get;set;}=new("","",Exchange.KRX,"");
    public RecurringPurchaseConfig Config {get;set;}=new();
    public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;
    public decimal Contributions {get;set;}
    public decimal Cash {get;set;}
    public long Quantity {get;set;}
    public decimal Value {get;set;}
    public decimal Fees {get;set;}
    public decimal AveragePrice {get;set;}
    public decimal MaxDrawdown {get;set;}
    public int Sessions {get;set;}
    public BarFill[] Purchases {get;set;}=[];
    public RecurringEquity[] Equity {get;set;}=[];
    public string DataHash {get;set;}="";
    public decimal ReturnPercent=>Contributions>0?(Value/Contributions-1)*100:0;
}
/// <summary>외부 납입을 반영한 정수 주식 정기매수. 미체결 잔액은 다음 회차로 이월합니다.</summary>
public static class RecurringPurchaseBacktest
{
    public static RecurringPurchaseResult Run(BarDataset data,RecurringPurchaseConfig config,CancellationToken token)
    {
        config.Validate();var c=Serialization.Unpack<RecurringPurchaseConfig>(Serialization.Pack(config));
        var bars=data.CompletedBars.Where(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date>=c.From.Date&&b.Time.ToOffset(TimeSpan.FromHours(9)).Date<=c.To.Date).OrderBy(b=>b.Time).DistinctBy(b=>b.Time).ToArray();
        if(bars.Length==0)throw new InvalidOperationException("선택 기간의 확정 일봉 데이터가 없습니다.");
        var fills=new List<BarFill>();var curve=new List<RecurringEquity>();decimal cash=0,contributed=0,units=0,peak=1,mdd=0,cost=0,fees=0;long qty=0;string? lastBucket=null;
        foreach(var b in bars)
        {
            token.ThrowIfCancellationRequested();b.Validate();var date=b.Time.ToOffset(TimeSpan.FromHours(9)).Date;var bucket=c.Frequency switch{PurchaseFrequency.Daily=>date.ToString("yyyy-MM-dd"),PurchaseFrequency.Weekly=>date.AddDays(-((int)date.DayOfWeek+6)%7).ToString("yyyy-MM-dd"),_=>date.ToString("yyyy-MM")};
            if(bucket!=lastBucket)
            {
                var price=c.Timing==PurchaseTiming.Open?b.Open:b.Close;var nav=units>0?(cash+qty*price)/units:1;
                var deposit=c.Amount+(lastBucket==null?c.InitialAmount:0);units+=deposit/nav;cash+=deposit;contributed+=deposit;lastBucket=bucket;
                var count=decimal.Floor(cash/(price*(1+c.CommissionPercent/100)));if(count>int.MaxValue)throw new InvalidOperationException("1회 매수 수량이 지원 한도 2,147,483,647주를 초과했습니다. 금액을 줄이세요.");var buy=(int)count;
                if(buy>0){var fee=buy*price*c.CommissionPercent/100;cash-=buy*price+fee;qty+=buy;if(qty>int.MaxValue)throw new InvalidOperationException("총 보유 수량이 지원 한도를 초과했습니다.");cost+=buy*price;fees+=fee;fills.Add(new(b.Time,"매수",buy,price,fee,0,"정기매수 · "+(c.Timing==PurchaseTiming.Open?"시가":"종가")));}
            }
            var value=cash+qty*b.Close;var unit=value/units;peak=Math.Max(peak,unit);mdd=Math.Max(mdd,(1-unit/peak)*100);curve.Add(new(b.Time,value,contributed,unit));
        }
        return new(){Stock=data.Stock,Config=c,Contributions=contributed,Cash=cash,Quantity=qty,Value=curve[^1].Equity,Fees=fees,AveragePrice=qty>0?cost/qty:0,MaxDrawdown=mdd,Sessions=bars.Length,Purchases=fills.ToArray(),Equity=curve.ToArray(),DataHash=TradingStore.Hash(Serialization.Pack(bars))};
    }
}
