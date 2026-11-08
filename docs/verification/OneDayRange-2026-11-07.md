# 분봉 전용 1일 기간 선택 검증 (2026-11-07)

## 요청과 결과

종목·차트의 기존 1/3/6개월 앞에 **1일**을 추가한다. 온라인 지정봉에서 분봉을 선택하면 자동으로 1일을 강조하고 수신 자료의 최근 거래일 하루만 표시한다. 일봉·시간봉·오프라인 일봉에서 1일을 누르면 기존 안내 팝업 경로를 호출하며 기존 기간/봉/차트를 유지한다. 분봉의 1일에서 일봉·시간봉/오프라인으로 바꾸면 1개월로 돌아간다.

원본 분봉 이력은 메모리에 그대로 두고 화면 배열만 제한한다. 국내/미국의 현지 거래 날짜로 하루를 구분하며 휴일에 가상의 오늘 봉을 생성하지 않는다. 최초 선택/조회 완료에만 하루 봉 수로 화면을 맞춘다. 시세 갱신은 사용자의 확대/이동을 매번 초기화하지 않는다. 백테스트 결과는 해당 실행 전체 기간을 계속 표시하므로 빠른 기간 강조를 해제한다.

추가로 59분→1시간→1분 전환 시 모델 값은 1인데 WPF SelectedIndex가 -1이 되어 숫자가 비는 문제가 재현됐다. 동일 값 setter 재진입을 막고, 숫자 목록을 안정적인 ObservableCollection으로 유지한다. 목록을 줄이기 전에 새로 유효한 숫자를 먼저 선택해 실제 SelectionBoxItem과 인덱스도 일치시킨다. 실제 콤보 선택으로 59분→12시간→12분→1분도 확인한다.

## 변경 파일과 역할

| 파일 | 역할 |
|---|---|
| Stock.App/MainWindow.xaml | 1일 RadioButton, 동일한 선택 색/툴팁/명령 바인딩 |
| Stock.App/RecurringWorkspace.cs | 1일 선택 명령, 비분봉 안내, 기간 상태/월 복귀 |
| Stock.App/ChartWorkspace.cs | 최근 거래일의 표시 배열, 첫 화면 봉 수, 숫자 목록/선택 동기화 |
| Stock.App/WorkspaceFeatures.cs | 봉/온라인·오프라인 전환에 기간 정책 연결 |
| Stock.App/WorkspaceViewModel.cs | ChartBars의 표시 전용 범위 처리 재사용 |
| Stock.App/OversoldWorkspace.cs | 백테스트 결과 표시 시 빠른 기간 강조 초기화 |
| Stock.Tests/OneDayRangeUiTests.cs | 실제 WPF 라디오 클릭/콤보·주기·모드 전환 검사 |
| Stock.Tests/MemoryModeTests.cs | 2거래일 공식 분봉 fixture/안내 수집, 신규 검사 연결 |
| Stock.Tests/PipelineRecurringUiTests.cs | 기간 선택 8개 및 기존 월/년 회귀 |
| Stock.Tests/RealtimeScreeningUiRevisionTests.cs | 숫자 목록의 Count 계약 반영 |
| docs/ui/ChartPage.md | 사용자 동작·실행 흐름·숫자 공백 원인 기록 |
| TASKS.md, HANDOFF.md | 작업 상태/다음 작업 인계 |

## 검증 근거

- Release 빌드 성공, 경고 0/오류 0: artifacts/one-day-build.log.
- 격리 WPF/메모리·모드 검사 **46개 통과**: artifacts/one-day-checks.log.
- 전체 WPF UI 회귀 **687개 통과**: artifacts/one-day-ui.log. 기존 화면·차트·백테스트·연결 fixture·표식·스크리닝·계좌 UI를 포함한다.
- 기간 추가, 1/5/59분의 자동 1일, 하루만 표시(전일 분봉은 원본에 유지), 시간봉/일봉의 클릭 거부 안내와 기존 라디오 강조 복원, 실제 콤보 숫자 표시, 온라인/오프라인 왕복을 확인했다.
- 거부 안내는 기존 WorkspaceViewModel의 alert 콜백으로 검사한다. 제품 MainWindow는 이를 제목 `안내`의 OK MessageBox로 연결한다. 테스트에서 실계좌 접속이나 주문은 수행하지 않는다.
- 화면: artifacts/one-day-checks/one-day-minute-chart.png, one-day-after-mode-switch.png 및 기존 창 크기 1540/1160 검사 이미지. 분봉 숫자/1일 강조/하루 봉 표시를 확인했다.
- git diff --check 성공. Git의 CRLF 변환 안내는 있으나 빌드 경고가 아니다.

## 배포/범위

로컬 publish 및 루트 단일 EXE smoke 모두 종료0, Success/NoBrokerCalls=true. publish/루트/release 해시 일치. 크기 80,865,492bytes, SHA256 `CF50697690477A7C45D9FB4012C1CBBFB426F5D0499DBE96D5A3E50257EDAF37`. 이전 실행 파일은 artifacts/before-one-day-20261107-221552에 보존. 근거 artifacts/one-day-exe.json, one-day-publish-smoke/result.json, one-day-root-smoke/result.json. 이번 요청에는 GitHub 게시가 포함되지 않아 커밋·push·태그·Release는 진행하지 않는다. 사용자 DB/API키/전략 값·주문 엔진·원본 시세 저장 정책은 변경하지 않는다. 검증은 가짜 broker/격리 DB 기반이며 실제 장중 증권사 조회 성능 측정은 아니다.
