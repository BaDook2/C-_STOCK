# 코덱스 개발 요청서 — 범용 당일 자동매매 엔진 + 전략 D

## 0. 작업 목적과 원칙

첨부된 `전략_상세설명서.md`(2026-11-07 기준)를 먼저 읽고 실제 저장소의 구현과 대조한 뒤 작업한다. 문서는 당시 코드 설명이지 새 기능의 구현 완료 증명이 아니다. 기존 프로젝트는 WPF/C# 기반 주식 스크리닝·백테스트·가상매매·실계좌 실행 앱이다.

**최종 목표는 두 기능을 분리하여 동시에 지원하는 것이다.**

1. **범용 당일 자동매매 엔진:** 사용자가 틱·초봉·분봉·일봉과 지표별 기간, 평가 주기, 진입·청산·재진입 정책을 조정할 수 있고, 동일 종목을 하루 여러 번 거래할 수 있는 공통 실행 기반.
2. **전략 D:** `최근 며칠간 급락 OR 당일 장중 급락` + `일봉 MA240 부근` + `거래량 매물대 중첩`을 필수로 확인하고, 지지 접근 후 **실시간 반등을 확인하여 매수**하는 전략. 매도는 사용자 선택 익절·손절 정책과 당일 청산 정책에 따른다.

**기존의 `krx-oversold-rebound`와 61개 설정, 프리셋, 과거 백테스트를 훼손하거나 의미를 몰래 변경하지 말 것.** 새 동작은 별도의 버전/전략 ID/설정 필드로 도입하고, 구 설정을 불러오면 이전 방식으로 동작하도록 보장한다. 이전 `백테스트 검증 1 · 1일 보유` JSON은 실제 거래 전략이 아니라 검사 스냅샷이다. 수익성 검증값으로 쓰거나 실계좌의 안전한 기본 프리셋으로 지정하지 말 것.

코드 분석만으로 확정할 수 없는 증권사 API 기능·과거 틱 보관기간·거래시간·체결 규칙은 추측하지 말고 지원 여부와 제약을 보고하라. 실제 계좌 주문은 검증 완료 및 사용자의 명시적 활성화 전까지 절대 자동 실행하지 말 것.

## 1. 기존 코드 조사 및 보존

우선 아래 파일·의존 관계와 실제 메서드 이름을 조사하고, **현행 동작 / 변경 필요 / 재사용 지점 / 위험**을 표로 보고한 뒤 구현하라. 목록은 설명서의 경로를 출발점으로 사용하되 저장소에서 실재 여부를 확인한다.

- `Stock.Core/Research/Oversold/OversoldConfig.cs`, `OversoldParameterMetadata.cs`, `OversoldParameterLayout.cs`
- `SupportProviders.cs`, `OversoldEntryDetector.cs`, `OversoldPositionRules.cs`, `OversoldLiveStrategy.cs`
- `OversoldBacktest.cs`, `OversoldMinuteBacktest.cs`, `OversoldResults.cs`, `OversoldBacktestService.cs`
- `CurrentOversoldScreening.cs`, `OversoldScreening.cs`, `RealtimeOversoldScreening.cs`, `ScreeningCandidatePolicy.cs`
- `BarSpecification.cs`, `HistoricalFacts.cs`, `RealtimeWarmup.cs`
- `LiveStrategyBatch.cs`, `RealtimePaperStrategyBatch.cs`, `LiveBatchModels.cs`
- `MultiTradeWorkspace.cs`, `PublicPaperWorkspace.cs`, `RealtimeScreeningWorkspace.cs`
- `StrategyDiagnosticPreset.cs`, `OversoldPresetExport.cs`

기존 WPF/MVVM/DI 방식, 종목 검색, 차트, 실시간 스크리닝, 백테스트 팝업, 다중매매, 중지·긴급중지 UI를 가능한 한 유지한다. 별도 프로젝트를 불필요하게 새로 만들지 말고 공통 서비스와 전략별 구현을 분리한다.

## 2. 공통 아키텍처 — 데이터/전략/실행을 분리

권장 경계(정확한 클래스명은 저장소 구조에 맞게 정해도 됨):

