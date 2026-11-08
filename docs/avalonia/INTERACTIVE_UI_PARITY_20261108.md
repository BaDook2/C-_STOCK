# 실제 마우스 상태 비교와 수정 — 2026-11-08

원본 `../STOCK/Stock.exe`와 Avalonia **일반 실행 앱**을 동시에 열어 비교했다. 앞선 정적 화면/Headless 검사만으로 디자인이 같다고 판단한 범위는 충분하지 않았다. 이 문서는 UI_PARITY.md의 초기 전환 기록보다 우선한다. 실제 Mac에서의 픽셀 일치를 의미하지 않는다.

## 원인과 수정

- Button의 둥근 외곽 템플릿만 교체해도 Fluent ControlTheme의 내부 `PART_ContentPresenter` 호버/누름/비활성 상태가 남았다. 그 결과 글자 뒤에 회색 네모와 별도 색상이 생겼다. Button·선택 항목·체크/라디오·펼침 헤더·표·스크롤바에 독립적인 테마를 적용했다. 눌림 시 크기를 바꾸지 않으며, 일반 버튼 외곽의 호버/누름 불투명도는 WPF의 0.88/0.76이다.
- Expander의 장식 화살표를 hit-test 대상에서 제외하고 토글 헤더의 빈 부분까지 투명 배경으로 채웠다. 화살표 가장자리와 글자 모두 같은 토글을 누른다. CheckBox의 상자와 글자 사이 빈 부분도 클릭할 수 있다.
- ComboBox는 흰색 둥근 팝업, 회색 `#F2F4F6` 옵션 호버, 파란 체크 표시로 정리했다. 생성된 항목/키보드 Escape/실제 선택/편집 입력/IME 기능은 유지한다. 편집형 전략 콤보의 Fluent 포커스 리소스가 내부에 두 번째 파란 테두리를 그리는 문제도 제거했다.
- 표는 소스의 Row 기본색만 가져오지 않았다. 실제 WPF에서는 시스템 Cell 선택색이 위에 그려졌다. 화면 픽셀 측정에 따라 호버 `#F7F9FC`, 그리드 포커스 선택 `#0078D7`, 포커스 이동 후 선택 `#F0F0F0`를 적용했다. 일반 셀의 선택 글자는 흰색, 종목 링크 버튼 글자는 원본처럼 검정이다.
- 헤더를 정렬 아이콘 예약 영역까지 포함한 전체 열의 중심에 맞췄고 셀 여백/가로·세로 정렬/클리핑을 수정했다. 열 정렬과 가상화는 기존 DataGrid 기능을 그대로 사용한다.
- 12px 스크롤바와 둥근 thumb, 호버/드래그 색상을 적용하고 시스템 화살표를 제거했다. 실제 thumb 드래그도 검사한다.
- 전략 툴바를 원본처럼 편집형 등록 전략 콤보와 기존 이름 변경 명령으로 연결했다. 제목/설명/툴바/조건 영역의 간격과 그룹 제목/개수의 글자 크기를 조정했다. 설정 값과 기존 61개 필드의 의미는 바꾸지 않았다.
- 스크리닝의 누락된 분야 선택/목록 갱신 UI와 원본 12개 표 열을 연결했다. 기존 공개 종목 메타데이터 및 공통 스크리닝을 사용한다. 일반/실시간이 같은 메모리 catalog를 재사용하며 명시적인 새로고침은 재조회한다. 종목명 클릭은 기존 백테스트 보기 명령과 동일하다. 새 전략 엔진/DB 시세 저장은 없다.
- 실제 작업이 없으면 취소/중지/빈 관심종목 관련 명령을 비활성화하고, 바뀔 때 CanExecute 알림을 보낸다. 실제 주문 차단 정책은 유지한다.
- 빈 차트에는 가짜 좌표계를 남기지 않고 원본처럼 자료 없음 안내를 표시한다. Windows에서는 창 장식을 포함한 원본 Width/Height와 Avalonia의 client 크기 차이를 첫 열림에서 보정한다. 계좌 창 client는 실제로 양쪽 모두 674×751이었다. Mac에는 이 보정을 적용하지 않는다.

## 실제 검사 범위와 증거

`artifacts`는 로컬 진단 자료이며 Git 업로드 대상이 아니다. 캡처 전에 foreground PID를 확인하고 Win32 client 좌표로 캡처했다. UIAutomation 값이나 별도 RenderTargetBitmap만으로 화면 정상이라고 판정하지 않았다.

