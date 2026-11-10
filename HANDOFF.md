# 현재 — Windows v0.1.16 및 Apple Silicon macOS 앱 게시 완료

## 진행 — v0.1.17 Windows·Mac 릴리즈 요청 (2026-11-10)

사용자가 현재 버전의 양OS GitHub 릴리즈를 명시 요청했다. 앱Version0.1.17, RELEASE_NOTES_v0.1.17.md, Actions 수동 요청 입력을 준비했다. HTTPS/SSH Git 인증이 없고 GitHub 연결의 쓰기도403이다. Safari에 BaDook2 로그인 확인. GitHub CLI 설치·기기 인증의 repo/workflow 등 권한을 안내했고 사용자 계속 진행 지시로 승인을 진행했다. GitHub가 추가 본인 재인증을 요구해 Safari Confirm access 화면에서 사용자 패스키/인증 앱 확인을 기다리는 중이다. 인증 완료 후 CLI로 소스와 Actions를 진행한다. 아직 게시/태그 생성 없음. 수동 배포는 CI 전체 검사 뒤에 새 Windows/Mac 태그를 만들고 자산을 추가한다(기존 tag/asset 덮어쓰기 없음). 공개 다운로드 재실행 단계·증거 보존을 추가했다. 원격 시작main=d172d5ac079ef3734dbf1dea50c5e3f60976be76, 최신 공개v0.1.16. 로컬 새 버전0/0/Core192/UI629/디자인18 성공. Windows 단일 EXE 교차 생성·Mac 로컬 패키지 서명/원본·ZIP·DMG 시작/공개 차트 성공. Windows 실제 CI·Mac 최종CI·게시/공개 다운로드는 아직 진행 전이며 성공으로 기록하지 않는다. 현재 설치 앱/개인 DB/키/실제 주문 변경 없음.

## 최신 소스 — 전략 JSON 점검·설명서 (2026-11-10)

JSON 실제 명령의 전체 값 왕복·DB 재로딩 검사를 추가했다. D Apply의 무조건 Size=1 및 기존 기간 콤보의 같은 단위 재설정으로 가져온 크기가 사라지는 문제를 수정했다. 현재는 그대로 보존하고 새 단위 선택만1부터 시작한다. 파서는 unknown member·내부 중복 키·잘못된 기간 키를 오류로 처리하고 canonical period key로 정규화한다. OFF D도 검증해 저장 후 열기 실패를 막으며 영문 enum import를 지원한다(numeric export 유지). 기본값·전략 계산 변경 없음. 관련303/Core192/UI629/디자인18·Release0/0 성공, Core manifest3개 갱신. 근거 docs/verification/Strategy-JSON-2026-11-10.md.

현재 전략 논의 정본은 docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md. 사용자 후속 요청에 따라 전달본은 `../outputs`의 동일 Markdown 하나이며, 기존 전략/D ON의 전체 JSON2개를 문서 안에 모두 수록했다. 코드 기본값 기준(개인 선택값 아님)이다. D의 전체 엔진 전환·공유5개·hidden19개·정책60개·기간 우선순위·검증을 설명한다. 생성 fixture/catalog는 artifacts/strategy-json-audit. 이번 수정은 소스만 반영했고 설치 앱은 이전 UI 수정 버전 유지. 새 importer를 쓰려면 앱 정상 종료 후 Rider `.app 빌드 후 실행`. 실제 계좌·사용자 DB·키·실제 API·커밋/push/게시 없음.

## 최신 UI 변경 — 2026-11-10

전략 설정을 `백테스트 전용 → 급락 판단 전략 → 공통 전략 조건`으로 정리하고, 시점별 검증 자료 상위 영역·당시 종목/위험/시장 세션 JSON 입력을 제거했다. 사용 체크박스는 접힌 제목 옆에서 조작한다. OFF일 때 전체 목록을 비활성화했던 원인을 고쳐 하위 그룹은 항상 펼침/접기 가능하며 입력만 잠긴다. 제목 체크박스 hit testing은 해당 Expander에만 허용했다. ViewModel의 기존 UsageGroups는 유지하고 Scope로 두 목록을 표시하며 DataTemplate을 공유한다. 내부 자료 검증·전략/저장 로직 변경 없음. Release 빌드0/0, 관련49/UI326/디자인18 성공. 상세: docs/verification/Strategy-Layout-2026-11-10.md. 사용자의 교체 요청을 받아 `/Applications/Stock Development.app`에 반영했다. 검증본과 앱 DLL SHA256 일치(e27437314c3364985d1e6d72df48d1a086c297d6ce956edfcd79ecf05c04a134), strict 서명·설치본 네이티브 시작 성공(601봉/2quote). 근거 `artifacts/strategy-layout-installed/result.json`.