- **MarketData:** 거래소/증권사 원본 틱, 체결량, 가능하면 최우선 매수·매도 호가, 원본 시각, 수신 시각, 순서/중복 여부, 세션 정보. 데이터 출처·신선도·결손 여부 포함.
- **BarAggregator:** 원본 이벤트로 1초·5초·N초·1분·N분·일봉을 생성; 진행봉 업데이트와 확정봉 이벤트를 구분한다. 원본이 없던 구간을 임의 체결로 만들지 않는다.
- **IndicatorEngine:** 개별 지표마다 `Timeframe(Unit, Size)`와 lookback 지정. 캐시/증분 계산을 사용하고 현재 시점까지 관측 가능한 값만 반환한다.
- **StrategySignalEngine:** 전략 D를 포함한 전략별 후보·반등·신호 생성. 매수 신호 자체에는 주문 실행을 넣지 않는다.
- **PositionAndRiskEngine:** 실제 체결 기준 평균단가·잔량, 고정 손절·지지 이탈·추적손절·익절, 중복 주문 방지, 손실 제한, 재진입, 장 마감 청산.
- **ExecutionAdapter:** 동일 주문 의도에 대해 백테스트 모의체결/실시간 가상체결/증권사 실주문을 각각 처리한다. 주문 접수와 체결 완료를 구분한다.
- **ReplayEngine:** 과거 원본 이벤트를 시간순으로 주입한다. 실시간과 **동일한 지표·신호·포지션 판단 코드**를 호출하고 체결 어댑터만 다르게 한다.

`스크리닝 후보 → 실제 진입 신호 → 주문 접수 → 부분/전체 체결 → 보유/청산`을 별도 상태로 기록한다. 화면을 닫거나 탭을 옮겨도 실행 서비스는 유지하되, 앱 종료·재시작 시 미체결 주문과 실계좌 보유수량은 증권사와 재동기화하기 전까지 새 진입을 차단한다.

## 3. 공통 기간 및 평가 주기

1. 설정 기간은 `숫자 + 단위`로 표현한다. 단위: 거래 틱(체결 이벤트), 초, 분, 일/거래일. 필요하면 N틱 집계봉을 별도 개념으로 지원한다. **1틱은 1초와 같지 않다.**
2. 급락·RSI·거래량·MA·매물대 등은 각각 독립된 분석 시간 기준을 가질 수 있다. 단일 기준봉을 모든 지표에 적용하는 **간편 모드**와 지표별 단위를 선택하는 **고급 모드**를 제공한다.
3. 신호 평가 정책: `OnTick`(새 체결마다), `OnActiveBarUpdate`(진행봉 갱신 시), `OnBarClosed`(봉 확정 시). **전략 D의 실시간 기본 평가 방식은 OnTick**이다.
4. 진행봉을 사용하는 지표는 잠정값이라는 사실을 기록하고, 확정된 과거 봉과 분리한다. 이미 확정된 신호를 미래 가격으로 수정하지 않는다.
5. 보유 중 위험 감시의 평가 주기는 진입 신호 주기와 별도다. 예: 5분봉 RSI를 쓰면서 손절은 틱마다 확인.
6. 당일 고점·누적량은 선택한 거래 세션 기준으로 초기화하고, 장 구분/휴장/특수장 및 분·초봉 경계를 명시적으로 다룬다. 시간대와 거래 세션은 시장 설정에 따른다.
7. 데이터 결손·오래된 시세·순서 뒤바뀜·중복 이벤트는 식별하여 기록하고, 엄격한 실거래 진입은 차단한다. 미확인 데이터와 0값을 혼동하지 않는다.

## 4. 전략 D — 진입 후보의 필수 조건

전략 D는 기존 과매도 전략을 삭제하지 않고 별도 전략/프리셋으로 추가한다. **전략 D의 필수 구조는 `(급락) AND (일봉 장기 MA) AND (매물대 중첩) AND (지지 접근) AND (새 반등 신호)`**이다. RSI·거래량 증가 등의 부가 필터는 사용자가 ON/OFF한다.

### 4.1 최근 며칠간 급락 (DailyCrash)

현재 시점 `t`의 실제 관측 가격 `P(t)`와, 현재까지 알려진 최근 N거래일 최고가 `H_N(t)`로 계산:

`DrawdownDaily(N,t) = 100 * (P(t) / H_N(t) - 1)`

- `H_N(t)`: 직전 N-1개 **확정 일봉 High**와 오늘 t시점까지의 **관측 High**를 합한 최근 N거래일 최고가(장중에는 미래 당일 고가 절대 사용 금지).
- 중기 N=20, 기준 -20%; 단기 N=3, 기준 -15%는 **초기 실험값**이며 전부 수정 가능.
- 중기/단기는 개별 ON/OFF, OR/AND 조합 가능. 부족한 일봉은 신호 불가로 처리.

### 4.2 당일 장중 급락 (IntradayCrash)

`DrawdownSession(t) = 100 * (P(t) / SessionHighSoFar(t) - 1)`

`DrawdownRolling(M,t) = 100 * (P(t) / RollingHigh(M minutes,t) - 1)`

- 당일 최고가 및 최근 M분 최고가는 **지금까지 수신한 유효 체결만** 반영한다.
- 당일 고점 -4%, 최근 30분 고점 -2%는 검증 전 실험값. 각 룰 ON/OFF·기간·낙폭 변경 가능.
- `DailyCrash`와 `IntradayCrash`의 기본 조합은 OR, 사용자가 AND/OR와 각 유형 ON/OFF를 선택할 수 있다.
- 급락 조건이 반등하면서 사라지더라도 지정된 후보 유효시간 동안 **급락 발생 이벤트 snapshot**을 유지할 수 있게 한다. 이후 완전히 새로운 진입 평가를 할 때에는 유효시간·현재 위험·지지 상태를 다시 확인한다.

