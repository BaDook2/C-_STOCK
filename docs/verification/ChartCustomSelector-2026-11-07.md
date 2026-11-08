# 온라인 지정봉 버튼 복구 — 2026-11-07

원인: 주/월/틱 표시 선택기를 정리하면서 지정봉 진입 버튼 없이 숫자/단위 콤보만 남겼다. 기간 영역의 가로 StackPanel+고정140px 여백이 선택기를 차트 밖으로 밀어낼 수 있었다.

수정: MainWindow.xaml에 ChartCustomButton/ChartCustomBarNumber/ChartCustomBarUnit, 온라인에서만 보이는 ChartCustomOptions를 추가하고 공용 WrapPanel로 배치했다. ChartWorkspace.CustomChartSelected는 기존 ChartPeriod/ChartBarValue/ChartBarUnit/분봉 조회를 그대로 사용한다. 작은 창 차트 높이 회귀가 발견돼 상단 옵션 여백과 기간 버튼 간격도 줄였다. 오프라인 일봉/온라인 지정봉·일봉 정책과 API 지원 조건은 유지한다.

검증: Release 빌드 경고0/오류0. 가짜 증권사 실제 WPF 전용28개 및 UI smoke612개 성공. 1540×1040/1160×760에서 선택기 전체가 차트 영역 안에 있고, 지정봉 버튼으로 분봉 렌더링/일봉→지정봉 복귀·기존 작은 창 차트 높이를 확인했다. ConnectionTests는 숨겨진 지정봉을 오프라인 기간 버튼으로 세지 않도록 실제 표시 상태를 검사한다. 로그 artifacts/chart-custom-{build,checks,ui,publish}.log. 최종 전용28개는 UI smoke612개에도 포함된다.

publish 및 루트 단일 EXE smoke 종료0/Success/NoBrokerCalls=true. artifacts/chart-custom-{publish-smoke,root-smoke}/result.json. 실제 계좌 통신/주문/키 변경 없음. 사용자가 전략 값을 편집 중인 실제 앱을 닫지 않았다. 이전 EXE를 artifacts/before-chart-custom-20261107-182456/running-Stock.exe로 보존하는 파일 교체가 성공해 현재 앱/편집 내용은 유지되며 재시작부터 새 UI가 적용된다.

루트Stock.exe/release/Stock.exe/publish/win-x64/Stock.exe는80,857,241bytes, SHA256 23399B9FB8B75953EAC177893E1A917BC8BB31ECE47E790BA9B05BABFB3F9B07. release 체크섬 갱신. GitHub commit/push/tag/Release 없음. 실제 앱에서 온라인 연결을 새로 수행하지 않았으며 온라인 지정봉 렌더링은 가짜 증권사로 검증했다.
