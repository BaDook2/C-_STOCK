# Bin's Strategy — Codex 구현 요구사항 설계서 v1.0

작성 기준: 2026-11-11 (Asia/Seoul)  
대상 저장소: https://github.com/BaDook2/C-_STOCK  
확인한 `main` 커밋: `ca8ea4a98f18355838d7fbf5ccfe34a9807a9358`  
문서 목적: **기존 매매 전략을 완전히 폐기하고 Bin's Strategy라는 단일 전략 체계로 교체**하기 위한 실행 가능한 기술 명세. 구현 대상은 **파라미터, 지표 계산, 시장 상태 분류, 매수·매도 신호, 주문·체결, 재진입, 위험관리, 백테스트 및 검증**이다.

> **Codex에 대한 최상위 지시:** 이 문서를 최종 요구사항으로 취급하라. 이전의 과매도/낙주반등/전략 D/레거시 매매 전략을 재현하거나 호환성을 이유로 남기지 말 것. 기존 전략 전용 클래스, 엔진, 활성 경로, 파라미터, 저장 프리셋 스키마는 사용 경로에서 제거하고 Bin's Strategy로 교체한다. 단, 전략과 무관한 시세 수신, 종목/거래소 모델, 브로커 연결, 주문 대사, 인증, 데이터 보존, 거래 내역, 계좌 안전장치는 무분별하게 파괴하지 말 것. **실계좌 자동주문은 별도 명시 승인 및 검증 전까지 비활성화한다.**

---

## 0. 최신 저장소 검토 결과 및 구현 범위

### 0.1 확인 사실

- 직전 확인 커밋 `d067be25553d502a3a1c16316164115bbd8e7cbc`에서 현재 `main`의 `ca8ea4a...`까지 **2개 커밋**이 추가됨. 비교된 변경 파일 **15개는 배포 워크플로·개발 문서·검증 기록**이며, 해당 구간의 `Stock.Core` 전략·주문 소스 변경은 확인되지 않았음.
- 현재 기본 실행 프로젝트는 `STOCK.Avalonia.sln`; 코어 프로젝트는 `Stock.Core`. 구 WPF 소스도 저장소에 있음. 구현 시 단일 코어 구현을 중심으로 작업할 것.
- 기존 코드에는 `IDayTradingSignalRuntime`, `DayTradingPortfolioEngine`, `CausalMarketInput`, `CausalBarBuilder`, `IntradayPositionRules`, `IntradayReplayEngine` 등의 기반 개념이 있음. **기존 전략 규칙의 재사용은 의무가 아니며**, 제약·불변식이 새 설계와 충돌하면 해당 로직을 교체한다.
- 기존 `DayTradingSessionService.Start`는 `StrategyDRuntime`에 결합되고, 기존 `DayTradingPortfolioEngine`의 진입·청산 주문은 `OrderKind.Market`으로 생성됨. 새로운 **범용 전략 런타임**과 **슬리피지 제한 지정가**는 구현·검증해야 함.
- 기존 D 데이터 엔진은 국내 일반주식으로 제한하는 검증이 있음. **Bin's Strategy는 미국(NASDAQ/NYSE/AMEX)과 한국(KRX)** 양쪽을 지원해야 하므로 D 전용 국내 제한을 계승하지 말 것.
- 브로커별 실제 주문/실시간 체결/호가/장전·장후 지원 가능 여부는 별도로 실측·검증해야 함. 데이터가 없는 기능을 있는 것처럼 작동시키지 말 것.
- 이것은 코드 열람 및 커밋 비교 결과다. 현재 명세에 대해 **빌드·테스트·실거래 검증을 실행한 것은 아님**.

관련 출발점: `Stock.Core/Research/DayTrading/`, `Stock.Core/Trading/MarketModels.cs`, `Stock.Core/Trading/MarketData.cs`, `Stock.Core/Trading/LiveStrategyBatch.cs`, `Stock.Core/Trading/RealtimePaperStrategyBatch.cs`, `Stock.Core/Research/DayTrading/DayTradingPortfolioEngine.cs`, `docs/maintenance/Verification.md`, `AGENTS.md`, `TASKS.md`, `HANDOFF.md`.

### 0.2 기존 전략 제거 경계

**제거:** 기존 과매도·낙주반등·전략 D의 시장 필터, 신호 발생기, 포지션 규칙, 재진입 규칙, 활성 선택 분기, 전용 JSON 타입/저장 프리셋의 실행 경로, 관련 전략 전용 테스트 기대값, 더 이상 사용되지 않는 전략 전용 의존 코드. 과거 전략의 `UseCrashStrategy` 등의 스위치로 새 전략을 분기하지 말 것. 새 전략 이름/ID를 **`Bin's Strategy` / `bins-strategy-v1`**으로 정의하고 한 종목의 선택된 전략 가족은 이 시스템 안에서만 관리한다.

**보호:** 전략과 무관한 브로커 주문 인증/취소/정정/체결 조회, 계좌 대사, 실제 계좌 잔고, 유효한 종목 메타데이터, 사용자 API 키, 기보유 거래 내역, 기존 DB의 비전략 사용자 데이터. 전략 이력 데이터는 새 실행에 사용하지 않지만 삭제/마이그레이션 전 **원본 백업**을 만들고 과거 거래 감사기록을 보존한다. 중요한 데이터 삭제·계좌 주문·릴리즈는 사용자 승인 없이 수행하지 말 것.

기존 클래스를 이름만 바꾼 채 내부의 MA240 필수·급락 필수·기존 D 재진입 상태를 남겨서는 안 된다. 아래 제시한 전략 명세만이 신규 실행 조건이다.

### 0.3 구현 원칙 및 금지사항

1. 모든 시장/전략 판단은 **그 시점에 알려진 데이터만** 사용한다(look-ahead 금지).
2. 시장 상태 분류는 선택한 분석봉의 **확정봉**으로만 갱신한다. **실제 진입·청산은 유효한 신규 체결 틱**으로 평가한다. 호가는 시세·체결 가능성 판단에 사용한다.
3. 시장 데이터, 지표, 상태 분류, 선택형 지지 필터, 개별 진입 엔진, 공통 포지션/위험관리, 주문 어댑터, 시뮬레이터를 분리한다.
4. 동일한 이벤트, 설정, 초기 상태에서 연구 재생과 모의운용의 **신호 시각·사유·의사결정은 결정적**이어야 한다. 실제 체결 결과는 어댑터에 따라 달라질 수 있다.
5. **한 종목에 동시 포지션 1개, 보유 중 추가매수 금지**. 전량 청산과 주문 상태 확인 후 새 후보 신호로 재진입 가능.
6. 모든 전략 가족과 진입 유형은 **하나의 공통 청산·위험관리 파라미터 세트**를 공유한다. 다만 기술적 손절 기준이 되는 *가격 위치*는 진입 유형별로 다르다.
7. 자료 결손, 미확인 체결, 시간 역전, 주문 상태 미확인, 부적절한 스프레드 등은 매수 허용 상태로 오인하지 않는다.
8. 보안·브로커 API·주문 상태의 미검증 기능을 가상 체결로 꾸며 실계좌 기능으로 표시하지 않는다.
9. 숫자·시간·횟수·비율·각 전략 선택 조건은 **코드에 고정하지 않고 유효성 검사 가능한 설정값**으로 만든다. 여기의 숫자는 모두 검증 전 초기 실험값이다.

---

## 1. 사용자 확정 의사결정(변경 금지)

| 분류 | 확정 규칙 |
|---|---|
| 시장 | 한국 주식 + 미국 주식(페니스탁 포함) |
| 1단계 | **선택형** MA+매물대 중첩 지지구간 반등 감지. ON이면 통과 후 제한된 활성시간에만 2단계 신규 진입 허용; OFF이면 2단계 단독 운용 |
| 1단계 활성시간 | 사용자 설정, 초기 **30분**. 새로운 *독립된 유효 반등* 감지 시 만료 시각 갱신 |
| 지지 붕괴 | 즉시 **신규매수 차단**, 활성 허가 취소. 보유 청산은 별도 공통 손절이 판단 |
| 시장 분류 | 상승 / 하락 / 박스권 / 판단 보류. 최근 **60분** 분석 초기값, **새 분류 2개 확정봉 연속 확인**, 불확실하면 신규매수 보류 |
| 분류 방법 | 가격 구조 + 방향성 + 변동성. 분류 봉은 **1·3·5·15분봉 또는 별도 설정 봉** 중 사용자 선택 |
| 진입 주기 | 신규 체결 **매 틱 평가**(확정봉 종료까지 기다리지 않음) |
| 하락 반등 | 빠른 진입/확인 진입 **둘 다 지원**. 기본은 확인 진입 |
| 상승 추세 | **이전 고점 돌파** + **눌림목 반등** 모두 지원. 돌파는 가격 유지 확인, 눌림목은 MA 접근+조정률+반등+단기 고점 돌파 |
| 박스권 | **하단 반등** + **상단 돌파** 모두 지원. 박스는 반복 반응 가격대로 정의, 상단 돌파는 유지 확인 |
| 전략 선택 | 수동으로 가족 1개 고정 또는 자동으로 시장 상태별 가족 선택 |
| 자동 전략 전환 | **ON/OFF 파라미터**. ON은 전량 청산 후 새 시장 상태에 맞춰 선택 가능, OFF는 종목의 당일 첫 선택 가족 고정. 보유 중 가족 변경 금지 |
| 청산 | **공통** TP1/TP2 분할 익절 + 추적손절 기본; 고정 손절 + 기술적 손절 동시 사용 |
| 포지션 | 종목당 1개, 보유 중 추가매수 **금지** |
| 재진입 | 익절 후보다 **손절 후 대기시간을 더 길게** 설정. 새 유효 신호 필수 |
| 유동성 | 스프레드 과대·호가 수량 부족 시 신규매수 차단 |
| 주문 | **슬리피지 제한 지정가**를 기본 주문 정책으로 사용 |
| 설정 자유도 | 모든 수치는 사용자 입력. 모든 시간성 파라미터는 **값 + 초/분/시/일** 선택. 시장 상황별 '자동으로 값 보정/튜닝' 옵션은 넣지 말 것. 자동 전략 선택·전환은 별개 기능이며 유지 |