### 4.3 실제 일봉 MA240

`MA240(t) = (직전 240개 확정 거래일 종가 합) / 240`

- 기본은 당일 진행 일봉을 제외한 **전일 확정 MA240**이다. 즉 실시간 1분봉을 선택하더라도 '240분선'으로 변하지 않는다.
- 사용자는 MA 기간(예: 120/240), 시간 기준, 현재 진행 일봉 포함 여부를 고급 옵션으로 변경할 수 있지만, **전략 D 기본 프리셋은 일봉 MA240**이다.
- 과거 시점의 액면분할·수정주가 처리 기준을 일관되게 적용하고, 그 시점에 알 수 없었던 수정 데이터를 사용해 미래정보가 유입되지 않도록 검증한다.

### 4.4 매물대(Volume Profile)

- 현재 `ApproximateVolumeZone`(OHLC Low~High 균등 배분)은 **근사 자료 모드**로 유지한다.
- 과거/실시간 가격별 체결량 확보 시 `TradeVolumeProfile` 모드를 추가한다. 각 가격 bin의 누적량은 `VP(bin)=sum(해당 bin 체결량)`으로 계산한다.
- 매물대 기간·기준 시간봉/거래일·bin 폭·최대 bin 개수·선택 임계치·병합거리 설정 가능. 기존 기본 `최대 bin 거래량의 70% 이상`은 유지 가능한 실험 출발점이며, 전체 거래량 70%의 Value Area로 오해하지 말 것.
- 거래소/종목 호가 단위와 통화에 맞게 bin을 처리한다. 실시간 중 필요한 히스토리를 제공할 수 없는 경우 조용히 원본 매물대로 둔갑시키지 말고 `정밀/근사/자료부족` 표시; 엄격 모드에서 필요한 정밀 자료가 없으면 신호 차단.
- 매 틱마다 전체 과거 틱을 다시 순회하지 말고 증분 누적 및 롤링 윈도 제거, 캐시 정책을 사용한다.

### 4.5 MA와 매물대의 중첩 판단

매물대 범위 `[ZoneLow, ZoneHigh]`와 `MA` 사이의 최소 상대거리 `Gap`을 구한다.

- `ZoneLow <= MA <= ZoneHigh`이면 `Gap=0`
- `MA < ZoneLow`이면 `Gap=(ZoneLow-MA)/MA * 100`
- `MA > ZoneHigh`이면 `Gap=(MA-ZoneHigh)/MA * 100`
- `Gap <= MaxOverlapDistancePercent`인 매물대만 인정.
- 매물대 폭 `100*(ZoneHigh-ZoneLow)/ZoneCenter <= MaxZoneWidthPercent`도 별도 검사.
- 0.5% 중첩 거리, 2% 매물대 폭은 **검증 전 실험값**이며 사용자 변경 가능.
- `MA120+MA240`처럼 같은 종류의 지지 2개만으로 통과시키지 않는다. 전략 D는 **MA 1개 이상 + 유효 VolumeZone 1개 이상**을 필수로 한다. 일반 `SupportCount`는 보조 표시로만 사용.
- 지지 구간은 진입 시점의 `MA`, `ZoneLow/High`, overlap 판정, 데이터 출처를 snapshot으로 저장한다. 매물대 갱신에 따라 같은 물리적 구간의 ID가 자주 바뀌어 후보가 무의미하게 초기화되지 않게 안정적인 Zone 식별 규칙을 구현한다.

### 4.6 지지 접근 → 저점 추적 → 새 반등 신호

- 현재 가격이 MA/유효 중첩 지지 구간에서 설정한 접근 거리 안에 도달하면 `WatchingRebound` 상태 진입(아직 매수 금지).
- 접근 순간의 **실제 첫 틱 가격**을 초기 저점으로 저장하고, 이후 들어오는 유효 틱의 최저가로 갱신한다. 기존의 '최초 접근 봉 Close로 초기화' 동작과 혼동하지 않는다.
- `ReboundPercent(t)=100*(P(t)/LowestSinceApproach-1)`.
- 기본 매수 정책: `ReboundPercent >= 설정 임계치`를 **처음 상향 통과**하는 새로운 이벤트에서만 신호를 생성. 0.5%는 검증 전 출발값.
- 선택 정책으로 `단기 고점 돌파` 등을 추가할 수 있지만, 미구현 상태에서 UI만 노출하지 않는다.
- 반등이 발생해도 현재가가 설정한 `MaxEntryDistanceFromSupportPercent`를 벗어나면 추격 매수 차단.
- 후보 유효시간 만료, 필수 지지 근거 소멸, 데이터 결손, 장 종료, 위험 상태 전환은 신규 매수를 차단하고 이유를 기록.
- 신호마다 `symbol+strategyInstance+candidateEpisode+signalSequence` 등 안정적인 식별자를 두어 같은 반등에 중복 주문이 나가지 않도록 한다.

