# WPF 기준 디자인 이식 — 2026-11-08

**후속 정정:** 이 문서의 정적 비교만으로 상호작용까지 같다고 판단할 수 없었다. 최신 [실제 마우스 상태 비교](INTERACTIVE_UI_PARITY_20261108.md)를 우선한다. Fluent 내부 상태 덮어쓰기, 펼침 hit 영역, 콤보/표/스크롤 및 누락된 분야 UI를 추가 수정했다. 편집형 전략 이름 변경도 원본 툴바 방식으로 맞췄으며, 아래의 별도 이름 변경 대화상자 설명은 이전판 기록이다.

원본 `../STOCK`의 실제 XAML·TossInspired 스타일과, 동일한 1540×1040 합성 자료 화면을 기준으로 별도 Avalonia UI를 조정했다. 원본 WPF 소스/루트 EXE, 사용자 DB와 키는 변경하지 않았다. 전략/Core 소스 80개의 원본·manifest 차이는 0이다.

## 반영한 범위

- 상단 STOCK 로고, 모드 선택, 계좌 연결/긴급중지, 메인 탭 순서·밑줄, 상태바.
- 원본의 배경 `#F7F8FA`, 카드 `White`, 선 `#E5E8EB`, 파랑 `#3182F6`, 본문 `#191F28`, 보조 글자 `#8B95A1`. 카드 반경18, 버튼/입력 반경10, 글꼴13 기본. 우측 주문 영역은 최소14.
- 종목·차트의 190 / 여백12 / 가변 차트 / 여백12 / 350 열 구성. 원본과 같은 제목·가격·지표/기간 행, 관심 별, 매매 표시 옵션, 우측 2×2 도구 탭.
- 실제/가상 주문, 전략/정기매수 백테스트는 기존 ViewModel/Command에 연결했다. 미구현 실계좌 전송은 계속 비활성이다. 빈 결과 카드는 실행 전 숨긴다.
- 일반/실시간 스크리닝의 분리 표시, 공통 시장·전략·데이터 기준 설정, 관심그룹 선택과 표. 공유 설정 UI만 분리했으며 판정·주문 엔진을 추가하지 않았다.
- 전략 설정의 백테스트8 / 공통53 분류, 접을 수 있는 의미별 그룹, 3열 배치와 의도적인 빈칸, 32높이 편집기, 옆 단위와 도움말. 이름 변경 대화상자, 기본값 편집 복구. 기본값 버튼은 저장 전 기존 등록값을 덮어쓰지 않는다.
- 계좌·주문은 상하 보유/주문 표, 기록은 실행일 조회+결과 표+메모리 로그, 시장은 국내/해외 지수 카드, 관심종목은 그룹 탭과 빈 상태 안내 형태로 정리했다.
- 계좌 팝업에 원본의 네 증권사 로고 타일을 포함했다. 기존 세 PNG와 토스 ICO의 이미지 프레임만 가져왔으며 비밀 데이터는 포함하지 않는다. 키는 이번 전환판의 기존 메모리 정책을 유지한다.
- 전역 버튼/텍스트박스/콤보박스/체크/라디오/탭/펼침/툴팁/표/스크롤바 스타일. 달력 버튼은 아이콘과 날짜 입력이 다시 정상 렌더링되도록 별도 스타일을 적용했다.
- ScottPlot5는 그대로 사용하면서 오른쪽 정수 가격축, 옅은 가로 격자, 거래량·날짜 축, 원본 MA5/20/60/120 색상과 굵기를 맞췄다. 표시 구간 밖의 과거 봉을 이동평균 준비 자료로 사용할 수 있어, 짧은 화면에서도 MA120이 사라지지 않는다. 준비 자료가 백테스트 입력을 변경하지는 않는다.
- 일목 표시용 선행구름을 추가했다. 확정된 과거 9/26/52 구간의 중간값을 기존 봉 안으로 26봉 이동해 그리며, 빈 미래 봉을 추가하지 않는다. 거래/지지 snapshot이나 전략 조건에는 사용하지 않는다.
- 시장 미니 차트는 축·검은 프레임을 없애고, 데이터가 없을 때 빈 좌표계를 보여주지 않는다. 자료가 없어졌을 때 이전 선도 지운다.

## 비교 중 발견해 수정한 문제

1. 패널의 DataContext가 각각 Screening/Realtime으로 바뀐 뒤 메인 모드 속성을 찾지 못해 두 패널이 겹쳤다. 메인 Window의 모드 바인딩을 명시했다.
2. 파생 TextBox인 `SecretInput`/`StrategyNumberBox`에 기본 TextBox 테마 키가 없어 입력칸이 실제로 렌더링되지 않았다. `StyleKeyOverride`를 TextBox로 연결하고 실제 Template/높이 검사로 보강했다.
3. 전역 버튼 템플릿이 날짜 선택기의 내부 버튼을 빈 파란 영역으로 만들었다. 날짜용 버튼의 캘린더 아이콘을 따로 정의했다.
4. 화면용 가격/모드 알림 초기화가 누락된 상태를 바로잡아 실제 quote 값이 제목 가격에도 반영된다.
5. ScottPlot 날짜 축에 숫자용 수동 눈금을 넣는 방식은 설치 버전에서 금지된다. 검증 중 예외를 확인해 제거했고, 날짜용 생성기와 서식을 유지한다.