**중요한 범주 구분:** 전략 가족은 정확히 **3개(하락, 상승, 박스권)**, 진입 유형은 정확히 **5개(하락 반등, 상승 고점 돌파, 상승 눌림목, 박스 하단 반등, 박스 상단 돌파)**. 하락 반등의 `Fast`/`Confirmed`는 **동일 진입 유형의 선택 모드**이지 6번째 전략이 아니다.

---

## 2. 시간·시장·가격 타입 정의

### 2.1 공통 시간 타입

```csharp
public enum DurationUnit { Second, Minute, Hour, Day }
public sealed record DurationSpec(decimal Value, DurationUnit Unit);
public enum MarketRegime { Unknown, Downtrend, Uptrend, Range }
public enum StrategySelectionMode { Manual, Automatic }
public enum StrategyFamily { DowntrendRebound, Uptrend, Range }
public enum EntryPattern { DowntrendRebound, UptrendBreakout, UptrendPullback, RangeLowerBounce, RangeUpperBreakout }
public enum DowntrendEntryMode { Fast, Confirmed }
public enum StageOneState { Disabled, Watching, Active, Invalidated, Expired }
public enum EntryCandidateState { None, Watching, Confirming, Signaled, Invalidated, Expired }
```

- `DurationSpec.Day`는 **24시간의 경과시간**. 실제 거래일 개수는 `TradingDayCount`/확정 **일봉 개수**와 별도 타입을 사용한다. 장을 넘는 유효기간은 세션 규칙에 따라 종료한다.
- `TimeframeSpec`은 봉 크기와 봉 단위(초·분·시·거래일)를 표현한다. 초기 선택 프리셋 1/3/5/15분봉도 내부에서는 일반화된 타입을 쓴다. 일봉 `N개`는 `N일 동안 경과시간`과 다르다.
- 분으로 제시한 모든 관측기간/확인기간/후보 유효기간/쿨다운/주문 타임아웃/시세 지연/지지 활성기간은 `DurationSpec`으로 통일한다. 횟수·봉 개수는 정수 파라미터.
- 분류용 관측기간은 지정된 경과 시간 내의 **완성된 봉들**만 사용한다. 세션 경계 간 무거래 시간을 가상의 봉으로 채우지 않는다. 과거 세션 포함은 명시 설정으로만 허용(기본 OFF).
- 내부 시각은 UTC/`DateTimeOffset`으로 저장하고, 세션·거래일·장전·정규장·장후의 해석은 **거래소별 캘린더와 타임존**으로 한다. KST 15:00 등의 고정값으로 미국장을 처리하지 않는다.
- 가격·수량·비율은 C# `decimal`을 사용하고, 시간 비교와 도메인 범위 검증을 결정적으로 구현한다.

### 2.2 공통 입력

`TradeTick(exchangeTime, receiveTime, symbol, price, size, sequence, source, marketSession, quality)` 및 `QuoteSnapshot(bid, ask, bidQty, askQty, asOf, depth, source)`를 정규화한다. 공급자가 허용하는 정밀도에서만 호가량/체결량을 사용한다. 없는 bid·ask·호가량을 `lastPrice`로 합성하여 매수 승인하지 말 것.

`InstrumentMetadata`에는 거래소, 통화, 거래 세션, 호가단위 규칙, 상장/거래정지/관리·위험 상태, 가격제한 및 허용 주문유형을 포함한다. 특히 미국 $1 미만 호가단위나 한국 호가단위를 현재 코드의 단순 가격 구간 함수만으로 절대 확정하지 말고, **시점별 거래소·브로커 규칙 제공자**로 분리·검증한다.

### 2.3 워밍업 및 자료 부족

- 기본 상태는 `Unknown`, 매수 금지.
- 지표별 요구 히스토리(EMA20, ATR14, MA240 일봉, 매물대, 구조 분석 봉 수)를 명시적으로 계산해 선로딩한다. 충분치 않으면 `InsufficientHistory`를 반환한다.
- 예: 60분 관측, 15분봉이면 4개 봉뿐이다. `MinimumRegimeBars=8`이면 **사용자에게 설정 충돌을 식별 가능한 오류로 반환**하거나 분류 보류한다. 120분 등으로 **몰래 자동 변경 금지**. EMA/ATR용 추가 과거 워밍업 자료는 실제 판단 관측기간과 구분한다.
- 장 시작 직후 자료가 부족하면 이전 세션 가격을 해당 세션의 실시간 연속 체결처럼 취급하지 않는다.
- 틱 수신 중단은 경과시간 유지 신호(2초 유지 등)가 충족되었다는 증거가 아니다.

---

## 3. 시장 상태 분류 엔진 (`MarketRegimeClassifier`)

### 3.1 입력·출력·변경 시점

입력: 선택한 분류 봉의 **확정봉**, 그 시점까지 알려진 추가 과거 확정봉, `RegimeConfig`.

출력: `RegimeSnapshot(regime, candidateRegime, lastConfirmedAt, evidence, confidenceDiagnostics, boxSnapshot?, validity, configVersion)`.

확정봉 하나가 끝날 때만 원시 분류를 계산한다. 원시 분류가 현재 상태와 달라졌다면 **연속 `ConfirmBars=2`개 확정봉**에 동일한 새 분류가 나타날 때 상태를 변경한다. 두 번째 봉이 해당 조건을 충족하지 못하면 연속 카운트를 재설정한다. 단, 확정된 기존 상태의 필수 구조가 명백히 무너졌거나 데이터가 불명확하면 매수 허가를 별도로 차단한다. 안정화를 명목으로 잘못된 과거 상태에서 신규 매수를 지속하지 말 것.

### 3.2 계산 수식

`N`개 확정봉의 종가 `C_i`, 고가 `H_i`, 저가 `L_i`를 사용한다.

**순가격 변화율**

`DirectionPct = 100 × (C_N / C_0 − 1)`

**Efficiency Ratio**

`ER = abs(C_N − C_0) / sum(i=1..N, abs(C_i − C_(i−1)))`  
분모가 0이면 `ER=0`으로 계산하되, 시장 정지/자료 결손인 경우 `Unknown`이 우선한다.

**ATR / 변동성**

`TR_i = max(H_i − L_i, abs(H_i − C_(i−1)), abs(L_i − C_(i−1)))`  
`ATR_N = WilderSmoothed(TR_i, AtrPeriod)`  
`ATRpct = 100 × ATR_N / C_N`  
`TrendMinMove = max(MinTrendMovePct, AtrTrendMultiplier × ATRpct)`.

ATR/ER/가격구조 모두 확정봉의 값만 사용한다. 서로 다른 시간 크기 선택에서 계수의 체감이 달라질 수 있으므로 동일한 기본값이 최적이라는 가정은 금지.

**고점·저점 구조**: 지정된 `RegimeLookback`을 과거 절반 A, 최근 절반 B로 나누고 유효 확정봉의 최고가·최저가 `HA, LA, HB, LB`를 계산한다.  
`Epsilon = StructureAtrTolerance × ATR`.

- `UpStructure = (HB > HA + Epsilon) AND (LB > LA + Epsilon)`
- `DownStructure = (HB < HA − Epsilon) AND (LB < LA − Epsilon)`
- `UpCandidate = DirectionPct >= TrendMinMove AND ER >= ERTrend AND UpStructure`
- `DownCandidate = DirectionPct <= −TrendMinMove AND ER >= ERTrend AND DownStructure`

구조는 추후 확정 피벗 고점/저점 버전으로 교체 가능하게 인터페이스를 분리한다. 피벗은 오른쪽 확인봉을 기다려야 확정되며 미래 피벗을 과거 시점에 알려졌다고 간주해서는 안 된다.

**박스 상·하단**: 반복 반응 가격대를 사용한다. 확정봉 고가 집합에서 `UpperCandidate = Q_UpperQuantile(H)`; 저가 집합에서 `LowerCandidate = Q_LowerQuantile(L)`. 후보 가격에 대해 지정 거리 이내의 `상단 접근→확인된 하향 반응`, `하단 접근→확인된 상향 반응`을 *서로 구별되는 에피소드*로 센다. 에피소드 간 최소 `RangeTouchSeparation` 시간, 최소 반전폭 `RangeReversalMinAtr × ATR`(또는 명시적 % 기준), 상·하단 각각 최소 반응횟수 이상 필요.

`RangeWidthPct = 100 × (Upper−Lower) / Lower`;  
`InsideRatio = 유효봉 종가 중 [Lower, Upper] 내부 비율`.

`RangeCandidate = ER <= ERRange AND touchesUpper >= MinUpperTouches AND touchesLower >= MinLowerTouches AND RangeMinWidthPct <= RangeWidthPct <= RangeMaxWidthPct AND InsideRatio >= MinInsideRatio AND NOT (UpCandidate OR DownCandidate)`.

박스 상단·하단 값은 **분류 확정 시 스냅샷으로 잠금**. 이후 돌파를 관측하는 도중 진행 틱으로 박스 상단을 재계산하지 않는다. 박스 자체가 붕괴·만료했으면 새로 박스를 확인하기 전까지 해당 박스에서 하단 반등 신호 금지. 상단 돌파 신호는 잠긴 유효 박스에 대해 한 번의 에피소드만 허용.

**모호함**: 유효한 후보가 없거나 여러 후보가 충돌, 자료 누락, 구조 불충분이면 `Unknown`이며 신규 매수하지 않는다.

### 3.3 시장 상태 전환과 2개 확인봉

`candidateRegime`, `candidateConsecutiveBars`를 보유하고 다음 정책을 실행한다.

