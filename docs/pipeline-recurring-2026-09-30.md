# 파이프라인·정기매수 구현 보고

## 1. 추가·수정한 파일

- Core 신규: `Pipeline/PipelineConfig.cs`, `PipelinePresetMapper.cs`, `RankingProviders.cs`, `PipelineEngine.cs`, `Research/RecurringPurchaseBacktest.cs`.
- Core 수정: `Trading/Settings.cs`, `StrategyParameters.cs`, `ParameterMetadata.cs`, `TradingEngine.cs`, 주문 모델, `Research/WorkspaceModels.cs`.
- App 신규: `PipelineWorkspace.cs`, `PipelineScreening.cs`, `NumericDisplay.cs`, `RecurringWorkspace.cs`, `RecurringPurchasePanel.xaml(.cs)`, `RecurringParameterCaption.cs`.
- App 수정: `MainWindow.xaml`, `App.xaml`, `WorkspaceViewModel.cs`, `WorkspaceFeatures.cs`, `StrategyEditorViewModel.cs`, `StrategyParameterPanel.xaml`, `ConfiguredBacktest.cs`, `TradeHistoryViewModel.cs`, `SettingsEditor.cs`, `CandleChart.cs`, `PublishedSmoke.cs`, 숫자를 출력하는 기존 스크리닝·관심종목 화면.
- 검사: `PipelineTests.cs`, `PipelineRecurringUiTests.cs`, 기존 StrategyConfiguration/UI 검사 진입점.

## 2. 모듈 구조

기존 세 가지 전략과 일봉 경로를 보존하고 `StrategyType.Pipeline`을 추가했다. 각 단계는 독립 Config 및 필터를 사용한다.

| 단계 | 계산·역할 |
|---|---|
| Universe | 시장, 가격, 시가총액, OTC, 순위/관심그룹 후보 |
| Attention | 검색순위 상승속도, 거래대금, 일·동시간대 상대 거래량 |
| Price Action | 등락, 갭, 최근 낙폭, VWAP, 지지·저항 거리 |
| Microstructure | 호가 스프레드·잔량·불균형, 체결강도, 1분 변동성, 거래량 유지 |
| 틱떼기 | 지정가 우선 진입, 익절·손절·시간·유동성·지지 이탈 청산 |
| 농부매매 | 초기 비중, 선택적 1회 추가 진입, 추세·가설·추적 손절, 일부/전체 수확 |
| Portfolio | 전체/종목 비중, 동시 종목, 예약 자금, 연속 손실, 재진입 제한 |
| Risk / Execution | 기존 위험 검사와 체결 엔진의 수수료·세금·슬리피지·주문 만료 재사용 |

전체 필터 상태와 탈락/자료 부족 사유를 표시한다. 시가총액·검색순위·VWAP·체결 방향 등 없는 자료를 가격으로 임의 추정하지 않는다. Core 수익 이동은 모의 포트폴리오의 가용 자금을 제한하는 내부 배정이며 실제 송금 기능이 아니다.

## 3. ParameterMetadata 위치

`Stock.Core/Trading/ParameterMetadata.cs`의 공통 모델/Catalog를 확장했다. 각 파라미터의 중앙 정의는 `PipelineConfig.cs`의 Parameter 속성에 있다. 표시 이름, 짧은 설명, 역할, 증가/감소 영향, 단위, 범위, Category/Group을 UI가 읽는다. 정기매수 설명은 `RecurringPurchaseBacktest.cs`에 있다. View에 계산이나 설명 문자열을 중복 작성하지 않는다.

## 4. Tooltip / Popup

기존 `ParameterField` 및 공통 도움말 창을 재사용한다. 각 입력 옆 `?`에 짧은 Tooltip, 클릭 시 역할·높임/낮춤 영향·단위·범위·현재값을 표시한다. 정기매수도 `RecurringParameterCaption`으로 같은 Metadata를 사용한다. 긴 섹션은 Expander로 접는다.

## 5. Provider

`ISearchRankingProvider`는 시점별 관측 자료, `ITrendingSymbolProvider`는 후보 키를 제공한다. Manual/CSV/Mock 구현과 factory를 분리했다. CSV는 Symbol/At 필수, 시간대 명시, 따옴표로 감싼 쉼표 숫자, 중복·알 수 없는 헤더 및 미래 관측 시점 검사를 지원한다. 동일 종목은 조회 시점 이하의 최신 관측을 선택한다.

Toss/Kiwoom 검색순위는 이 앱에 아직 연동되지 않았다. 선택하면 미연동 메시지를 표시하며 자동으로 Mock으로 바꾸지 않는다. 실제 차트의 공개 가격 제공처와 순위 Provider는 별개이다.

## 6. Standard → Advanced

