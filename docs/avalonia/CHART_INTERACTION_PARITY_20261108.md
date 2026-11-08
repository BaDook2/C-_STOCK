# 원본 차트 상호작용 비교·수정 — 2026-11-08

이 보고서는 이전 컨트롤 디자인 비교와 별개인 **차트 동작** 검증이다. 기존 검사는 ScottPlot에서 휠/드래그가 작동한다는 것만 확인했으며 WPF와 같은 단위·경계·안내선을 확인하지 못했다. 사용자의 지적대로 이 검증이 빠져 있었다.

## 실제 재현과 원인

두 일반 앱에서 삼성전자를 실제 공개 자료로 검색했다. 기존 Avalonia에는 안내선 자체가 없었다. WPF의 `CandleChart`는 봉 간격이 균일하고, 보이는 봉 개수를 한 번에15개 바꾸며, 마우스가 가리키는 봉을 유지하고, 보이는 OHLC로 가격축을 다시 계산했다. Avalonia는 날짜 간격 그대로인 X축과 ScottPlot 기본 XY 비율 줌·기본 이동을 사용해 주말 공백/줌 폭/가격축/경계가 달랐다.

`artifacts/chart-native-before/`는 수정 전 일반 앱의 실제 화면이다. 원본 일반 일봉의 실제 안내선은 세로 점선이었다. 이번 십자가 요청을 반영해 Avalonia에는 그 세로 봉 안내선에 가로 가격선/가격 표시도 함께 넣었다.

## 반영한 동작

- 봉 간격은 ScottPlot5.1.59의 `CandlestickPlot.Sequential=true`로 원본과 같이 맞췄다. 캔들·거래량·MA·일목·지지 구간·체결 표식이 같은 봉 좌표를 쓴다. OHLC·실제 체결 시각·가격 및 백테스트 원본은 수정하거나 축약하지 않는다.
- `CandleViewport`는 원본 `CandleChart`의 UI 규칙을 옮긴 어댑터다. 휠 이벤트당15봉, 포인터 기준 확대, 일반 최소25/최대2000봉(자료가 더 적으면 해당 개수), 최신/최초 경계,4px 드래그 dead zone과 최대12px/봉 이동 단위를 사용한다. 드래그했다가 처음 위치로 돌아오면 처음 화면으로 복구된다.
- 확대/이동 후 보이는 봉의 최저 Low/최고 High에7% 여백을 적용해 Y축을 자동 조정한다. 거래량 X축도 함께 이동하고 같은 봉의 세로 안내선을 유지한다.
- 안내선은 hit-test하지 않는 Canvas 오버레이다. 포인터 이동 때마다 가격 시리즈를 재생성하거나 전체 과거 자료를 다시 계산하지 않는다. 봉으로 스냅한 세로 점선은 거래량 영역까지 이어지고, 가격 영역에는 가로선과 정수 가격이 보인다. OHLC/거래량 툴팁을 복구하고 이탈·빈 자료·잠금 상태에서는 안내선과 툴팁을 지운다. 드래그 중 커서도 원본처럼 좌우 이동 모양이다.
- 스크리닝 전체 화면은 잠금, 우클릭 시 `min(500, max(25, ceil(전체/4)))`봉을 포인터 주위로 표시한다. 상세에서는 휠·드래그가 되고 처음 상세 범위보다 넓게 축소할 수 없다. 다시 우클릭하면 전체로 돌아간다. 전체와 상세가 같을 정도로 자료가 적으면 처음부터 조작할 수 있다. 새 자료가 들어와도 잠긴 전체 화면에서는 전체 기간을 유지한다.
- 일반 종목 차트의 우클릭은 ScottPlot 기본 메뉴 대신 원본의 시작/종료 기간 설정을 백테스트 입력에 연결한다. 더블클릭으로 ScottPlot 기본 자동 축소가 실행되지 않는다. 매매한 봉의 단일 클릭은 기존 거래 상세 명령을 사용한다.
- 체결을 가장 가까운 다음 timestamp에 붙이지 않는다. 원본의 시장 날짜/분·시간 버킷/주·월 버킷으로 해당 봉을 찾는다. 예를 들어10시40분 체결은10시 시간봉에 붙고, 주중 체결은 해당 주의 봉에 붙는다. 체결 원본 시간의 초·밀리초는 그대로다.
- 실제 비교에서 발견한 월 단위 기간 첫날 제외도 수정했다. 시각까지 비교하는 대신 해당 시장의 날짜 경계를 사용한다. 날짜 눈금도 원본의 간격/개수 규칙을 사용한다.