1. 확정봉 완료 시 raw candidate 계산.
2. `Unknown`이면 추세 변환 카운트 리셋, 신규매수 차단.
3. 같은 후보가 연속 `ConfirmBars` 동안 유지되면 committed regime 갱신.
4. 상태가 바뀌어도 **현재 보유 포지션에 연결된 EntryPattern·청산 기준·설정 스냅샷은 절대 바꾸지 않는다**.
5. **박스 상단 돌파만 특별 처리**: 현재 committed Range의 잠긴 Upper를 유효 틱으로 돌파하면 RangeUpperBreakout 신호를 허용할 수 있다. 그 후 확정봉의 상승 추세 확인을 기다린다. 이 사건에서 상승 고점 돌파 신호를 **동시에 추가 발생시키지 않는다**.
6. `AutoSwitch=OFF`인 종목은 시장이 다른 가족으로 확정돼도 당일 선택된 가족을 바꾸지 않는다. 다만 그 가족의 필수 가격 구조가 사라지면 신규 진입을 중단한다.

### 3.4 초기값 (전체 수정 가능)

| 설정 키 | 초기값 | 유효 범위/의미 |
|---|---|---|
| `RegimeTimeframe` | `5 Minute` | 사용자 선택 봉(1/3/5/15분 프리셋, 사용자 정의 초/분/시/거래일 선택 가능) |
| `RegimeLookback` | `60 Minute` | 양의 `DurationSpec` |
| `MinimumRegimeBars` | `8` | 정수 ≥ 4. 부족하면 `Unknown`/설정 검증 오류 |
| `RegimeConfirmBars` | `2` | 정수 ≥ 1 |
| `AtrPeriod` | `14` | 확정봉 수, ≥ 2 |
| `ERTrend` | `0.40` | 0~1 |
| `ERRange` | `0.20` | 0~1, `ERRange < ERTrend` 필수 |
| `MinTrendMovePct` | `0.60` | 0 초과 |
| `AtrTrendMultiplier` | `2.0` | 0 이상 |
| `StructureAtrTolerance` | `0.10` | 0 이상 |
| `RangeUpperQuantile` | `0.85` | 0~1 |
| `RangeLowerQuantile` | `0.15` | 0~1; Upper > Lower |
| `RangeMinTouchesPerSide` | `2` | 정수 ≥ 2 |
| `RangeTouchSeparation` | `5 Minute` | DurationSpec |
| `RangeReversalMinAtr` | `0.3` | 0 이상 |
| `RangeTouchTolerancePct` | `0.30` | 0 이상 |
| `RangeMinWidthPct` | `1.5` | 0 이상 |
| `RangeMaxWidthPct` | `12` | Min보다 큼 |
| `RangeMinInsideRatio` | `0.80` | 0~1 |
| `RangeSnapshotLifetime` | `60 Minute` | 후보 경계 사용 가능 최대시간 |
| `IncludePreviousSessionForLookback` | `false` | 명시적 옵션, 데이터/세션 경계 준수 |

---

## 4. 선택형 1단계 지지구간 반등 허가 (`SupportActivationGate`)

### 4.1 정확한 역할

1단계는 **매수하지 않는다**. 마찬가지로 1단계가 활성화됐다고 다른 매수 조건을 생략하지 않는다. `EnableStage1=false`면 Stage2는 독립 실행. `true`면 1단계의 유효 반등 이후에만 Stage2 신규 진입 허용.

`Stage1AllowedToEnter(t) = NOT EnableStage1 OR (state==Active AND t<activeUntil AND supportNotBroken AND sameSession)`.

1단계 ON으로 활성화된 동안에는 2단계가 **여러 번 매수→전량 매도→재진입**할 수 있으며, 매번 지지 반등을 다시 요구하지 않는다. 단 신규 매수 신호·쿨다운·위험필터는 매번 다시 충족해야 한다.

### 4.2 MA와 매물대 중첩

MA 기간·기준봉과 Volume Profile 기간·방식을 사용자가 결정한다. 기본 MA는 **전일 확정 일봉 종가 240개** 평균, 기본 매물대는 **최근 확정 일봉 240개의 OHLCV 기반 근사**. 원본 틱 체결량이 있으면 거래량 기반 프로파일을 선택할 수 있으나, 출처를 구분한다.

`ZoneCenter=(ZoneLow+ZoneHigh)/2`  
`ZoneWidthPct=100×(ZoneHigh−ZoneLow)/ZoneCenter`

`GapPct = 0` if `ZoneLow <= MA <= ZoneHigh`; else `100 × min(abs(MA−ZoneLow),abs(MA−ZoneHigh)) / MA`.

`ValidSupport = GapPct <= MaxOverlapPct AND ZoneWidthPct <= MaxZoneWidthPct`.

근사 프로파일에서 `VolumeZoneThresholdPct=70`은 **최대 bin 거래량에 대한 70%**를 의미하며, 전체 거래량의 70% Value Area와 혼동 금지. 거래량을 OHLC Low~High bin에 균등 분배하는 방식은 모델링된 근사이고 실제 체결량이라고 표시하거나 사용하지 말 것. 거래소별 호가단위에 맞춰 bin을 생성한다.

지지범위: `[min(MA,ZoneLow), max(MA,ZoneHigh)]`. 현재가가 `[lower×(1−ApproachPct/100), upper×(1+ApproachPct/100)]` 안으로 들어오면 **새 접근 에피소드** 생성. 이후 유효 신규 틱의 최저가 `L` 갱신.

`ReboundPct(t) = 100×(P(t)/L−1)`  
이전 틱에서 `ReboundPct < MinReboundPct`였고 현재 틱에서 상향 통과한 **신규 교차 사건**만 신호로 인정한다. 이미 기준 위에 있는 가격에서 후보를 뒤늦게 만들어 즉시 활성화하지 말 것.

### 4.3 상태·갱신·붕괴

- `Watching`: 지지 후보/새 접근/저점 추적.
- `Active`: 신호 시각 `t`부터 `activeUntil=min(t+ActivationDuration, sessionEntryEnd)`; 최초 진입 및 여러 재진입 허용.
- `Refresh`: **별도 `episodeId`의 새 유효 반등** 발생에 한해 `activeUntil` 갱신. 동일 저점 근처의 작은 등락이나 조건 유지 중 반복 갱신 금지.
- `Invalidated`: 활성 구간의 잠긴 `SupportLower` 이하로 `BreakBufferPct`만큼 하락(가격 `< SupportLower×(1−BreakBufferPct/100)`)하면 **즉시 신규 진입 차단하고 activeUntil 제거**. 새로운 접근·저점·반등의 새 에피소드만 재활성화할 수 있다.
- `Expired`: 시간 종료·세션 종료·자료 품질 미확인. 신규 진입 금지.
- **보유 중** 붕괴·만료 발생 시 기존 보유를 무조건 청산하지 않는다. 보유 포지션의 고정/기술적 손절 엔진이 독립적으로 판단한다.
- MA/Zone 후보가 재계산되어도 동일 물리 구간이면 에피소드 ID가 흔들리지 않도록 스냅샷/거리 기반 동일성 검증. 기준 지지구간의 하단을 위험 회피 없이 아래로 옮겨 붕괴를 무효화하지 말 것.

### 4.4 1단계 초기 파라미터

| 키 | 초기값 |
|---|---|
| `EnableStage1` | `false` (사용자 선택, OFF 시 완전 우회) |
| `SupportMAPeriod` | `240` 확정 일봉 |
| `SupportMABar` | `1 TradingDay` |
| `ProfileLookbackBars` | `240` 확정 일봉 |
| `ProfileBar` | `1 TradingDay` |
| `ProfileSource` | `ApproximateOHLCV` (실제 틱 시 TradeBased 선택 가능) |
| `ProfileBinSizeTicks` | `5` 호가단위(초기 실험) |
| `ProfileMaxBins` | `512` |
| `VolumeZoneThresholdPct` | `70%` of max bin volume |
| `ProfileMergeRangePct` | `1.0%` |
| `SupportMaxOverlapPct` | `0.5%` |
| `SupportMaxZoneWidthPct` | `2.0%` |
| `SupportApproachPct` | `1.0%` |
| `SupportMinReboundPct` | `0.5%` |
| `SupportCandidateLifetime` | `15 Minute` |
| `ActivationDuration` | `30 Minute` |
| `SupportBreakBufferPct` | `0.3%` |
| `MinDistinctSupportEpisodeGap` | `15 Second` |
| `SupportZoneIdentityTolerancePct` | `0.5%` |

모든 항목 설정 가능. `Stage1=true`인데 MA240/프로파일 자료가 충분하지 않으면 **허가 불가**, Stage2 우회 금지. 미국 페니스탁의 히스토리가 부족하다는 이유로 자동으로 MA120으로 줄이지 말 것.

---

## 5. 2단계 진입 신호 공통 상태 머신

**전략 3개, 진입 패턴 5개.** 패턴은 각자의 가격 후보·조건을 가지지만 `EntrySignal` 형식, 실행 가능성 검사, 주문 승인 및 청산 엔진은 공유한다.

`None -> Watching -> Confirming -> Signaled -> (PendingOrder/Rejected/Expired)`; 어느 상태에서든 데이터 오류/후보 만료/시장 자격 상실/한도 초과/세션 종료는 `Invalidated` 또는 주문 차단 사유로 기록한다. **신호 1개는 유일한 `symbol|session|family|pattern|episode|sequence` ID 1개**를 가진다. 보유/미체결과 겹치면 동일 ID의 주문 반복 금지. 청산 후에는 `조건이 계속 true`라는 이유만으로 재주문하지 말고 **새 저점·새 고점·새 박스 반응 등의 신규 사건**을 필요로 한다.