## 최신 로컬 작업 — Rider .app 워크플로

사용자의 Rider 수정→.app 생성 요청을 이행했다. `.run`의 `Mac · .app 빌드 후 실행` 또는 `.app 만들기`가 `avalonia-scripts/build-and-run-macos.sh`를 호출한다. 결과 `/Applications/Stock Development.app` (self-contained ARM64). 기존 publish-macos.sh 재사용, 출력의 FinderInfo/ResourceFork 정리 추가. Codex File Provider가 앱 루트의 FinderInfo를 재부착하여 작업 폴더 서명이 실패하므로 /private/tmp staging→Applications 설치를 사용한다. 실제 앱 생성·설치 후 strict 서명 검증·LaunchServices 격리 시작 성공(601봉/2quote, macOS27.2/osx-arm64). Rider UI에서 실행 설정 클릭 검증은 하지 않았다. 기존 소스 수정은 유지하며 커밋/push/Release 없음. 문서 docs/maintenance/Rider-Mac-App.md.

## 최신 로컬 변경 — 2026-11-09, 아직 미게시

사용자 요청 1~5에 따라 D 설정을 StrategyPanel의 고급 영역 `급락 판단 전략`으로 통합했다. 사용자 확인대로 OFF는 기존 전략, ON은 D 전체 진입·청산 규칙이다. 기존 전략 이름과 함께 설정·단위를 보존하고 자금/비용은 부모 전략을 공유한다. 별도 버튼·탭 제거, 동일 너비의 봉 콤보·온라인 독립 완성봉·오프라인 일봉 안내·강제 소수 자리 제거, 실제 일봉 검은 MA240과 체결 신호 저점의 보라색 선/이름을 반영했다. 기존 전략 엔진·D 런타임/포트폴리오를 재사용한다. 상세 파일 지도·제한은 docs/ui/Strategy-2026-11-09.md.

Release 빌드0경고0오류, Core192/UI288/디자인18, 라벨 관련11 재검사 통과. Mac 네이티브 합성 시작601봉/2quote 성공. 검증 정본 docs/verification/Strategy-2026-11-09.md. 원본 체결 실운영/실제 계좌 주문은 미검증이며 D 실계좌는 기존처럼 차단한다. git 작업트리에 수정되어 있고 커밋/push/태그/게시 없음. 설치 환경 기록과 기존 릴리즈 이력은 아래에 보존한다.

## 로컬 Mac 개발 환경 (2026-11-09)

사용자 요청으로 현재 Mac에 Rider2026.2.4.1 ARM64 + AvaloniaRider, Homebrew .NET10.0.403 및 Avalonia.Templates12.1.5 설치. 소스는 `<저장소 루트>`, 열 솔루션은 `STOCK.Avalonia.sln`, 실행 프로젝트는 `Stock.Avalonia`. .NET CLI `/opt/homebrew/bin/dotnet`. `bash avalonia-scripts/verify.sh` 성공(빌드0/0, Core192/UI275/디자인18). Mac 네이티브 합성 시작도 성공: `artifacts/mac-setup-smoke/result.json` 및 PNG, 601봉/2quote. 실제 계좌/API 사용 없음. Rider 최초 라이선스/로그인은 사용자 단계이며 이번 작업은 소스 릴리즈가 아니다.

## Apple Silicon 앱 — 최신 완료

2026-11-08 사용자 요청을 이행했다. 기존 v0.1.16 앱/Windows 자산은 유지하고 macos-v0.1.16-r3(a2ba9d8c8991a0d60ab6bce06ddf8b4bacd87478)에서 Stock.app/DMG/ZIP을 생성했다. Mac ARM64 CI42487371788 전체 성공, Core192/UI275/디자인18/실제 더미 Keychain4 및 앱/ZIP/DMG 시작·공개 차트3종목 통과. 공개 재다운로드·CI 산출물·GitHub digest·SHA256 일치, Mac 이미지 확인. 현재 릴리즈에 Stock-AppleSilicon-v0.1.16-r3.dmg(53,161,455bytes, SHA256 3d06c06bd8166dd0ef8e2c7b47299215eab08a0df8fe8ddbd320d05a90e0cbbe)과 ZIP/개별 체크섬을 추가했다. docs/avalonia/MACOS_APPLE_SILICON.md가 검증/설치 정본이다. Developer ID/공증은 없으며 최초 실행 안내를 포함했다. 실제 돈 주문0. 이 Mac 게시 요청도 완료했으므로 새 요청 없이 추가 태그/Release하지 않는다.

