# UI·차트·전략 설정 구현 보고 — 2026-09-29

## 1. 추가·수정 파일

| 영역 | 주요 파일 |
|---|---|
| 검색·로딩·주기·버튼 | Stock.App/WorkspaceViewModel.cs, WorkspaceFeatures.cs, ChartWorkspace.cs, App.xaml, MainWindow.xaml(.cs) |
| 차트·체결 저장 | Stock.App/CandleChart.cs, TradeHistoryViewModel.cs, Stock.Core/Research/TradeMarker.cs, WorkspaceModels.cs, PublicMarketData.cs |
| 전략 모델·실행 | Stock.Core/Trading/StrategyParameters.cs, ParameterMetadata.cs, StrategyPresetMapper.cs, AdvancedStrategyGate.cs, AdvancedPaperRisk.cs, TradingEngine.cs, Settings.cs, MarketModels.cs |
| 전략 UI | Stock.App/StrategyEditorViewModel.cs, StrategyParameterPanel.xaml(.cs), ParameterHelpDialog.cs, SettingsEditor.cs |
| 테스트·모의·스크리닝 | Stock.App/ConfiguredBacktest.cs, ConfiguredPractice.cs, ConfiguredScreening.cs |
| 토스 계좌 | Stock.Core/TossBroker.cs, BrokerConnection.cs, Models.cs, Stock.App/ConnectionDialog.cs, Assets/Brokers/Toss.ico, sources.md |
| 검증 | Stock.Tests/StrategyConfigurationTests.cs, TossBrokerTests.cs, UiRevisionTests.cs, UiTests.cs, ConnectionTests.cs, Program.cs, Stock.App/PublishedSmoke.cs |
| 문서 | docs/ui/ChartPage.md, StrategyPage.md, MainWindow.md, ScreenerPage.md, ConnectionDialog.md, docs/api/PublicData.md, Toss.md, docs/architecture/ARCHITECTURE.md, TASKS.md, VALIDATION.md |

기존 C#/.NET 10 WPF/MVVM과 SQLite를 유지했다. AGENTS.md는 실행 파일 옆에 그대로 있다.

## 2. Standard / Advanced 변환 구조

StandardStrategyConfig → StrategyPresetMapper.ConvertToAdvanced → AdvancedStrategyConfig → ToEngine → TradingEngine.
AdvancedStrategyConfig → 같은 ToEngine → 같은 TradingEngine.

강도 1~5 표는 StrategyPresetMapper 한곳에서 관리한다. Standard에서 Advanced로 전환하면 실제 계산값을 보여주며 직접 편집 시 Custom이 된다. 기본값은 Default, 핵심값 편집/프리셋은 Preset으로 구분한다. 동일 설정을 전달한 세 전략의 두 모드는 체결·손익·자산곡선이 같음을 검사했다.

새 설정 사용 체크는 기본 해제이며 기존 전략을 임의로 바꾸지 않는다. 활성화하면 백테스트·모의 재생·스크리닝에 저장된 실제 체결 전략을 사용한다. Advanced가 null인 기존 엔진 경로는 유지한다. 실계좌 자동 주문은 추가하지 않았다.

## 3. Parameter Metadata 위치

Stock.Core/Trading/ParameterMetadata.cs: ParameterMetadata, ParameterAttribute, ParameterCatalog, StrategyParameterValidation.
Stock.Core/Trading/StrategyParameters.cs: 각 파라미터의 한국어 이름, 설명, 증가/감소 영향, 단위, 범위를 선언한다. UI에 개별 설명을 복제하지 않는다.

## 4. Tooltip과 상세 Popup

StrategyParameterPanel의 각 [?]는 같은 Metadata.ShortDescription을 Tooltip으로 사용하고, 클릭하면 공통 ParameterHelpDialog를 연다. 팝업에는 이름·설명·전략 역할·높일 때/낮출 때 영향·단위·범위·현재값·닫기를 표시한다. 기존 전략 입력도 같은 도움말 컴포넌트를 사용한다. Advanced는 Signal/Entry/Exit/Risk/Execution으로 묶어 접을 수 있다.

## 5. 전략별 파라미터

- 공통 Standard: 1회 투자금, 최대 동시 보유 종목, 진입/청산 강도, 손절/익절률, 최대 보유시간, 재진입 대기, 주문 방식, 한국시간 진입 시간대.
- 급등 Standard: 거래량·상승속도·호가 활성도 강도, 거래량 감소 청산.
- 급등 Advanced: VolumeLookbackSeconds, VolumeRatioThreshold, VolumePercentileThreshold, VolumeZScoreThreshold, MomentumWindowSeconds, MinimumPriceIncreasePercent, MinimumTradesPerSecond, BuyTradeRatio, OrderBookImbalance, MaximumSpreadPercent, EntryScoreThreshold, ExitScoreThreshold, VolumeDecayThreshold 및 청산 사용 여부.
- 박스권 Standard: 탐색기간, 엄격도, 지지선 접근/유입 강도, 상단 청산/하단 이탈 손절.
- 박스권 Advanced: RangeLookbackMinutes, MaximumRangePercent, ATRRatioThreshold, MinimumSupportTouches, MinimumResistanceTouches, SupportDistancePercent, ResistanceDistancePercent, LowVolumePercentile, VolumeReentryRatio, SupportScoreThreshold, RangeScoreThreshold, BreakdownPercent 및 두 청산 사용 여부.
- 반등 Standard: 탐색기간, 급락/반등 강도, 거래량 확인, 뉴스/공시 필터, 자동매수.
- 반등 Advanced: CrashLookbackSeconds, MinimumCrashPercent, MaximumCrashPercent, CrashVelocity, CrashVolumeRatio, MinimumReboundPercent, ReboundConfirmationSeconds, BuyTradeRatio, BidRecoveryRatio, CrashScoreThreshold, ReboundScoreThreshold 및 확인/필터/자동매수 여부. 공통 청산 강도는 추가한 ExitRetracementPercent로 변환해 관측 최고가 이후 되돌림에 적용한다.
- Risk: MaxPositionPercent, DailyLossLimit, MaxConsecutiveLosses, MaxOpenPositions, ReentryCooldown.
- Execution: OrderType, SlippagePercent, LimitOrderOffsetTicks, OrderTimeoutMilliseconds, Commission, Tax.