공통 신호 기록: `symbol`, `market`, `family`, `entryPattern`, `episodeId`, `time`, `rawTickSequence`, `referencePrice`, `thresholdsSnapshot`, `regimeSnapshot`, `stage1Snapshot`, `technicalStopAnchor`, `candidateLow/High`, `spread/quoteQuality`, `configurationHash`, `reason`.

### 5.1 하락 반등(`DowntrendRebound`)

**선행조건:** 확정된 `Downtrend` 또는 명시된 수동 선택 하락 전략이 요구하는 유효 하락 구조. 하락 중 단순 1~2틱 상승은 반등 아님.

- 관측 기간 최고가 `H_drop(t)`는 시점 `t`까지 알고 있는 데이터만 사용.
- `DropPct=100×(1−P_t/H_drop)`.
- `RequiredDropPct=max(MinDropPct, DropAtrMultiplier×ATRpct)`.
- `DropPct >= RequiredDropPct`에서 후보 생성. 후보 저점 `L=min(trades since candidate)` 갱신.
- 마지막 새 저점 후 `LowStabilizationDuration` 동안 **유효 체결이 존재하며** 저점을 새로 갱신하지 않았을 때 안정화 후보.
- `ReboundPct=100×(P_t/L−1) >= MinReboundPct`.
- `Fast`: 위 반등률 최초 상향 교차 시 신호 생성.
- `Confirmed`(기본): 위 조건에 더해, **반등 시작 전에 잠겨 있던** `MicroHigh`를 `BreakBufferPct` 이상 돌파하는 신규 틱 필요. `MicroHigh`는 이전의 확정된 미세 가격구조(선행 틱/1분봉)에 기반하며, 현재 가격을 포함해 최고가를 다시 정의하고 그 고가를 돌파 대상으로 삼지 말 것. 검증 가능하도록 에피소드별 기준 생성 시각 저장.
- `P_t <= L×(1+MaxChasePct/100)` 추격 제한, `CandidateLifetime` 만료/새 저가/반등 구조 파괴 시 재설정.
- `Fast`와 `Confirmed`는 선택적 **하나의 모드**. 두 모드의 동시 중복 주문 금지.

초기값: `DropLookback=10 Minute`, `MinDropPct=1.0`, `DropAtrMultiplier=2.0`, `LowStabilization=5 Second`, `MinReboundPct=0.4`, `MicroHighLookback=10 Second`, `BreakBufferPct=0.1`, `CandidateLifetime=3 Minute`, `MaxChasePct=1.0`, `EntryMode=Confirmed`.

### 5.2 상승 추세: 고점 돌파(`UptrendBreakout`)

**선행조건:** committed `Uptrend`.

- 이전 유효 확정봉 또는 독립 스윙 구조의 `H_break=max(confirmed prior highs within BreakoutLookback)` 산출. 현재 진행봉/현재 틱 고점은 기준값에 불포함.
- `P_t >= H_break×(1+BreakoutBufferPct/100)`에서 후보 생성, 기준가격 고정.
- 연속 `HoldDuration` 동안 가격이 `HoldFloor=H_break×(1+BreakoutBufferPct/100)` **아래로 내려가지 않아야** 함. 이 기간 **MinValidTicks** 이상의 유효 체결을 확인해야 하며 틱이 오지 않는 시간은 증거로 세지 않음.
- `P_t <= H_break×(1+MaxChasePct/100)`; 추격 시 신호 무효.
- 돌파 실패, 데이터 결손, 후보 유효시간 만료 시 취소. 같은 이전 고점을 재사용하는 반복 돌파 에피소드에는 의미 있는 재진입을 위한 새 하락/재상향 교차와 쿨다운이 필요.

초기값: `BreakoutLookback=20 Minute`, `BreakoutBufferPct=0.1`, `HoldDuration=2 Second`, `MinValidTicks=2`, `CandidateLifetime=2 Minute`, `MaxChasePct=0.5`.

### 5.3 상승 추세: 눌림목 반등(`UptrendPullback`)

**선행조건:** committed `Uptrend`; 상승 추세의 가격 구조 무효 시 신규 진입 취소.

- 최근 확정 상승 파동의 고점 `H_impulse`를 기록. `PullbackPct=100×(1−P_t/H_impulse)`.
- `MinPullbackPct <= PullbackPct <= MaxPullbackPct`.
- `EMADistancePct=100×abs(P_t−EMA(Period))/EMA(Period)`; `<= MaxEMADistancePct`. EMA는 선택한 기준 봉(기본 1분봉) *확정*값을 사용하여 진행봉 변화에 따라 기준이 흔들리지 않게 함.
- 하락 중 새 저점 `L_pullback` 기록. `100×(P_t/L_pullback−1) >= MinReboundPct` 신규 상향 교차.
- 반등 후보 생성 당시까지 알려진 미세 고점 `H_micro`를 `MicroBreakBufferPct`만큼 재상향 돌파.
- `P_t`가 설정한 최대 추격폭을 넘거나 유효 상승 추세가 깨지면 신호 폐기.

초기값: `EmaPeriod=20`(1분 확정봉), `ImpulseLookback=30 Minute`, `MinPullbackPct=0.3`, `MaxPullbackPct=2.0`, `MaxEMADistancePct=0.3`, `MinReboundPct=0.25`, `MicroHighLookback=10 Second`, `MicroBreakBufferPct=0.1`, `LowStabilization=3 Second`, `CandidateLifetime=5 Minute`, `MaxChasePct=0.8`.

### 5.4 박스권: 하단 반등(`RangeLowerBounce`)

**선행조건:** committed `Range`이며 잠긴 박스 `[Lower,Upper]`가 유효함.

- 하단 접근: `P_t <= Lower×(1+ApproachPct/100)`, 단 하단 붕괴 수준 이상이어야 함.
- 접근 시점 이후 유효 틱의 후보 저점 `L_candidate` 추적.
- `100×(P_t/L_candidate−1) >= MinReboundPct` 신규 상향 교차 후 진입.
- 후보 기간 중 `P_t < Lower×(1−BreakTolerancePct/100)`가 되면 박스 하단 무효; 다음 신규 박스 확인 전 재사용 금지.
- `P_t <= Lower×(1+MaxChasePct/100)`.

초기값: `ApproachPct=0.3`, `MinReboundPct=0.25`, `BreakTolerancePct=0.3`, `LowStabilization=3 Second`, `CandidateLifetime=3 Minute`, `MaxChasePct=1.0`.

### 5.5 박스권: 상단 돌파(`RangeUpperBreakout`)

**선행조건:** committed `Range`의 **잠긴** `Upper`가 유효함.

- `P_t >= Upper×(1+BreakoutBufferPct/100)`에서 돌파 후보 생성.
- `HoldDuration` 동안 기준 가격 아래 이탈 없음 및 `MinValidTicks` 유효 체결 확인.
- `P_t <= Upper×(1+MaxChasePct/100)`.
- 돌파 에피소드마다 최대 한 건의 매수 신호. 해당 박스가 상방 돌파되면 박스 상태를 `BrokenUp`으로 전환하여 하단 반등·상단 재돌파의 과거 박스를 재사용하지 않는다.
- 상승 상태 분류가 2개 확정봉으로 변경되기 전이라도 **현재 박스의 유효 상단 돌파 신호 자체는 허용**. 같은 순간 `UptrendBreakout` 별도 주문 금지.

초기값: `BreakoutBufferPct=0.1`, `HoldDuration=2 Second`, `MinValidTicks=2`, `CandidateLifetime=2 Minute`, `MaxChasePct=0.5`.

### 5.6 공통 패턴 우선순위

- 시장 가족이 **하락**이면 하락 반등 1개 패턴만 평가.
- 시장 가족이 **상승**이면 고점 돌파/눌림목 두 패턴을 모두 감시. 동일 틱에서 두 패턴이 모두 유효하면 `UptrendPullback` 우선(설정 가능한 우선순위 `PatternPriority`). **주문은 1개만**; 선택되지 않은 패턴도 진단에 남김.
- 시장 가족이 **박스**이면 하단 반등/상단 돌파를 모두 감시. 유효한 상단 돌파가 확정되는 시점에는 `RangeUpperBreakout` 우선. 서로 다른 진입 패턴이 한 종목에 동시에 주문하지 않음.
- 이미 보유/매수 미체결/매도 미체결/주문 미확인 상태면 새로운 진입 주문 금지.

---

## 6. 전략 가족 선택·자동 전환 정책

**모드 1 — `Manual`:** `ManualFamily`가 하락/상승/박스 중 하나. 이 가족의 진입 신호만 평가하되, 해당 가족의 **시장 구조 필수조건은 여전히 요구**한다. 단순히 수동 선택했다는 이유로 하락장에서 상승 추세 돌파를 허용하지 않는다. 수동 모드는 일중 자동 전환 없음.

**모드 2 — `Automatic`:** 확정된 시장 상태의 가족을 선택하며 `Unknown`은 신규매수 보류. `AutoSwitchAfterExit`를 사용자가 ON/OFF로 정함.

- `AutoSwitchAfterExit=true`: 전량 청산 및 미체결/주문 대사 완료 후, 신규 진입 후보를 평가하기 **전** 가장 최근에 확정된 시장 상태로 다음 전략 가족을 재선택. 보유 중에는 변경 금지.
- `AutoSwitchAfterExit=false`: 종목별 그 거래일 최초 유효 분류/가족 선택값을 `LockedFamily`로 고정. 하루 중 다른 가족으로 변경하지 않으며, 기존 가족의 구조가 무효이면 매수 보류. 거래일 바뀌면 새 선택 가능.
- 한 번 보유한 포지션의 `EntryFamily`, `EntryPattern`, `TechnicalStop`, TP 진행상태, 설정 스냅샷은 청산까지 잠금.
- `Stage1` 활성화/갱신/붕괴는 가족 선택을 자동 변경시키지 않음. Stage1 허가시간은 전략 전환·부분익절·전량익절 때문에 리셋되지 않음.
- 전략 수동 선택을 실행 도중 바꾸는 것은 기존 보유·미체결이 정리된 다음 신규 진입부터 적용. 중간 설정 변경이 기존 청산 조건을 소급 변경하지 않음.

