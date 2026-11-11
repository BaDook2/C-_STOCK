# 현재 인수인계 — v0.1.18 릴리즈 진행

사용자가 2026-11-11 현재 프로그램의 GitHub 릴리즈를 명시 요청했다. 이전 게시 금지는 이번 Windows/Apple Silicon 릴리즈 범위에서 대체된다. 실제 자동주문·실제 기록 삭제 금지는 유지한다. source HEAD ca8ea4a에서 누적 미커밋 Inje/Bin/UI/공개 가격 변경을 v0.1.18로 준비했다. 원격 main도 ca8ea4a이며 기존 공개 최신은 v0.1.17이다.

## 완료/다음

릴리즈 노트 `docs/avalonia/RELEASE_NOTES_v0.1.18.md`, Avalonia 버전0.1.18, 두 CI에 Bin187 검사와 통합 설명서 JSON 검사를 추가했다. `AGENTS.md`에 명시 게시 승인을 기록했다. 키/DB/바이너리는 제외했고 credential pattern 후보0개다. 첨부 Markdown 원문의 두 칸 줄바꿈은 의도된 줄바꿈이므로 보존한다.

Git 추적/추가 예정579파일만 `/private/tmp/stock-v0.1.18-gitonly-ipbshdim`로 복사해 실행 소스가 staged source와 같음을 확인했다. Git-only Avalonia/WPF교차 빌드0경고/0오류, Inje363/Bin187/Avalonia861/디자인17/manifest104/설명서 JSON6개 PASS. 로그·복사목록은 `../work/release-0.1.18-gitonly*`다. 현재 staged 파일은237개이며 이전 누적 변경 전체를 포함한다. 소스를 commit/push한 뒤 Windows workflow_dispatch `release_tag=v0.1.18`로 실행한다. workflow는 검사와 EXE smoke 후에만 새 태그/Release를 만든다. 기존 태그/자산을 덮어쓰지 않는다.

Windows 성공 후 `macos-release.yml`을 `build_tag=macos-v0.1.18-r1`, `runner_image=macos-26`으로 실행한다(지난 macos-15가 배정되지 않았던 근거로 성공한26을 사용). 동일 Core/Avalonia 소스, Mac 전체 검사·Keychain·서명·앱/ZIP/DMG·실제 공개 차트·공개 다운로드 실행이 필수다. GitHub CLI 로그인은 BaDook2로 유효하며 릴리즈/dispatch 지원 connector가 없어 gh를 사용한다. 최종 공개6개 자산을 다시 다운로드해 hash/API digest/CI 결과를 대조하고 링크를 사용자에게 전달한다.

기존 v0.1.17 자산 snapshot은 `../work/release-0.1.18-existing-release.json`이며 이전6개 ID/hash가 그대로인지 확인한다. 새 검증 정본은 `docs/verification/Release-v0.1.18.md`로 아직 준비 상태다. 성공 후 최종 결과와 TASKS/HANDOFF를 기록한다. 현재 실패/막힘 없음. 사용자의 '계속'은 이 배포의 진행을 의미하며 새 태그를 중복 만들지 않는다.

## 보존할 범위

두 전략 설정은 Inje/Bin 단일 사용 체크·전체 JSON 프로필·실제 파라미터다. Bin은 백테스트/일반 스크리닝 연결, **실시간 가상 주문/원장 복원은 미연결로 시작 차단**. Inje 원장 보호/복원 유지, 실계좌 자동 전략 주문 잠금 유지. 공개 가격에 StockViewer NXT/미국 시간외·데이마켓·1초 목표 조회/재시도를 포팅했다. 세부 근거는 `Viewer-Price-JSON-2026-11-11.md`와 기존 한 파일 통합 전략 설명서를 따른다.

설치 개발 앱은 13:36 KST 검증본 `/Applications/Stock Development.app`이고 공개 예정0.1.18과 다르다. 사용자 DB/키/실제 기록은 열거나 수정하지 않는다. 이전 앱/중복정리 복구본·StockViewer 저장소·기존 ZIP/DMG 보존. 릴리즈 전 로컬 상태는 `docs/history/*before-Release-v0.1.18-20261111.md`에 있다.