## 5. 범용 진입·재진입 정책

- 실시간 스크리닝은 기존의 종목군/관심종목과 10·25·50·100개 선택 기능을 유지한다. 후보 표시(근접 후보 포함)는 실제 매수 승인을 의미하지 않는다.
- 재진입 모드: `NewLowerZoneOnly`(레거시), `SameZoneWithFreshSignal`, `NewZoneAnyDirection`(선택), `Disabled`.
- **전략 D 기본은 `SameZoneWithFreshSignal`**. 예: 09:10 매수→09:15 전량 청산→09:30 유효한 **새** 반등 신호라면 같은 MA/매물대에서도 재매수 가능.
- `MinReentryInterval`(초/분/봉), 익절 직후와 손절 직후 별도 대기시간, 일일 종목별/전략별/계좌별 진입 횟수 제한을 선택할 수 있게 한다.
- 단순히 이전 `EntryCondition=true`가 계속 유지된다는 이유로 반복 주문하면 안 된다. 청산 뒤 새 후보 episode/새로운 반등 발생을 요구한다.
- 종목·전략별 동시 포지션 수 제한을 기본 1로 하고, 미체결 매수 주문이 존재하면 재진입 차단. 실계좌 실보유와 전략 소유 포지션의 차이를 관리한다.
- 기존 분할매수(`MaxEntries`, `EntryRatios` 등)는 유지하되, 전략 D 초기 실험 프리셋에서는 OFF(1회 진입)로 시작. ON인 경우 각 추가매수가 전체 손실 예산을 넘지 않도록 사전 검증.

## 6. 범용 손절·익절·포지션 관리

### 6.1 독립 ON/OFF 손절 3종

1. **지지 이탈 손절:** 진입 순간 저장한 유효 매물대 하단 `EntryZoneLow` 기준 `SupportStop=EntryZoneLow*(1-SupportBreakBufferPercent/100)`. 기본은 틱 실시간 감시. 향후 최신 Zone이 아래로 이동했다고 기존 손절선을 자동 하향 조정하지 않는다.
2. **고정 비상손절:** 실제 평균 매수 체결가격 `AverageFillPrice` 기준 `FixedStop=AverageFillPrice*(1-FixedStopLossPercent/100)`.
3. **추적손절:** 활성화된 기간의 최고 관측 체결가 `PeakPrice`에서 `TrailingStop=PeakPrice*(1-TrailingStopPercent/100)`; Peak 상승에만 손절선 상승, 하락에 따라 낮아지지 않음. 시작 시점 `AfterEntry/AfterTP1`, 최고가 초기화 정책 `FromEntry/ResetAtTP1` 별도 선택.

세 기능은 별도 ON/OFF 및 비율을 가진다. 동시에 켠 경우 **먼저 발동한 보호 조건으로 청산 의도 한 건만** 만든다. 하나의 틱에서 조건이 동시에 충족하면 위험 관리 우선순위로 원인을 결정하고, 다른 조건도 진단 기록에 보관한다. 실시간 호가·체결 시점에 손절가격 그대로 거래된다고 가정하지 않는다. 전량청산 주문 접수 중 중복 전량청산 주문을 만들지 않는다.

### 6.2 익절 모드 및 조합

- `FullTakeProfit`: 설정 수익률에서 전량 익절.
- `PartialTakeProfit`: TP1/TP2 각각 목표 수익률·매도 비율 설정. 남은 수량은 선택한 보호/추적손절 규칙으로 관리.
- `TrailingOnly`: 고정 목표 익절을 사용하지 않고 활성화된 추적손절로 청산. 이 모드에서는 TP1 이후 활성화만 허용하는 설정은 금지하거나 `AfterEntry`로 명시적 변환.
- 분할익절 + 추적손절 동시 사용 허용. TP 비율의 기준(최대 보유수량/초기 보유수량/남은 수량)을 명시하고, 기존 레거시 의미는 유지. 소수 주식 수량/정수 내림 때문에 TP 매도수량이 0이면 허위 체결/허위 단계 완료를 기록하지 않는다.
- 선택형 `BreakEvenStop`: TP1 체결 이후 잔량에 대해 취득 수수료 및 예상 매도 비용을 고려한 손익분기 가격으로 손절선 상향. 기존 손절선보다 불리한 방향으로 내리지 않는다.
- 손절, 강제청산, 익절, 추가매수가 같은 평가 시점에 겹치면 **위험청산 > 정해진 익절 단계 > 추가매수** 순으로 처리한다. 같은 틱에서 복수 청산 주문 중복 전송 금지.
- 손절률, 익절률, 추적손절률, 배분 비율은 **모두 사용자 설정값**이다. 제시된 0.5%·1% 등의 값은 검증되지 않은 실험용 예시이며 코드 상수/수익보장 문구로 취급하지 말 것.