---

## 7. 공통 매매 가능성·위험 필터

### 7.1 신규 매수 전 모두 통과해야 할 게이트

1. 해당 시장·세션에서 그 주문유형의 거래가 실제로 허용되고 거래소 휴장/정지 아님.
2. 전략이 활성, 종목의 유효 가족과 패턴 확인, Stage1 ON 시 활성 허가 존재.
3. 지표/확정봉/체결/호가의 품질과 워밍업이 충분하고 **모두 해당 시각 기준**.
4. 종목당 보유 0, 신규매수 미체결 0, 청산 미체결 0, 주문 상태 미확인 0.
5. 재진입 쿨다운 및 하루 진입 횟수 한도, 일일 손실·연속 손절·동시 포지션 한도 통과.
6. `bid>0`, `ask>=bid`, 양쪽 유효 수량 존재, 데이터 소스·시간이 유효함.
7. `SpreadPct=100×(Ask−Bid)/((Ask+Bid)/2)`가 허용치 이하, 상위 호가 체결 가능 수량·기준 통과.
8. 기대 매도 가격에서 수수료·세금·스프레드·허용 슬리피지를 반영한 **최소 순보상**을 확보 가능. 보상을 추정할 정보가 부족하면 통과로 취급하지 않음.
9. 허용 매수 지정가, 주문 가능 현금, 통화별 노출 및 손절 위험예산 통과.

펜니스탁은 스프레드가 넓고 수량이 얕을 수 있다. `US`와 `KRX`에 각각 설정 프로필을 저장하고, 매도가능잔고/통화/호가/거래세 차이를 반영한다. 미국 장전·장후는 브로커가 실제 주문 가능과 호가/체결 데이터를 제공하는 것을 확인한 경우에만 허용(기본 정규장만).

### 7.2 공통 초기 품질·유동성 파라미터

| 키 | 초기 실험값 | 의미 |
|---|---|---|
| `MaxTradeAge` | `2 Second` | 거래소 시각 기준 최근 체결 유효기간 |
| `MaxQuoteAge` | `1 Second` | 유효 bid/ask 유효기간 |
| `MaximumProcessingLag` | `2 Second` | 큐/처리 지연 최대 허용 |
| `MaxSpreadPct` | `0.50%` | 종목별 프로필에서 변경 가능 |
| `MinBestBidQuantity` | `100` 주 | 수량 없는 호가 매수 불가 |
| `MinBestAskQuantity` | `100` 주 | 수량 없는 호가 매수 불가 |
| `MinEntryNetRewardPct` | `0.20%` | 비용 차감 뒤 목표 기대여유 최소 |
| `RequireContinuousTrades` | `true` | 자료 단절/순서 확인 |
| `RequireVerifiedExchangeTime` | `true` | 거래소 시각 신뢰 |
| `RequireTradableInstrument` | `true` | 거래정지·위험종목 제외 |
| `RegularSessionOnly` | `true` | 장외 주문은 별도 검증 전 OFF |

수량 100주 같은 초기값은 페니스탁·고가주에 항상 적합하지 않다. 금액 기준 호가 규모와 비교하는 선택 파라미터(`MinBidNotional`, `MinAskNotional`)도 지원한다. 주문량이 유효 호가 수량을 넘는다면 주문 금액을 임의로 무한 축소해 진입하지 않고 **설정된 최대 호가 사용 비율** 이하로 수량을 산정하거나 거래 차단한다. 요구된 기본 동작은 불충분 시 **차단**이다.

### 7.3 통화별 위험예산/수량

`AvailableCash = verifiedCash − activeBuyReservations`  
`RemainingExposure = MaxConcurrentExposure − (marketValueOfOwned + reservedBuyNotional)`  
`CashBudget = min(PlannedInvestment, MaxOrderCash, AvailableCash, RemainingExposure)`.

손절 기준가격은 진입 당시 `ReferencePrice`보다 낮아야 함.

`RiskPerShare = EntryReference − EffectiveStop + EntryFeePerShare + ExitFeePerShare + EstimatedSellTaxPerShare + SafetySlippagePerShare`  
`CashQty=floor(CashBudget / CostPerShare)`  
`RiskQty=floor(RemainingPositionRiskBudget / RiskPerShare)`  
`Qty=max(0,min(CashQty,RiskQty,VerifiedQuoteCapacityQty))`.

모든 비용은 해당 통화로 계산. KRW·USD를 단일 금액 잔고처럼 합쳐 쓰지 않음. 교차통화 총자산 통계가 필요하면 **시점별 검증된 FX**가 있을 때만 계산하며 손실/노출 한도는 시장/통화별 독립 정책 우선.

기본 추가매수 금지. 초기 매수 부분체결은 **동일 주문의 남은 체결**로서 허용하지만, 체결 중 새로운 추격·물타기 주문을 만들지 않음. 매도 주문 중에도 새로운 진입 불가.

---

## 8. 공통 손절·익절·추적손절 엔진

### 8.1 반드시 하나의 설정 세트

모든 `EntryPattern`이 `CommonExitConfig`, `CommonRiskConfig`, `CommonReentryConfig`를 참조한다. 패턴별로 같은 `TakeProfit1Percent`를 복제·별도로 저장하지 않는다. 패턴별 저장값은 진입 시점의 **기술적 손절 가격 근거**만 다름.

**고정 손절**: `FixedStop = AvgFillPrice×(1−FixedStopLossPct/100)`.

**기술적 손절(종목·패턴별 진입 시 스냅샷):**

- 하락 반등: `CandidateLow×(1−TechnicalBreakBufferPct/100)`.
- 상승 고점 돌파: `PreBreakoutResistance×(1−TechnicalBreakBufferPct/100)`.
- 상승 눌림목: `PullbackLow×(1−TechnicalBreakBufferPct/100)`.
- 박스 하단 반등: `BoxLower×(1−TechnicalBreakBufferPct/100)`.
- 박스 상단 돌파: `BoxUpper×(1−TechnicalBreakBufferPct/100)`.

매수 전에 계산된 각각의 보호 손절선이 실제 참조 가격보다 낮아야 함. 그렇지 않으면 **주문 거절**. 매수 이후 가격구조가 변했다고 기존 기술적 손절선을 불리한 쪽으로 하향하지 않는다. 다만 사용자 설정에 따른 *안전한 방향의 상향*은 장래 고급 기능으로만 고려하고 본 명세의 기본값은 **진입 스냅샷 고정**.

고정과 기술적 손절을 동시에 사용하므로 long 포지션의 유효 보호 손절선은 `max(FixedStop, TechnicalStop, ActiveTrailingStop, ActiveBreakEvenStop)`이며, 현재가가 어느 하나라도 하향 교차하면 해당 우선순위와 사유로 청산한다. 동시에 여러 조건을 만족하면 보수적으로 위험청산 1건만 요청한다. 손절 시 가격/체결은 **보장되지 않음**.

**익절:**  
`TP1Price=AvgFillPrice×(1+TakeProfit1Pct/100)`; 최초 매수확인 수량의 `TakeProfit1RatioPct` 매도.  
`TP2Price=AvgFillPrice×(1+TakeProfit2Pct/100)`; 최초 매수확인 수량의 `TakeProfit2RatioPct` 매도.  
두 비율의 합 ≤100%, 남은 수량은 추적손절·보호 손절·세션 종료 청산으로 관리. 가격 기준은 **평균 실제 체결가**, 분할매수 추가 금지로 평균가의 변동은 최초 매수 주문의 실제 부분체결에 따른 변화만 반영.

**추적 손절:** TP1 **실제 체결** 이후 활성화(기본); `Peak`는 설정한 정책에 따라 진입 후 최고가를 유지하거나 TP1 체결 후부터 새 최고가를 사용.  
`TrailingStop=max(PreviousTrailingStop, Peak×(1−TrailingStopPct/100))`.  
TP1 매도 주문 *접수*만으로 활성화하지 않으며, 미체결 TP1를 완료 처리하지 않음. `TrailingOnly`/전량 익절 같은 향후 모드를 별도 선택 가능하더라도 기본은 분할+추적.

**본전 보호(선택):** TP1 체결 후 실제 남은 매수 수수료 및 예상 매도 비용을 고려한 본전 손절선. 기본 OFF.

**체결 의도 우선순위:** 비상중지/세션 청산(그 정책이 요구하는 경우) > 보호 손절 > TP1/TP2 단계 > 신규 진입. 사용자 중지는 **신규 진입 차단**이 기본이며 보유분을 사용자 승인 없이 임의 시장가 전량청산하지 않음.

### 8.2 공통 초기 청산 파라미터

| 키 | 초기값 |
|---|---|
| `ExitMode` | `PartialTakeProfitWithTrailing` |
| `TakeProfit1Pct` | `1.0` |
| `TakeProfit1RatioPct` | `30` |
| `TakeProfit2Pct` | `2.0` |
| `TakeProfit2RatioPct` | `30` |
| `TrailingStopPct` | `0.7` |
| `TrailingActivation` | `AfterTP1Filled` |
| `TrailingPeakBasis` | `FromEntry` |
| `FixedStopEnabled` | `true` |
| `FixedStopLossPct` | `1.0` |
| `TechnicalStopEnabled` | `true` |
| `TechnicalBreakBufferPct` | `0.3` |
| `BreakEvenAfterTP1` | `false` |
| `ExitSignalEvaluation` | `OnTick` |
| `ClosePositionsAtSessionEnd` | `true` (시도, 체결 보장 아님) |
| `StopNewEntriesBeforeClose` | `10 Minute` |
| `StartLiquidationBeforeClose` | `5 Minute` |

