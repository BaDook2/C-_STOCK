# Bin’s Strategy — 구현·설정·검증 가이드

2026-11-11 로컬 소스 기준. 전략 ID는 `bins-strategy-v1`다. 이 문서에 구현 결정, 수식, 파라미터, **생략 없는 전체 JSON**, 검사 명령과 실제 제한을 모았다. 초기 숫자는 실험값이며 시장 수익성 검증 결과가 아니다.

## 이번 요청에 적용한 범위

사용자 요청의 파라미터·지표·시장 분류·5개 매수 패턴·공통 매도/위험/재진입·주문/체결·백테스트·테스트를 Core에 구현했다. `Stock.Core/Strategies/Bins/`가 독립 진입점이다. 이전 대화에서 별도 체계로 정한 Inje와 기존 UI는 보존했다. 첨부 설계서의 오래된 공개 커밋 기준 일괄 교체 지시로 현재 Inje나 실제 기록을 삭제하지 않았다.

**후속 UI 연결 완료:** 전략설정에 Inje/Bin 상위 그룹과 단일 사용 체크·전체 필드·기간 단위·전체 JSON 저장/불러오기를 연결했다. Bin 백테스트·일반 스크리닝도 연결했다. 실시간 가상 주문/재시작 복원은 미연결이므로 시작을 차단한다. 최신 사용자용 통합 설명과 전체 설정 JSON은 `현재_전략_상세설명_및_JSON_가이드_2026-11-11.md` 한 파일에 모았다. GitHub 게시와 실계좌 자동 주문은 하지 않는다.**

원본 명세는 `docs/requirements/Bins_Strategy_Codex_Requirements_v1.0.md`에 그대로 보관했다. 원문의 복합 결정을 아래 22개 검증 단위로 나눴다.

## 확정 결정 22개 추적표

| 번호 | 적용 결정 | 구현·검사 근거 |
|---|---|---|
| 01 | 한국 KRX 및 미국 NASDAQ/NYSE/AMEX 지원 | MarketConfig, Instrument, KRW/USD 계좌; E06/E07 |
| 02 | 선택형 Stage1 OFF이면 지지 자료 없이 Stage2 평가 | SupportActivationGate; B01/B02 |
| 03 | 설정 가능한 MA와 매물대 중첩·폭·접근·반등 교차 | SupportCalculator, StageOneConfig; B03/B10 |
| 04 | 기본30분 허가, 독립된 새 반등만 만료 갱신 | episode·departure·최소 간격; B04/B05/B06 |
| 05 | 지지 붕괴는 허가 취소·신규매수 차단, 보유는 공통 보호 | 잠긴 Lower, 별도 PositionState; B07/B08/B09 |
| 06 | 상승/하락/박스/Unknown 네 상태, 기본60분 관측 | MarketRegimeClassifier; A03~A07 |
| 07 | 동일 새 분류 기본2개 확정봉 확인, 불확실하면 매수 차단 | committed/candidate/연속수 분리; A04/A05/A08 |
| 08 | 가격 구조+방향성+변동성, 선택 분류봉1/3/5/15분 및 사용자 봉 | HalfWindowStructure, ER/ATR, BarTimeframeSpec; A02/A10 |
| 09 | 진입은 유효 신규 체결 매 틱, 호가/시계로 반등 생성 금지 | OnMarket의 Trade 분기; C03/C04 |
| 10 | 하락 반등 Fast/Confirmed 두 모드, 기본Confirmed | 같은 DowntrendRebound 패턴의 선택; C01/C02 |
| 11 | 상승 이전 고점 돌파는 확정 과거 저항+가격 유지+유효 틱 | UptrendBreakout; C03/C04/C11 |
| 12 | 상승 눌림은 조정률+확정 EMA+반등+미세 고점 돌파 모두 필요 | UptrendPullback; C05/C06 |
| 13 | 박스 하단 접근 후 안정·신규 반등, 붕괴 박스 재사용 금지 | RangeLowerBounce, 독립 box validity; C07/C08 |
| 14 | 잠긴 박스 상단 돌파·유지, 단일 에피소드와 상승 전환 특례 | RangeUpperBreakout, BrokenUp; C09/C10 |
| 15 | Manual 가족 고정 또는 Automatic 확정 상태 선택 | StrategyFamilySelector, 구조 검사 유지; D01/D03 |
| 16 | 자동 전환 ON은 flat/주문 해소 후, OFF는 종목 당일 첫 가족 고정 | 보유·미체결·Unknown 동안 잠금; D01/D02 |
| 17 | 공통 TP1/TP2 분할 익절+확인된 TP1 체결 후 추적 | CommonExitConfig, 실제 수량 기준; D04/D05/D07 |
| 18 | 공통 고정+기술 손절 동시 사용, 패턴별 가격 앵커만 다름 | TechnicalStop snapshot, 한 보호 의도; D06/D07 |
| 19 | 종목당 포지션1·추가매수0, 초기 주문의 부분체결만 허용 | 엔진/어댑터 이중 가드; C12/E03 |
| 20 | 새 에피소드와 공통 쿨다운, 손절 대기는 익절보다 김 | full-close 확인 시각, 일중/연속/계좌 한도; D08~D10 |
| 21 | 유동성·순보상·통화별 현금/위험·슬리피지 제한 지정가 | BinsRisk, ExecutionPolicy, cancel/Unknown; E01~E07 |
| 22 | 모든 수치 설정·경과시간 초/분/시/일, 비공개 자동 튜닝 없음 | Configuration/metadata/strict JSON, 고정 정책 검증; CONFIG/TIME/E08~E10 |

## 코드 구조와 호출

| 파일 | 책임 |
|---|---|
| Configuration.cs | immutable record 설정, DurationSpec/BarTimeframeSpec, 범위·조합 검사, JSON, SHA256, 파라미터 메타데이터 |
| MarketData.cs | 시점별 세션/메타데이터/가격 규칙/수수료 계약, 원본 이벤트 인과성, 빈 봉 없는 확정봉 생성 |
| Indicators.cs | 증분 EMA/Wilder ATR, ER·분위수·가격 구조·반응 횟수·분류 확인, 이력/워밍업 계획 |
| SupportActivation.cs | 거래량 보존 프로파일, MA 중첩, 독립 지지 접근/반등·활성/붕괴/만료 |
| EntryPatterns.cs | 다섯 패턴의 후보/신규 교차·고정 앵커·우선순위, 세 가족 선택·잠금 |
| PositionRisk.cs | 공유 청산, 확인된 TP 진행·추적/본전 보호, 신규 매수 가능성·비용·위험 수량 |
| Execution.cs | 공통 IStrategyExecutionAdapter 기반 paper 어댑터, 지정가·부분체결·취소 확인·Unknown·보고 멱등성 |
| BinsStrategyEngine.cs | 계좌 단일 lock, 이벤트 순서·가족/설정 잠금·원장 체결·카운터·대사·감사 snapshot |
| Replay.cs | 동일 엔진의 원본 가용/수신 순 재생, 통화별·가족/패턴별 성과와 결정적 해시 |
| ../Trading/PaperBrokerConfirmedFill.cs | 기존 PaperBroker의 공통 가상 원장에 검증 체결을 원자적으로 반영; 실주문 전송 없음 |