| 검사 | 결과/근거 |
|---|---|
| 두 일반 앱의 메인 7개 탭 | 각 탭 이동 후 실제 호버/팝업 캡처, `interactive-native-final/result.json` |
| 일반 버튼 호버 | WPF58 + Avalonia50 = 108개, 버튼 명령 실행은 하지 않음 |
| ComboBox 실제 열기/옵션 위치 호버/Escape 닫기 | 양쪽 각7개 = 14개, 닫힘 상태 확인 |
| 펼침 헤더 | 양쪽 각18개 × 좌측2/8/14/40px = 144개 상태 검사, 복원 포함288회 실제 클릭. `interactive-native-expanders-final/result.json` |
| 채운 스크리닝 표 | 공개 종목20개 조회, 실제 행의 3상태 픽셀 모두 WPF 기준 일치. `interactive-native-final/actual-row-colors.json` |
| 표 헤더 중심 | 원본/이식 각12열, 중심 오차 최대0.5px. `interactive-design-pass2/*-header-centers.json` |
| 실제 코드 열 정렬 | 헤더 클릭 후 첫 코드000250 확인, `interactive-native-final/avalonia-grid-sorted.png` |
| 계좌 창 | 양쪽 마스킹 상태로 열기/닫기만. 키 읽기·표시·초기화·연결 명령 없음. `interactive-native-final/account-client-sizes.json` |
| 편집 콤보/전략D 창 | 일반 앱에서 포커스/펼침/닫기·설정 창 열기/닫기, `interactive-native-final/avalonia-editable-strategy-*.png`, `avalonia-day-settings.png` |
| 격리 상호작용 회귀 | 26개: 버튼 hover/pressed/release, 내부 배경, 비활성, 옵션 생성/선택, 4개 화살표 위치, 체크 영역, 표 선택/정렬, 스크롤 드래그, 편집 콤보, 분야 필터/메모리 재사용/수동 갱신. `interactive-last-states.log` |

`audit-native-control-states.ps1`는 일반 버튼에 호버만 한다. `audit-native-expander-hits.ps1`는 명시한 조건 헤더 이름/ID만 클릭한다. 사용자의 정상 앱을 조작하므로 데스크톱 자동화끼리 동시에 실행하지 않는다. 기존 스크롤 위치 때문에 미방문한 헤더가 생기는 검사 문제를 수정하여 처음에 스크롤 위치만 초기화한다.

모든 활성 버튼을 일괄 누르려던 검사는 자동 승인 검토에서 거절됐다. 키 초기화·삭제·중지·주문이 MouseDown 시 실행될 수 있다는 이유다. 우회하지 않았으며, 일반 버튼의 눌림/해제는 외부 작업을 하지 않는 격리 위젯에서 검증했다. 따라서 모든 실제 명령을 실행해 봤다고 보고하지 않는다.

## 수정 파일

- `Stock.Avalonia/Styles/ControlStates.axaml`, `Styles/StockTheme.axaml`, `App.axaml`: 상태별 테마/공통 외관.
- `MainWindow.axaml`, `Views/StrategyPanel.axaml`, `Views/AccountDialog.axaml`, `Views/WpfFrameSizing.cs`: 툴바/제목/마스킹 입력/창 크기.
- `Views/ScreeningSettings.axaml`, `ScreeningPanel.axaml(.cs)`: 분야/원본 표 열/종목 클릭.
- `ViewModels/ScreeningWorkspace.cs`, `RealtimeScreeningWorkspace.cs`, `StrategyEditor.cs`, `MainViewModel.cs`, `MarketWorkspace.cs`, `PresentationWorkspace.cs`: 표시·분야 catalog·명령 상태 알림.
- `Views/StockChart.axaml(.cs)`: 빈 차트 표시.
- `Stock.Desktop.Tests/InteractiveStateChecks.cs`, `Program.cs`: 새 상태 검사를 기존 전체 회귀에 포함.
- `scripts/audit-native-control-states.ps1`, `audit-native-expander-hits.ps1`, `NativeUiAuditHelpers.ps1`: 안전한 일반 앱 마우스 검사. `-Helpers scripts/NativeUiAuditHelpers.ps1`를 전달한다. 검사에 사용했던 로컬 `artifacts/interactive-tools/NativeUiHelpers.ps1`과 동일한 helper다.

Core80개 및 원본 WPF는 수정하지 않는다. 최종 EXE 해시·회귀/배포 실행 결과는 HANDOFF/TASKS의 이 문서 이후 마감 기록을 따른다.

## 남은 차이/실행 범위

원본과 이식 앱의 저장된 전략/관심종목/실행 결과가 서로 달라 데이터 내용을 픽셀 비교 대상으로 삼지 않았다. 사용자 DB와 API 키는 복제하지 않는다. Avalonia 계좌 저장은 아직 메모리만 지원하며 원본의 암호화 영속 키/기본 계좌 UI와 기능상 차이가 있다. 이를 작동하는 것처럼 표시하지 않는다. 실제 주문은 잠겨 있다.

Windows 실기에서 공통 컨트롤의 요청된 차이를 수정·검증했지만, 모든 창의 모든 픽셀이 완전히 동일하다고 결론 내리지 않는다. WPF/Skia 글꼴 렌더링·차트 눈금/자동 범위 차이와 실제 Mac 폰트·DPI·트랙패드·창 장식 검증은 남는다. Mac cross-publish 성공은 실제 Mac 실행 검증이 아니다. GitHub 게시 요청은 없어 push/tag/Release하지 않았다.