장 종료 시간을 거래소 캘린더로 가져와 `session.Close − DurationSpec`로 계산한다. 마감 시각·주문 가능 시각은 브로커별 정책 검증. 만료/불능 체결 시 `UnclosedPosition`과 계좌 대사 필요 상태로 남길 것.

---

## 9. 재진입·포지션·전략 전환

### 9.1 진입 금지 상태

`Holding`, `PendingBuy`, `PendingSell`, `CancelPending`, `UnknownOrder`, `AwaitReconciliation`, `SessionClosed`, `ExceededRisk`, `UnknownRegime`, `Stage1NotActive`, `LiquidityRejected`에서는 신규매수 불가. 일부 거래가 체결돼 포지션이 남으면 동일 종목 추가매수/새 패턴 진입 금지.

### 9.2 청산 후 재진입

- **완전 청산 확인 시각**을 기준으로 `MinReentryInterval` 및 청산사유별 쿨다운을 적용.
- 익절 후 `AfterProfitCooldown`, 손절 후 `AfterStopCooldown`. 기본값에서 `AfterStopCooldown > AfterProfitCooldown` 검증. 사용자가 숫자를 수정해도 정책상 손절 후 더 길어야 한다면 검증 오류를 주고 임의 수정하지 말 것.
- 신규 진입은 이전 주문/매수 신호와 **다른 episodeId** 필요. 같은 이전 고점/저점을 기준으로 계속 참인 조건을 새 매수로 재활용하지 않음.
- `MaxEntriesPerSymbolPerDay`, `MaxEntriesPerFamilyPerDay`, `MaxEntriesPerAccountPerDay`, `MaxConsecutiveStops`, `MaxDailyLoss`를 계좌/세션 단위로 검사. 횟수 집계는 **확인 체결된 새로운 포지션의 첫 매수**를 기준. 주문만 발행 후 취소된 건 실진입 횟수에 포함하지 않되 예약/중복 검사에는 포함.
- 가족 전환 ON이더라도 이전 가족의 손절 쿨다운은 동일 종목에 계속 적용된다. 가족을 바꿔 회피하는 것을 금지.
- 거래일 전환 시 카운터 리셋과 과거 보유/미체결 대사를 명확히 분리. 미청산이 있다면 다음 날 신규매수 금지.

### 9.3 초기 공통 리스크 파라미터

| 키 | 초기값 / 참고 |
|---|---|
| `MinReentryInterval` | `60 Second` |
| `AfterProfitCooldown` | `60 Second` |
| `AfterStopCooldown` | `5 Minute` |
| `MaxEntriesPerSymbolPerDay` | `10` |
| `MaxEntriesPerFamilyPerDay` | `50` |
| `MaxEntriesPerAccountPerDay` | `100` |
| `MaxConcurrentPositionsPerSymbol` | **`1` 고정 정책** |
| `AllowPyramiding` | **`false` 고정 정책** |
| `MaxConcurrentPositionsPerAccount` | `5` (사용자 수정 가능) |
| `MaxConsecutiveStops` | `3` |
| `MaxOrderCash` | **통화별 입력 필수**, 미설정 시 실거래 차단 |
| `MaxConcurrentExposure` | **통화별 입력 필수** |
| `MaxDailyLoss` | **통화별 입력 필수** |
| `RiskBudgetPerPosition` | **통화별 입력 필수** |
| `PlannedInvestment` | **통화별 입력 필수** |

금액은 사용자 규모에 따라 달라지므로 기존 D의 원화 실험 수치를 새 실계좌 기본값으로 복사하지 않는다. 백테스트에는 별도 명시된 가상 예산을 사용하고, 실계좌에서는 미설정·미확인 금액으로 주문하지 않는다.

---

## 10. 슬리피지 제한 지정가 주문 (`ExecutionPolicy`)

### 10.1 매수

기본 진입 주문유형은 `Limit`(가격 상한 있는 지정가). 매수 신호 기준 `SignalReference`는 **신호 발생 시 관측한 유효 체결가격**, `BestAsk`는 별도 유효 호가다.

`MaxBuyPriceRaw = SignalReference × (1+MaxEntrySlippagePct/100)`.

- `BestAsk > MaxBuyPriceRaw`이면 **주문 차단**, 유리한 체결을 가정하지 않음.
- `MaxBuyLimit = floorToValidTick(MaxBuyPriceRaw)`; 시장별 호가단위/브로커 가격규칙을 준수.
- 보수적으로 미체결 위험과 부분체결을 감수하며, 주문을 시장가로 암묵적 전환하지 않음.
- `EntryOrderTimeout` 경과 후 미체결 잔량은 취소 요청. **취소 확인 전 재주문/다른 패턴 매수 금지**. 취소 거부·응답 모호함은 상태 조회 및 계좌 대사.
- 호가 깊이의 실제 매도잔량이 목표 수량을 감당할지 검증. 유효 호가가 없으면 매수하지 않음.

### 10.2 매도/손절

기본 매도도 지정가 정책. 일반 TP 주문은 목표가와 유효 bid/ask 및 정해진 제한 가격으로 생성. 손절은 시장 상황 때문에 지정가가 체결되지 않을 수 있으므로 **손절 신호가 발생해도 실제 손실 한도를 보장하지 않는다**. 다음 정책을 명시적으로 구현:

- 청산 주문 제출→부분체결/미체결 추적→설정된 시간 경과 시 취소·현재 호가 재평가→허용된 가격 한도 내에서만 재견적.
- `ExitLimitSlippagePct` 및 `ProtectiveExitTimeout`은 설정 가능.
- 손절이 미체결이라고 새 매수를 허용하지 않음.
- 손절 실패 시 원치 않는 시장가 강제전환 금지. `EmergencyMarketFallback`은 기본 `false`이며, 실제 계좌에서 사용할 때 별도의 사용자 명시 설정·브로커 지원·최악 손실 안내 및 검증 필요.
- 부분체결된 매도 수량을 반영해 남은 잔량만 재주문. 주문 상태 미확인 시 재전송 금지.

### 10.3 주문 상태 및 멱등성

`Created -> Submitted -> PartiallyFilled -> Filled` / `CancelRequested -> Cancelled` / `Rejected` / `Unknown` 분기. 거래소/증권사 주문번호+체결 ID+신호 ID로 멱등 처리. 네트워크 타임아웃은 **거부**가 아니라 **미확인(Unknown)** 상태로 두고 조회/대사해야 한다.

### 10.4 초기 실행 파라미터

| 키 | 초기 실험값 |
|---|---|
| `EntryOrderKind` | `Limit` |
| `MaxEntrySlippagePct` | `0.20` |
| `EntryOrderTimeout` | `3 Second` |
| `ExitOrderKind` | `Limit` |
| `ExitLimitSlippagePct` | `0.30` |
| `ProtectiveExitTimeout` | `2 Second` |
| `MaxEntryQuoteParticipationPct` | `25` |
| `EmergencyMarketFallback` | `false` |
| `AllowUnverifiedLiveExecution` | **항상 false** |

수수료, 시장/날짜별 거래세, 브로커 체결 비용을 `FeeSchedule`로 분리하고, 0% 비용을 아무 설명 없는 기본값으로 사용하지 않는다. 수익/손실 판정 및 리스크 모델에 왕복 비용을 반영한다.

---

## 11. 이벤트 처리 순서 및 동시성

**단일 종목 순서 보장, 계좌 차원의 예약·위험은 원자적 처리.** 성능을 위해 여러 종목은 동시 계산할 수 있으나, 주문 제출 전 계좌 한도 검사/현금 예약은 경쟁조건 없이 직렬화·락/트랜잭션 처리.

```text
OnTradeTick(event):
  1. 거래소/세션/시각/순서/원본 품질 검증
  2. 이전까지 확정된 봉 스냅샷을 참조하고, 이벤트로 새 봉이 닫히면 완성봉 추가
  3. 새 완성봉이 생긴 경우 지표/시장 후보/연속 확인봉 갱신
  4. 현재 틱으로 Stage1 감시·만료·붕괴·새 반등 에피소드 갱신
  5. 이미 보유한 포지션의 위험·익절·청산 신호 우선 평가
  6. 보유/미체결/대사 상태면 신규매수 중단
  7. 선택 가족/자동전환 정책, 자료, 유동성, 세션, Stage1 허가 확인
  8. 해당 가족의 패턴별 상태머신을 현재 틱으로 평가
  9. 동시 신호가 여러 개면 설정된 우선순위로 1개만 선택
 10. 지정가/최악 체결비용/현금/위험예산/호가 수량 재확인
 11. 고유 신호 ID에 대해 주문 의도 예약·발행(중복 제출 불가)
 12. 사유·입력 스냅샷/계산 지표/설정 버전·결정 기록

OnQuote(event):
  - 호가·잔량·유동성·현재 주문의 체결 가능성 정보 갱신.
  - 새로운 체결 틱 없이 가격 반등/돌파 신호를 가짜로 만들지 않음.

OnClock(now):
  - 세션 종료/주문 타임아웃/Stage1 만료/입력 결손/취소·대사 점검.
  - 시세가 없는 동안 단순 시간 흐름만으로 '유지 확인' 매수 성립 금지.

OnFill(fill):
  - 실제 확인 체결만 잔고·평균가·TP 단계·피크·진입/청산 카운트에 반영.
  - 매수 최초 부분체결 후 추가 신규매수 신호 금지.
  - 전량 청산 + 미확인 주문 없음 + 쿨다운 완료 시 다음 진입 감시 가능.
```

- `SignalId`/`OrderIntentId`/`PositionId`/`EpisodeId`는 각각 역할을 구별한다. CPU/네트워크 지연으로 중복 틱·중복 체결 callback이 들어와도 중복 주문/체결 집계 금지.
- 최근 시장 분류, 봉·기준 저점/고점·박스, 에피소드, Stage1 활성 시각, 잠긴 가족, 매수/매도 미체결, TP 상태, 누적 리스크를 상태 스냅샷으로 관리한다.
- 설정 변경 시 **새 진입부터 새 설정 스냅샷 적용**. 이미 보유한 포지션의 손절·익절을 변경 설정으로 소급 재계산하지 않는다. 실시간 운용 중 거래 금지/안전 중지처럼 위험을 더 강하게 제한하는 전역 안전설정은 즉시 적용 가능하되 이 동작을 명시적으로 테스트.