기본 사용 순서는 `config.Validate()` → `BinsWarmupPlan.For(config)` → 통화별 `BinsPaperExecution` → `BinsStrategyEngine`다. 공급자가 확인한 완료봉은 `LoadCompletedBar`, 원본 틱 매물대 자료는 `LoadProfileTrade`로 가용시각 순서에 맞춰 제공한다. 실시간처럼 처리할 때는 `OnMarket(input, now)`와 `OnClock(now)`를 전달한다. 실행 통지는 `OnExecutionReport(currency, report, now)`로 같은 계좌 lock 안에서 반영한다. `Snapshot(symbol)` 및 `RiskSnapshot(currency, tradingDate)`가 판단·보유·주문·위험 상태를 제공한다.

원본 재생은 `BinsReplayRequest`와 `BinsReplayEngine.Run(request)`을 사용한다. `InitialCash`를 `USD`/`KRW`별로 명시하고 `Precision=OriginalTicks`, 원본 수신 순서/거래소시각, 자료 출처와 시계 간격을 제공해야 한다. 동일 KnownAt의 시장 이벤트는 원래 배열의 도착 순서를 보존한다. 미래 봉을 나중 시각에 선로딩한 뒤 과거 틱으로 시계를 되돌려 실행할 수 없다.

```csharp
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

BinsStrategyConfig config = BinsJson.Import(fullJson);
config.Validate();
BinsWarmupPlan warmup = BinsWarmupPlan.For(config);
var engine = new BinsStrategyEngine(config,
    [new Instrument("AAPL", Exchange.NASDAQ), new Instrument("005930", Exchange.KRX)],
    [new BinsPaperExecution("USD", 10_000m), new BinsPaperExecution("KRW", 10_000_000m)]);
// warmup.Bars에 맞는 실제 확정봉과 시점별 세션/호가단위/원본 틱을 제공한다.
// engine.OnMarket(input, input.KnownAt); engine.OnClock(now);
// engine.SetEntriesEnabled(false, now);  // 보유 보호는 계속 작동한다.
```

예시 초기 현금은 별도 가상 예산이다. JSON의 통화별 `plannedInvestment`, `maxOrderCash`, `maxConcurrentExposure`, `maxDailyLoss`, `riskBudgetPerPosition`과 날짜별 `fees`도 명시해야 매수 승인된다. 이 설정을 비워 두면 실제 기본 숫자를 추측해서 채우지 않고 진입을 차단한다.

## 시간·자료·가격 규칙

- `DurationSpec = {value: decimal, unit: Second|Minute|Hour|Day}`. Day는 정확히24시간이다. 음수·정밀도100ns 미만·TimeSpan 초과 값은 오류다. 횟수와 봉 수는 정수다.
- `BarTimeframeSpec = {value: int, unit: Second|Minute|Hour|TradingDay}`. N TradingDay는 실제 N개 거래 세션의 공급자 확인 완료봉을 뜻한다. 일봉은 경과시간24시간으로 합성하지 않는다. 거래일 봉은 공급자 완료 입력으로 선로딩하고, 초/분/시 봉은 검증된 원본 체결로 생성한다.
- 세션은 거래소 시간대와 제공자 확인 개장/폐장·거래일·주문 유형을 사용한다. 미국은 America/New_York의 DST를 반영하며 휴장일·조기 마감·장외 권한을 시간대 계산으로 추측하지 않는다.
- 양방향 호가와 실제 잔량이 없으면 신규매수 불가다. 가격 규칙은 날짜·KnownAt·출처·가격 구간별 TickBand로 제공하며 `Instrument.Tick()` 추측 함수를 호출하지 않는다.
- 확정봉의 ClosedAt/KnownAt, 틱의 ExchangeTime/ReceivedTime/KnownAt, 원천/순서를 검사한다. 중복은 반복 판단하지 않고, 시간 역전·자료 공백은 후보·허가를 차단/초기화한다. 양의 실제 체결량만 유지·안정의 증거로 센다.
- 기본 Stage1 OFF는 MA240 자료나 그에 필요한 버퍼 크기를 요구하지 않는다. ATR14에는 이전 종가를 포함한15개 완료봉, EMA20에는20개 완료봉이 필요하다. 관측60분과 추가 워밍업 봉을 구분한다. 저항/상승 파동의 선언된 과거 관측 구간 및 미세 고점 창도 부족하면 해당 패턴에 InsufficientHistory를 반환한다.

## 수식과 상태 전환

### 시장 분류

`DirectionPct=100*(lastClose/firstClose-1)`.
`ER=abs(lastClose-firstClose)/sum(abs(adjacentCloseDifference))`; 분모0이면0이다.
`TR=max(H-L, abs(H-prevC), abs(L-prevC))`; 처음 AtrPeriod개 TR의 평균을 seed로 쓰고 이후 `(previousATR*(period-1)+TR)/period`를 사용한다. `ATRpct=100*ATR/lastClose`, `TrendMinMove=max(MinTrendMovePct,AtrTrendMultiplier*ATRpct)`다. EMA는 첫 Period개 종가의 SMA로 seed 후 `alpha=2/(period+1)`를 쓴다. 증분 ATR/EMA는 버퍼에서 오래된 봉을 내보내도 재시작하지 않는다.

관측 봉을 과거 절반A/최근 절반B로 나눈다. `epsilon=StructureAtrTolerance*ATR`. 상승은 `HB>HA+epsilon AND LB>LA+epsilon`, 하락은 반대다. 방향률·ER·최소 이동폭까지 모두 통과한 후보만 사용한다. 이 구조 분석은 IPriceStructureAnalyzer로 분리했다.

박스 경계는 완료봉 High/Low의 설정 분위수다. 분위수는 정렬 후 `(n-1)*q` 위치에서 선형 보간한다. 접근한 봉 이후 **다른 완료봉의 종가 반전**을 확인해 반응을 세므로 한 OHLC 안의 고저 선후를 꾸미지 않는다. 같은 구간의 접촉 반복을 여러 에피소드로 세지 않고 분리 시간·이탈/재접근을 요구한다. ER·양쪽 반응 수·폭·내부 종가 비율이 모두 맞아야 Range 후보다.

새 후보 기본2개 연속 완료봉에서만 committed 상태를 바꾼다. Unknown이면 확인 수를 리셋하고 기존 라벨을 유지하더라도 신규 진입 허가는 중지한다. 박스 경계는 확정 순간 잠근다. BrokenUp/BrokenDown/Expired 박스는 새 독립 반응과 확인을 거친 새 ID로만 대체한다. 보유 중에도 박스 붕괴를 기록한다.

### Stage1

기본 전일 확정 일봉240개 종가 MA와240개 일봉 근사 매물대를 쓴다. 근사 프로파일은 Low~High와 겹치는 bin에 각 봉의 거래량을 균등 배분하고 마지막 bin에 잔여 소수를 넣어 총량을 보존한다. 원본 틱 방식은 각 완료봉 거래량과 개별 틱 합계를 대조한다. `70%`는 최대 bin 거래량의70%이며 Value Area가 아니다. 인접 zone의 병합 간격은 ProfileMergeRangePct로, 병합 결과의 전체 폭은 MaxZoneWidthPct로 제한한다. bin 용량을 넘으면 오류를 반환하고 몰래 bin 폭을 늘리지 않는다.

`ZoneWidthPct=100*(high-low)/((high+low)/2)`.
`GapPct=0` if `low<=MA<=high`, otherwise `100*min(abs(MA-low),abs(MA-high))/MA`.
겹침·폭 조건을 통과한 지지 범위는 `[min(MA,zoneLow),max(MA,zoneHigh)]`다.

