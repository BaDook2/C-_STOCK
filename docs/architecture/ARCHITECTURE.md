# 아키텍처

## 프로젝트 구조

- Stock.App: .NET 10 WPF 화면, WorkspaceViewModel, 명령, 차트 렌더링, 로컬 키 입력창.
- Stock.Core: 공통 종목·일봉 모델, 공개 시세 공급자, 전략·가상 체결, 키움 REST, 주문 보호, SQLite.
- Stock.Tests: 가짜 HTTP/WebSocket, 전략·회계·저장 검증, WPF 화면 통합 검사.

## View / ViewModel / Service

MainWindow → WorkspaceViewModel → IPublicMarketData / ChartStrategies / BarPaperAccount / RemoteTradingSession / TradingStore.
CandleChart는 이미 가져온 PriceBar를 렌더링하며 API나 DB를 호출하지 않는다. ConnectionDialog는 선택 증권사 연결 정보를 전달하고, WorkspaceViewModel이 실제 인증·계좌 검증 후 BrokerCredentialStore에 저장한다. 이전 MainViewModel은 호환 검증용으로 보존하며 현재 창은 WorkspaceViewModel만 사용한다.

## 데이터 흐름

공개 검색 → StockItem → 공개 일봉 → BarDataset → SQLite 캐시 + 메모리 PriceBar 배열 → 지표/전략 → 화면.
스크리닝과 백테스트는 ChartStrategies.Evaluate를 공통 사용한다. 백테스트는 종가로 판단한 주문을 다음 시가로 체결하며 미래 봉은 판단에 전달되지 않는다. 모의매매 재생은 진행한 봉만 화면에 전달한다.
실계좌 주문은 명시적 사용자 확인 → 자동 조회 가격과 한도 검사 → RemoteTradingSession → OrderGuard 선기록 → KiwoomBroker POST → 접수/미확인 상태 기록이다. 자동 실계좌 전략은 없다.

## DB 구조

TradingStore는 SQLite WAL 모드. 기존 presets/settings_history/events/runs/audit와 범용 state(key,json)를 관리한다. 새 화면의 workspace-v2.db에서 state는 종목 목록, bars:<종목>, chart-settings, chart-preset:<이름>, chart-run:<ID>를 보관한다. 실행 결과에는 설정, 데이터 해시, 초기자금, 손익, 체결, 자산곡선을 저장한다.
기존 stock.db는 보존한다. 실제 주문 write-ahead 저널은 계좌 해시를 포함한 별도 JSON으로 원자적으로 저장하며 실전/모의 접두사를 구분한다. 실시간 가격의 매번 조회를 DB로 수행하지 않는다. 새 화면의 데이터는 일봉이며 실시간 스트리밍 화면은 아니다.

## 외부 API

PublicMarketData: 네이버 이름 검색·목록·업종·국내 일봉, Yahoo 미국 일봉. 인증키 없음, 오류 시 합성 자료 대체 없음.
KiwoomBroker: REST 인증·현재가·계좌·주문·취소. live=true로 명시할 때만 운영 호스트. 증권사 키와 공개 공급자 HTTP 클라이언트는 분리한다.
KiwoomMarketDataProvider: 기존 WebSocket 틱·호가 엔진용. 현재 새 UI에는 연결하지 않는다.

## 오류와 검증

명령 실행 예외는 상태 메시지로 표시한다. 공개 조회 실패·누락은 결과에서 제외하고 표시한다. 주문 응답 불명은 계좌 차단 상태로 유지하고 재전송하지 않는다. 테스트는 외부 주문을 전송하지 않으며 공개 시세 읽기 통합 검사와 오프라인 가짜 응답 검사를 분리한다.

