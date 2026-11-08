# 현재 — E1~E4 구현/검수 완료, v0.1.15 Windows 게시 준비

2026-11-08 최종 우선 상태: 사용자 요청의 미완료를 구현했고 Windows 검증을 마쳤다. C162D6C5DFD3538F5151F4BC93D09D82E27B7370ACB0FB935E8DB7A105D8EC23 후보 v0.1.15/Stock.exe. 원본 WPF Stock.exe E433...1934 보존 중. 아직 GitHub 게시/기존 EXE 교체 안 함.

- build0/0, Core192, 전체 Avalonia273, 디자인18, WPF1652, 실제공개28. 더미보안13/가짜 주문17/최종전송7/가짜실전adapter7/일봉checkpoint16/앱호스트5 포함.
- 최종 후보: 시작/종료601봉·2quote, 실제 키보드/마우스/Enter/반복·재실행4회 성공. 실제 백테스트4거래/+66,000, 양방향 팬/복귀/단일클릭 상세·해당 행 선택/202×201달력/익절빨강322픽셀·손절파랑333픽셀 성공. Mac 양RID 같은 소스 교차빌드 성공(실기 아님).
- 표시용 DataGrid 가격 변환이 기본 TwoWay로 원본을0/null로 변경하는 결함을 실제 채운 표 캡처에서 재현했다. 표시 열 모두 OneWay, 실제 시간 간결 표시로 수정. 전후 원본 값과 셀10,000/10,100/100을 검사했다. 편집 TextBox/선택 CheckBox TwoWay 유지. `artifacts/e5-account-display-fixed/synthetic-account-tables.png`.
- 소스만 sibling STOCK Git 저장소에 통합: Stock.Avalonia/Stock.Desktop.Tests/Stock.Portable.Tests/STOCK.Avalonia.sln/avalonia-scripts/packaging. 공유85개 local raw byte 동일; Git CI/Mac 줄바꿈을 위해 manifest는 HashMode Utf8Lf. 기존 WPF/UI/원본 DB/키/EXE는 유지.
- `release.yml`은 기존 WPF 전체/전략D + Core/새 UI/디자인/manifest + Avalonia 단일EXE --startup-smoke 후에만 새 태그를 게시한다. 기존 자산 clobber 금지. README-Avalonia/개인파일 ignore 추가. 포함 후보471개에서 DB/키/실행파일/로그0, 의심 비밀 literal0.
- 다음: 통합 repo 경로 빌드/Core/UI273 확인(artifacts/avalonia-integrated-*), 최신 docs 복사/검토→commit/push/새 v0.1.15 tag→CI 끝까지 확인→공개 Stock.exe/sha 재다운로드→실행/화면 검사→이전 EXE 백업 후 root Stock.exe 및 별도 Avalonia EXE 교체. 로컬 바이너리와 CI SourceRevision 포함 바이너리 SHA가 다를 수 있어 공개 checksum과 비교한다. 태그v0.1.15는 마지막 조회 때 없었고 origin main fbeed251이었다. git push 인증은 아직 검사하지 않음.
- 실제 돈 주문0/원본 키·DB 읽기·복제·삭제0. 실제 Mac Keychain/실기·실제 증권사 API·서명/공증 미검증. Windows EXE만 게시하며 Mac 번들을 실기 검증한 것으로 공개하지 않는다. 실전 자동은 키움 국내+검증 facts/실제거래대금 입력 필요, paper 가정 금지. 일봉 가상은 다음 거래일 새 관측 이후 체결 모형이며 공식 시가 보장 아님. 새 가상 복원은 paused/신규매수 OFF, checkpointv2(일 손실 기준) 사용.

상세: docs/COMPLETION_E1_E4_20261108.md. 아래는 구현 중/이전 D3 이력이다. 사용량40% 중단은 해제됐고 새 agent 생성 요청 없음.

---

# 이전 — 사용자 답변 수신: 미완료 기능부터 완성 (E1/E2 진행)

2026-11-08 최신 사용자 답변은 **미완료 기능을 먼저 완성**이다. 아래 D3의 답변 대기 문장은 이전 기록이다. 기존 WPF EXE와 공개 릴리즈는 보존하며 기능 완료/검증 후 조건부 교체·게시한다. 사용량 중단 조건은 해제됐다. 실제 돈 주문은 실행하지 않는다.

