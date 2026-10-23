using System.Globalization;
using Stock.Core;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class MainViewModel
{
    private bool actualFeed;
    private DateTimeOffset lastReceived;
    private async Task Connect()
    {
        ApplyEditors();if(credentials==null)credentials=credentialsPrompt();if(credentials==null){Status="연결 설정 취소";return;}
        remote=null;kiwoom?.Dispose();kiwoom=new(credentials,SelectedExchange);await kiwoom.ConnectAsync();remote=new(kiwoom,settings,store,StateDirectory);await remote.Refresh(KoreaClock.Today);RunMode="키움 모의 수동 · "+SelectedExchange+" / "+kiwoom.MaskedAccount;await RefreshRemote();Status="키움 모의 계좌 연결 완료. 자동 전략은 로컬 Paper에서만 실행합니다.";
    }
    private async Task RefreshRemote()
    {if(remote!=null&&!Live){await remote.Refresh(DateOnly.FromDateTime(QueryDate??DateTime.Today));Replace(Orders,remote.Orders);Replace(Holdings,remote.Balance!.Holdings);Metrics=$"키움 {remote.Balance.Currency} 예수금 {remote.Balance.Cash:N4} / 현금+선택시장 평가 {remote.Balance.Total:N4} · {(remote.Unknown?"응답 불명/취소확인 대기":"조회 완료")}";}Replace(Logs,await Task.Run(()=>store.RecentAudit()));RefreshSavedLists();}
    private async Task Start()
    {
        ApplyEditors();if(Mode=="키움 모의 수동"){await Connect();return;}
        var instrument=new Instrument(Symbol.Trim().ToUpperInvariant(),SelectedExchange);instrument.Validate();
        if(Mode=="오프라인 데모")StartFeed(new DemoMarketDataProvider(instrument,settings.Data.DemoIntervalMs),"오프라인 합성 데모 + 로컬 Paper",false,instrument.Currency);
        else
        {
            if(kiwoom==null||kiwoom.Exchange!=SelectedExchange)await Connect();if(kiwoom==null)return;
            var universe=settings.Data.Universe.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Instrument.Parse).Where(i=>i.Currency==Currency).ToArray();
            if(!universe.Contains(instrument))throw new InvalidOperationException("수동 종목을 구독 universe에 먼저 추가하세요.");
            StartFeed(new KiwoomMarketDataProvider(kiwoom.GetTokenAsync,universe,settings.Data,FeedStatus),"키움 실시간 + 로컬 Paper (증권사 주문 없음)",true,Currency);
        }
    }
    private void FeedStatus(string text)
    {if(text.Contains("단절")||text.Contains("손실"))try{lock(engineLock)engine?.EmergencyStop();}catch{}dispatcher.BeginInvoke(()=>{DataStatus=text;Status=text;});}
    private void StartFeed(IMarketDataProvider source,string name,bool real,string currency)
    {
        var snapshot=settings.Clone();actualFeed=real;lastReceived=DateTimeOffset.UtcNow;received=stored=excluded=0;feedCancel=new();var token=feedCancel.Token;var run=store.BeginRun(name+" / "+TradingEngine.ModelVersion,snapshot);engine=new(snapshot,currency,snapshot.Risk.AccountCapital,(cat,obj,t)=>store.Audit(run,cat,obj,t));RunMode=name;Live=true;
        feedTask=Task.Run(async()=>
        {
            var quality=new DataQualityGate(snapshot.Data,snapshot.Execution);bool failed=false;
            try
            {
                await foreach(var e in source.Stream(token))
                {
                    lock(engineLock)lastReceived=DateTimeOffset.UtcNow;
                    Interlocked.Increment(ref received);Interlocked.Add(ref stored,store.InsertEvents([e]));
                    if(!quality.Accept(e,real,out var why)){Interlocked.Increment(ref excluded);store.Audit(run,"데이터 제외",new{e.Key,why},e.ExchangeTime);continue;}
                    lock(engineLock){if(real&&quality.Gaps>0&&!engine.Risk.Stopped)engine.EmergencyStop();engine.OnEvent(e);}
                    if(why!="정상")store.Audit(run,"데이터 주의",new{e.Key,why},e.ExchangeTime);
                }
            }
            catch(OperationCanceledException){ }
            catch(Exception){failed=true;try{lock(engineLock)engine.EmergencyStop();}catch{}FeedStatus("수신/기록 실패: 신규 주문 중지. 누락 구간 확인 필요");}
            finally
            {await source.DisposeAsync();_ = dispatcher.BeginInvoke(()=>{Live=false;Status=failed?"수신/SQLite 기록 실패 · 거래 중지 · 누락 구간 확인 필요":"데이터 수신 종료. 다음 이벤트가 없으면 로컬 취소 요청은 미확정으로 남습니다.";});}
        },token);
        Status="수신 시작 · SQLite 저장 / 선택 통화만 운용 / 스캐너는 구독 universe만 검사";
    }
    private async Task Stop(){feedCancel?.Cancel();if(feedTask!=null)await feedTask;Live=false;RefreshSavedLists();}
    private async Task Place(bool buy)
    {
        var i=new Instrument(Symbol.Trim().ToUpperInvariant(),SelectedExchange);if(!int.TryParse(Quantity,out var q)||!decimal.TryParse(Price,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var p))throw new InvalidOperationException("수량 정수·가격 숫자를 입력하세요.");var request=new OrderRequest(i.Symbol,q,p,buy,i.Exchange,ManualKind);request.Validate();
        if(!confirm($"{RunMode}\n{i.Key} / {i.Currency}\n{(buy?"매수":"매도")} {q}주 · 참조/지정가 {p} · {ManualKind}\n리스크 검사를 거쳐 주문할까요?"))return;
        if(Live){lock(engineLock){if(actualFeed&&DateTimeOffset.UtcNow-lastReceived>TimeSpan.FromSeconds(settings.Execution.StaleSeconds))throw new InvalidOperationException("실시간 데이터 stale: 수동 주문 금지");var o=engine!.Manual(i,buy,q,p,ManualKind);Status=o==null?"리스크 거절: 판단 로그를 확인하세요.":"로컬 Paper 접수: "+o.Id;}}
        else if(remote!=null&&RunMode.StartsWith("키움 모의")){var receipt=await remote.Submit(request);Status="키움 모의 접수 "+receipt.Number+" · 체결 완료 아님";await RefreshRemote();}
        else throw new InvalidOperationException("활성 모드를 시작하세요.");
    }
    public void Snapshot()
    {
        if(engine==null||Busy)return;lock(engineLock)
        {
            if(Live&&actualFeed&&DateTimeOffset.UtcNow-lastReceived>TimeSpan.FromSeconds(settings.Execution.StaleSeconds)&&!engine.Risk.Stopped){try{engine.EmergencyStop();}catch{}Status="시세 stale: 신규 주문 중지. 데이터 상태를 확인한 후 새 세션을 시작하세요.";}
            Replace(PaperOrders,engine.Broker.Orders.TakeLast(300).Reverse());Replace(Positions,engine.Broker.Positions.Values.Where(p=>p.Quantity>0));if(Live)Replace(Fills,engine.Broker.Fills.TakeLast(300).Reverse());
            Replace(Scanner,engine.Contexts.Values.Where(c=>c.Price>0).Select(c=>new ScannerRow(c.Instrument.Key,c.Instrument.Currency,c.Price,c.Spread,c.Last?.CumulativeVolume,c.Rate(Math.Max(1,settings.Scalping.CurrentSeconds)),string.Join(" / ",SupportResistanceService.Levels(c).Take(5)),string.Join(" / ",VolumeProfileService.Calculate(c).Select(v=>$"{v.Price}:{v.Volume}")),engine.Decisions.LastOrDefault(d=>d.Symbol==c.Instrument.Key)?.Reason??"준비중")));
            if(Live){Metrics=$"{engine.Broker.Currency} 현금 {engine.Broker.Cash:N4} / 예약 {engine.Broker.ReservedCash:N4} / 자산 {engine.Broker.Equity:N4} / {(engine.Risk.Stopped?"신규주문 중지":"Paper 가동")}";DataStatus=$"수신 {received:N0} / 저장 {stored:N0} / 제외 {excluded:N0} · 버퍼 {settings.Data.BufferCapacity:N0} · 최대 {settings.Data.SubscriptionLimit}종목";Replace(Logs,engine.Decisions.TakeLast(100).Reverse().Select(d=>$"{d.Time:HH:mm:ss} {d.Symbol} {d.Strategy} [{d.Signal}] {d.Reason}"));}
        }
    }
}