### 6.3 범용 계좌 위험 제한

- 계좌/전략/종목별 최대 주문 금액, 동시 총 노출, 하루 최대 실현+미실현 손실, 일일 최대 진입 횟수, 연속 손절 후 진입 중단, 비정상 체결/통신 장애 시 신규주문 중단을 지원한다.
- 주문 수량은 예산·현금뿐 아니라 설정한 `RiskBudget`과 손절선 간 거리도 고려해 상한을 둔다. 손절 슬리피지로 실제 손실이 상한을 초과할 수 있음을 명확히 표시한다.
- 비상중지는 새 진입 중단/미체결 취소/기존 보유 청산을 구분해 안전하고 명확하게 정의한다. 사용자가 청산을 명시하지 않았는데 무조건 보유분을 시장가 매도하지 않는다.

## 7. 당일 청산 정책 (범용 엔진 기능)

- `DayOnly=true`일 때 날짜를 넘어 보유하지 않도록 **당일 청산을 시도하는 독립 실행 서비스**를 둔다. `MaxHoldingDays=1`은 기존 구현상 다음 날 시가 매도가 가능하므로 절대 대용하지 않는다.
- `NoNewEntryAfter`, `LiquidationStartAt`, `MarketSession`, `SessionEnd`를 시장·계좌별로 설정한다. 시각의 실제 유효 범위와 주문 가능 호가/거래 세션은 증권사·거래소 지원에 따라 확인해야 하며 예시 시간값을 무조건 강제하지 않는다.
- 신규매수 차단 → 미체결 매수 주문 취소/체결 결과 재확인 → 기존 실제 잔량 청산 주문 → 부분체결/미체결 재조회 → 종료 결과 기록 순서.
- 장 마감까지 미청산이 발생하면 `UnexpectedOvernightPosition` 경고·감사 기록을 남기고, 다음 세션 재시작 시 포지션을 재동기화할 때까지 신규 진입을 제한한다. 당일 청산 **보장**이라고 UI에 쓰지 않는다.
- 단순 '자동매매 체크 해제'는 신규 매수를 중단하되 기존 보유 보호/청산 로직은 계속 작동하게 한다. 기존 중지/긴급중지 의미와 충돌하지 않도록 정리한다.

## 8. 공통 백테스트·가상매매·실계좌의 동작 일치

- **틱 백테스트:** 과거 체결 이벤트를 실제 시각 순서대로 재생하며 장중 현재가, 당일 최고가, 매물대, 진행봉을 그 시점까지의 정보로 갱신한다. 미래 틱·미래 일봉 고가·당일 완성 매물대 사용 금지.
- 동일 입력 시퀀스, 동일 전략 설정, 동일 초기 상태에 대해 백테스트와 실시간 모의모드의 **진입·청산 신호 시각 및 사유**가 일치하도록 한다. 실제 체결가격의 일치는 요구하지 않는다.
- **체결 시뮬레이터는 신호 발생에 사용한 동일 틱 가격으로 즉시 완전 체결시키지 말 것.** 신호 이후 사용 가능한 호가/다음 틱, 주문지연, 스프레드, 슬리피지, 부분체결, 현금 제한, 호가단위를 모델링하고 가정을 결과에 표시한다.
- 과거 틱이 없으면 `분봉 확정 기반 근사 백테스트`와 `틱 재생 정밀 백테스트`를 명확히 분리한다. 분봉 OHLC에서 알 수 없는 고가·저가 선후관계를 임의로 유리하게 가정하지 말며, 진행봉 틱 신호를 정확하게 재현했다고 표시하지 않는다.
- `KnownAt`·당시 거래정지/관리/위험 사실 확인, 당시 구성 종목과 과거 상장폐지/합병 데이터의 가용 범위를 기록한다. 연구용 `UsePriceOnlyBacktest` 가정은 **실계좌 주문 승인에 절대 전이하지 않는다**.
- 실계좌는 주문접수/정정/취소/부분체결/전체체결/거부/미확인 상태를 주문번호·체결 ID로 멱등 처리한다. 주문 응답 지연 시 임의 재주문 전에 조회/재동기화를 수행한다.
- 비용(편도 수수료, 시장별 매도세금 등, 슬리피지)을 실제 시장·일자에 맞게 설정하고, 0% 검증 프리셋을 수익성 평가나 실계좌 기본값으로 자동 사용하지 않는다.
- 결과에는 거래별 매수·매도·잔량·비용·손익·진입 신호/실제 체결시각·급락 종류·MA 가격·매물대·반등 저점·익절/손절 사유·데이터 품질·미체결·강제청산 여부를 남긴다.
- 여러 종목을 동시에 거래하는 시뮬레이션은 **공유 계좌 현금과 주문 경쟁**을 반영한다. 기존 단일 종목 독립계좌 결과와 별도 표기. 당일·거래별 수익, 승률, 순기대손익, Profit Factor, MDD, 평균 보유시간(초·분), 미청산 포지션을 표시하며 짧은 구간의 과장된 연율화 성과만 강조하지 않는다.