- E1: 공통 Core/Security로 기존 Windows DPAPI 형식을 추출해 원본 WPF wrapper도 재사용. Avalonia Mac Keychain native adapter 추가(실기 미검증), 별도 경로/기본 계좌/인증+잔고 확인 후 저장/초기화 확인. 격리 더미 키 검사11/기존 온라인21 성공.
- E2: AccountOrders.cs/RemoteTradingSession/OrderGuard 기반 수동 주문 활성화·각 주문 확인/한도·최종 계좌/중지 확인·취소 대사/실제 확인 체결/차트 표시 추가. 가짜 broker14검사 성공, 실제 주문0. 자신의 write-ahead 전송 전 기록을 Unknown으로 오인하던 차단을 발견했다. 안전 검사 제거는 자동 승인 거절. 대신 동일 guard가 쓰기 성공 후 발급한 현재 intent ID만 식별하고 다른 전송 중/모든 응답 불명/미확인 취소 차단은 마지막까지 유지했다.
- 아직 E1/E2 전체 회귀·원본 WPF 검사/manifest 갱신 필요. E3 실제 전략 adapter, E4 일봉 자동 가상+복원, E5 통합 검수/게시 판단 미완료. 아래 후보258F...는 **현재 소스보다 이전 바이너리**다. 원본 Stock.exe 교체/새 GitHub 작업 없음.
- 다음 시작: TASKS E1~E5, AccountWorkspace.cs/AccountOrders.cs, Core/Security, OrderGuard.cs/Trading/RemoteTradingSession.cs. 신규 코어3개/수정 파일은 양쪽 동기화하고 hash manifest 재생성 후 검사. 기존 계정 키/개인 DB 읽기·복제 없음.

---

# 이전 — D3 손익 색상/최종 검수 완료, 교체·릴리즈 범위 답변 대기

2026-11-08. AGENTS → TASKS → 이 문서 → docs/RELEASE_READINESS_20261108.md. 이전 D2 정본은 docs/history/HANDOFF-before-D3-release-check-20261108.md, 세부 차트/달력 원인과 코드는 docs/D2_NATIVE_CHART_CALENDAR_20261108.md.

## 최신 사용자 요청과 현재 결정

사용자는 익절 빨강/손절 파랑, 모든 화면·기능 재검수 후 완벽하면 기존 WPF Stock.exe를 없애고 Avalonia Stock.exe로 교체·GitHub Release하라고 했다. 색상/검수/실행 후보까지 완료했으나 다음 기능의 동등성은 충족하지 못했다.

- 실계좌 주문·취소·실제 체결 표시 UI가 비활성화돼 있다.
- 키·기본 계좌 영구 저장/복원 및 Mac Keychain 미구현. 현재 메모리 인증/조회만 가능.
- 일봉 다음 거래일 자동 가상 실행·미체결/보유 복원 어댑터 없음. 일봉 자동 주문을 명시 차단한다.
- 실제 증권사 read-only API/실제 Mac·다른 DPI·트랙패드·서명/공증 미검증.

따라서 기존 Stock.exe와 GitHub는 바꾸지 않았다. 사용자에게 (1) 미완료부터 완성 (2) WPF 유지 테스트용 사전 공개 (3) 누락 수용 후 교체·게시 중 범위를 질문했다. 아직 답변이 없다. 시간 경과/기본 선택을 승인으로 간주하지 않는다. 답변이 오면 그 범위를 우선한다. 누락을 무시하고 완벽하다고 보고하지 않는다.

사용량40% 중단 조건은 사용자가 해제했다. 실계좌 실제 주문은 여전히 실행 금지. 새 agent/채팅 생성 요청 없음.

## 산출물

- 현재 root Stock.Avalonia.exe와 후보 artifacts/release-candidate/Stock.exe는 같은 단일 파일,107330359bytes.
- SHA256 258F8CB67F7E67C90FAE57D5923DC70CCD983D25F9D69E2B120318AF267C095E. 각각 .sha256 파일 일치.
- 후보 readiness.json: supported tests pass / ReplacementReady:false / PublicationPerformed:false / PendingUserScope:true.
- 기존78CB판: artifacts/before-outcome-colors-20261108/. 이전1D/659/E85 백업도 보존.
- 원본 WPF Stock.exe E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934 그대로. Core80개 원본/manifest 변경0.
- GitHub 최신 v0.1.14, main fbeed2515294254d72286e6da83812af8857d6ce는 조회만 했고 변경 없음.
- Mac 양RID artifacts/macos/<RID>/Stock.Avalonia.app도 최종 색상 소스로 cross-publish. 실제 Mac 성공 아님.

## 수정과 검사