## 2026-09-28 확장
WorkspaceFeatures는 WorkspaceViewModel의 모드 인증, 시장 갱신, 기록, 차트 주기, 틱 구독을 분리한 partial 구현이다. IMarketOverviewData는 지수/종목 스냅샷을 제공한다. 탭 활성 시 5초 DispatcherTimer로 조회하며 중복 호출은 건너뛰고 수명 취소 토큰으로 종료한다. 일평균 거래량은 메모리의 확정 일봉 20개로 계산한다.
ChartPeriods는 주·월 OHLCV를 집계한다. SavedBacktest는 기존 chart-run: JSON과 호환되며 TradingStore.States로 목록을 복원한다. UI 정수 표시와 원본 decimal 계산을 분리한다.
틱은 독립 KiwoomBroker 토큰과 실전 KiwoomMarketDataProvider 구독을 사용한다. 모드 인증 실패·종목/주기 변경·종료 시 구독을 취소한다. DataQualityGate로 중복·역순·낡은 시각을 배제하고 실제 체결 최대 2,000개를 메모리에서 표시한다. 지지저항·매물대와 전략 일치는 수신 이벤트에서 계산하며 자동 주문 경로에 연결하지 않는다. live-ticks: 상태는 이전 수신 틱의 오프라인 재표시용이다. 실시간 가격마다 DB를 읽거나 쓰지 않는다.

## 관심종목 및 지수 미니 차트
WatchlistViewModel partial은 관심 그룹·종목 등록/해제·그룹 이름 편집·공개 시세 조회를 담당한다. WatchGroupData[]를 watch-groups 단일 상태에 저장하며 Quote 자체는 영구 저장하지 않는다. WatchEntry는 종목별 시세 상태를 분리하고 조회 중 그룹 전환 시 이전 결과가 새 그룹의 다른 행에 적용되지 않게 한다.
IIndexChartData → IndexTrend(전일 종가·실제 관측 시간/가격 배열·현재 값) → IndexCard → IndexSparkline으로 렌더링한다. 국내/해외 선택은 검색 시장 필터와 독립적이다. 시세·추이 오류는 카드별로 분리한다. 서로 다른 범위의 비동기 요청이 끝나도 선택한 시장의 카드만 표시한다.

## 증권사 선택·키 저장·관심그룹 스크리닝
IAccountBroker가 증권사 ID/계좌/시장/현재가/주문/취소를 통일한다. KiwoomBroker·KisBroker(live:true)·MeritzBroker를 CreateAccount에서 선택한다. 세션은 언제나 하나이며 계좌를 바꾸면 기존 세션을 폐기한다. 저장 키 전체를 열거나 주문을 순회하는 경로는 없다. RemoteTradingSession 주문/취소 저널은 증권사 ID + 실전 여부 + 계좌 해시로 분리하며 기존 키움 저널 이름과 호환된다.
BrokerCredentialStore는 DPAPI CurrentUser (CryptProtectData/CryptUnprotectData), 임시 암호문 파일과 원자적 이동을 사용한다. 실패한 연결은 저장 함수를 부르지 않는다. 기본 증권사는 별도 비밀 없는 설정이며 인증 성공 후에만 변경한다. UI가 항상 연결창을 거치므로 앱 시작 시 자동 온라인 진입하지 않는다.
관심그룹은 안정적인 그룹 ID를 선택 상태로 저장한다. 관심종목 스크리닝은 그룹 합집합을 StockItem.Key로 중복 제거한 뒤 국가·업종을 필터링하고 기존 ChartStrategies를 사용한다. 상위 시장 목록과 관심그룹 경로는 분리되며 관심그룹에 ScreeningSize를 적용하지 않는다.
Enter 검색과 마우스/키보드 종목 열기는 UI 이벤트 → 명령/VM OpenStock → 기존 LoadData 경로를 쓴다. 틱·일·주·월은 라디오 탭으로 동일 ChartPeriod를 변경한다.