## 수정 파일 지도

- `Stock.Avalonia/MainWindow.axaml`, `Styles/StockTheme.axaml`: 공통 외관/메인 구성.
- `Views/OrderToolsPanel`, `AccountPanel`, `HistoryPanel`, `MarketPanel`, `WatchPanel`, `ScreeningSettings`의 axaml/cs: 재사용 가능한 화면 구성.
- `Views/StrategyPanel`, `ScreeningPanel`, `RealtimeScreeningPanel`, `AccountDialog`: 기존 UI 재배치.
- `Views/StockChart.axaml(.cs)`, `StockChart.Appearance.cs`, `LineChart.cs`: 기존 차트의 외관·표시 지표.
- `Views/SettingInput.cs`, `StrategyNumberBox.cs`, `SecretInput.cs`, `BrokerLogoConverter.cs`, `Assets/Brokers`, csproj 리소스 선언.
- `ViewModels/PresentationWorkspace.cs`, `MainViewModel.cs`, `MarketWorkspace.cs`, `StrategyEditor.cs`, `ResearchWorkspace.cs`, `ScreeningWorkspace.cs`, `AccountWorkspace.cs`: 화면 알림·기존 편집 모델의 분류/표시 어댑터.
- `Stock.Desktop.Tests/DesignChecks.cs`, `Program.cs`, `scripts/verify.ps1/.sh`: 합성 비교 화면과 조작 회귀 검사.

AXAML은 들여쓰기로 정리해 직접 수정할 수 있다. 모델/전략/Repository의 책임은 기존 구조를 유지한다. 상태바 로그는 최근100개 메모리 자료이며 재조회 시세의 DB 저장을 추가하지 않았다.

## 검증

| 검사 | 결과 |
|---|---|
| Release solution | 경고0 / 오류0 |
| Portable Core | 192개 성공 |
| 기존 AXAML/기능 회귀 | 74개 성공 |
| 디자인·실제 입력 상호작용 | 16개 성공 |
| 루트 EXE Windows native 시작 | Exit0,93봉,2회 quote,정상 종료 |
| Core 원본·manifest | 80개 동일 |
| Mac arm64/x64 | self-contained cross-publish 성공, Mach-O apphost 확인 |

16개 추가 검사는 가격 바인딩, 모드별 패널 교체, 의미별 설정/61개 입력, 두 창 크기, 빈 결과 숨김, 달력 열기, 일목 표시, 숫자·비밀 입력칸의 실제 테마/높이, 로고4개 로드, 증권사 변경 시 키 비우기를 포함한다. 기존74개에는 검색, 일주월/온라인 지정봉, 백테스트·기록, 실시간 가상매매, 긴급중지, 마커·휠·드래그·우클릭 범위가 포함된다. 실제 주문/개인키/실시간 시장으로 검사한 결과는 아니다.

로그: `artifacts/design-final-build.log`, `design-final-verification.log`, `design-ui-checks.log`, `design-capture.log`, `design-native-final/result.json`, `design-verification-summary.json`. 마지막 미니차트 표시 수정 후 UI74개와 디자인16개도 다시 성공했다.

비교 PNG는 `artifacts/design-reference/wpf-*.png` / `avalonia-*.png`. 원본 WPF는 읽기 전용 참조 프로그램(`artifacts/visual-reference`)으로 격리 합성 DB에서 렌더링했다. 비교 이미지의 가격은 합성값이다.

## 실행 산출물·확인 범위

루트 `Stock.Avalonia.exe`:107,264,823bytes, SHA256 **EF972F6F7D4EF48E928607554CF2DAA78E771B7587853E9E82DFCF6F9FC6B897**. 이전 실행 파일은 `artifacts/design-before/Stock.Avalonia.exe`에 보존했다. Mac 개발용 묶음은 `artifacts/macos/osx-arm64`와 `osx-x64` 아래에 갱신했다. 실제 Mac에서 소스의 `scripts/publish-macos.sh`를 실행하면 실행 권한도 설정한다.

원본 WPF 루트 `Stock.exe`의 SHA256 **E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934**는 유지했다. GitHub에 게시하지 않았다.

이 결과는 원본의 배치·색상·공통 컨트롤을 기준으로 한 디자인 이식이다. WPF의 사용자 정의 차트와 ScottPlot의 눈금 배치·자동 범위·글자 렌더링까지 픽셀 단위로 동일하다고 검증한 결과는 아니다. 최초 Avalonia 전환에서 아직 이식하지 않은 실계좌 전송/보안 저장/일봉 자동 실행 등 기능 제한도 그대로 남아 있다. 실제 Mac의 기본 폰트·DPI·창 장식·트랙패드는 아직 확인하지 못했다. Mac 실기 비교가 필요하며 이 항목을 완료로 표시하지 않는다.