- Views/ChartMarkerLayout.cs: TradeMarkerPalette/그룹 ColorHex. 실제 손익 우선, 본전/미확인 중립. 병합 순손익으로 색 지정, 미확인 실현손익을0원으로 만들지 않음.
- Views/StockChart.axaml.cs: 글자·테두리·ScottPlot 가격 표식에 같은 색 사용. 매수는 기존 빨강, 익절빨강/손절파랑. 모든 공유 차트 화면에 적용.
- Stock.Desktop.Tests/TradeOutcomeColorChecks.cs + Program.cs:8개 실제 outcome/renderer 검사, 전체 UI207.
- scripts/verify-native-backtest.ps1 -CheckOutcomeColors: 정상 일반 EXE의 배지 텍스트 영역에서 실제 red/blue 픽셀을 확인.
- 0경고0오류/Core192/UI207/디자인18, 실제공개28. artifacts/release-final-regression.log, release-final-public.log.
- 두 일반 앱 각7탭/버튼 호버99(WPF49/Ava50)/콤보14 실제 열고 Escape 닫기 성공:release-final-native-pages/result.json. 주문/키 초기화/삭제 명령 일괄 실행은 하지 않음. 함수 실행은 격리 workflow fixtures와 실제공개 흐름으로 구분.
- 후보 Stock.exe --startup-smoke:Exit0/Success/NoBrokerCalls,601봉/2quote. release-candidate-smoke/result.json.
- 후보 일반 EXE 실제 키보드·마우스/Enter·반복/재실행4회 성공:release-candidate-native-search/result.json.
- 후보 실제 백테스트4거래/+66,000, 익절+98,000 배지 빨강322/파랑0, 손절-32,000 배지 파랑333/빨강0. 팬/복귀/단일 클릭·상세 행 선택/달력 통과:release-candidate-native-backtest/result.json.
- 검사 옵션 초기 실수 --smoke-test는 WPF용이라 결과가 없었고 직접 만든 후보 프로세스만 닫았다. 올바른 Avalonia --startup-smoke로 다시 성공. 이 실패를 앱 오류/성공으로 혼동하지 않는다.
- 배포 출력에 DB/보호키/credentials/secrets 파일0개. 원본 DB/키 복제·실제 주문·데이터 삭제 없음. 공개 백테스트 기록은 검수로 생성됐으며 삭제하지 않았다.

## 현재 실행/다음 작업

후보 프로세스 PID는 artifacts/release-candidate-native-pid.txt에 기록. 검토용 빨강/파랑 백테스트 창이 열려 있을 수 있다. 직접 시작한 비교용 WPF/소스 앱은 정상 닫았다. 다음 실행에서는 PID/path를 재확인하고 다른 사용자 앱을 종료하지 않는다.

현재 수정 중 코드 없음. 사용자 범위 답변이 필요하다. 미완료 우선이면 A7/일봉 실행/복원을 단위로 나눠 진행한다. 사전 공개/교체를 수용하면 다음 준비도 필요하다.

- stock_avalonia는 아직 Git 저장소가 아니다. 공개 원격은 sibling STOCK의 https://github.com/BaDook2/C-_STOCK.git.
- 기존 GitHub release workflow는 WPF Stock.App을 게시한다. exe 자산만 올리지 말고 Avalonia UI/검사 프로젝트·공유 Core·스크립트/README·CI를 같은 소스 커밋에 반영한다.
- 원본 STOCK에는 이전 작업의 다수 tracked 수정과 untracked 소스/문서가 있다. 버리거나 무조건 stage하지 말고 현재 코드와 개인정보 제외를 검토한다. Core80개는 같은 검증 소스이므로 중복 코어 폴더를 만들지 않는다.
- 새 태그를 사용하고 기존 태그 덮어쓰기/자산 clobber 금지. CI 성공 뒤 공개 파일을 재다운로드해 SHA/실행 재검증한다. 실제 기능 누락과 설정/키 자동이관 없음은 릴리즈 설명에 명시한다.

## 보존할 D2 수정

Program.cs Win32 RedirectionSurface+ShouldRenderOnUIThread=true, QueryTimeout45초/취소·세대/HTTP timeout 구분, Realtime.Dispose ConfigureAwait(false), 전체 backing history/DisplayBarCount·새 봉 팬 유지, Sequential봉·포인터15봉 줌·자동Y/거래량·4px 팬·스크리닝 전체/상세, 한글 Canvas 배지/충돌 병합/번호·클릭/202×201 달력, 인증 전 모드 OneWay 표시/지정봉 지원 사유·기본 시장 전체 유지.

native 검사기는 foreground/client를 확인하고 다른 UI 자동화와 병행하지 않는다. 창 생성 대기12초/유효 좌표만 클릭, topmost 소유자에서는 새 자식 창도 올린다. DataGrid UIA Selection 미지원은 실제 상세 텍스트/같은 행의 선택색 픽셀과 VM 객체 검사로 구분한다. 개인 키를 채팅으로 받거나 원본에서 복제하지 않는다.