`PipelineConfiguration.Resolve(mode)` → `PipelinePresetMapper` → `PipelineAdvancedConfig` → 공통 `TradingEngine` 경로이다. 1~5 매핑 배열은 Mapper 한 곳에 있다. Standard에서 Advanced로 전환하면 계산된 실제값이 나타난다. Advanced 수정은 Custom이다. 별도 전략 엔진을 복제하지 않는다. 초기 매핑은 합리적인 시작값이며 수익성을 검증·최적화한 값은 아니다.

## 7. 저장 / 복원

기존 `TradingStore`의 SQLite state 저장을 재사용한다. 기본 위치는 `%LOCALAPPDATA%/StockPaperTrading/KiwoomQuant/workspace-v2.db`이며 실행 환경의 기존 경로 설정을 따른다.

- `strategy-configuration-v1`: 모드, Standard/Advanced, 9개 섹션, 선택 전략.
- 기존 이름 있는 프리셋에도 동일 모델 포함.
- `pipeline-observations-v1`, `pipeline-ranking-input-v1`: 관측 자료와 입력.
- `recurring-config-v1`: 최근 정기매수 조건.
- `recurring-run:*`: 조건·납입·현금·체결·평가 곡선·자료 해시.
- `chart-run:recurring-run:*`: 기존 테스트 기록 화면과 연결하는 요약·체결 기록.

별도 DB/설정 저장 시스템을 만들지 않았다. 테스트 기록에서 과거 정기매수의 요약과 매수 내역을 확인할 수 있다.

## 8. Validation

Metadata 범위/타입 검사와 `PipelineValidation` 교차 검사를 결합한다. 가격/시가총액/등락·갭·변동성 min≤max, 양수 시간, 1~5 강도, 음수 손절률, 양수 익절률, 농부 가설/추세 점수 관계, 부분/전체 수확 순서, 비중과 Core 목적지 등을 확인한다. UI는 한국어 오류를 표시하고 실패한 실행으로 기존 결과를 덮어쓰지 않는다.

정기매수는 기간, 양수 회차 금액, 0 이상 초기금, 0~100% 미만 수수료, 정수 주식 수량 한도를 검사한다. 숫자 입력·표시에는 쉼표를 사용하고 수수료·전략 임계값의 소수 정밀도는 보존한다. 계좌번호·종목코드·날짜는 숫자 금액으로 포맷하지 않는다.

## 9. 정기매수 화면과 계산·지원 한계

종목·차트 화면을 검색 / 차트·테스트 / 주문 3열로 유지하면서 주가·등락·날짜, 빠른 조회 기간, 차트 설정, 전략/정기매수 탭과 좌우 설정/결과 카드를 배치했다. 작은 창에서는 중앙 영역을 스크롤한다.

정기매수는 확정 일봉만 사용한다. 매일 또는 해당 주/월에 제공된 첫 거래일에 납입하고 시가/종가로 정수 수량을 매수한다. 초기금은 첫 회차에 합산, 잔액은 이월, 수수료는 현금에서 공제한다. 수익률은 총 납입 대비 손익, MDD는 외부 납입 영향을 제거한 단위가치 기준이다. 최종 평가액에는 남은 현금이 포함된다. 환전·환율·배당·실제 주문은 포함하지 않는다.

배당 지급 자료가 없어 배당 재투자는 비활성화했다. 요청 기간보다 제공된 이력이 짧으면 실제 계산 구간과 안내를 표시한다. 차트 녹색 평가 곡선은 주가와 별도 축이며 금액/범위를 명시한다. 빨간 점은 정기매수 시점이다. 빠른 조회 기간 버튼은 차트만 변경하고 입력한 백테스트 기간을 바꾸지 않는다.

파이프라인 실제 백테스트/모의 재생에는 저장된 체결·호가와 해당 시점의 보조 관측이 필요하다. 일봉을 가짜 틱으로 바꾸지 않는다. 전용 Mock 버튼은 합성 검사임을 명확히 표시하고 일반 차트·실계좌에 자료를 넣지 않는다. 실시간 경로는 같은 최종 Config/필터로 신호를 평가하며 실제 자동 주문은 하지 않는다. 기존 수동 실계좌 주문은 선택 계좌 한 곳으로만 보낸다. 현재 UI 백테스트는 종목별이며 서로 다른 통화의 다종목 통합 성과는 제공하지 않는다.

## 10. 검증

Release 빌드와 492개 회귀 검사에서 경고 0, 오류 0. Metadata, 동일 설정, 범위/교차 검사, Provider 시간·CSV 숫자, 결측 자료, 두 전략의 진입/청산, Portfolio 배정, SQLite, 정기매수 휴장·월/주기·수수료·잔액·MDD, UI 저장 복원·결과·마커를 검사했다. 실제 계좌 인증·주문을 사용하지 않았다.

최종 EXE 경로/해시, 공개 가격·단독 실행 결과는 `VALIDATION.md`와 `TASKS.md`에 기록한다. 검사 원문은 `artifacts/pipeline-recurring-tests.txt`, 화면은 `artifacts/recurring-layout.png`, `pipeline-standard.png`, `pipeline-advanced.png`에 있다.
