# 스크리닝 전략 백테스트 팝업

## 화면 목적

스크리닝 탭의 후보를 다른 탭으로 이동하지 않고 연속 검토한다. 스크리닝 실행 당시 기간, 데이터 기준, 전략 설정, 과거 사실 snapshot을 사용하므로 이후 설정 편집으로 과거 결과가 달라지지 않는다.

## UI 기능

| UI | 기능 | 실행 코드 |
|---|---|---|
| 후보 행 더블클릭 | 선택 종목 팝업 열기 | `ScreeningRowDoubleClick → OpenScreeningStock` |
| 백테스트 보기 | 선택 종목 팝업 열기 | `Actions[결과열기] → OpenScreeningStock` |
| 차트 주기 | 실행 snapshot의 정확한 틱/분/시/일 지정봉과 보조 일봉 전환 | `ScreeningChartSeries` |
| 전체 기간에서 우클릭 | 포인터 봉 중심으로 최대 500봉 확대, 탐색 활성화 | `EnableScreeningReviewMode`, `ToggleScreeningReviewView` |
| 확대 상태에서 우클릭 | 전체 기간으로 복귀, 차트 입력 잠금 | `ToggleScreeningReviewView` |
| ▲/▼ 또는 거래 행 | 해당 체결 선택, 상세 snapshot 표시, 차트 이동 | `TradeMarkerClicked`, `FocusTrade` |
| 이전/다음 종목 | 현재 창에서 후보 목록의 앞뒤 종목으로 이동 | `ScreeningChartDialog.Navigate` |
| 다시 실행 | 같은 종목과 저장 snapshot으로 새로 표시 | `Navigate(0)` |

## 동작 과정

`더블클릭/백테스트 보기 → OpenScreeningStock → LoadScreeningChart → OversoldBacktestService → OversoldBacktest → ScreeningChartDialog`

종목/차트 탭도 같은 `IOversoldBacktestService.RunAsync`를 사용한다. 캐시는 종목·일봉·기간·설정·사실 해시가 모두 같은 경우에만 재사용한다. 이전/다음 요청은 직전 CancellationToken을 취소하고 generation이 최신인 결과만 반영한다.

상세 차트의 첫 시리즈는 현재 UI 값을 다시 읽지 않고 스크리닝 실행 snapshot의 `BarSpecification`을 사용한다. 공통 `ScreeningBars` 경로에서 틱/분/시 봉을 생성하고 snapshot의 시작일 00:00부터 종료일 23:59:59까지만 남긴다. 일봉 스크리닝은 같은 기간의 일봉을 사용한다. 사용자에게 보조 비교용 일봉을 제공할 수 있지만, 지정봉 조회 실패를 일봉으로 가장해 성공시키지 않는다.

오프라인 스크리닝은 제공 가능한 일봉만 사용하므로 지정봉 입력을 숨기고 저장된 틱/분/시 설정도 실행에 적용하지 않는다. 온라인 연결이 완료되면 `틱봉`, `분봉`, `시간봉` 선택이 나타난다. 분봉과 시간봉 상세는 일반 종목 차트의 빠른 최근 3페이지 제한을 사용하지 않고, 스크리닝 종료일을 기준일로 요청한 뒤 시작일이 포함된 페이지까지 이전 분봉을 연속 조회한다. 키움이 직접 지원하는 간격은 해당 지정 분봉을 요청하며, 1시간봉은 60분봉을 바로 사용한다. 같은 종목·주기·기간의 스크리닝 직후 상세 창을 열면 2분 범위 캐시를 사용해 같은 REST 범위를 다시 요청하지 않는다.

상세 팝업은 시작일~종료일의 집계봉 전체를 처음 화면에 맞추고 입력을 잠근다. 예를 들어 2026-10-01~2026-10-20의 1시간봉은 fixture 기준 완성봉 84개가 모두 첫 화면에 들어온다. 봉과 거래 표식 모두 실행 snapshot의 시작일 00:00 이상, 종료일 다음날 00:00 미만으로 자르므로 이전/이후 거래가 가장자리 봉에 잘못 표시되지 않는다. 이 전체 화면에서는 휠·좌클릭·드래그·표식 선택을 처리하지 않는다.

원하는 봉에서 우클릭하면 해당 봉을 가운데에 두고 `min(500, 전체 봉의 약 1/4)` 범위로 확대한다. 짧은 자료도 최소 25봉 범위에서 가능한 만큼 확대한다. 이때부터 휠 확대·축소, 좌우 드래그와 매매 표식 선택을 허용하며, 최대 축소는 처음 우클릭으로 연 범위까지다. 다시 우클릭하면 시작일~종료일 전체 화면으로 돌아가고 입력을 잠근다. 이는 화면 범위만 바꾸며 원본 지정봉 배열과 체결 snapshot은 변경하지 않는다.

시간봉은 일봉이 아닌 장중 주기로 분류한다. X축은 한국시간을 표시하고 매수·매도 시각은 해당 시간을 포함하는 시간봉에 배치한다. 매매 표식은 종목/차트의 백테스트 표식과 같은 번호·매수/매도 모양을 사용한다.

일봉 백테스트는 성과 요약과 실제 모형 체결, 판단 당시 Support Cluster zone을 표시한다. 매수 snapshot의 모든 유효 cluster를 표시하며 `UsedForEntry`인 zone을 파란색으로 강조한다. 미래 데이터로 현재 cluster를 다시 계산하지 않는다. 지정 틱/분/시 스크리닝은 검증 원본 봉을 차트에 표시하지만, 현재 `OversoldBacktest`가 일봉 체결 모델이므로 별도 체결을 만들어내지 않고 저장된 실제·모의 체결만 표시한다.

키움 틱·분봉 응답의 마지막 현재 진행 행은 일시적으로 0 또는 서로 맞지 않는 OHLC를 포함할 수 있다. 과거에는 이 한 행을 `PriceBar.Validate()`하는 즉시 전체 조회가 실패해 `가격 데이터 형식이 올바르지 않습니다`가 표시됐다. 이제 완성된 정상 행은 유지하고 잘못된 진행 행만 제외한다. 원본 틱 한 건은 한 체결 가격이므로 O/H/L/C를 해당 체결가로 정규화하며, 가격을 임의 생성하지 않는다. 모든 행이 잘못된 경우에는 완성된 가격 봉이 없다는 구체적인 오류를 표시한다.

## 관련 코드

- `Stock.App/ScreeningNavigation.cs`
- `Stock.App/ScreeningChartDialog.cs`
- `Stock.App/OversoldBacktestService.cs`
- `Stock.App/CandleChart.cs`
- `Stock.App/MainWindow.xaml(.cs)`
- `Stock.App/ScreeningResultDialog.cs`