2026-11-08. “미완료 기능부터 완성” 답변에 따라 E1~E4를 구현했고, 앞선 조건부 Windows 교체·릴리즈 요청을 검증 후 이행했다. 현재 진행 작업 없음. 이전 “답변 대기/교체 전/주간40% 중단” 기록은 이력이다. 추가 릴리즈는 새 명시 요청이 필요하다.

## 현재 소스·실행 파일

- 공개 Git 소스: db1ee44e3083118b05982f4574446a8ba8f7a787 / tag v0.1.16.
- https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.16
- https://github.com/BaDook2/C-_STOCK/actions/runs/42480054799 전체 성공.
- Stock.exe 107,497,278 bytes / FileVersion0.1.16.0 / SHA256 993dcfeee8fad08be12fbe5ad2bf81e30f2a40259d824155f103ad75c1cff266.
- STOCK/Stock.exe와 release/Stock.exe 및 sibling stock_avalonia/Stock.Avalonia.exe가 모두 위 공개 바이너리다.
- 이전 WPF E433834B...1934 및 별도 Avalonia258F8CB6...C095E는 artifacts/before-avalonia-v0.1.16-20261108-215113에 해시 확인해 보존했다.
- 원본 WPF Stock.App/Stock.sln은 유지한다. 새 UI 개발/시작 프로젝트는 STOCK.Avalonia.sln / Stock.Avalonia. 공통 Core85개를 공유하며 Git용 manifest는 UTF8/LF 정규화 SHA다.
- sibling stock_avalonia는 별도 개발 사본이다. Core 변경은 양쪽/manifest/검사를 함께 갱신한다. 개인 DB/키/설정/.git/빌드 출력을 복사하지 않는다.

## 검증 근거

Git tree211849d7만 새 경로에 추출해 양 Release 빌드0경고0오류, WPF1652/D13/Core192/Avalonia273/디자인18 성공. 실제공개28 및 Windows 일반 EXE 입력·검색·재실행/백테스트·차트·달력 검수도 성공.
공개 파일 재다운로드→GitHub digest/.sha256/파일SHA/버전 일치→격리 시작0/601봉·2quote/NoBrokerCalls→실제 검색4회/백테스트4거래/+66,000/양방향팬·단일클릭·거래 행 선택/익절빨강·손절파랑→백업·교체 후 루트 검색4회와 별도 루트 시작까지 성공했다.
증거: artifacts/releases/v0.1.16-verified. 검증 중 만든 모든 일반 앱은 정상 닫았다. 실제 돈 주문0, 원본 DB/키 읽기·복제·삭제0.

## 동작 경계

키/기본 연결은 OS 보안 저장, 검증 후 저장한다. 실제 주문은 기본 잠김/활성화·건별 확인과 기존 최종 전송 guard를 거친다. 실전 자동은 기존 키움 국내+검증 facts/실제 거래대금 입력이 필요하며 전략 D 실전은 미연결이다. 다른 브로커 수동 주문과 구분한다.
일봉 가상은 다음 거래일의 신선한 관측 이후 주문·후속 관측 체결 모형이다. 공식 시가 보장 아님. checkpointv2 원자 저장/대사, paused 복원, 보호 재개도 신규/추가매수OFF다. raw tick/일반 시세 캐시는 메모리에만 둔다.
원본 WPF와 새 앱 저장소는 별도다. 기존 자료를 자동 이관하지 않으므로 계좌를 새 앱에서 다시 연결한다. Mac ARM64 CI의 시작·공개 차트·더미 Keychain은 검증했지만 사용자 Mac의 DPI/트랙패드/키체인 권한·잠금, Developer ID·공증과 실제 증권사 API는 미검증이다. 이전 양RID 교차 빌드와 이번 Mac 네이티브 CI를 구분한다.

## 다음 시작

새 요청 없으면 완료 상태를 유지한다. [구현](docs/avalonia/COMPLETION_E1_E4_20261108.md), [최종 검증·첫 CI 실패 원인](docs/verification/Avalonia-Release-2026-11-08.md), README-Avalonia.md를 우선한다.
추가된 검증 도구 avalonia-scripts/verify-release-download.ps1은 성공한 RunId/SourceCommit/새 Output을 요구하며 실제 계좌 대신 격리 시작만 검사한다. 기존 v0.1.15는 ignore가 Stock.App까지 제외해 CI 실패/미공개한 이력이며 태그를 덮어쓰지 않았다. 문서·검증 도구만 정리한 후속 커밋으로 새 릴리즈를 반복하지 않는다.