## 9. 기존 61개 파라미터의 보존·연결 계획

다음은 **기존 이름의 의미를 유지하면서** 새 전략에서 재사용하거나 새 필드와 연결할 그룹이다. 실제 소스의 직렬화 이름·기본값·UI 라벨을 확인하고 `legacy → new` 매핑표를 작성하라. 아래에 없는 레거시 필드를 삭제하거나 묵시적으로 재해석하지 말 것.

### A. 가능한 한 계산식/의미 유지 및 재사용

- 자본·비용·통계: `InitialCapital`, `CommissionPercent`, `TaxPercent`, `SlippagePercent`, `AnnualRiskFreePercent`, `AnnualTradingDays`.
- 후보 표시: `ShowNearCandidates`, `MinimumCandidateProximity` (근접 점수는 **확률이 아니며** 주문 허가에 사용 금지).
- RSI·거래량 보조필터: `UseRsiFilter`, `RsiPeriod`, `MaximumRsi`, `UseVolumeFilter`, `VolumeLookbackDays`, `CrashVolumeRatio`.
- Pivot 선택 필터: `UsePivotSupport`, `PivotLeftBars`, `PivotRightBars` (전략 D 필수 지지 근거가 아니며 기본 프리셋에서는 선택적/비활성).
- 투자·분할매수: `PlannedInvestment`, `MaxEntries`, `EntryRatios`, `AdditionalEntryDropPercent`, `RequireSupportForAdditionalEntry`.
- 기존 청산 프리셋 호환: `UseEmergencyStop`, `EmergencyStopLossPercent`, `UseTakeProfit1`, `TakeProfit1Percent`, `TakeProfit1Ratio`, `UseTakeProfit2`, `TakeProfit2Percent`, `TakeProfit2Ratio`, `UseTrailingStop`, `TrailingStopPercent`, `TrailingActivation`.

### B. 레거시 동작을 보존하면서 새 전략에는 개선 정책 연결

- 유동성: `AverageTradingValueLookbackDays`, `MinimumAverageTradingValue` — 기존의 선택 봉 평균 의미는 보존하고 신규 일평균/장중 평균 기준 옵션을 별도 추가.
- 급락: `CrashLookbackDays`, `MinimumDrawdownPercent`, `ShortCrashLookbackDays`, `ShortCrashPercent` — 기존 '선택 봉 N개' 의미는 보존하고 신규 Daily/Intraday 룰로 분리.
- MA: `UseMovingAverageSupport`, `MovingAveragePeriods` — 기존 선택 봉 MA 기능 유지; 전략 D에는 실제 일봉 MA240을 독립 지정.
- 매물대: `UseApproximateVolumeZone`, `VolumeZoneLookbackDays`, `VolumeZoneBinSize`, `VolumeZoneMaxBins`, `VolumeZoneThresholdPercent`, `VolumeZoneMergeRangePercent` — 기존 근사 모드 유지, 정밀 체결 매물대 모드 추가.
- 군집: `SupportClusterRangePercent`, `MinimumSupportCount`, `SupportDistancePercent`, `ClusterChangeTolerancePercent` — 기존 인접 연결식 유지, 전략 D에는 **이종 지지(MA+VolumeZone) 중첩/폭 조건**을 별도 제공.
- 후보/반등: `RequireReboundConfirmation`, `ReboundPercent`, `CandidateLifetimeDays` — 레거시 확정봉/거래일 기반 유지, 새 틱 기반 접근저점/유효시간 별도 지원.
- 지지 손절: `SupportBreakPercent` — 레거시 cluster 하단 기준 유지, 전략 D에서는 최초 진입 매물대 하단 snapshot 기준을 새 정책으로 제공.
- 보유/재진입: `MaxHoldingDays`, `ReentryCooldownDays`, `MinimumNewClusterDistancePercent`, `AllowEarlyReentryWithNewCluster` — 레거시 사용 유지, 새 `DayOnly`, `ReentryMode`, 초·분·봉 대기 정책 추가.
- 연구/체결: `TouchPolicy`, `UsePriceOnlyBacktest` — 구 백테스트에만 의미 유지. 틱 재생 시 별도 체결 모델과 연구 가정 표시 적용.

### C. 새 설정 그룹(이름은 프로젝트 규약에 따라 변경 가능)

