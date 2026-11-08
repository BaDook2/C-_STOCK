# 진행 — 전략 D 완료 / 별도 Avalonia 1차 이식

배포 최신(2026-11-08): v0.1.15는 Mac 번들 ignore가 Stock.App까지 제외해 CI 빌드 실패/미공개. 규칙·누락 소스 9개 수정 후 Git tree만 새 경로에 추출해 빌드0/0, WPF1652/D13/Core192/Avalonia273/디자인을 모두 통과했다. 현재 commit db1ee44 / v0.1.16 / Actions42480054799 검사 중이다. 공개 다운로드·SHA/실행 검증 후에만 기존 EXE를 교체한다. `docs/verification/Avalonia-Release-2026-11-08.md`가 아래 v0.1.15 게시 준비 기록보다 우선한다.

최신 E1~E5(2026-11-08): 미완료부터 완성하라는 답변에 따라 보안 저장/기본 연결, 실제 주문·취소·체결/기존 실전 batch, 일봉 가상 checkpointv2·보호 재개를 구현했다. 현재 source/검증 정본은 docs/avalonia/TASKS.md와 HANDOFF.md. build0/0·Core192·Avalonia273·디자인18·WPF1652·실제공개28 및 최종C162후보 일반 검색4회/백테스트·팬·클릭/달력·손익색 성공. GitHub CI/공개 다운로드 검증 전이며 기존 Stock.exe E433...1934와 사용자 자료는 유지 중이다. 실제 Mac/실제 증권사/돈 주문 미검증. 아래 옛 미구현·답변 대기/사용량 제한 기록보다 최신 상태를 우선한다.

최신(2026-11-08 D3): 사용자가 Avalonia 익절/손절 색상·최종 검수 후 Stock.exe 교체/Release를 조건부 요청했다. sibling stock_avalonia에서 색상·전체 검사와 실행 후보까지 완료했으나 실계좌/키 영속/일봉 자동 가상 실행·복원 누락을 확인해 교체 범위 답변을 기다린다. 원본 EXE/원격은 유지. 정본은 sibling TASKS/HANDOFF와 docs/RELEASE_READINESS_20261108.md. 이전의 주간40% 중단 조건은 사용자가 해제했다.

최신 사용자: 하던 일을 먼저 끝내고 원본 STOCK을 보존한 채 sibling stock_avalonia로 Mac 개발용 전환. 두 작업 전체에서 주간잔여40% 하한 유지. GitHub 게시 요청 없음. 실계좌 주문 명시 활성화 전 실행 금지.

- [x] P0~P5 범용 당일매매 코어 + 전략 D: 설정/causal 지표/신호/계좌/원본·봉 재생/성과·검사.
- [x] P6 WPF 설정/연구/가상 실행·차트/기록·긴급중지·회귀·루트 Stock.exe 갱신.
- [~] P7 별도 `C:/Users/wkdru/Desktop/C#/stock_avalonia`: Avalonia1차 이식/Windows 실행/양 Mac CPU 교차 빌드 완료. 실제 Mac 실행·Keychain·실계좌 주문 UI/대사·일부 기존 화면/차트 UX는 미완료.
- [!] 실제 주문 어댑터/실계좌 활성화는 별도 사용자 지시 전 금지.
- [!] Mac 실행환경이 없어 실제 Mac 검증은 아직 할 수 없음. 원본 WPF는 유지한다.
- [!] 이전 SQLite 최초 손상 사건 미확정. 현재 작업에서 원본 DB 변경/복제/복구하지 않음. docs/maintenance/Storage.md 참조.

## 최신 상태 — 2026-11-08

원본 WPF 전체1651 + 새 Core192 + D WPF13 성공, Release0경고0오류, 단일EXE 격리 smoke 성공. 루트/release/publish Stock.exe 갱신. SHA256 E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934 (80,983,144bytes). 이전 exe artifacts/before-strategy-d-20261108-000540에 보존. docs/ui/StrategyD.md 및 docs/verification/StrategyD-Progress.md.

별도 Avalonia: .NET10/Avalonia12.1.5/ScottPlot5.1.59, Core소스80개 원본과 해시 차이0. 원본 DB/API키/개인설정/.git 미복제. 검색/시세/일주월/분시간차트·61개/JSON·공통 백테스트·일반/실시간 스크리닝·D·관심/시장/정기매수·기록·계좌조회 이식. Release0/0, portable192, 실제 AXAML Headless74 성공. Windows self-contained EXE두 번 native 실행 Exit0/Success/NoBrokerCalls. Mac arm64/x64 publish와 apphost/native 파일 검사 성공; 실제 Mac은 미검증.

실제 수정 파일·기능 경계·검사·재개는 sibling stock_avalonia/AGENTS.md → TASKS.md → HANDOFF.md → docs/IMPLEMENTATION.md/VERIFICATION.md. Mac Keychain·실계좌 주문 전송 UI/대사·일봉 다음날 가상주문·보유 복원·일부 화면 동등성은 남았다. 새 루트 Stock.Avalonia.exe는 원본 Stock.exe와 다른 앱이다. GitHub push/tag/release 없음. 주간잔여 직전70%; 재개 전 새로 조회,40% 이하 중지.

과거 작업 로그는 docs/history/2026-11-08-TASKS-before-Avalonia-handoff.md에 보존했다. 옛 '회귀 진행 중/아직 폴더 미생성/예전 EXE' 문장은 현재 상태가 아니다.
