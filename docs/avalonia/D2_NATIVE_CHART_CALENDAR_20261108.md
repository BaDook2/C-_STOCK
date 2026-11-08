# D2 — 차트·달력·온라인 지정봉 수정 및 검증

2026-11-08. 이전 D2 중단 기록을 대체하는 최종 상태다. 원본 WPF와 공통 Core80개는 수정하지 않았다. GitHub push/tag/Release 및 실계좌 주문은 실행하지 않았다.

## 실행 파일

- 루트 `Stock.Avalonia.exe`:107,330,359bytes.
- SHA256:`78CB5E2E35024702F55F32311241B000F3C8459836E29044406B9DC16AFBA02D`.
- 기존1D7783판과 SHA 파일: `artifacts/before-d2-ui-fix-20261108/`.
- 새 봉 팬 유지 후속 전659F92E9판: `artifacts/before-d2-live-pan-fix-20261108/`.
- 기본 시장 변경 전E85CC27B판: `artifacts/before-default-market-all-20261108/`.
- 원본 WPF EXE:`E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934` 보존.
- Mac arm64/x64 번들: `artifacts/macos/<RID>/Stock.Avalonia.app`. 같은 소스로 교차 빌드했으며 실제 Mac에서 실행한 것은 아니다. 실행권한·서명·공증은 별도 확인 대상이다.

## 원인과 수정

최신 추가 요청에 따라 종목·차트 탭의 시작 시장을 `국내`에서 `전체`로 바꿨다. 앱 실행 시 전체가 선택되며 이후 사용자가 선택한 시장을 탭 이동만으로 덮어쓰지 않는다. 새 EXE에서 실제 기본 콤보 `전체`와 삼성/SK 검색·재실행을 확인했고, 시장을 바꾸지 않고 애플 검색→AAPL 차트790ms를 확인했다. `artifacts/d2-final-exe-native-backtest/entire-market.json`과 PNG가 근거다.

### 차트 이동

`ChartBars`를 선택 기간으로 잘라 초기 화면 전체 표시 때 과거로 이동할 backing history가 없었다. 전체 로딩 자료를 유지하고 `ChartDisplayCount`로 초기 표시 봉 수만 분리했다. 백테스트 표시에서는 종료일까지의 자료를 유지한다. `CandleViewport`의 포인터15봉 줌·드래그·경계·스크리닝 전체/상세 규칙은 그대로 사용한다. 원본 백테스트 입력을 줄이지 않는다.

마지막 실시간 회귀에서 새 봉 추가로 `DisplayBarCount`가 늘면 팬이 초기화되는 경우도 재현했다. 표시 수만 바뀔 때는 기존 팬/줌과 `UpdateCount`를 유지하고, 명시적인 기간 `Revision` 또는 탐색 모드 변경 때만 초기화한다. 새 봉/표시 수 증가 이후 과거 위치 유지와 명시 기간 변경의 초기화를 각각 검사했다.

### 거래 표식

ScottPlot의 글꼴 이름만 지정한 수정은 실제 화면에서 한글 네모를 해결하지 못했다. 가격 위에 별도 `TradeLayer` Canvas를 두고 Avalonia TextBlock/Border로 한글·번호·손익을 그린다. 가격 삼각형/혼합 표식은 ScottPlot에서 유지한다.

텍스트를 측정해 차트 안으로 배치하고, 빈 세로 공간을 찾는다. 화면이 부족하면 시간대별 표식 묶음의 폭을 늘려 다시 병합한다. 원본 체결 배열·시각·가격·수량은 유지하며 확대하면 개별 표식으로 돌아온다. 배지와 거래 봉 모두 단일 클릭으로 선택한다. 상세 차트는 같은 체결을 거래 표에서 선택하고 구간을 확대한다. 빈 자료·종목 변경 시 배지와 hit 영역도 지운다.

### 달력과 날짜 입력

Fluent CalendarItem의 내부294px 최소폭/290px 최소높이를 둔 채 외곽만224px로 줄여 헤더와 요일이 잘렸다. 별도 compact CalendarItem/일/월 테마로 원본 실제 크기202×201을 구성했다. 배경은 연한 회색, 헤더는 회색, 선택은 파란색이다. 흰색으로 다시 덮던 중복 스타일도 제거했다.