원본 계산식 참조: `../STOCK/Stock.App/CandleChart.cs`의 ZoomAt/CalculateZoomOffset/CalculatePanOffset/ScreeningFocusCount/TradeBarIndex. ScottPlot 설치 버전의 [Sequential 구현](https://github.com/ScottPlot/ScottPlot/blob/5.1.59/src/ScottPlot5/ScottPlot5/Plottables/CandlestickPlot.cs)도 확인했다. UI별로 전략 엔진을 새로 만들지는 않았다.

## 검증과 범위

- 빌드0경고0오류, 공통 코어192개, 전체 Avalonia UI172개, 기존 디자인16개를 최종 검사한다. UI172에는 차트 전용30개가 포함된다. 최종 로그는 `artifacts/chart-final-regression.log`이며 실행 파일 마감 해시는 HANDOFF/TASKS를 따른다.
- 차트 전용 검사는15봉 줌/포인터 위치/최소·최대/양방향 드래그·복귀/스크리닝 전체·상세/신규 자료/십자선·거래량/툴팁·이탈/5종 봉/시간·주간 체결 위치를 포함한다. 기존 매매 봉 클릭/표식 병합/10K 전체 및 상세/저장된 백테스트·스크리닝 재열람 검사도 유지했다.
- 10,000봉 합성 자료의 전체→상세500봉 전환 시간을 로그로 남긴다. `artifacts/avalonia-ui/candle-navigation-performance.json`은 Windows Headless 측정이며 실제 Mac 성능 수치가 아니다.
- `scripts/audit-native-chart.ps1`는 진단 옵션 없는 두 앱의 실제 마우스 데스크톱을 사용한다. 일봉 좌/중/우 호버, 각 위치 휠 in/out, 양방향 드래그, 거래량 호버, 이탈을 화면 캡처했다. `artifacts/chart-native-final/`에 원본/이식 비교 이미지와 결과를 저장했다.
- 비교 전 기간을1개월→3개월로 바꿔 출발 범위를 초기화한다. 원본은 같은 DisplayBarCount를 다시 넣어도 이전 줌이 남아, 단순히3개월을 다시 누르는 것으로는 초기화되지 않았다. 스크립트의 UIAutomation도 WPF RadioButton/Avalonia 항목의 역할 차이를 구분한다. 이런 검사 문제와 앱 결함을 섞지 않는다.
- 현재 두 앱의 오프라인 메인 화면은 사용자 정책대로 일봉만 노출한다. 따라서 실제 일반 앱에서 숨겨진 주/월/분/시간 설정을 강제로 활성화하지 않았다. 공통 차트의 해당 봉 동작은 격리 데이터 검사이며 실제 증권사 자료 검증이라고 보고하지 않는다. 실제 Mac 실행/트랙패드/서로 다른 DPI는 여전히 미검증이다.
- 최종 일반 앱의 화면 픽셀로 가로/세로 안내선 표시를 별도 확인하고, 배포 루트EXE의 실제 검색/Enter/반복/재실행 검사도 수행한다. 이 결과는 `chart-native-final/crosshair-pixels.json`, `chart-exe-native-search/result.json`을 따른다. RenderTargetBitmap만으로 실제 창 표시 성공을 판단하지 않는다.

## 파일 지도

- `Views/CandleViewport.cs`: 봉 단위 UI 탐색 상태/규칙.
- `Views/StockChart.Interaction.cs`: 실제 포인터 입력, 안내선/툴팁, 범위·가격축 동기화.
- `Views/StockChart.axaml(.cs)`, `StockChart.Appearance.cs`: 오버레이, Sequential 시리즈와 공통 좌표/표식·지지/날짜 눈금.
- `MainWindow.axaml.cs`: 우클릭한 날짜를 기존 백테스트 기간 입력에 전달.
- `ViewModels/MarketWorkspace.cs`: 표시 기간의 시장 날짜 경계.
- `Stock.Desktop.Tests/ChartInteractionChecks.cs`, `ChartAndHistoryChecks.cs`, `Program.cs`: 원본 규칙/공통 차트/기존 회귀.
- `scripts/audit-native-chart.ps1`: 안전한 일반 앱 차트 제스처 캡처.

원본 WPF/Core80개/사용자 DB·키는 변경하지 않았다. 실제 주문 및 GitHub 게시도 하지 않았다. 모든 차트의 모든 픽셀이 완전히 동일하다고 보고하는 문서는 아니다.

## 최종 마감

실제 최종 결과: 빌드0경고0오류, Core192/UI172/디자인16 성공. 화면 십자선 픽셀은 가로274/세로176 확인. 새 루트 EXE 실제 키보드/검색/Enter/반복/재실행4회 모두 성공(삼성전자 빨강657/파랑527 표본, SK539/501). 최종 파일107,318,071bytes SHA256 `1D7783EAF7183AA750145524A827298A2BC30EF6C7B74500D3ABC93C137CEB0D`. 이전C475판은 `artifacts/before-chart-interaction-20261108/`에 보존했다.

동일 소스로 Mac arm64/x64 app bundle도 갱신했다. 실제Mac 실행/서명/공증 검증은 아니다. 원본WPF exe E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934 및 Core80개 manifest 차이0을 확인했다. 최신 합성 성능 자료는10K 전체221ms/상세500봉59ms(반올림, Windows Headless)이다. 이 값은 사용자의 실제장중/Mac 측정이 아니다. 주간잔여45% 확인, GitHub 게시 없음.