## 2026-09-29 전략·차트 확장
- StrategyParameters의 Standard/Advanced/Risk/Execution 모델과 ParameterCatalog를 UI와 엔진이 공유한다. StrategyPresetMapper의 중앙 표로 변환하며 엔진은 동일하다. 새 설정은 opt-in이고 null Advanced는 기존 판단을 그대로 반환한다.
- StrategyEditorViewModel → StrategyParameterPanel → ParameterHelpDialog. UI에서 조건 계산을 하지 않는다. 표준/고급 모드 및 프리셋은 기존 SQLite state를 사용한다.
- ConfiguredBacktest / ConfiguredPractice / ConfiguredScreening은 저장된 KIWOOM 실제 이벤트를 재사용한다. 실시간 수신 이벤트는 메모리에서 처리하고 128개 묶음 및 종료 시 저장한다.
- AdvancedStrategyGate는 기존 세 전략을 감싸며 추가 조건을 평가한다. AdvancedPaperRisk는 로컬 PaperBroker 엔진에서만 호출한다. 실계좌 공유 RiskManager와 RemoteTradingSession의 주문 경로는 별도다. 실계좌 자동 전략 주문을 추가하지 않았다.
- PriceBar.IsComplete로 표시용 최신 데이터와 확정봉 연구 자료를 구분한다. null 보조 데이터는 거래 방향·뉴스 등으로 추정하지 않는다.
- TradeHistoryViewModel은 확인된 체결을 기존 state에 영속화하고 선택 계좌만 차트에 전달한다. 차트는 주기별 매매 위치와 기간 선택을 처리한다.
- TossBroker는 IAccountBroker 및 IMinuteHistoryBroker를 구현한다. 선택 accountSeq 하나만 요청하며 POST 재시도는 하지 않는다.

## 2026-09-30 파이프라인·정기매수
PipelineConfig/중앙 Mapper/Metadata → PipelineEvaluator의 4단계 필터 → TickScalping 또는 Farmer → PortfolioPolicy → 기존 TradingEngine/Risk/PaperBroker.
UI는 모델 편집·서비스 호출·결과 바인딩만 담당한다. 저장은 기존 TradingStore state/프리셋을 재사용한다.
순위 Provider는 가격 Provider와 분리하고 관측 시점/유효기간을 검증한다. 실시간 관측 갱신 주기는 Universe 설정을 사용한다.
정기매수는 Core Research 서비스에서 확정 일봉과 RecurringPurchaseConfig로 계산하며 가상/실제 주문 세션과 별개이다.
상세 모듈·저장 키·한계: ../pipeline-recurring-2026-09-30.md