접근 → 후보 저점L 추적 → `100*(P/L-1)`의 최초 상향 교차에서 Active가 된다. 만료는 `min(signalTime+activationDuration, sessionEntryEnd)`다. 새 접근 에피소드에는 실제 이탈·재접근과 최소 분리 시간이 필요하다. 같은 저점의 등락으로 만료를 계속 밀지 않는다. 활성 중 지지 하단을 낮추어 기존 붕괴를 피하지 않는다. `P<lockedLower*(1-buffer/100)`면 즉시 Invalidated, 활성 취소다. 시간/세션/품질 만료 뒤에는 새 에피소드가 필요하다. 전량 익절이나 가족 전환으로 허가시간을 리셋하지 않는다.

### 다섯 매수 패턴

| 패턴 | 조건과 잠긴 기술적 앵커 |
|---|---|
| DowntrendRebound | 관측 고점대비 하락 ≥max(최소하락,ATR배수*ATRpct) → 후보 저점·안정 → 새 반등 교차. Confirmed는 반등 전 틱들로 잠근 MicroHigh+buffer도 새로 돌파. 앵커=CandidateLow |
| UptrendBreakout | 완료 과거봉의 고점+buffer 상향 교차 → HoldDuration동안 아래 이탈 없이 MinValidTicks → 추격 상한. 앵커=PreBreakoutResistance |
| UptrendPullback | 확정 상승파동 고점대비 최소/최대 조정+확정 EMA 접근 → 저점 안정+새 반등 → 반등 전 미세 고점 돌파. 앵커=PullbackLow |
| RangeLowerBounce | 유효 잠긴 Lower 접근 → 저점 안정 → 새 반등 교차 → Lower기준 추격 상한. 앵커=BoxLower |
| RangeUpperBreakout | 유효 잠긴 Upper+buffer 교차 → 유지시간·체결 수 → 추격 상한. 앵커=BoxUpper. 확정되면 BrokenUp |

새 저점은 안정 시각과 미세 구조를 다시 준비한다. 안정 전에 이미 반등 기준을 넘고 계속 true인 상태를 나중의 새 교차로 인정하지 않는다. 현재 진행봉/신호 틱을 자기 돌파 기준 고점에 포함하지 않는다. MicroHigh의 KnownAt도 남긴다. 초기 돌파 후보는 이전 가격이 기준 아래였다는 실제 교차를 요구한다.

상승 가족은 두 패턴을 동시에 감시하되 기본 눌림 우선, 박스 가족은 유효 상단 돌파 우선이다. PatternPriority는 중복 없는5패턴 순열이다. 아직 committed Range이고 첫 Up 후보를 확인 중인 경우 잠긴 상단 돌파만 예외로 허용한다. 그때 하단 반등이나 UptrendBreakout을 함께 발행하지 않는다.

### 공통 청산·위험·재진입

고정 손절은 `AvgFill*(1-FixedStopPct/100)`, 기술 손절은 `entryAnchor*(1-TechnicalBuffer/100)`다. long의 유효 보호선은 켜진 고정/기술/추적/본전 보호의 최댓값이다. 현재가가 보호선 이하로 확인되면 갭을 포함해 보호 청산을 시도한다. 기술 앵커와 포지션 설정은 진입 당시 고정한다.

TP1/TP2 가격은 실제 평균 매수가 기준이다. 목표 수량은 **동일 초기 매수 주문에서 확인된 누적 수량×비율을 각각 floor**한 정수다.10주/30%/30%면3/3/4다.1주에서 비율을 임의 조정해 가짜 분할을 만들지 않으며, 남은 주식은 보호·세션 정책으로 관리한다. TP1의 첫 실제 부분 매도 체결 이후 추적을 활성화하고 TP1 단계의 완료 여부는 목표 수량 대비 별도로 관리한다. 주문 접수는 TP 체결이 아니다.

`TrailingStop=max(previousStop,Peak*(1-TrailingPct/100))`. 선택한 Peak 기준은 FromEntry 또는 FromTP1Fill이다. 본전 보호 ON은 미배분 매수 비용과 예상 매도 비용을 반영한다. 보호 청산은 TP보다 우선하며 기존 TP를 취소하고 확인 후 남은 수량만 요청한다. 사용자 중지는 신규 진입 차단이며 보유를 임의 시장가로 처분하지 않는다.

완전 청산이 **알려진 시각**부터 `max(MinInterval, reasonCooldown)`을 적용한다. 고정/기술 손절은 앞선 분할익절로 총손익이 양수여도 긴 손절 쿨다운을 유지한다. 이익인 추적 청산은 익절, 손실인 청산은 손절로 분류한다. 손절 후 대기는 익절보다 길어야 한다. 같은 episode·계속 true 조건을 재진입으로 재사용하지 않는다. Switch OFF의 가족 잠금과 손절 쿨다운은 가족을 바꿔 회피할 수 없다.

횟수는 포지션의 **최초 확인 매수 체결**에서 증가한다. 미체결 첫 매수는 확인 횟수에 더하지 않고 한도 자리를 예약한다. 무체결 취소 시 예약을 돌려준다. 종목/가족/계좌별 일중 수·연속 손절·동시 포지션과 미체결 예약·일손실 잠금을 계좌 lock 안에서 검사한다. 일손실은 수수료·평가 손익을 포함하며 회복됐다고 당일 자동 재무장하지 않는다. 미청산/미확인 주문을 새 거래일 카운터 초기화로 없애지 않는다.

`CashBudget=min(PlannedInvestment,MaxOrderCash,verifiedCash-buyReservations,exposureRemaining)`.
`RiskPerShare=worstBuyLimit-effectiveStop+buyFee+estimatedSellFeeTax+safetySlippage`.
현금 수량·위험 수량·호가 사용 한도를 모두 통과해야 한다. 호가 용량이 부족하면 기본은 차단이며 ReduceQuantityToQuoteCapacity를 명시한 경우에만 축소한다. 비용 차감 순보상도 TP 목표로 검사한다. KRW와 USD는 한 현금/노출/손실 금액으로 합치지 않는다.

### 주문·체결

`maxBuyRaw=signalTrade*(1+MaxEntrySlippagePct/100)`, `limit=floorToProvidedTick(maxBuyRaw)`다. Ask가 상한보다 높거나 해당 패턴의 추격 상한을 실제 주문가격이 넘으면 거절한다. 매도도 지정가이며 TP는 목표가 이상, 보호 청산은 원래 의도의 허용 최저가 이상에서만 재견적한다.

신호의 같은 이벤트는 체결하지 않는다. 후속 실제 양방향 호가 또는 엄격히 통과한 체결에 설정 참여율을 적용한다. 같은 가격 체결로 수동적 지정가의 대기열을 채웠다고 가정하지 않는다. 실제 대기열 재현 모델은 아니다. 취소 요청 이후 확인 전까지는 실제 체결 가능성을 추적한다. 만료→취소 요청→확인→잔량 재견적 순서이며 상태 Unknown이면 조회/대사 전 재전송하지 않는다.

broker order ID+execution ID+signal ID를 구분한다. 중복 실행은 한 번만 반영하며 충돌한 ID/수량/가격/상태는 원장 변경 전에 거절한다. 취소 확인 전에 발생한 체결이 늦게 통지되면 실제 수량을 반영하고 미확인 잔량을 Unknown으로 잠근다. 이후 명시 상태조회와 ConfirmReconciled가 필요하다. 실계좌 실행 어댑터와 EmergencyMarketFallback은 비활성이다.