---

## 12. JSON 설정·버전·검증

신규 전략 최상위 스키마(예시):

```json
{
  "schemaVersion": 1,
  "strategyId": "bins-strategy-v1",
  "strategyName": "Bin's Strategy",
  "market": {
    "enabledExchanges": ["KRX", "NASDAQ", "NYSE", "AMEX"],
    "regularSessionOnly": true,
    "countryProfiles": { "KRX": {}, "US": {} }
  },
  "selection": {
    "mode": "Automatic",
    "manualFamily": "Uptrend",
    "autoSwitchAfterExit": true
  },
  "regime": {
    "timeframe": { "value": 5, "unit": "Minute" },
    "lookback": { "value": 60, "unit": "Minute" },
    "confirmBars": 2,
    "minimumBars": 8,
    "atrPeriod": 14,
    "erTrend": 0.4,
    "erRange": 0.2
  },
  "stage1": {
    "enabled": false,
    "movingAveragePeriod": 240,
    "movingAverageBar": { "value": 1, "unit": "TradingDay" },
    "profileLookbackBars": 240,
    "activationDuration": { "value": 30, "unit": "Minute" },
    "breakBufferPct": 0.3
  },
  "entries": {
    "downtrendRebound": { "enabled": true, "entryMode": "Confirmed" },
    "uptrendBreakout": { "enabled": true },
    "uptrendPullback": { "enabled": true },
    "rangeLowerBounce": { "enabled": true },
    "rangeUpperBreakout": { "enabled": true }
  },
  "sharedExit": {
    "mode": "PartialTakeProfitWithTrailing",
    "takeProfit1Pct": 1.0,
    "takeProfit1RatioPct": 30,
    "takeProfit2Pct": 2.0,
    "takeProfit2RatioPct": 30,
    "trailingStopPct": 0.7,
    "fixedStopLossPct": 1.0,
    "technicalStopEnabled": true
  },
  "sharedReentry": {
    "minInterval": { "value": 60, "unit": "Second" },
    "afterProfit": { "value": 60, "unit": "Second" },
    "afterStop": { "value": 5, "unit": "Minute" }
  },
  "sharedRisk": {
    "maxConcurrentPositionsPerSymbol": 1,
    "allowPyramiding": false
  },
  "execution": {
    "entryOrderKind": "Limit",
    "maxEntrySlippagePct": 0.2,
    "entryTimeout": { "value": 3, "unit": "Second" },
    "emergencyMarketFallback": false
  }
}
```

위 JSON은 구조 예시이며 **모든 설정 키를 축약한 샘플**이다. 실제 구현에서는 이 문서의 모든 파라미터(시장 분류, Stage1, 진입 패턴 5개, 청산, 재진입, 위험, 유동성, 실행, 자료 품질, 시장별 금액 설정)를 빠짐없이 직렬화·역직렬화해야 한다.

- 모든 숫자·비율·시간의 입력 범위, 단위, 기본값과 조합 제약을 `Validate()`에서 엄격히 검사한다.
- 직렬화 이름은 안정적인 camelCase 사용, enum은 이름 문자열 권장. 파라미터 메타데이터(설명, 단위, 최소/최대, 기본값)는 타입에 한 군데 정의하여 중복 불일치 방지.
- `schemaVersion`과 전략 ID 불일치 시 명확한 오류. 기존 D/과매도 JSON은 **실행용 Bin's Strategy로 자동 변환하지 않는다**. 기존 프리셋을 선택할 수 있는 활성 경로 제거. 과거 데이터가 있을 경우 안전하게 보관하지만 새 엔진이 잘못 읽지 않도록 분리.
- 실행 시작 시 유효성 검사한 immutable configuration snapshot을 만들고, `ConfigurationHash`를 거래·재생·감사 로그에 저장한다.
- `DurationSpec`에 `TradingDay`를 섞지 않는다. MA의 기준봉 개수는 별도 `BarTimeframeSpec`이다.
- 시간 단위/수치 검증 예: `ERRange < ERTrend`, `TP2Pct>TP1Pct`, `TP1Ratio+TP2Ratio<=100`, `MaxPullback>=MinPullback`, `RangeMaxWidth>RangeMinWidth`, `AfterStopCooldown>AfterProfitCooldown`, `QuoteAge>0`, `RiskBudgetPerPosition>0`.
- 시장 전환 자동 스위치는 `AutoSwitchAfterExit` **불리언**, 숫자 값의 자동 시장별 튜닝과 혼동하지 말 것.

---

## 13. 백테스트·모의매매·실주문 안전 경계

1. **원본 틱 재생:** 원본 거래소 시각과 수신 순서, 유효 호가, 세션 정보와 데이터 품질로 동일 전략 상태머신을 실행. 매수 신호를 발생시킨 동일 틱 가격으로 즉시 전량 체결시키지 말 것. 신호 이후의 실제 관측 호가/다음 유효 이벤트에서 지정가 체결 가능성을 판단하고, 보수적 대기열/부분체결·지연 가정을 사용.
2. **확정봉만 있는 연구:** 1분 OHLC의 고가·저가 선후관계, 2초 유지, 틱 저점, 순간 스프레드와 지정가 체결을 확정할 수 없다. **틱 단타의 정밀 백테스트로 표시하거나 성과를 비교하지 말 것**. 데이터가 없으면 `UnsupportedPrecision` 또는 별도 '확정봉 근사' 결과로 분리.
3. 동일 원본 틱/설정/비용/시간 순서에 대해 **시장 분류·패턴 진입 신호·Stage1 허가·공통 청산 의도**가 백테스트와 모의운용에서 일치하도록 회귀 테스트.
4. 과거 체결량이 없는 OHLCV 매물대는 `Approximate`라는 품질을 결과에 남김. 원본 틱 기반 프로파일과 동일 정확도인 척하지 말 것.
5. 공유 계좌 시뮬레이션에서 여러 종목이 동시에 진입하면 현금/통화/예약금/노출을 공유. 독립 종목 무한 현금 백테스트로 과대평가하지 말 것.
6. 비용: 체결가, 스프레드, 왕복 수수료, 세금, 지정가 미체결 및 재호가 가정, 체결 지연, 부분체결을 반영.
7. 성과 기록: 전략 가족/패턴별 진입 수, 완료 거래 수, 승률, 순기대손익, TP1/TP2 실행 비율, 미체결률, 평균 슬리피지, 평균 보유시간, 손실·익절별 쿨다운, Profit Factor, MDD, 동시노출, 미청산 건수, 세션별 손익. 시장별 통화가 다르면 합산 가정/FX 출처를 명시.
8. 미래 데이터 누출 방지를 위해 `KnownAt`을 보존: 피벗/MA/ATR/EMA/박스/분류 스냅샷의 사용 가능 시각이 실제 신호 시각보다 늦으면 신호 생성 금지.
9. 실계좌 자동매매는 기본 OFF. 브로커/시장/호가 지원, 주문 정정·취소·대사, 부분체결/미확인 상태, 세션 종료, 통화별 위험 제한, 실제 소수점 가격/호가단위를 검증하고 **사용자가 별도 명시 승인**하기 전에는 주문 어댑터의 실거래 송신을 연결/활성화하지 말 것.
10. 가상·실거래 모드 구분은 신호 엔진과 무관한 `ExecutionAdapter`로 관리. 이미 기록된 거래를 임의로 삭제하지 말 것.

---

## 14. 필수 테스트 시나리오 및 합격 기준

아래 테스트는 자동화해야 한다. 시장 분류부터 진입, 주문, 청산까지 단위/통합/회귀/재생 테스트를 구분하고, **테스트 데이터·예상 결과·실제 결과·실행 명령**을 남길 것.

### A. 데이터/지표/시장 분류

- A01: 미래 봉·미완성 봉을 넣어도 시장 분류 및 이전 고점값에 반영되지 않음.
- A02: 같은 자료에서 1/3/5/15분봉 선택에 따라 서로 다른 분류 봉을 사용하고, 실제 진입은 틱마다 평가됨.
- A03: 60분/15분/최소8봉 같은 부적절한 설정은 몰래 값 변경 없이 명확한 오류 또는 `Unknown`.
- A04: 상승 구조+ER+변동성 만족 → 2연속 확정봉 뒤 Uptrend.
- A05: 하락 구조 만족 → 2연속 확정봉 뒤 Downtrend.
- A06: 양쪽 상·하단 각 2회 독립 반응, 저ER, 내부 체류 → Range.
- A07: ER 낮지만 뚜렷한 상·하단 반응 없음 → Range 아님/Unknown.
- A08: 1개 봉에서 새 분류가 나왔다가 복귀 → 가족 변경 없음.
- A09: 자료 중복/시간 역전/세션 끊김/휴장/무체결 → 신규 진입 차단.
- A10: ATR/EMA/MA/ER의 수학적 기준값을 독립 계산 예제와 비교.

### B. 선택형 Stage1

- B01: Stage1 OFF → 지지 자료가 없어도 Stage2 독립 평가.
- B02: Stage1 ON + 지지 근거 없음 → Stage2 신규매수 0.
- B03: MA+매물대 중첩 → 접근 → 최초 반등 상향교차 → 30분 활성화.
- B04: 활성 30분 이내에 2단계 여러 차례 전량청산/재진입 허용.
- B05: **같은 반등 이벤트/episode**로 틱이 계속 오면 활성시간 무한 갱신되지 않음.
- B06: 새 독립 반등 20분 뒤 발생 → 남은 시간이 아닌 새 시각부터 30분 갱신.
- B07: 지지 하단 붕괴 → 즉시 신규매수 차단, 이미 보유한 포지션은 공통 청산 규칙으로 관리.
- B08: 지지 붕괴 뒤 새 반등 확인 없이 이전 허가 재활성화 금지.
- B09: 세션 종료 시 허가 만료, 다음 거래일 자동 이월 금지.
- B10: 240 일봉 미확보 + Stage1 ON → 데이터 부족 처리, 자동 MA 기간 축소 금지.