필드 이름 `PART_ElementHeaderButton`의 실제 문자열 값은 `PART_HeaderButton`이다. 실제 템플릿은 `PART_HeaderButton`, `PART_PreviousButton`, `PART_NextButton`, `PART_MonthView`, `PART_YearView`를 사용한다. 잘못 추정한 Element 이름 스타일은 제거했다. [설치 버전의 공식 CalendarItem 템플릿](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Themes.Fluent/Controls/CalendarItem.xaml)과 [컨트롤 소스](https://github.com/AvaloniaUI/Avalonia/blob/12.1.5/src/Avalonia.Controls/Calendar/CalendarItem.cs)를 대조했다.

날짜 입력 높이36px·여백·아이콘 크기를 조정하고,125~126px 입력에서도 연/월/일 텍스트가 들어가는지 검사했다. 여섯 주·연도·10년 단위 화면과 날짜 선택까지 확인했다.

### 온라인 지정봉

인증 없이 온라인 라디오를 눌러도 자체 체크 상태만 먼저 바뀔 수 있었다. 단순 PropertyChanged 재통지는 실제 클릭 검사에서 실패했다. 모드 표시를 검증된 연결의 OneWay 상태로 바꾸고, View 클릭은 기존 계좌 명령에 전달한 뒤 현재 상태를 다시 표시한다. 인증 전/취소 후에는 오프라인 표시를 유지한다.

지정봉 영역 전체를 무조건 비활성화하지 않는다. 지원하는 연결·국내 종목이면 분1~59/시간1~12 선택과 실제 지정봉 버튼으로 조회한다. 미지원이면 버튼을 눌러 정확한 사유를 별도 팝업에서 확인할 수 있다. 같은 지정봉 재선택은 재조회하며 잘못된 값은 실행 전에 안내한다. 봉 기준 전환 시 이전 기준의 봉/지표를 그대로 남기지 않는다.

인증/시세/집계는 기존 AccountWorkspace·MarketHistoryService·공통 ChartPeriods를 사용한다. 시간봉 검사에서는60분의 확정 OHLCV와 최신 관측으로 추가되는 미완성 봉을 구분했다. 미완성 봉의 최신 관측 시각을 확정 봉 시작 시각으로 오해해 Core를 변경하지 않았다.

## 변경 파일

- `Stock.Avalonia/MainWindow.axaml`, `.axaml.cs`: 표시 수 binding, 지정봉 안내, 모드 클릭/표시 동기화.
- `ViewModels/MarketWorkspace.cs`, `ChartTimeframeWorkspace.cs`, `PresentationWorkspace.cs`: backing 자료/표시 수 분리, 조회/검증, 실제 모드 표시.
- `Views/CandleViewport.cs`, `StockChart.axaml`, `StockChart.axaml.cs`, `ChartMarkerLayout.cs`: 탐색·한글 배지·충돌 배치/병합·클릭·번호.
- `Styles/ControlStates.axaml`, `StockTheme.axaml`: 달력/날짜 입력 테마.
- `Stock.Desktop.Tests/Program.cs`, `ChartInteractionChecks.cs`, `OnlineDesktopChecks.cs`, `ChartAndHistoryChecks.cs`, `DesignChecks.cs`, 새 `CalendarInteractionChecks.cs`.
- `scripts/NativeUiAuditHelpers.ps1`, 새 `verify-native-backtest.ps1`: 일반 앱 포커스 재시도, 실제 백테스트/팬/클릭/선택/달력 검사.
- AGENTS/TASKS/HANDOFF와 이 문서. 코어 변경0개.

## 검사 결과

| 구분 | 결과/근거 |
|---|---|
| Release solution | 경고0/오류0 |
| 공통 Core |192개 성공; `scripts/verify-core-manifest.ps1 -OriginalRoot ../STOCK`80개 일치 |
| UI 회귀 |199개 성공; 실제 증권사가 아닌 fixture 계좌/자료 |
| 디자인 |18개 성공; 날짜 텍스트 포함1160/1540 폭 검사 |
| 온라인 UI 전용 |21개 성공; 인증 취소/분봉/시간 OHLCV/미지원 팝업/메모리 재연결/주문0 |
| 달력 전용 |11개 성공; 아이콘 실제 포인터,42일·연도·10년 화면·날짜 선택 |
| 새 루트 EXE 검색 |실제 키보드/마우스/Enter·삼성→SK→삼성·종료/재실행4회 성공 |
| 새 루트 EXE 백테스트 |삼성전자, 검증1·1일 보유,2026-08-01~10-30:4거래, 손익+66,000, 승률75.00%, MDD0.40%; 해당 공개자료 입력에서 계산된 재현 결과 |
| 실제 팬 |같은 포인터의 봉 날짜09/23→09/04, 반대 드래그로09/23 복귀 |
| 실제 표식 클릭 |한 번 클릭으로 거래#1/2026.08.04/매수/244,500/4주 안내 |
| 실제 상세 차트 |표식 클릭 후 같은 거래의 상세와 선택 행 화면 색 확인 |
| 실제 달력 |원본 WPF/수정 앱을 각각 열어 대조; 새 EXE의202×201 월/연도 화면·1160 폭 확인 |
| 실제 인증 취소 |키를 읽지 않고 온라인 진입 창만 열어 닫음; 오프라인 설명/상태 유지 |
| Mac |arm64/x64 교차 빌드 성공. 실기 실행 미검증 |

전체 로그 `artifacts/d2-final-regression.log`. 최종78CB EXE 검색 결과 `artifacts/d2-final-exe-native-search/result.json`, 실제 백테스트 `artifacts/d2-final-exe-native-backtest/result.json`. 좁은 달력/모드 선행 검사는 `artifacts/d2-exe-native-backtest/native-mode-layout.json`. 소스 일반 창 결과는 `artifacts/d2-source-native/`이며 EXE 결과와 구분한다. 실제 검증으로 새 공개 백테스트 기록이 생성됐으며 임의 삭제하지 않았다.

## 네이티브 검사기에서 구분한 문제

- 검사기가 메인 창을 topmost로 고정한 상태에서는 새 모달 창이 뒤에 가려질 수 있었다. 자식 창도 올린 뒤 화면 캡처·확인 클릭을 하도록 검사기를 수정했다. 이를 앱에서 확인 버튼이 동작하지 않는 문제로 보고하지 않는다.
- Avalonia DataGrid12.1.2의 UIAutomation Selection 패턴은 지원되지 않았다. VM의 동일 객체 선택 검사와 별도로 실제 창에서는 거래 상세 텍스트/동일 체결 행/선택색F0F0F0 또는0078D7 픽셀을 검증했다. Unsupported Pattern을 성공으로 무시하지 않았다.
- 선택색 검사기의 PowerShell and/or 우선순위도 괄호로 바로잡았다. 최종 검사 결과는 Success:true이며 초기 실패 JSON은 원인 기록으로 보존한다.
- 콜드 시작 직후에는 검사기가 창 생성 전에 포커스를 요청할 수 있었다. 최대12초 동안 실제 창/충분한 client 크기를 기다리고, 유효한 좌표 변환 후에만 클릭하도록 보완했다. 창이 없을 때 바탕화면 좌표를 클릭하지 않는다.

## 남은 범위

실제 사용자 키로 인증한 증권사 조회는 이번에 수행하지 않았다. 원본 키를 복제하거나 채팅으로 받지 않는다. 실제 Mac·다른 DPI·트랙패드·폰트/창 장식은 미검증이다. A7의 Keychain·영속 계좌 설정·실계좌 주문 UI/대사는 별도 미완료다. 모든 픽셀/모든 기능이 원본과 동일하다고 보고하지 않는다.

사용자 최신 답변에 따라 사용량40% 중단 조건은 해제됐다. 이후 GitHub 게시나 실제 주문은 별도 명시 요청이 있어야 한다.