- `IndicatorTimeframeOverrides`, `EvaluationMode`, `SessionPolicy`, `MarketDataQualityPolicy`.
- `DailyCrashRules`, `IntradayCrashRules`, `CrashCombinationMode`, `CrashCandidateLifetime`.
- `MAReferenceTimeframe`, `MAPeriod`, `MAIncludeActiveDailyBar`.
- `VolumeProfileSource(Approximate/TradeBased)`, `VolumeProfileLookback`, `MaxOverlapDistancePercent`, `MaxZoneWidthPercent`, `MaxEntryDistanceFromSupportPercent`.
- `ReboundEntryMode`, `ReboundThresholdPercent`, `ReboundCandidateLifetime`, `FreshSignalRequired`.
- `ReentryMode`, `MinReentryInterval`, `AfterStopCooldown`, `MaxEntriesPerSymbolPerDay`, `MaxEntriesPerAccountPerDay`.
- `ExitMode(Full/Partial/TrailingOnly)`, `SupportBreakStopEnabled`, `FixedStopEnabled`, `TrailingStopEnabled`, `TrailingPeakBasis`, `BreakEvenStopEnabled`.
- `DayOnly`, `NoNewEntryAfter`, `LiquidationStartAt`, `UnexpectedOvernightHandling`, `RiskBudget`, `MaxDailyLoss`, `MaxConcurrentExposure`.

`TrailingStopEnabled` 같은 신규 필드가 기존 `UseTrailingStop`과 중복될 경우 실제 모델에서는 **하나의 진실 공급원**만 두고 마이그레이션/화면 어댑터로 통합한다. 신규 설정 개수는 위 목록을 기계적으로 전부 복제하지 말고 중복을 최소화하라.

## 10. UI 요구사항 (WPF)

- 현재 전략 설정·종목 검색/차트·스크리닝 결과·백테스트 팝업·실시간 다중매매 흐름을 유지한다.
- 전략 설정 상단에 `전략 D` 프리셋, `간편/고급 설정`, `전략 저장/불러오기/내보내기` 제공. 기존 저장 전략은 마이그레이션 없이도 읽을 수 있어야 한다.
- 입력 그룹: `급락(일봉/당일)`, `MA 지지`, `매물대`, `중첩/지지 접근`, `반등 매수`, `분할매수/재진입`, `익절`, `손절`, `당일청산/위험`, `백테스트 체결가정`.
- 숫자는 반드시 단위(%/원/초/분/봉/거래일), ON/OFF 의존성, 허용범위, 실제 데이터 지원 여부를 표시한다. `0.015%`를 `0.02%`로 표시해 저장값을 변경하지 않는다.
- 스크리닝 표는 `근접 후보`, `정확 후보`, `매수 신호`, `주문 중`, `부분체결`, `보유`, `매도 중`, `청산`, `주문 차단/자료부족`을 구분하고 종목별 현재 매수 가능 이유/불가 이유를 노출한다.
- 차트에는 일봉 MA240, 매물대 범위, 접근/반등 저점, 진입시각, 매수·매도 실제 체결, 지지손절·고정손절·추적손절, 강제청산 시각을 출처별로 표시한다.
- 실계좌 ON/OFF와 '전략 실행 중' 표시를 별도로 두고, 자동매매 체크 해제 후에도 기존 포지션 보호가 실행됨을 명확히 표시한다.
- 지원되지 않는 데이터(예: 과거 틱 부재)는 비활성 옵션 또는 명확한 차단 사유로 처리. UI만 있고 연결되지 않은 선택지는 배포하지 않는다.

## 11. 테스트 및 완료 판정 기준 (필수)

