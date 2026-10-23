using System.Collections.ObjectModel;
using System.Globalization;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed record RecurringResultCard(string Label,string Value,string Color="#333D4B");
public sealed partial class WorkspaceViewModel
{
    private int backtestTab,selectedChartRangeMonths;private RecurringPurchaseResult? recurringResult;private bool recurringMarkers=true;
    public int ChartWindowCount {get;private set;}=100;
    public bool ChartRange1Selected=>selectedChartRangeMonths==1;
    public bool ChartRange3Selected=>selectedChartRangeMonths==3;
    public bool ChartRange6Selected=>selectedChartRangeMonths==6;
    public bool ChartRange12Selected=>selectedChartRangeMonths==12;
    public bool ChartRange36Selected=>selectedChartRangeMonths==36;
    public bool ChartRange60Selected=>selectedChartRangeMonths==60;
    public bool ChartRange120Selected=>selectedChartRangeMonths==120;
    public int WorkspaceToolTab {get=>workspaceToolTab;set{if(workspaceToolTab==value)return;workspaceToolTab=value;var next=value==3?1:0;Changed();if(backtestTab!=next){backtestTab=next;Changed(nameof(BacktestTab));}RefreshRecurringChart();}} private int workspaceToolTab=1;
    public int BacktestTab {get=>backtestTab;set{var target=value==1?3:2;if(backtestTab==value&&workspaceToolTab==target)return;backtestTab=value;workspaceToolTab=target;Changed();Changed(nameof(WorkspaceToolTab));RefreshRecurringChart();}}
    private string recurringAmount="100,000";public string RecurringAmount {get=>recurringAmount;set{recurringAmount=value;Changed();}}
    private string recurringInitial="3,000,000";public string RecurringInitial {get=>recurringInitial;set{recurringInitial=value;Changed();}}
    private string recurringFee="0.015";public string RecurringFee {get=>recurringFee;set{recurringFee=value;Changed();}}
    private PurchaseFrequency recurringFrequency;private PurchaseTiming recurringTiming;
    public bool RecurringDaily {get=>recurringFrequency==PurchaseFrequency.Daily;set{if(value)SetFrequency(PurchaseFrequency.Daily);}}
    public bool RecurringWeekly {get=>recurringFrequency==PurchaseFrequency.Weekly;set{if(value)SetFrequency(PurchaseFrequency.Weekly);}}
    public bool RecurringMonthly {get=>recurringFrequency==PurchaseFrequency.Monthly;set{if(value)SetFrequency(PurchaseFrequency.Monthly);}}
    public string RecurringFrequencyText=>recurringFrequency switch {PurchaseFrequency.Daily=>"매일",PurchaseFrequency.Weekly=>"매주",_=>"매월"};
    public string RecurringTimingText=>RecurringAtOpen?"시가":"종가";
    private void SetFrequency(PurchaseFrequency f){recurringFrequency=f;Changed(nameof(RecurringDaily));Changed(nameof(RecurringWeekly));Changed(nameof(RecurringMonthly));Changed(nameof(RecurringFrequencyText));}
    public bool RecurringAtOpen {get=>recurringTiming==PurchaseTiming.Open;set{if(value)SetTiming(PurchaseTiming.Open);}}
    public bool RecurringAtClose {get=>recurringTiming==PurchaseTiming.Close;set{if(value)SetTiming(PurchaseTiming.Close);}}
    private void SetTiming(PurchaseTiming t){recurringTiming=t;Changed(nameof(RecurringAtOpen));Changed(nameof(RecurringAtClose));Changed(nameof(RecurringTimingText));}
    public bool ShowRecurringMarkers {get=>recurringMarkers;set{recurringMarkers=value;Changed();RefreshRecurringChart();}}
    public bool ChartSettingsOpen {get;set;}
    public ObservableCollection<RecurringResultCard> RecurringCards {get;}=[];
    public string RecurringResultPeriod=>recurringResult==null?"실행 후 결과가 표시됩니다.":$"{recurringResult.Equity[0].Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd} ~ {recurringResult.Equity[^1].Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd} · {recurringResult.Sessions:N0}거래일";
    public string RecurringDetails=>recurringResult==null?"정수 주식으로 매수하며 남는 현금은 이월합니다.":$"최종 보유 수량  {recurringResult.Quantity:N0}주\n잔여 현금  {recurringResult.Cash:N0} {recurringResult.Stock.Currency}\n수수료 합계  {recurringResult.Fees:N0} {recurringResult.Stock.Currency}\n평균 매수 금액  {(recurringResult.Purchases.Length>0?recurringResult.Purchases.Sum(f=>f.Price*f.Quantity)/recurringResult.Purchases.Length:0):N0} {recurringResult.Stock.Currency}\n배당금  자료 미제공 · 계산 제외\nMDD는 외부 납입 영향을 제거한 단위가치 기준";
    public string RecurringNotice {get;private set;}="매주·매월은 해당 기간의 첫 거래일에 매수합니다. 초기 투자금은 첫 회차 금액에 더합니다. 배당 자료는 제공되지 않아 재투자는 지원하지 않습니다.";
    public IReadOnlyList<RecurringEquity> ChartEquity=>BacktestTab==1&&recurringResult!=null&&recurringResult.Stock.Key==selected?.Key?recurringResult.Equity:[];
    public IReadOnlyList<TradeMarker> DisplayTradeMarkers
    {
        get
        {
            var rows=new List<TradeMarker>(TradeMarkers);rows.AddRange(BacktestTradeMarkers());
            if(BacktestTab==1&&ShowRecurringMarkers&&recurringResult!=null&&recurringResult.Stock.Key==selected?.Key)
                rows.AddRange(recurringResult.Purchases.Select((f,i)=>new TradeMarker("recurring:"+i,recurringResult.Stock.Key,f.Time,true,f.Quantity,f.Price,"정기매수")));
            return rows.DistinctBy(m=>m.Id).OrderBy(m=>m.Time).ToArray();
        }
    }
    public string StockCode=>selected?.Symbol??"";
    public string StockCurrency=>selected?.Currency=="KRW"?"원":"달러";
    private void InitializeRecurring()
    {
        var saved=store.State<RecurringPurchaseConfig>("recurring-config-v1");if(saved!=null){RecurringAmount=NumericDisplay.Format(saved.Amount);RecurringInitial=NumericDisplay.Format(saved.InitialAmount);RecurringFee=NumericDisplay.Format(saved.CommissionPercent);recurringFrequency=saved.Frequency;recurringTiming=saved.Timing;}
        Add("정기매수실행",RunRecurring,()=>CanConfigure&&dataset!=null);
        Add("정기매수초기화",()=>{RecurringAmount=selected?.Currency=="USD"?"100":"100,000";RecurringInitial=selected?.Currency=="USD"?"3,000":"3,000,000";RecurringFee="0.015";SetFrequency(PurchaseFrequency.Daily);SetTiming(PurchaseTiming.Open);foreach(var n in new[]{nameof(RecurringAmount),nameof(RecurringInitial),nameof(RecurringFee)})Changed(n);return Task.CompletedTask;},()=>CanConfigure);
        foreach(var months in new[]{1,3,6,12,36,60,120}){int captured=months;Add("기간"+months,()=>{if(dataset!=null){var chartFrom=dataset.Bars[^1].Time.ToOffset(TimeSpan.FromHours(9)).Date.AddMonths(-captured);ChartBars=dataset.Bars.Where(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date>=chartFrom).ToArray();ChartWindowCount=ChartBars.Count;SetChartRangeSelection(captured);Changed(nameof(ChartWindowCount));}return Task.CompletedTask;},()=>CanConfigure&&dataset!=null&&!IsTick);}
    }
    private void SetChartRangeSelection(int months)
    {
        selectedChartRangeMonths=months;
        foreach(var name in new[]{nameof(ChartRange1Selected),nameof(ChartRange3Selected),nameof(ChartRange6Selected),nameof(ChartRange12Selected),nameof(ChartRange36Selected),nameof(ChartRange60Selected),nameof(ChartRange120Selected)})Changed(name);
    }
    private async Task RunRecurring()
    {
        if(dataset==null||From==null||To==null)throw new InvalidOperationException("종목과 기간을 선택하세요.");
        decimal Number(string text)=>decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)?value:throw new InvalidOperationException("매수 금액·초기 투자금·수수료를 숫자로 입력하세요.");
        var config=new RecurringPurchaseConfig{From=From.Value,To=To.Value,Frequency=recurringFrequency,Timing=recurringTiming,Amount=Number(RecurringAmount),InitialAmount=Number(RecurringInitial),CommissionPercent=Number(RecurringFee)};config.Validate();var data=dataset;var token=Token;
        var result=await Task.Run(()=>RecurringPurchaseBacktest.Run(data,config,token),token);recurringResult=result;store.State("recurring-config-v1",config);store.State(result.Id,result);
        var history=new SavedBacktest{Id="chart-run:"+result.Id,At=result.At,Stock=result.Stock,Period="정기매수 · "+recurringFrequency,DataHash=result.DataHash,Initial=result.Contributions,Cash=result.Cash,Quantity=(int)Math.Min(int.MaxValue,result.Quantity),Profit=result.Value-result.Contributions,MaxDrawdown=result.MaxDrawdown,Fills=result.Purchases,Equity=result.Equity.Select(p=>new BarEquity(p.Time,p.Equity)).ToArray()};
        store.State(history.Id,history);LatestResult=history;SelectedRun=history;History.Insert(0,history);Changed(nameof(LatestResult));BacktestTab=1;
        var unit=result.Stock.Currency;Replace(RecurringCards,new[]{new RecurringResultCard("총 투자금",$"{result.Contributions:N0} {unit}","#3182F6"),new RecurringResultCard("평가금액",$"{result.Value:N0} {unit}","#20A080"),new RecurringResultCard("수익률",$"{result.ReturnPercent:+#,##0.00;-#,##0.00;0.00}%",result.ReturnPercent>=0?"#F04452":"#3182F6"),new RecurringResultCard("매수 횟수",$"{result.Purchases.Length:N0}회"),new RecurringResultCard("평균 매입단가",$"{result.AveragePrice:N0} {unit}"),new RecurringResultCard("최대 낙폭 (MDD)",$"-{result.MaxDrawdown:N2}%","#3182F6")});
        ChartBars=data.CompletedBars.Where(b=>b.Time>=result.Equity[0].Time&&b.Time<=result.Equity[^1].Time).ToArray();
        RecurringNotice="정기매수 테스트 완료 · 실제 주문 없음. "+(data.Bars[0].Time.ToOffset(TimeSpan.FromHours(9)).Date>config.From.Date?"요청 시작일 이전 자료가 없어 제공된 구간만 계산했습니다. ":"")+"배당 미포함 · 초기금은 첫 정기 납입에 합산 · 정수 주식/잔액 이월";
        foreach(var n in new[]{nameof(RecurringResultPeriod),nameof(RecurringDetails),nameof(RecurringNotice)})Changed(n);ChartWindowCount=ChartBars.Count;Changed(nameof(ChartWindowCount));RefreshRecurringChart();Log("정기매수 백테스트 완료 · 기록 저장");
    }
    private void RefreshRecurringChart(){Changed(nameof(ChartEquity));Changed(nameof(DisplayTradeMarkers));Changed(nameof(StockCode));Changed(nameof(StockCurrency));}
}