세션 종료 청산은 시도다. 호가·권한·가격 한도 때문에 못 팔면 UnclosedPosition과 대사 필요 상태를 남긴다. 손절 지정가의 실제 체결과 손실 한도를 보장하지 않는다.

## DecisionLog

| 결정 | 선택한 해석과 이유 |
|---|---|
| S00 범위 | 사용자 제한과 기존 두 체계 분리 맥락을 우선했다. 현재 Inje/UI/실제 기록을 보존하고 Bin Core만 추가했다. |
| M01 수학 | EMA의 SMA seed, 이전 종가를 요구하는 Wilder ATR seed, R7 분위수를 명시해 재현 가능한 기준을 고정했다. |
| M02 OHLC 반응 | 접근 봉 다음 봉의 종가 반전을 요구해 한 봉 내부의 High/Low 선후를 추정하지 않는다. |
| M03 프로파일 | 각 봉의 겹치는 bin에 균등 분배, 잔여량 보존.70%는 max bin 기준. 용량 초과는 명시 오류다. |
| M04 미세 고점 | 후보/새 저점 이전의 유효 틱과 충분한 관측 창으로 고정하며, 신호 틱을 포함하지 않는다. |
| M05 유지·교차 | 유효 틱의 연속 관측과 새 상향 교차를 필요로 한다. 시계만 흐르거나 안정 전 기준을 넘은 상태는 매수 증거가 아니다. |
| M06 정수 수량 | TP 비율은 각각 floor. 잔량을 임의로 늘려 팔거나1주를 가짜 분할하지 않는다. |
| M07 TP1 부분체결 | 첫 확인된 TP1 부분체결부터 추적을 켜되 목표 TP 수량의 완료와 분리한다. |
| M08 취소 경합 | 늦게 도착한 실제 체결은 보존하고 잔량은 Unknown으로 대사한다. 과거 취소 상태 때문에 실수량을 버리지 않는다. |
| M09 기간·거래일 | Day24시간과 N거래일 봉을 분리한다. 거래일 봉/휴장·조기폐장·가격 규칙은 공급자 확인 계약이다. |
| M10 설정 수정 | flat/주문 해소 후 새 진입부터 적용한다. 즉시 신규 진입 OFF는 가능하고 보유 보호는 계속 유지한다. 가족 전환 때 이전 가족의 미완성 후보는 폐기한다. |
| M11 재현·통화 | 원본 도착 순서와 시계 간격을 고정하고 전체 상태 감사 해시를 대조한다. 검증 FX 없이 통화 성과를 합치지 않는다. |
| M12 재진입 사유 | 고정/기술 손절의 긴 대기는 앞선 TP 이익으로 줄이지 않는다. 실제 이익인 추적 청산은 익절로 본다. |

## 자동 검사 결과와 명령

최종 Bin **180개 통과**, 원문 A01~E10의 **53개 시나리오 ID 전부 포함**. 초기값/모든 시간 단위 JSON 왕복, 독립 수식 기준값, 미래 자료/진행봉 제외, 네 상태·반응·확인, 선택 지지 허가, 다섯 패턴, 우선순위, 잠금/전환, TP와 보호/쿨다운, KRW/USD, 제한 지정가/부분체결/취소/Unknown/중복/늦은 통지, 원본 재생·가상 운용의 전체 감사 해시를 검사했다.

테스트 자료와 독립 기대값은 `Stock.Bins.Tests/{DataChecks,PatternChecks,ExecutionChecks,EngineChecks,BoundaryChecks,ExtendedChecks}.cs`에 있다. 실행 결과는 `artifacts/bins-tests/results.json`의 항목별 expectation/actual, 전체 재생 근거는 같은 폴더의 `replay-result.json`에 남긴다. 이 산출물은 합성/격리 검사이며 개인 자료가 아니다. 코드에서 생성한 설정과 메타데이터를 아래 표/JSON에 사용했다.

| 묶음 | 자료·예상 결과 | 실제 결과 |
|---|---|---|
| A01~A10 | 미래·진행봉·다른 봉 주기·설정 충돌·2봉 확인·독립 반응·정확한 지표 | 전체 포함·통과 |
| B01~B10 | OFF 우회/ON 부족,30분 허가, 같은/다른 episode, 붕괴·세션·MA240 부족 | 전체 포함·통과 |
| C01~C12 | Fast/Confirmed, 안정 리셋,2초 유지·공백,5패턴·추격·단일 포지션 | 전체 포함·통과 |
| D01~D11 | 전환/당일/보유 잠금,30/30/40,실제 TP,손절/추적/재진입/한도/미청산 | 전체 포함·통과 |
| E01~E10 | 품질·슬리피지·부분/취소/Unknown·통화/호가/DST·정밀도·결정성·실계좌0 | 전체 포함·통과 |

추가 회귀: 기존 Inje363개, Avalonia263개 통과. Avalonia Release와 Windows WPF 교차 빌드는 경고0/오류0이다. Core manifest99파일을 검증한다. **이 Mac에서 실제 Windows UI/EXE 실행을 검증하지 않았다.** UI 소스는 이번 Bin 작업에서 변경하지 않았다.

```sh
dotnet build STOCK.Avalonia.sln -c Release -p:UseSharedCompilation=false
dotnet run --project Stock.Bins.Tests -c Release --no-build -- artifacts/bins-tests
dotnet run --project Stock.Bins.Tests -c Release --no-build -- --validate-guide docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md
dotnet run --project Stock.Portable.Tests -c Release --no-build
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/bins-desktop-regression
dotnet run --project Stock.Portable.Tests -c Release --no-build -- --verify-manifest docs/core-source-manifest.json
dotnet build Stock.sln -c Release -p:EnableWindowsTargeting=true -p:UseSharedCompilation=false
# 기본 로컬 verify.sh/verify.ps1에도 Bin 검사 연결
```

## 실제 지원 한계

1. 실시간 공급자별 원본 틱/호가·확인 세션/거래정지·위험 상태·유효 호가 규칙·날짜별 비용을 실제 자료로 검증하지 않았다. 계약을 충족하지 못하면 진입을 차단한다. 한국·미국 지원은 Core와 합성 통합 검사 기준이다.
2. 단순 일봉/분봉 자료를 틱 정밀도로 재생하지 않는다. CompletedBarsOnly 입력은 UnsupportedPrecision 오류다. 체결 순서·대기열·실제 시장 수익성은 합성 검사로 검증하지 않았다.
3. 후속 작업에서 Bin UI·전체 프로필·백테스트·일반 스크리닝을 연결했다. Bin 실시간 가상 주문과 재시작 복원은 시작을 차단한다. 공급자 및 운영 영속화는 별도 검증이 필요하다.
4. Live 자동 주문, 기록 삭제, GitHub 게시 및 설치 앱 교체는 수행하지 않았다. 원본 프로젝트의 기존 비전략 기능을 변경하는 migration도 추가하지 않았다.

## 전체 파라미터 메타데이터

설명·단위·범위는 Configuration.cs와 FeeRate 타입의 BinParameter에서 생성한다. 금액의 null은 미설정이며 해당 통화 신규 진입을 차단한다. 불리언/선택값 및 조합 제약은 Validate에서 함께 검사한다.