1. **MA 기준 분리:** 1분봉을 선택해도 전략 D의 '일봉 MA240' 값은 전일 확정 240일 SMA와 같아야 하고, 틱마다 임의로 240분 SMA로 바뀌면 실패.
2. **미래자료 차단:** 오전 10시 신호 계산에 당일 15시 최고가/완성 매물대, 내일 일봉, 사후 위험 사실을 넣지 않는다.
3. **급락 OR:** 3일 급락만 / 당일 급락만 / 둘 다 / 둘 다 아님을 각각 테스트; 각 유형 ON/OFF 및 AND/OR 테스트.
4. **이종 지지:** MA 두 개만 가까워도 D는 매수 후보가 될 수 없고, 유효 매물대가 반드시 있어야 한다. 중첩 거리와 매물대 폭 초과 시 차단.
5. **실시간 반등:** 10,100원 접근 → 9,900원 저점 → 9,920원 대기 → 9,950원 반등(약 0.505%)일 때, 0.5% 기준 최초 한 번만 신호. 지지에서 너무 멀어진 추격 가격은 차단.
6. **동일 군집 재진입:** 09:10 진입→09:15 전량 청산→09:30 **새 후보/새 반등** 발생 시 같은 Zone 재매수 가능. 이전 반등 조건이 계속 true인 것만으로 즉시 재매수는 불가.
7. **손절 병행:** 지지 이탈/고정손절/추적손절 동시 ON; 한 틱에 다수 충족돼도 청산 주문 한 건. 손절선은 지지/Peak 변화에 의해 불리하게 내려가지 않는다.
8. **익절 3모드:** 전량/부분/추적 중심 검증. TP1 후 Peak 유지/재설정 옵션, BreakEvenStop, 0주 부분익절, 실제 부분체결 검증.
9. **장 마감:** `DayOnly`가 ON이면 신규 진입 제한과 잔여 포지션 청산 시도가 독립적으로 수행되어야 한다. 미체결/거래정지 예외는 숨기지 않고 경고와 재동기화 필요 상태를 기록.
10. **재현성:** 동일 기록 틱 이벤트를 replay/가상 실시간 엔진에 주입하면 신호 발생 시각·사유·전략 상태가 같아야 한다(체결 모델 차이는 별도 비교).
11. **주문 멱등성:** 중복 틱, 지연 주문응답, 부분체결·취소 후 잔량조회, 앱 재시작에서 중복 매수·매도 주문을 만들지 않는다.
12. **레거시 회귀:** 기존 61개 설정, JSON import/export, 일봉·지정봉 백테스트, 진단 프리셋 결과가 의도치 않게 변경되지 않는다. 변경이 불가피하면 비교표·마이그레이션 테스트 제시.
13. **다종목:** 두 종목 동시 신호에 대해 공유계좌 현금/최대 노출/주문 중복 제약을 준수한다.
14. **데이터 결손:** 원본 틱이 없거나 상태·위험이 미확인인 엄격한 실거래 경로는 주문을 막고 사유를 표시. 근사 백테스트 결과를 정밀 틱 결과로 표기하지 않는다.
15. **성능/안정성:** 기존 상위 10/25/50/100개 감시에서 틱 처리 큐 적체, 지연, 메모리 증가를 계측하고 이벤트 유실/지연 시 실거래 신호를 차단. 실측 전 임의 성능 보장 수치는 제시하지 않는다.

## 12. 구현 순서와 산출물

**P0 — 현행 코드 감사:** 61개 설정과 실제 연결 경로, 온라인/오프라인 지원범위, 데이터 공급자의 틱/호가/과거틱 기능 확인. 변경 계획과 위험 보고.

**P1 — 설정/호환성:** 새 `TimeframeSpec`, 위험/당일/재진입/익절 정책, 전략 D 프리셋, schema migration 및 검증·저장 테스트.

**P2 — 데이터·지표:** 틱 이벤트 표준화, 진행봉 처리, 일봉 MA240, daily/intraday 급락, 정밀/근사 매물대, 지지 중첩 및 스냅샷.

**P3 — 전략 D 신호:** 후보 episode, 접근 저점, 신규 반등 이벤트, 중복 방지, 동일 Zone 재진입.

**P4 — 범용 실행/위험:** 3종 손절, 3종 익절, 부분체결, 포지션 재동기화, 당일 청산, 계좌 공통 한도. 먼저 모의모드로 검증.

**P5 — 백테스트:** 과거 틱 replay, 모의체결, 원본 데이터 가용성 검증, 신호 재현성, 비용/성과 및 다종목 공유자본.

**P6 — WPF UI 및 회귀 테스트:** 설정/차트/스크리닝/백테스트/다중매매 상태 표시, 단위/도움말, 자동화 테스트, 사용 가이드.

각 단계마다 **변경 파일, 핵심 설계 결정, 추가/변경 설정, 통과/실패 테스트, 남은 제약**을 보고한다. 기능 전체를 한 번에 교체하지 말고 가능한 작은 단위로 구현·검증한다. 실제 계좌 기능은 사용자가 별도로 실행을 승인하기 전까지 비활성 상태로 유지한다.

## 13. 반드시 지켜야 할 완료 기준

- 사용자는 기존 프리셋/61개 파라미터를 잃지 않는다.
- 새로운 전략 D 설정으로 `최근 며칠 OR 당일 급락 + 실제 일봉 MA240 + 매물대 중첩 + 틱 반등 확인`이 동작한다.
- 같은 종목을 같은 날 여러 번 매매하되 신호와 주문 중복이 발생하지 않는다.
- 일반/지지/추적 손절을 독립 ON/OFF, 전량/분할/추적 중심 익절과 조합할 수 있다.
- 당일 매매 모드에서 장 종료 전 청산을 시도하고 실패/미체결 상태를 숨기지 않는다.
- 백테스트·가상매매·실계좌가 신호 판단 로직을 공유하고, 체결 모델 차이는 명시적으로 보고한다.
- **과거 틱/호가 또는 위험·상태 확인 데이터가 없으면 그 한계를 표시하고 안전하게 기능을 제한한다.**
- 사용자는 실계좌 매매를 명시적으로 활성화하기 전까지 어떤 실제 주문도 발생시키지 않는다.

이 요청은 **소프트웨어 구현 요구사항**이며, 전략 D의 수익성이나 특정 파라미터의 우수성이 검증됐다는 뜻이 아니다.