이름만 저장하는 설정이 아니라 기존 엔진 매핑·추가 신호 조건·모의 위험 제한·모의 체결 비용에 연결했다. 겹치지 않는 기존 가격 범위/수량/호가 필터는 기존 상세값을 유지한다.

## 6. 저장 위치와 방식

기존 `%LOCALAPPDATA%\StockPaperTrading\KiwoomQuant\workspace-v2.db` 사용. 별도 DB 저장 시스템을 만들지 않았다.

- `strategy-configuration-v1`: StrategyType, Mode, State, Enabled, Standard, Advanced 및 Risk/Execution.
- `strategy-configuration-preset:<이름>`: 이름별 새 전략 설정. 기존 일봉 프리셋도 함께 유지.
- `chart-run:*`: 이전/직후 백테스트, 실제 사용 설정과 자료 해시.
- `trade-marker:*`: 확인한 모의 체결 및 선택 실계좌 체결. 실계좌는 증권사 ID + 계좌 해시로 구분.
- 기존 events 테이블: 실제 KIWOOM 체결·호가. 메모리 처리 후 묶음 저장.
- 기존 증권사별 DPAPI 키 저장소: 연결 성공한 키와 기본 계좌. 실제 키를 테스트에 사용하지 않았다.

## 7. Validation

숫자 타입·유한값·허용 범위·1~5·양수 시간·enum·한국시간 HH:mm을 검사한다. 최소 급락률≤최대 급락률, 거래량 비교기간>상승속도 기간, 청산 점수<진입 점수를 교차 검사한다. 잘못된 입력은 입력 옆 오류와 설명을 표시하며 저장/실행/모드 변경을 차단한다. 기존 엔진 Validation도 실행한다.

차트 우클릭의 잘못된 시작/종료일은 사용자 요청대로 반대쪽 날짜에 맞춘다.

## 8. 데이터 제한과 미검증 부분

- 9/23에서 멈춰 보이던 원인: 오늘 봉을 일괄 제외하던 History 처리. 최신 봉을 차트에 포함하고 확정봉만 백테스트에 전달하도록 수정했다. 국내 15:40, 미국 현지 16:10 이후 확정으로 처리하는 보수적 기준이며 조기 폐장일을 따로 계산하지 않는다.
- 오프라인 틱·분 조회는 비활성화한다. 키움은 실제 수신 체결, 토스는 공식 1분봉을 사용한다. 일봉을 틱으로 합성하지 않는다. 새 세 전략은 저장된 실제 체결이 없는 기간에 실행 자료 없음으로 안내한다.
- 뉴스/공시 및 매수 방향 체결 데이터는 현재 피드에 없을 수 있다. 관련 조건을 켜면 자료 없는 진입을 보류하며 악재 여부를 추정하지 않는다. 초기 Mapping은 합리적인 시작값이며 수익성/시장별 최적화 검증은 하지 않았다. 비용과 투자금은 사용자가 계좌 통화에 맞춰 설정한다.
- 토스 현재 구현은 국내 계좌·주문·분봉이다. 해외·WebSocket은 미구현. 실제 키 인증과 주문·체결은 미검증이며 테스트에서 실제 돈을 사용하지 않았다.
- 이전부터 있던 미래에셋·삼성 API 규격/신한 indi 환경의 연동 제한은 그대로다. 연결 카드를 실제 연동 완료로 오인하지 않도록 표시한다.
- 체결 기록은 앱이 조회/관측한 확정 체결을 영속화한다. 일부 증권사의 체결시각·가격 미제공 건은 날짜 주석으로 표시한다. 모든 과거 개별 부분체결을 자동 복구한다고 보장하지 않는다.
- 버튼은 WPF AutomationPeer로 실행하고 전역 Busy 변경·중복 조회·화면 배치·팝업을 검사했다. 실제 사용자 모니터에서 발생할 수 있는 모든 프레임 단위 깜빡임을 측정한 것은 아니다.

## 9. 빌드와 검증 결과

최종 결과는 VALIDATION.md의 2026-09-29 항목 및 artifacts/full-revision-tests.txt, artifacts/revision-public/result.json, artifacts/revision-clean-result/result.json에 기록한다. 솔루션 Release 빌드, 회귀 검사, 공개 시세 통합 검사, EXE 단독 실행을 각각 구분한다.

일반 Publish 시 기존 refint/Stock.dll 파일 잠금(CS2012)이 발생하여 프로세스 종료/삭제 없이 별도 artifacts/revision-build 출력에서 동일 소스를 패키징했다.

최종: Release 경고0 오류0, 회귀447개 통과, WPF 버튼 클릭172회·비활성23회. 공개 읽기 및 EXE 단독 실행 모두 성공·종료0. 루트 Stock.exe 갱신 완료(66444742 bytes, SHA256 FC3001E938540EE9E6AF66DB018EDD62830C39845CD948A076C4796FEA48DBC2). 실제 인증·주문 없음. 공개 목록 일부 종목 조회 실패는 VALIDATION.md에 기록했다.