| 경로 | 기본값 | 단위·범위 | 의미 |
|---|---|---|---|
| `enabled` | `true` |  | Global entry enable; disabling never turns off held-position protection |
| `market.enabledExchanges` | `KRX, NASDAQ, NYSE, AMEX` |  | Supported exchanges, no automatic cross-currency aggregation |
| `market.regularSessionOnly` | `true` |  | Extended sessions require explicit provider permissions even when false |
| `market.countryProfiles.krx.maxSpreadPct` | `0.5` | % 0 ~ 100 | Maximum spread |
| `market.countryProfiles.krx.minBestBidQuantity` | `100` | shares 0 ~ 1000000000000000 | Required best bid quantity |
| `market.countryProfiles.krx.minBestAskQuantity` | `100` | shares 0 ~ 1000000000000000 | Required best ask quantity |
| `market.countryProfiles.krx.minBidNotional` | `null` | currency 0 ~ 1000000000000000 | Optional minimum bid notional |
| `market.countryProfiles.krx.minAskNotional` | `null` | currency 0 ~ 1000000000000000 | Optional minimum ask notional |
| `market.countryProfiles.krx.minEntryNetRewardPct` | `0.2` | % 0 ~ 100 | Minimum net reward after round-trip costs |
| `market.countryProfiles.krx.maxOrderCash` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit max order cash; null prevents entries |
| `market.countryProfiles.krx.maxConcurrentExposure` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit exposure cap; null prevents entries |
| `market.countryProfiles.krx.maxDailyLoss` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit daily loss cap; null prevents entries |
| `market.countryProfiles.krx.riskBudgetPerPosition` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit position risk budget; null prevents entries |
| `market.countryProfiles.krx.plannedInvestment` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit planned investment; null prevents entries |
| `market.countryProfiles.krx.fees` | `[]` |  | Dated fee assumptions; empty prevents entries |
| `market.countryProfiles.krx.fees[].buyPercent` | `null` | % 0 ~ 99.999 | Dated buy commission |
| `market.countryProfiles.krx.fees[].sellPercent` | `null` | % 0 ~ 99.999 | Dated sell commission |
| `market.countryProfiles.krx.fees[].sellTaxPercent` | `null` | % 0 ~ 99.999 | Dated market sell tax, including explicit zero assumptions |
| `market.countryProfiles.krx.fees[].perShare` | `null` | currency/share 0 ~ 1000000000000000 | Dated per-share broker cost |
| `market.countryProfiles.us.maxSpreadPct` | `0.5` | % 0 ~ 100 | Maximum spread |
| `market.countryProfiles.us.minBestBidQuantity` | `100` | shares 0 ~ 1000000000000000 | Required best bid quantity |
| `market.countryProfiles.us.minBestAskQuantity` | `100` | shares 0 ~ 1000000000000000 | Required best ask quantity |
| `market.countryProfiles.us.minBidNotional` | `null` | currency 0 ~ 1000000000000000 | Optional minimum bid notional |
| `market.countryProfiles.us.minAskNotional` | `null` | currency 0 ~ 1000000000000000 | Optional minimum ask notional |
| `market.countryProfiles.us.minEntryNetRewardPct` | `0.2` | % 0 ~ 100 | Minimum net reward after round-trip costs |
| `market.countryProfiles.us.maxOrderCash` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit max order cash; null prevents entries |
| `market.countryProfiles.us.maxConcurrentExposure` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit exposure cap; null prevents entries |
| `market.countryProfiles.us.maxDailyLoss` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit daily loss cap; null prevents entries |
| `market.countryProfiles.us.riskBudgetPerPosition` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit position risk budget; null prevents entries |
| `market.countryProfiles.us.plannedInvestment` | `null` | currency 1e-07 ~ 1000000000000000 | Explicit planned investment; null prevents entries |
| `market.countryProfiles.us.fees` | `[]` |  | Dated fee assumptions; empty prevents entries |
| `market.countryProfiles.us.fees[].buyPercent` | `null` | % 0 ~ 99.999 | Dated buy commission |
| `market.countryProfiles.us.fees[].sellPercent` | `null` | % 0 ~ 99.999 | Dated sell commission |
| `market.countryProfiles.us.fees[].sellTaxPercent` | `null` | % 0 ~ 99.999 | Dated market sell tax, including explicit zero assumptions |
| `market.countryProfiles.us.fees[].perShare` | `null` | currency/share 0 ~ 1000000000000000 | Dated per-share broker cost |
| `selection.mode` | `Automatic` |  | Manual family selection or confirmed regime selection |
| `selection.manualFamily` | `Uptrend` |  | Manual mode still requires this family's valid market structure |
| `selection.autoSwitchAfterExit` | `true` |  | Switch only while flat with every previous order resolved |
| `regime.timeframe` | `5 Minute` |  | Classification completed bar size |
| `regime.lookback` | `60 Minute` | duration | Actual classification observation interval |
| `regime.confirmBars` | `2` | bars 1 ~ 10000 | Consecutive new completed bars to commit a regime |
| `regime.minimumBars` | `8` | bars 4 ~ 100000 | Minimum bars inside the observation interval |
| `regime.atrPeriod` | `14` | bars 2 ~ 100000 | Wilder ATR seed/window |
| `regime.erTrend` | `0.4` | ratio 0 ~ 1 | Minimum directional efficiency for trend |
| `regime.erRange` | `0.2` | ratio 0 ~ 1 | Maximum efficiency for range |
| `regime.minTrendMovePct` | `0.6` | % 1e-07 ~ 10000 | Minimum directional net change |
| `regime.atrTrendMultiplier` | `2` | multiple 0 ~ 1000000000000000 | ATR percent multiplier for directional move |
| `regime.structureAtrTolerance` | `0.1` | multiple 0 ~ 1000000000000000 | ATR structure tolerance |
| `regime.rangeUpperQuantile` | `0.85` | ratio 0 ~ 1 | Quantile of completed highs |
| `regime.rangeLowerQuantile` | `0.15` | ratio 0 ~ 1 | Quantile of completed lows |
| `regime.rangeMinTouchesPerSide` | `2` | episodes 2 ~ 100000 | Distinct reactions needed on each box side |
| `regime.rangeTouchSeparation` | `5 Minute` | duration | Minimum separation of independent reactions |
| `regime.rangeReversalMinAtr` | `0.3` | ATR multiple 0 ~ 1000000000000000 | Minimum reversal after approach |
| `regime.rangeTouchTolerancePct` | `0.3` | % 0 ~ 100 | Distance to count a box side approach |
| `regime.rangeMinWidthPct` | `1.5` | % 0 ~ 10000 | Minimum box width |
| `regime.rangeMaxWidthPct` | `12` | % 1e-07 ~ 10000 | Maximum box width |
| `regime.rangeMinInsideRatio` | `0.8` | ratio 0 ~ 1 | Required closes inside candidate box |
| `regime.rangeSnapshotLifetime` | `60 Minute` | duration | Frozen box lifetime |
| `regime.includePreviousSessionForLookback` | `false` |  | Allow real completed bars from prior sessions in observation interval |
| `stage1.enabled` | `false` |  | Optional support permission; never an entry signal |
| `stage1.movingAveragePeriod` | `240` | bars 1 ~ 100000 | Completed support MA bars |
| `stage1.movingAverageBar` | `1 TradingDay` |  | MA bar size, distinct from elapsed days |
| `stage1.profileLookbackBars` | `240` | bars 1 ~ 100000 | Completed profile bars |
| `stage1.profileBar` | `1 TradingDay` |  | Profile bar size |
| `stage1.profileSource` | `ApproximateOHLCV` |  | Modeled OHLCV or genuine historical trades |
| `stage1.profileBinSizeTicks` | `5` | ticks 1 ~ 100000 | Verified tick multiples per profile bin |
| `stage1.profileMaxBins` | `512` | bins 1 ~ 1000000 | Maximum profile bin count; overflow blocks, never silently coarsens |
| `stage1.volumeZoneThresholdPct` | `70` | % 1e-07 ~ 100 | Threshold as percent of MAXIMUM bin volume, not value area |
| `stage1.profileMergeRangePct` | `1` | % 0 ~ 100 | Maximum merge gap and merged region width |
| `stage1.maxOverlapPct` | `0.5` | % 0 ~ 100 | Maximum gap between MA and volume zone |
| `stage1.maxZoneWidthPct` | `2` | % 0 ~ 100 | Maximum volume zone width |
| `stage1.approachPct` | `1` | % 0 ~ 100 | Approach distance around support |
| `stage1.minReboundPct` | `0.5` | % 1e-07 ~ 100 | Fresh low-relative rebound crossing |
| `stage1.candidateLifetime` | `15 Minute` | duration | Support candidate lifetime |
| `stage1.activationDuration` | `30 Minute` | duration | Stage two permission lifetime |
| `stage1.breakBufferPct` | `0.3` | % 0 ~ 100 | Strict below-lower invalidation |
| `stage1.minDistinctEpisodeGap` | `15 Second` | duration | Gap between independent support episodes |
| `stage1.zoneIdentityTolerancePct` | `0.5` | % 0 ~ 100 | Identity tolerance for the same physical support |
| `entries.downtrendRebound.enabled` | `true` |  | Evaluate the downtrend rebound pattern |
| `entries.downtrendRebound.entryMode` | `Confirmed` |  | One mode, not two simultaneous patterns |
| `entries.downtrendRebound.dropLookback` | `10 Minute` | duration | Past observed drop peak interval |
| `entries.downtrendRebound.minDropPct` | `1` | % 1e-07 ~ 100 | Minimum drop |
| `entries.downtrendRebound.dropAtrMultiplier` | `2` | multiple 0 ~ 1000000000000000 | Drop ATR multiplier |
| `entries.downtrendRebound.lowStabilization` | `5 Second` | duration | Valid trade stability after last new low |
| `entries.downtrendRebound.minReboundPct` | `0.4` | % 1e-07 ~ 100 | Fresh rebound crossing |
| `entries.downtrendRebound.microHighLookback` | `10 Second` | duration | Prior micro high observation interval |
| `entries.downtrendRebound.breakBufferPct` | `0.1` | % 0 ~ 100 | Confirmed-mode micro high break buffer |
| `entries.downtrendRebound.candidateLifetime` | `3 Minute` | duration | Drop candidate lifetime |
| `entries.downtrendRebound.maxChasePct` | `1` | % 1e-07 ~ 100 | Maximum chase above candidate low |
| `entries.uptrendBreakout.enabled` | `true` |  | Enable pattern |
| `entries.uptrendBreakout.lookback` | `20 Minute` | duration | Confirmed prior high observation interval |
| `entries.uptrendBreakout.breakoutBufferPct` | `0.1` | % 0 ~ 100 | Breakout buffer |
| `entries.uptrendBreakout.holdDuration` | `2 Second` | duration | Price hold supported by continuous valid trades |
| `entries.uptrendBreakout.minValidTicks` | `2` | ticks 2 ~ 1000000 | Minimum distinct trades during price hold |
| `entries.uptrendBreakout.candidateLifetime` | `2 Minute` | duration | Breakout candidate lifetime |
| `entries.uptrendBreakout.maxChasePct` | `0.5` | % 1e-07 ~ 100 | Maximum chase above locked resistance |
| `entries.uptrendPullback.enabled` | `true` |  | Enable pattern |
| `entries.uptrendPullback.emaPeriod` | `20` | bars 2 ~ 100000 | EMA completed bar count |
| `entries.uptrendPullback.emaBar` | `1 Minute` |  | EMA completed timeframe |
| `entries.uptrendPullback.impulseLookback` | `30 Minute` | duration | Confirmed impulse high interval |
| `entries.uptrendPullback.minPullbackPct` | `0.3` | % 0 ~ 100 | Minimum correction |
| `entries.uptrendPullback.maxPullbackPct` | `2` | % 1e-07 ~ 100 | Maximum correction |
| `entries.uptrendPullback.maxEMADistancePct` | `0.3` | % 0 ~ 100 | Maximum distance to confirmed EMA |
| `entries.uptrendPullback.minReboundPct` | `0.25` | % 1e-07 ~ 100 | Fresh rebound crossing |
| `entries.uptrendPullback.microHighLookback` | `10 Second` | duration | Prior micro high interval |
| `entries.uptrendPullback.microBreakBufferPct` | `0.1` | % 0 ~ 100 | Micro break buffer |
| `entries.uptrendPullback.lowStabilization` | `3 Second` | duration | Stability supported by valid trades |
| `entries.uptrendPullback.candidateLifetime` | `5 Minute` | duration | Candidate lifetime |
| `entries.uptrendPullback.maxChasePct` | `0.8` | % 1e-07 ~ 100 | Maximum chase above pullback low |
| `entries.rangeLowerBounce.enabled` | `true` |  | Enable pattern |
| `entries.rangeLowerBounce.approachPct` | `0.3` | % 0 ~ 100 | Upper distance from box lower |
| `entries.rangeLowerBounce.minReboundPct` | `0.25` | % 1e-07 ~ 100 | Fresh rebound crossing |
| `entries.rangeLowerBounce.breakTolerancePct` | `0.3` | % 0 ~ 100 | Box lower invalidation |
| `entries.rangeLowerBounce.lowStabilization` | `3 Second` | duration | Low stability with valid trades |
| `entries.rangeLowerBounce.candidateLifetime` | `3 Minute` | duration | Candidate lifetime |
| `entries.rangeLowerBounce.maxChasePct` | `1` | % 1e-07 ~ 100 | Maximum chase above box lower |
| `entries.rangeUpperBreakout.enabled` | `true` |  | Enable pattern |
| `entries.rangeUpperBreakout.lookback` | `20 Minute` | duration | Confirmed prior high observation interval |
| `entries.rangeUpperBreakout.breakoutBufferPct` | `0.1` | % 0 ~ 100 | Breakout buffer |
| `entries.rangeUpperBreakout.holdDuration` | `2 Second` | duration | Price hold supported by continuous valid trades |
| `entries.rangeUpperBreakout.minValidTicks` | `2` | ticks 2 ~ 1000000 | Minimum distinct trades during price hold |
| `entries.rangeUpperBreakout.candidateLifetime` | `2 Minute` | duration | Breakout candidate lifetime |
| `entries.rangeUpperBreakout.maxChasePct` | `0.5` | % 1e-07 ~ 100 | Maximum chase above locked resistance |
| `entries.patternPriority` | `RangeUpperBreakout, UptrendPullback, UptrendBreakout, RangeLowerBounce, DowntrendRebound` |  | Priority permutation; one entry winner per symbol/event |
| `sharedExit.mode` | `PartialTakeProfitWithTrailing` |  | Shared exit policy for all five patterns |
| `sharedExit.takeProfit1Pct` | `1` | % 1e-07 ~ 10000 | TP1 above actual average fill |
| `sharedExit.takeProfit1RatioPct` | `30` | % 0 ~ 100 | TP1 fraction of confirmed initial entry quantity |
| `sharedExit.takeProfit2Pct` | `2` | % 1e-07 ~ 10000 | TP2 above actual average fill |
| `sharedExit.takeProfit2RatioPct` | `30` | % 0 ~ 100 | TP2 fraction of confirmed initial entry quantity |
| `sharedExit.trailingStopPct` | `0.7` | % 1e-07 ~ 99.999 | Trailing distance |
| `sharedExit.trailingActivation` | `AfterTP1Filled` |  | Activation requires confirmed TP1 fill |
| `sharedExit.trailingPeakBasis` | `FromEntry` |  | Peak basis after trailing activation |
| `sharedExit.fixedStopEnabled` | `true` |  | Use common fixed stop |
| `sharedExit.fixedStopLossPct` | `1` | % 1e-07 ~ 99.999 | Positive fixed stop distance |
| `sharedExit.technicalStopEnabled` | `true` |  | Use immutable pattern-specific technical anchor |
| `sharedExit.technicalBreakBufferPct` | `0.3` | % 0 ~ 99.999 | Technical anchor buffer |
| `sharedExit.breakEvenAfterTP1` | `false` |  | Cost-aware break even after confirmed TP1 |
| `sharedExit.exitSignalEvaluation` | `OnTick` |  | Protection evaluated on real trade ticks |
| `sharedExit.closePositionsAtSessionEnd` | `true` |  | Attempt liquidation with limits, never assume a close fill |
| `sharedExit.stopNewEntriesBeforeClose` | `10 Minute` | duration | New entry cutoff before verified session close |
| `sharedExit.startLiquidationBeforeClose` | `5 Minute` | duration | Attempt liquidation before verified session close |
| `sharedReentry.minInterval` | `60 Second` | duration | Minimum time after full close confirmation |
| `sharedReentry.afterProfit` | `60 Second` | duration | Profit cooldown after full close |
| `sharedReentry.afterStop` | `5 Minute` | duration | Loss/stop cooldown, strictly longer than profit |
| `sharedRisk.maxConcurrentPositionsPerSymbol` | `1` | positions 1 ~ 1 | Fixed single-position invariant |
| `sharedRisk.allowPyramiding` | `false` |  | Fixed no-pyramiding invariant |
| `sharedRisk.maxEntriesPerSymbolPerDay` | `10` | entries 1 ~ 1000000 | Confirmed daily entry positions per symbol |
| `sharedRisk.maxEntriesPerFamilyPerDay` | `50` | entries 1 ~ 1000000 | Confirmed daily entry positions per family/account |
| `sharedRisk.maxEntriesPerAccountPerDay` | `100` | entries 1 ~ 1000000 | Confirmed daily account entries |
| `sharedRisk.maxConcurrentPositionsPerAccount` | `5` | positions 1 ~ 100000 | Holdings plus first-entry reservations |
| `sharedRisk.maxConsecutiveStops` | `3` | stops 1 ~ 100000 | Consecutive closed stops/losses |
| `quality.maxTradeAge` | `2 Second` | duration | Trade exchange-time freshness |
| `quality.maxQuoteAge` | `1 Second` | duration | Quote exchange-time freshness |
| `quality.maximumProcessingLag` | `2 Second` | duration | Receive-to-process maximum lag |
| `quality.maximumTradeGap` | `2 Second` | duration | Maximum gap of valid trades supporting hold/stability |
| `quality.requireContinuousTrades` | `true` |  | Require continuous trade evidence |
| `quality.requireVerifiedExchangeTime` | `true` |  | Require verified exchange timestamps |
| `quality.requireTradableInstrument` | `true` |  | Require verified listing/halt/risk status |
| `quality.maximumHistoryBars` | `20000` | bars 32 ~ 1000000 | Explicit memory bound for warmup/indicators |
| `quality.maximumHistoryTicks` | `200000` | ticks 32 ~ 2000000 | Explicit memory bound for observation ticks |
| `execution.entryOrderKind` | `Limit` |  | Capped limit entry only |
| `execution.maxEntrySlippagePct` | `0.2` | % 0 ~ 100 | Max entry slippage from signal trade |
| `execution.entryTimeout` | `3 Second` | duration | Cancel remainder after entry timeout |
| `execution.exitOrderKind` | `Limit` |  | Limit exit, no implicit market conversion |
| `execution.exitLimitSlippagePct` | `0.3` | % 0 ~ 99.999 | Lowest permitted exit quote |
| `execution.protectiveExitTimeout` | `2 Second` | duration | Cancel, confirm, then requote remaining exit quantity |
| `execution.maxEntryQuoteParticipationPct` | `25` | % 1e-07 ~ 100 | Maximum usable fraction of observed ask depth |
| `execution.reduceQuantityToQuoteCapacity` | `false` |  | Explicit optional capacity reduction; default insufficient capacity BLOCKS |
| `execution.emergencyMarketFallback` | `false` |  | Fallback remains unsupported/disabled without separate authorization |
| `execution.allowUnverifiedLiveExecution` | `false` |  | Always false |
| `execution.submissionLatency` | `0.1 Second` | duration | Simulator submission latency |
| `execution.cancellationLatency` | `0.1 Second` | duration | Simulator cancellation acknowledgement latency |
| `execution.simulationParticipationPct` | `10` | % 1e-07 ~ 100 | Conservative shared event liquidity participation |