### C. 5가지 진입 유형

- C01: 하락 반등 Fast vs Confirmed는 동일 데이터에서 확인 조건 차이로 신호 차별화.
- C02: 저점 갱신 시 안정화 타이머 리셋; 반등 단순 유지 중 중복 주문 없음.
- C03: 고점 돌파 직후 2초 미만이면 신호 없음; 2초 동안 틱 누락이면 신호 없음.
- C04: 고점 돌파 후보가 2초 유지+최소 유효 틱을 만족하면 단일 신호 발생.
- C05: 상승 눌림목은 MA 접근, 조정률, 반등률, 단기 고점 돌파 **모두** 만족할 때만 진입.
- C06: 눌림목 2% 이상 과도 이탈 및 상승 구조 붕괴 시 후보 취소.
- C07: 박스 하단 접근만으로는 매수하지 않음; 반등률 상향교차 필요.
- C08: 박스 하단 붕괴 시 과거 박스 후보 재사용 불가.
- C09: 박스 상단 돌파 및 2초 유지 후 **RangeUpperBreakout 1개만** 발생.
- C10: 박스 상단 돌파와 상승 고점 돌파가 동일 틱에 유효해도 중복 주문 없음.
- C11: 모든 진입 패턴에서 최대 추격 거리를 넘으면 매수 신호/주문 차단.
- C12: 진입 조건 충족 → 보유 중 추가매수 금지, 종목 동시 포지션 1개.

### D. 선택·전환·청산·재진입

- D01: Automatic+Switch ON → 청산 후 새 시장 가족으로 전환 가능, 보유 중 변경 없음.
- D02: Automatic+Switch OFF → 종목 당일 가족 고정, 다른 가족 조건 발생해도 변경 없음.
- D03: Manual → 선택한 가족만 신호 평가, 해당 시장 조건 없으면 진입하지 않음.
- D04: TP1 30%/TP2 30%/잔량 40%와 수수료·정수 수량 처리가 실제 확인 체결 기준.
- D05: TP1 주문만 접수되고 미체결일 때 TP1 후 추적손절 활성화 금지.
- D06: 고정손절과 기술손절 동시 작동, 하나의 청산 주문 의도만 발행.
- D07: 추적손절은 최고가 상승 시에만 상향; 기존 지지/저점이 내려가도 손절선 하향 금지.
- D08: 청산 후 동일 true 상태로 재매수하지 않음; 새 episodeId 필요.
- D09: 손절 쿨다운이 익절 쿨다운보다 길고 전략 가족 변경으로 회피되지 않음.
- D10: 일중 횟수·연속 손절·계좌 손실·통화별 위험 초과는 매수 차단.
- D11: 세션 종료 전 신규 매수 제한, 전량청산 **시도**와 미체결 상태 기록.

### E. 주문·품질·실계좌 보호

- E01: bid/ask 미확인, 과도한 스프레드, 호가량 부족, 틱/호가 지연 → 신규매수 0.
- E02: Ask가 허용 매수 상한을 넘으면 주문 없음.
- E03: 지정가 주문 미체결/부분체결/취소 요청 중 새 매수 금지.
- E04: 네트워크 타임아웃→Unknown; 주문번호 조회 전 재전송 금지.
- E05: 손절 지정가 미체결시 손실보장 표현·자동 시장가 전환 없음.
- E06: KRW/USD 계좌 현금·위험 한도 혼합 금지.
- E07: 한국·미국 호가단위/시간대/정규장·장외 정책을 각각 검증.
- E08: 원본 틱 시뮬레이션과 단순 OHLC 근사 결과를 정밀도 레벨로 분리.
- E09: 동일 틱/설정 재생 2번 → 동일 전략 신호·상태 변화·감사 로그 해시.
- E10: 실계좌 송신은 별도 승인 전 네트워크 mock을 사용하며 실제 주문 0건.

---

## 15. 구현 단계·산출물·완료 조건

1. **코드 감사**: 현재 `main` SHA와 실제 사용 중인 기존 전략 의존 구조/브로커/데이터 경로를 도식화하여 삭제 대상·유지 대상 확정. `AGENTS.md`, `TASKS.md`, `HANDOFF.md`를 읽되 **이 문서의 사용자 신규 요청을 우선**. 구 코드를 별도 보존 전략으로 남기지 않음.
2. **새 설정 모델**: `bins-strategy-v1` 전용 immutable config, 모든 기간 `DurationSpec`, 모든 파라미터 기본값/범위/검증/JSON/해시 구현. 기존 D 파라미터를 새 config에 억지로 연결하지 않음.
3. **시장 데이터·지표**: 공통 시점별 입력, 틱·완성봉, EMA/ATR/ER/가격구조, 안정적 박스 경계/접촉 에피소드, 거래소 캘린더 및 워밍업.
4. **시장 분류**: 4상태, 2개 확정봉 전환, `Unknown` 차단, 박스 돌파 특례, 설정별 계층 테스트.
5. **선택형 Stage1**: MA+프로파일 중첩, 접근/최저가/반등 상향교차, 30분 허가, 새 이벤트 갱신, 붕괴·만료·재활성화 및 보유 포지션 불간섭.
6. **5개 진입 패턴**: 독립 상태머신 구현, 하락 Fast/Confirmed, 상승 고점/눌림목, 박스 하단/상단. 모든 신호 ID 멱등성·우선순위 검증.
7. **선택·전환 정책**: Manual/Automatic, `AutoSwitchAfterExit` ON/OFF, 종목별 당일 잠금, 전량청산 이후 시장 재분류 재사용.
8. **공통 청산·재진입·위험**: 기술적 스톱 앵커 스냅샷, 공통 TP1/TP2/추적/고정 손절, 단일 포지션, 손절 쿨다운, 계좌·통화별 리스크.
9. **실행 어댑터**: 슬리피지 제한 지정가, 호가·체결·부분체결·취소·Unknown 대사. 우선 **mock/가상 실행**. 실제 주문 OFF.
10. **백테스트**: 원본 틱 재생, 실행 가정/세션, 성과 레포트, 전략 가족·패턴별 통계, 동일 입력 결과 재현.
11. **레거시 제거**: 사용 경로에서 과매도/D 전용 타입·전략 분기·프리셋·죽은 의존 제거, 빌드가 안전하게 통과하도록 정리. 기존 비전략 사용자 데이터/감사 거래는 삭제 금지.
12. **자동 테스트 및 문서**: 필수 A01~E10 테스트, 관련 회귀, 빌드, Windows/Mac 가능한 환경에 맞는 검증. 마지막에 `TASKS.md`/`HANDOFF.md`/전략 기술 명세 및 테스트 결과 반영.

**최종 완료 조건(Acceptance Criteria)**

- 신규 실행 전략은 정확히 `Bin's Strategy` 1체계만 존재. 레거시 과매도/전략 D가 활성 실행 경로에 남아 있지 않음.
- 시장 상태 정확히 4개(Unknown 포함), 전략 가족 3개, 진입 패턴 5개, 하락 반등 모드 2개.
- 모든 기간·숫자·비율·확인 횟수·손실 한도·가격 여유값은 적합한 타입의 설정에서 읽고 검증. **임의 고정 숫자 및 시장에 따른 비공개 자동 튜닝 없음**.
- Stage1 ON/OFF·시간 갱신·지지 붕괴 차단, 자동 전략 선택·전량청산 후 전환 ON/OFF가 명세대로 작동.
- 시장 분류는 확정봉, 진입/보호 청산은 틱. 미래 데이터 사용 없음.
- 5가지 패턴은 동일한 공통 익절·손절·리스크 파라미터를 사용하며 기술적 손절의 진입 기준 스냅샷만 서로 다름.
- 한 종목의 추가매수 0건·동시 포지션 최대 1개·중복 진입 주문 0건.
- 슬리피지 제한 **지정가**, 부분체결/미체결/주문 미확인 상태가 재현되고 실패를 성공으로 위장하지 않음.
- 미국·한국 시장을 구조적으로 지원하되 데이터/브로커의 실지원 제약은 명시적으로 차단/보고.
- 원본 틱/모의운용 신호 결정성·동일 입력 재현 테스트 통과. 확정봉 근사로 틱 정밀도를 주장하지 않음.
- 실제 돈 주문 0건, 새 릴리즈/태그 게시 0건(사용자 별도 요청 전까지).
- 컴파일 및 적용 테스트 결과, 실패·미구현 지원 경계, 삭제/유지한 파일, 테스트 실행 명령을 최종 보고.

---

## 16. Codex 최종 수행 지시

**이 명세를 읽고 질의응답만 반복하지 말고 구현 가능한 범위부터 즉시 작업한다.** 불명확한 세부 규칙은 위의 기본값과 안전한 진입 차단 원칙을 채택하고, 임의로 사용자 확정 결정을 뒤집지 않는다. 모든 구현 선택을 `DecisionLog`와 테스트로 남긴다. 다만 실계좌 자동주문 승인, 기존 실제 데이터 삭제, 공개 릴리즈는 명시적 사용자 지시가 필요하므로 실행하지 않는다.

작업 우선순위는 **정확한 신호/수학적 정의 → 인과적 데이터 처리 → 포지션/주문 안전 → 테스트·백테스트 → 기존 전략 제거 확인**이다. 구현된 것과 검증되지 않은 것을 구별하고, 완료하지 못한 기능은 남은 작업과 차단 이유를 명확히 보고한다. 기대수익이나 승률을 검증 없이 보장하지 않는다.
