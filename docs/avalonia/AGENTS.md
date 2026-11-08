# STOCK Avalonia 지침

이 파일 → TASKS.md → HANDOFF.md → docs/MIGRATION.md 순으로 읽는다. 원본 sibling STOCK(WPF) 소스를 유지한다. 최신 사용자(2026-11-08)는 모든 화면·기능 검증이 완료되면 기존 Stock.exe를 Avalonia Stock.exe로 교체하고 GitHub Release하라고 조건부 요청했다. 검증/기능 동등성 미충족 상태에서 기존 EXE를 삭제·교체하거나 게시하지 않는다. 교체 시에도 이전 EXE는 백업한다.

- 사용자 예산(2026-11-08 최신 답변): 사용량 조건은 무시하고 계속 작업한다. 이전 주간/월간잔여40% 중단 지시는 해제됐다. 과거 TASKS/HANDOFF의40% 중단 기록은 당시 이력이며 현재 작업 중단 조건으로 적용하지 않는다.
- Core는 검증된 공통 소스85개이며 docs/core-source-manifest.json에 SHA256. 공통 전략/시세/백테스트/주문 로직을 UI별로 중복 구현하지 않는다. 코어 수정은 원본 반영 여부와 양쪽 검사를 기록한다. E1~E4는 docs/COMPLETION_E1_E4_20261108.md, 일봉 가상 원장은 checkpoint v2(거래일 손실 기준 포함)를 사용한다.
- MVVM/DI/Service/Repository 재사용, 비동기/취소/늦은 응답 방지, UI collection은 Dispatcher. 원본 이벤트·재조회 시세는 메모리만, 설정/관심종목/확인체결/테스트 재현 snapshot은 영속.
- 원본 DB/API키/개인설정/.git/bin/obj/배포파일을 복사하지 않는다. 별도 앱 저장 경로. 키를 평문 소스/로그/SQLite/JSON에 저장하지 않는다.
- 기존61개/레거시ID/JSON 의미 유지. D는 schema2/별도ID, 같은Runtime/Portfolio/BacktestService. 실계좌 주문은 사용자 명시 활성화 전 실행 금지. 테스트는 격리 DB/더미키/가짜broker/합성 스트림.
- 오프라인 일봉 / 온라인 지정봉(분1~59·시간1~12)·일봉. 틱은 내부 데이터이며 공개 폴링을 원본틱으로 위장하지 않는다. 가격 표시는 내림 정수·계산 정밀도 유지. %두자리 기본, 수수료0.015는 정밀도 보존.
- 실패를 성공/빈 자료로 숨기지 않는다. 미지원은 차단 사유 표시. 시각/연속성/당시 종목·위험 미확인은 신규 진입 차단. Windows/headless/cross-publish와 실제 Mac 실행 검증을 구분한다.
- 단위별 빌드/관련 검사, 완료 전 회귀. 작업/미검증/제한은 TASKS/HANDOFF. GitHub push/tag/Release는 명시 요청 때만(현재는 위 조건부 요청 있음). 사용자/지침 별도 요청 없으면 agent/스레드 생성 금지.
- 검색·시세·차트 작업은 fixture 검사만으로 실제 조회 완료를 보고하지 않는다. scripts/verify-public.ps1/.sh의 실제 공개자료 흐름과 배포 앱 --verify-public <격리출력폴더>를 확인한다. 네트워크 실패/실제 Mac 미검증/실제 증권사 미검증은 별도로 기록한다. 지원하는 각 화면의 정상·오류·취소·종료 경로를 점검하되 실제 주문은 실행하지 않는다.
- Windows 화면 갱신/검색 수정은 scripts/verify-native-search.ps1로 **일반 EXE·실제 키보드·마우스/Enter·화면 픽셀**도 검사한다. --verify-public의 명령 직접 호출·RenderTargetBitmap이나 UIAutomation 텍스트만으로 실제 창 표시 성공을 판단하지 않는다. 보이는 차트와 로딩 해제를 확인하고 재실행/반복 검색을 포함한다. 이 검사는 실제 데스크톱을 조작하므로 다른 자동화와 동시에 실행하지 않는다.
- 디자인 수정은 기본/호버/누름/비활성/포커스/선택/펼침 상태를 구분하고 원본 실제 창과 대조한다. PART_ContentPresenter 등 Fluent 테마의 내부 상태 덮어쓰기를 확인한다. 실제 캡처는 foreground PID·같은 client 크기를 확인한다. 원본 표는 Row 기본색과 실제 Cell 선택색이 다를 수 있다. 안전한 탭/팝업/명시된 펼침 헤더만 실제 클릭하고, 키 초기화/주문/삭제를 포함한 모든 활성 버튼 일괄 누름은 하지 않는다. 눌림 상태는 격리 위젯으로 검증한다. 상세 docs/INTERACTIVE_UI_PARITY_20261108.md.
- 차트 수정은 표시 유무만으로 원본 동등성을 판정하지 않는다. 십자선/툴팁·거래량·포인터 기준15봉 줌·Y범위·드래그 양방향/복귀/경계·단일 거래 클릭·스크리닝 전체/상세 잠금을 따로 확인한다. 실제 정상 앱 마우스와 화면 픽셀도 검사한다. 숨겨진 온라인 봉은 강제 활성화하지 말고 격리/실제 자료 구분을 남긴다. scripts/audit-native-chart.ps1, docs/CHART_INTERACTION_PARITY_20261108.md 참고. 가격 시리즈의 봉 좌표와 실제 체결 시간을 혼동하거나 원본 백테스트 자료를 줄이지 않는다.