## 불러오기용 전체 JSON

아래는 `BinsJson.Export(new BinsStrategyConfig())`의 전체 출력이다. 축약 스키마가 아니다. 정확한 이름/enum/필수 필드와 모든 설정을 보존한다. 불명확한 키·중복 키·누락 키·다른 전략 ID/버전은 거절한다. 금액과 비용표가 비어 있는 기본값이므로 그대로 실행하면 신규 매수는 차단된다. 이 JSON은 Bin Core의 `BinsJson.Import`용이며 현재 Inje UI 불러오기에 넣는 파일은 아니다.

```json
{
  "schemaVersion": 1,
  "strategyId": "bins-strategy-v1",
  "strategyName": "Bin\u0027s Strategy",
  "enabled": true,
  "market": {
    "enabledExchanges": [
      "KRX",
      "NASDAQ",
      "NYSE",
      "AMEX"
    ],
    "regularSessionOnly": true,
    "countryProfiles": {
      "krx": {
        "maxSpreadPct": 0.5,
        "minBestBidQuantity": 100,
        "minBestAskQuantity": 100,
        "minBidNotional": null,
        "minAskNotional": null,
        "minEntryNetRewardPct": 0.2,
        "maxOrderCash": null,
        "maxConcurrentExposure": null,
        "maxDailyLoss": null,
        "riskBudgetPerPosition": null,
        "plannedInvestment": null,
        "fees": []
      },
      "us": {
        "maxSpreadPct": 0.5,
        "minBestBidQuantity": 100,
        "minBestAskQuantity": 100,
        "minBidNotional": null,
        "minAskNotional": null,
        "minEntryNetRewardPct": 0.2,
        "maxOrderCash": null,
        "maxConcurrentExposure": null,
        "maxDailyLoss": null,
        "riskBudgetPerPosition": null,
        "plannedInvestment": null,
        "fees": []
      }
    }
  },
  "selection": {
    "mode": "Automatic",
    "manualFamily": "Uptrend",
    "autoSwitchAfterExit": true
  },
  "regime": {
    "timeframe": {
      "value": 5,
      "unit": "Minute"
    },
    "lookback": {
      "value": 60,
      "unit": "Minute"
    },
    "confirmBars": 2,
    "minimumBars": 8,
    "atrPeriod": 14,
    "erTrend": 0.4,
    "erRange": 0.2,
    "minTrendMovePct": 0.6,
    "atrTrendMultiplier": 2,
    "structureAtrTolerance": 0.1,
    "rangeUpperQuantile": 0.85,
    "rangeLowerQuantile": 0.15,
    "rangeMinTouchesPerSide": 2,
    "rangeTouchSeparation": {
      "value": 5,
      "unit": "Minute"
    },
    "rangeReversalMinAtr": 0.3,
    "rangeTouchTolerancePct": 0.3,
    "rangeMinWidthPct": 1.5,
    "rangeMaxWidthPct": 12,
    "rangeMinInsideRatio": 0.8,
    "rangeSnapshotLifetime": {
      "value": 60,
      "unit": "Minute"
    },
    "includePreviousSessionForLookback": false
  },
  "stage1": {
    "enabled": false,
    "movingAveragePeriod": 240,
    "movingAverageBar": {
      "value": 1,
      "unit": "TradingDay"
    },
    "profileLookbackBars": 240,
    "profileBar": {
      "value": 1,
      "unit": "TradingDay"
    },
    "profileSource": "ApproximateOHLCV",
    "profileBinSizeTicks": 5,
    "profileMaxBins": 512,
    "volumeZoneThresholdPct": 70,
    "profileMergeRangePct": 1,
    "maxOverlapPct": 0.5,
    "maxZoneWidthPct": 2,
    "approachPct": 1,
    "minReboundPct": 0.5,
    "candidateLifetime": {
      "value": 15,
      "unit": "Minute"
    },
    "activationDuration": {
      "value": 30,
      "unit": "Minute"
    },
    "breakBufferPct": 0.3,
    "minDistinctEpisodeGap": {
      "value": 15,
      "unit": "Second"
    },
    "zoneIdentityTolerancePct": 0.5
  },
  "entries": {
    "downtrendRebound": {
      "enabled": true,
      "entryMode": "Confirmed",
      "dropLookback": {
        "value": 10,
        "unit": "Minute"
      },
      "minDropPct": 1,
      "dropAtrMultiplier": 2,
      "lowStabilization": {
        "value": 5,
        "unit": "Second"
      },
      "minReboundPct": 0.4,
      "microHighLookback": {
        "value": 10,
        "unit": "Second"
      },
      "breakBufferPct": 0.1,
      "candidateLifetime": {
        "value": 3,
        "unit": "Minute"
      },
      "maxChasePct": 1
    },
    "uptrendBreakout": {
      "enabled": true,
      "lookback": {
        "value": 20,
        "unit": "Minute"
      },
      "breakoutBufferPct": 0.1,
      "holdDuration": {
        "value": 2,
        "unit": "Second"
      },
      "minValidTicks": 2,
      "candidateLifetime": {
        "value": 2,
        "unit": "Minute"
      },
      "maxChasePct": 0.5
    },
    "uptrendPullback": {
      "enabled": true,
      "emaPeriod": 20,
      "emaBar": {
        "value": 1,
        "unit": "Minute"
      },
      "impulseLookback": {
        "value": 30,
        "unit": "Minute"
      },
      "minPullbackPct": 0.3,
      "maxPullbackPct": 2,
      "maxEMADistancePct": 0.3,
      "minReboundPct": 0.25,
      "microHighLookback": {
        "value": 10,
        "unit": "Second"
      },
      "microBreakBufferPct": 0.1,
      "lowStabilization": {
        "value": 3,
        "unit": "Second"
      },
      "candidateLifetime": {
        "value": 5,
        "unit": "Minute"
      },
      "maxChasePct": 0.8
    },
    "rangeLowerBounce": {
      "enabled": true,
      "approachPct": 0.3,
      "minReboundPct": 0.25,
      "breakTolerancePct": 0.3,
      "lowStabilization": {
        "value": 3,
        "unit": "Second"
      },
      "candidateLifetime": {
        "value": 3,
        "unit": "Minute"
      },
      "maxChasePct": 1
    },
    "rangeUpperBreakout": {
      "enabled": true,
      "lookback": {
        "value": 20,
        "unit": "Minute"
      },
      "breakoutBufferPct": 0.1,
      "holdDuration": {
        "value": 2,
        "unit": "Second"
      },
      "minValidTicks": 2,
      "candidateLifetime": {
        "value": 2,
        "unit": "Minute"
      },
      "maxChasePct": 0.5
    },
    "patternPriority": [
      "RangeUpperBreakout",
      "UptrendPullback",
      "UptrendBreakout",
      "RangeLowerBounce",
      "DowntrendRebound"
    ]
  },
  "sharedExit": {
    "mode": "PartialTakeProfitWithTrailing",
    "takeProfit1Pct": 1,
    "takeProfit1RatioPct": 30,
    "takeProfit2Pct": 2,
    "takeProfit2RatioPct": 30,
    "trailingStopPct": 0.7,
    "trailingActivation": "AfterTP1Filled",
    "trailingPeakBasis": "FromEntry",
    "fixedStopEnabled": true,
    "fixedStopLossPct": 1,
    "technicalStopEnabled": true,
    "technicalBreakBufferPct": 0.3,
    "breakEvenAfterTP1": false,
    "exitSignalEvaluation": "OnTick",
    "closePositionsAtSessionEnd": true,
    "stopNewEntriesBeforeClose": {
      "value": 10,
      "unit": "Minute"
    },
    "startLiquidationBeforeClose": {
      "value": 5,
      "unit": "Minute"
    }
  },
  "sharedReentry": {
    "minInterval": {
      "value": 60,
      "unit": "Second"
    },
    "afterProfit": {
      "value": 60,
      "unit": "Second"
    },
    "afterStop": {
      "value": 5,
      "unit": "Minute"
    }
  },
  "sharedRisk": {
    "maxConcurrentPositionsPerSymbol": 1,
    "allowPyramiding": false,
    "maxEntriesPerSymbolPerDay": 10,
    "maxEntriesPerFamilyPerDay": 50,
    "maxEntriesPerAccountPerDay": 100,
    "maxConcurrentPositionsPerAccount": 5,
    "maxConsecutiveStops": 3
  },
  "quality": {
    "maxTradeAge": {
      "value": 2,
      "unit": "Second"
    },
    "maxQuoteAge": {
      "value": 1,
      "unit": "Second"
    },
    "maximumProcessingLag": {
      "value": 2,
      "unit": "Second"
    },
    "maximumTradeGap": {
      "value": 2,
      "unit": "Second"
    },
    "requireContinuousTrades": true,
    "requireVerifiedExchangeTime": true,
    "requireTradableInstrument": true,
    "maximumHistoryBars": 20000,
    "maximumHistoryTicks": 200000
  },
  "execution": {
    "entryOrderKind": "Limit",
    "maxEntrySlippagePct": 0.2,
    "entryTimeout": {
      "value": 3,
      "unit": "Second"
    },
    "exitOrderKind": "Limit",
    "exitLimitSlippagePct": 0.3,
    "protectiveExitTimeout": {
      "value": 2,
      "unit": "Second"
    },
    "maxEntryQuoteParticipationPct": 25,
    "reduceQuantityToQuoteCapacity": false,
    "emergencyMarketFallback": false,
    "allowUnverifiedLiveExecution": false,
    "submissionLatency": {
      "value": 0.1,
      "unit": "Second"
    },
    "cancellationLatency": {
      "value": 0.1,
      "unit": "Second"
    },
    "simulationParticipationPct": 10
  }
}
```
