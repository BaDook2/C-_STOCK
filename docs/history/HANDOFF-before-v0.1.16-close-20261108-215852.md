# 현재 — Avalonia 미완료 E1~E4 구현, Windows 최종 배포 검수

배포 최신: db1ee44 / 새 v0.1.16 / Actions42480054799 진행 중. v0.1.15 CI는 *.app가 Stock.App 신규 소스까지 제외한 문제로 실패/미공개; 제외 범위를 좁히고 9개를 포함했다. Git tree만 추출한 새 경로에서 양 빌드0/0 및 WPF1652/D13/Core192/UI273/디자인 성공. `docs/verification/Avalonia-Release-2026-11-08.md` 참조. 아직 원본 EXE 교체/공개 다운로드 검증 안 함. 다음은 CI 끝까지 확인→공개 파일 다운로드/해시/일반 실행·검색·백테스트 검사→백업 후 교체다.

2026-11-08 최신 사용자 답변은 **미완료 기능부터 완성**. 키 영속/기본 연결, 실제 주문·취소·확인 체결/기존 실전 전략 adapter, 일봉 관측 가상 실행·원장 복원을 구현했다. 정본은 `docs/avalonia/HANDOFF.md`, `docs/avalonia/TASKS.md`, `docs/avalonia/COMPLETION_E1_E4_20261108.md`다. 아래 옛 미구현·답변 대기는 과거 기록이다.

새 소스는 이 저장소의 `Stock.Avalonia`/`Stock.Desktop.Tests`/`Stock.Portable.Tests`/`STOCK.Avalonia.sln`/`avalonia-scripts`에 소스만 통합했다. 원본 WPF 소스/개인 DB/키/기존 EXE는 보존한다. 공통85개 source와 정규화 SHA manifest. WPF1652/Core192/UI273/디자인18/실제공개28 성공; 최종 v0.1.15 후보 C162D6C5...D8EC23은 시작·일반 검색/재실행4회/Mac 양RID cross-publish 성공. 일반 백테스트·표식·팬/달력과 새 GitHub CI/다운로드 검증 중이다. 아직 공개/기존 EXE 교체 안 함. 실제 Mac/API/돈 주문/서명·공증 미검증.

검사: `pwsh avalonia-scripts/verify.ps1`, WPF `dotnet run --project Stock.Tests -c Release -- artifacts/wpf-tests`. 사용량40% 중단 지시는 해제됐다. 실제 주문 금지, 키/DB/빌드 출력 Git 포함 금지. 실제 계좌는 명시 활성화/건별 확인, 실전 자동은 키움 국내+검증 facts/거래대금 자료만. 가상 복원은 paused/신규매수 OFF. 표시 열은 OneWay로 원본을 바꾸지 않는다.

---

# 이전 — WPF 전략 D 마무리 / 별도 Avalonia 1차 전환

최신(2026-11-08 D3): Avalonia 색상/검수/Stock.exe 후보258F8CB6...C095E 완료. 모든 기능 동등성 조건은 실계좌/키 영속/일봉 자동 실행·복원 누락으로 아직 충족하지 않아 원본 Stock.exe와 GitHub를 유지했다. 교체/사전 공개 범위를 사용자에게 질문한 상태다. 다음 작업은 sibling stock_avalonia/AGENTS → TASKS → HANDOFF 및 docs/RELEASE_READINESS_20261108.md를 먼저 읽는다. 아래 '게시 요청 없음/주간40%' 문장은 이전 이력이며 최신 사용자는 조건부 게시를 요청하고 사용량 중단 조건은 해제했다.

AGENTS.md → TASKS.md 먼저. 원본 STOCK 유지. 새 전환은 sibling `../stock_avalonia`다. GitHub 게시 요청 없음. 실제 주문 금지. 주간잔여40% 하한(직전70%, 재개 전 get_usage_limits 재조회).

## 원본 WPF는 검증·EXE 반영까지 마침

P0~P6 첫 가상/연구 버전 완료. 전체1651/신규 Core192/WPF D13 성공. Release0경고0오류, 격리 단일EXE smoke Success/NoBrokerCalls. 루트/release Stock.exe80,983,144bytes SHA256 E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934. 이전 파일 artifacts/before-strategy-d-20261108-000540/Stock.exe. 현재 같은 전체 회귀를 다시 실행할 필요 없음.

코어 Research/DayTrading, UI DayTradingWorkspace/ViewModel/Dialog/ChartPanel. docs/architecture/StrategyD-Plan.md, docs/ui/StrategyD.md, docs/verification/StrategyD-Progress.md. 기존61개·레거시 엔진은 보존. 새 schema2/strategy-d-intraday-v1. 실제 broker order adapter 미연결, 원본 가상 구독20종목, strict 시각/당시 facts 미확인 차단. 공개가격을 원본 체결로 위장하지 않음.

## Avalonia 다음 작업

별도 경로 C:/Users/wkdru/Desktop/C#/stock_avalonia에 실제 .sln/AXAML/DI/MVVM와 공통Core80개를 생성했다. 개인 DB/키/config/.git/bin/obj/EXE는 복제하지 않았다. 새 DB는 독립 LocalApplicationData/Stock.Avalonia. 자세한 재개는 그 프로젝트 AGENTS/TASKS/HANDOFF와 docs/IMPLEMENTATION.md/VERIFICATION.md가 정본이다.

새 프로젝트 Release0/0, portable192 + Headless74, Windows native 단일EXE 두 번 Exit0/Success, osx-arm64/x64 publish 및 Mach-O/native 의존성 검사 성공. 실제 Mac 실행은 미검증이다. macOS Keychain·실계좌 주문 UI/대사·일봉 다음날 가상주문/보유복원·일부 고급 차트 설정·Mac UI 동등성은 후속이다. 전체 이식 완료라고 보고하지 않는다.

PowerShell/.NET, Python PATH 없음. 빌드/restore/native test require_escalated. 사용자/지침 요청 없이 agent 생성 금지. 원본 DB 최초 손상/리다이렉션은 docs/maintenance/Storage.md; 이번 범위에서 복구/자료삭제는 하지 않음. 과거 HANDOFF는 docs/history/2026-11-08-HANDOFF-before-Avalonia-handoff.md.