## 2026-09-30 원본 틱
WPF ScottPlot.WPF 5.1.59 SignalXY를 별도 RawTickChartView로 통합. ViewModel은 취소/generation/좌표 준비, Core Repository/Window는 원본 조회와 제한된 캐시를 담당한다. 상대 초 double축 + 원본 UTC ticks/row ID를 병행 유지한다.
기존 events 복합 index를 추가하고 별도 읽기 연결에서 keyset query. SQLite/백테스트는 차트 cap과 독립이다. StreamingBacktest는 기존 TradingEngine을 사용하여 전체 원본을 소비한다. Fill/BarFill의 원래 이유·주문번호를 보존하고 TickTradeMatcher가 FIFO 부분 체결을 연결한다.
상세: ../ui/RawTickPage.md, ../api/RawTickData.md, ../performance/RawTick.md.
## 실계좌 다중매매와 차트 도구 (2026-10-03)
MultiTradeDialog → MultiTradeViewModel → WorkspaceViewModel.MultiTradeWorkspace → LiveStrategyBatch.
종목별 MarketContext/기존 AdvancedStrategyGate를 독립 생성하고 실행 당시 설정을 복제한다. 실제 KIWOOM feed는 worker에서 처리하며 DB는 원본/주문/체결/상태 변경 때 저장한다. 동일 상태 반복 저장을 생략한다.
주문은 공통 RemoteTradingSession 계좌 lease/직렬화 → OrderGuard write-ahead → 선택 IAccountBroker. 실제 IExecutionHistoryBroker 체결 ID로만 소유 수량/평균가/실현손익을 변경한다. PaperBroker는 기존 RiskManager에 전달할 읽기 스냅샷으로만 사용되며 시세를 통한 모의 체결에는 사용하지 않는다.
차트 오른쪽 3탭과 독립 scroll, 중앙 Auto/* stretch, 원본 편집값을 보존한 읽기 % 템플릿은 docs/ui/ChartTools.md 참고. 지원 범위와 저장/수명주기는 docs/ui/MultiTrade.md, API 계약은 docs/api/LiveBatchKiwoom.md.

## 국내 과매도·낙주 일봉 백테스트 (2026-10-03)
- 기존 WPF/MVVM/Core/SQLite 경계를 유지한다. OversoldWorkspace → OversoldBacktest → 일별 Equity/Trades/Signals/Decisions/Transitions → 기존 state/테스트 기록.
- Config/Metadata: Research/Oversold/OversoldConfig.cs. 범위와 설명은 ParameterAttribute, 교차검증은 Config.Validate. UI ParameterField/공통 ParameterHelpDialog 재사용.
- HistoricalFacts는 IHistoricalUniverseProvider/IFundamentalRiskProvider/IDailySessionProvider 구현. KnownAt 기준 최신 당일 사실만 허용. 현재 분류·risk false 기본값 소급 금지. 캘린더 입력 필수.
- SupportProviders는 MA, 오른쪽 확인 후 엄격 고점, 일봉 범위 균등배분 근사 거래량 구간. 캐시 순차 append, 날짜당 후보 한 번 생성, 인접가격 연결식 군집.
- OversoldBacktest는 단일 종목 상태·자본을 소유. 신호는 마감, 체결은 다음 거래가능 Open. 순서: 이전 주문 실제모형 체결 → 현재 확정봉 지표 → 위험/익절/진입 판단 → 비용포함 평가. 손절은 미체결 익절보다 우선. 최초 cluster 불변.
- PerformanceCalculator는 완결 거래 통계와 전체 비용포함 평가곡선의 CAGR/MDD/Sharpe를 분리. 미청산 제외/0분모 미산출.
- 결과는 OversoldResultId로 기존 SavedBacktest에 연결. 현재 자료와 별도로 해시 기반 facts/일봉 snapshot 저장. 상세한 시점/설정/거래 감사 정보 유지.
- 실제 계좌 다중매매의 StrategyType/LiveCapability/OrderGuard 경로는 이 전략으로 확장하지 않는다. 별도 승인 없는 실제 자동 주문 없음.

## 2026-10-03 단일 일봉 전략 제품 경계
`OnlyOversoldWorkspace.InitializeOnlyOversold`에서 공개 전략 명령을 Oversold engine으로 통일한다. `LegacyStrategiesAvailable=false`는 DB에서 복원할 수 없는 코드 경계이며 구형 configured engine/프리셋/다중매매 진입에도 적용한다. 화면에는 Oversold 설정 하나를 제공한다. 수동 paper/live 주문과 정기매수는 별도 도구로 유지한다. 틱 피드의 engine에는 빈 strategy 목록을 전달해 시세 수집만 유지한다. 과거 engine 구현과 저장 데이터는 호환 기록·Core 회귀를 위해 보존한다.

낙주 프리셋은 기존 SQLite state 내 별도 `oversold-presets-v1` / `oversold-preset:` namespace를 사용한다. 스크리닝은 관심그룹 중복 제거 → 지역/분야 → 국내 지원 여부 → 실제 과거 일봉+저장 시점 facts → OversoldBacktest의 마지막 확정일 신호 조회로 연결한다. 자료 부족은 통과하지 않으며 예전 전략으로 대체하지 않는다.

`MinimumTextSize` attached property는 RightToolsPanel에 국소 적용되어 컨트롤 템플릿과 detached popup의 최소 글자 크기를 보정한다. 전략 계산이나 API 동작은 View에 추가하지 않는다.

## 2026-10-03 파라미터 사용범위 UI
ParameterCatalog가 OversoldParameterMetadata의 명시적 사용범위/하위그룹/실제 사용 설명을 결합한다. 기존 ParameterField 인스턴스를 저장용 flat 그룹과 화면용 OversoldUsageGroups가 공유한다. 같은 Config와 validation·엔진·SQLite 저장 경로를 유지한다. 온라인 빈 영역은 미지원 설명만 제공한다. 가격자료 검증 모드를 포함한 59항목 분류와 스크리닝 영향은 docs/ui/ParameterUsageScopes.md.

## 2026-10-11 기간 스크리닝과 다량 종목 모의 백테스트

OversoldScreeningWorkspace는 기간·대상·설정·당시 자료를 한 실행 snapshot으로 저장한다. Core의 OversoldScreeningEngine은 시작일 이전 봉을 지표 준비에만 사용하고 시작일~종료일의 확정봉을 순차 평가한다. 종료일 이후 데이터는 판단하지 않는다. ScreeningNavigation은 결과 종목을 열고 저장된 기간·설정·자료로 같은 OversoldBacktest를 실행한다. CandleChart의 마커는 그 모형 체결에서 생성하며 실제 계좌 거래와 구분한다.

MultiTradeDialog/MultiTradeViewModel은 관심그룹과 종목 선택을 공통 key 집합으로 동기화하고 중복을 제거한다. 오프라인 StartMockMulti는 선택한 국내 종목마다 독립된 OversoldBacktest를 수행해 기존 SQLite state에 결과·batch 기록을 보존한다. 실행 중 선택 편집을 잠그고 중지로 잠금을 해제한다. 온라인 팝업은 현재 연결한 계좌만 표시하며 LegacyStrategiesAvailable=false 경계 때문에 자동 실행 명령이 비활성이다. 새 경로는 실계좌 주문·피드를 호출하지 않는다.

WPF 테스트는 App.ManualWindowStartup으로 테스트 호스트가 직접 창을 관리한다. 일반 앱 실행은 기존 OnStartup/단일 인스턴스 경로를 그대로 사용한다. 이 분리로 테스트용 fixture 외의 사용자 DB와 일반 앱 창이 테스트 중 열리지 않는다.

## 2026-10-04 고정 소수 입력 계층
ParameterLayoutRow(26×3칸, null은 빈칸) → OversoldWorkspace의 ParameterRow/ParameterCell → UniformGrid 행. Metadata의 InputKind로 목록과 scalar를 명확히 구분한다. ParameterField.Value는 기존 저장 원값, InputText는 고정2자리 표시·사용자 편집 진입점이다. FixedDecimalEdit는 순수 편집 규칙이며 FixedDecimalInput은 WPF routed input/paste/key/cut 연결만 담당한다. NumericDisplay는 목록 및 이 editor를 scalar 숫자로 재포맷하지 않는다. 포커스 이벤트는 반올림된 표시값을 source에 쓰지 않는다. DB/계산 엔진/기본값/온라인 경계는 유지한다.

## 2026-10-13 백테스트 진단과 스크리닝 백테스트 팝업

`OversoldBacktestService`는 종목/차트의 낙주 백테스트와 스크리닝 결과 팝업이 공유하는 단일 진입점이다. 캐시 키는 종목, 기간, 확정 일봉, StrategyConfig snapshot, HistoricalFacts snapshot의 해시다. 반환값은 깊은 복사해 UI가 캐시 원본을 바꾸지 못하게 한다.

과거 세션 배열이 비어 있더라도 실제로 존재하는 완성 일봉 날짜에서 해당 일자의 09:00~15:30 KST 세션만 파생한다. 없는 평일이나 휴장일은 만들지 않는다. 기본 전략은 종목 상태·거래대금·위험 사실이 없으면 계속 진입을 보류한다. 별도 `UsePriceOnlyBacktest`는 엔진 검증 프리셋에서만 누락 상태/위험을 일반·거래가능·위험없음으로 가정하고 거래대금 기준 0을 허용한다. 스크리닝과 실시간 주문 판단에는 이 우회값을 전달하지 않는다.

`ScreeningNavigation → OversoldBacktestService → OversoldBacktest`가 저장된 스크리닝의 기간·봉 규격·설정·사실 snapshot을 사용한다. 일봉 결과는 `ScreeningChartDialog`에서 요약, 거래표, ▲/▼, 판단 당시 Support Cluster zone으로 표시한다. 후보 행 더블클릭과 `백테스트 보기`는 같은 `OpenScreeningStock` 경로다. 이전/다음은 창을 재사용하며 이전 로드를 취소하고, 같은 조건은 서비스 캐시를 사용한다. 상세: `docs/ui/ScreeningBacktestPopup.md`.

## 2026-10-05 검증된 스크리닝 확장
- StrategyBarAggregator: 명시 세션+검증 원본으로 틱/분/시, price*size 거래대금 및 인과성.
- OversoldEntryDetector/PositionRules: 기존 일봉 Backtest에서 추출, ScreeningEngine과 공유. 기존 계산 회귀 유지.
- OversoldScreeningRun: immutable 대상/조건/최종목록, config JSON deepcopy, 기존 TradingStore.State 저장. 화면 결과는 별도 팝업이며 작업 중 최신 snapshot을 갱신한다.
- OversoldSignal/Execution optional Snapshot은 옛 저장자료 호환. ClusterAtDecision/AdditionalEntry/PositionRules 시점 보존. UI 계산 없음.
- CredentialResetService/SecretKeyInput: scope 확인→세션 revoke→기존 DPAPI 파일 삭제. 키/거래DB 저장체계를 중복 구현하지 않음.
- 새 실계좌 자동매매 라우팅은 승인 검토 거부로 미연결. OversoldLiveStrategy는 독립 FakeBroker 검사만 진행. 자세한 실제 상태: OversoldExpansion-20261005.md / docs/ui/ScreeningResults.md / CredentialReset.md.

## 현재 일봉 평가/실시간 표시 분리 (2026-10-10)
CurrentOversoldScreening은 과거 엔진의 기술적 계산을 재사용하면서 미확인 자료를 보존한다. LiveDailyWorkspace/SelectedQuotePump/DailyQuoteMerger는 화면용 진행봉을 갱신하며 연구 dataset 확정봉과 분리한다. 상세: docs/ui/CurrentDailyScreening.md. 실제 자동주문 경로는 확장하지 않았다.

## 낙주 전략 선택 계좌 실시간 실행 (2026-10-11)

현재 다량 전략 창은 모의가 기본 체크 상태다. 모의는 기존 종목별 과거 `OversoldBacktest`를 사용한다. 온라인에서 모의를 해제한 경우 `MultiTradeWorkspace`가 선택 키움 국내 계좌와 유효한 현재 자료를 검증하고, 틱/분 기준의 `OversoldLiveConfiguration`을 각 `LiveBatchItem`에 고정한다. `LiveStrategyBatch`의 계좌 점유·주문 보호·체결 대사와 `RemoteTradingSession → OrderGuard`를 그대로 사용하며, 신호 계산만 `OversoldLiveStrategy`로 분기한다. 이전 전략의 `Advanced` 개체는 실행 제한용 한도로만 쓰고 Box 신호 엔진은 호출하지 않는다. 실제 체결이 확인되기 전에는 전략 포지션이 바뀌지 않는다. 개발 검사는 FakeBroker/FakeFeed에서만 수행했다. 앞 절은 당시 상태 기록이다.

## 전략 선택과 스크리닝 병렬화 (2026-10-14)

`SelectedOversoldPreset`은 전략 설정, 종목/차트 백테스트, 종목 골라보기의 공통 선택값이다. `OversoldScreeningRun.StrategyName`에 실행 당시 이름을 저장하므로 이후 선택이 바뀌어도 과거 결과의 전략 표시가 달라지지 않는다. 두 화면 모두 같은 `OversoldConfig` snapshot과 `IOversoldBacktestService`/`OversoldScreeningEngine`을 사용한다.

시장 목록의 `Describe`와 각 종목의 스크리닝은 최대 4개씩 병렬 처리한다. 종목 내부에서는 일봉 이후 현재 상태와 현재가를 함께 조회한다. `PublicMarketData.Current`의 cadence gate/cache는 전역 1개가 아니라 symbol별 `CurrentQuoteSlot`이라 한 종목의 7초 제공자 간격이 다른 종목을 직렬 대기시키지 않는다. `bars:<종목>`은 10분 동안 재사용하고 실행 snapshot의 DB 쓰기는 8종목 단위로 제한한다. 이 최적화는 읽기/평가 순서만 바꾸며 종목별 계산과 결과 snapshot은 독립이다.

## 다중 전략 실시간 모의매매 (2026-10-14)

`MultiTradeViewModel`은 모의와 실시간을 서로 다른 설정으로 저장한다. `모의 + 비실시간`은 기존 `OversoldBacktest`, `모의 + 실시간`은 `RealtimePaperStrategyBatch`, 모의 해제는 기존 `LiveStrategyBatch`로 분기한다. 실계좌 경로의 `RemoteTradingSession → OrderGuard`와 주문 직전 상태 재검증은 변경하지 않았다.

`RealtimePaperStrategyBatch`는 실제 키움 `MarketEvent`와 기존 `OversoldLiveStrategy`/`DataQualityGate`를 재사용하고 종목별 `PaperBroker`만 소유한다. 따라서 실제 브로커 주문 API를 호출하지 않는다. 전략 신호 이벤트 다음의 실제 이벤트부터 가상 체결하고, 체결시각·가격을 `TradeMarker` source `실시간 모의매매`로 저장한다. `MultiTradeWorkspace`의 단일 WebSocket 수신 루프는 원본 이벤트 저장, 전략 평가, 선택 종목 차트의 `ObserveMultiMarket` 갱신을 함께 수행한다. 차트는 저장된 동일 표식을 틱·분·일·주 집계에 매핑하며 실시간 모드 전환과 무관하게 실시간 모의 표식을 다시 읽는다.

실시간 모의와 실계좌 모두 종목별 상태, 주문 단계 중복 방지, 피드 신선도, 정규장, 시점별 상태·위험, 검증 준비 봉을 요구한다. 전체 중지는 피드를 종료하고 신규 실행을 막지만 보유분을 임의 청산하지 않는다. 실시간 모의 정지는 가상 미체결만 취소한다.
# 2026-10-23 스크리닝 시장·지정봉 조회 경계

- `StockItem.Board`가 공개 상위 목록의 KOSPI/KOSDAQ 구분을 보존한다. 스크리닝은 종목 검색의 국내/해외 선택과 분리된 `ScreeningMarketSelection`을 사용한다.
- `PublicMarketData.Universe`는 코스피·코스닥 단독 선택 시 해당 시장만, 결합 선택 시 두 시장 각각 상위 N개를 요청한다.
- `IRangedMinuteIntervalHistoryBroker`는 기간과 지정 분 간격을 함께 받는다. 키움은 1시간봉에 `ka10080 tic_scope=60`을 사용하고, 지원하지 않는 간격만 기존 1분봉 집계 경로로 보낸다.
- `WorkspaceViewModel.ScreeningBars`의 작은 기간 캐시는 스크리닝 직후 상세 차트를 열 때 같은 네트워크 범위를 다시 받지 않게 한다. 캐시는 최대 128개, 유효기간 2분이며 원본 저장소를 대체하지 않는다.
- 상세 차트는 immutable screening snapshot의 시작일~종료일로 봉과 모든 체결 표식을 함께 자른다.

