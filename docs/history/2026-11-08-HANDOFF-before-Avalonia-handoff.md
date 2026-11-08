# 진행 — 전략 D 마무리 → 별도 Avalonia 전환

최신 사용자: 기존 STOCK의 진행 작업을 마친 뒤 C:/Users/wkdru/Desktop/C#/stock_avalonia를 별도 생성해 Mac 개발용 Avalonia로 전환. 원본 STOCK 유지, 키/DB/개인설정 복제 금지. 두 작업 전체에서 주간잔여40% 이하 중지. get_usage_limits codex secondary10080분 창으로 확인; 최근72%. GitHub 게시 요청 없음. 실제주문 별도 명시 활성화 전 금지.

## 현재

- P0~P5 완료(가상 코어). docs/architecture/StrategyD-Plan.md, docs/ui/StrategyD.md, docs/verification/StrategyD-Progress.md. 요구사항 docs/requirements/StrategyD-DayTrading-20261107.md.
- P6 UI/서비스 연결 완료 후 전체 회귀 검증 중. Release0경고0오류(artifacts/strategy-d-final-build.log), 새 Core191개 + WPF13개 성공. 전체 회귀 session14689, artifacts/strategy-d-regression.log; 이 로그 종료PASS/ExitCode를 먼저 확인한다. 진행 중이므로 임의로 중복 전체 검사 시작하지 않는다. 이후 필요하면 추가 변경 영향만 검증한다.
- 새 Core는 Stock.Core/Research/DayTrading. 기존61개/구 엔진은 유지. schema2/strategy-d-intraday-v1, causal data+MA240/매물대, fresh rebound episode, generic runtime+portfolio+기존PaperBroker adapter, raw receive-order replay / 명시적인 확정봉 근사, 공통report+typed cache, 프레임워크 독립 editor metadata, bounded 앱 수명 SessionService.
- 새 WPF DayTradingWorkspace/ViewModel/Dialog/ChartPanel. 기존 설정/백테스트/스크리닝의 별도 D 진입 버튼. 전용 설정 namespace, 당시 봉/설정/체결/zone/history 저장, single-click fill/detail, 메인 모의매매 표식 연동. 팝업을 닫아도 가상 감시 유지. 메인/전체 긴급중지 연결. 기본진입OFF, 해제시보호유지, 자료오류 차단.
- 한계: 실제 IAccountBroker 주문 adapter 미연결(실주문 없음). 신규 D 원본 감시 호스트는 키움 국내20종목 단일구독, 연구는100종목. strict exchange time/facts 불확실 시 차단; parser의 KST 날짜 추정을 verified로 바꾸지 않음. 봉 근사는 ResearchOnly. 공개가격을 틱으로 위장하지 않음. 실제 시장/수익성 테스트 아님.
- Source 변경 목록/성능(100종목10만2.23→1.13초, API/UI 아님)은 Progress 문서. UI 그림 artifacts/strategy-d-ui/strategy-d-research.png.

## 다음

1. 진행 중 전체 회귀 종료/실패 확인. legacy-source-baseline.json 6개 해시 대조. 관련 새 UI는 --day-trading-ui, Core --day-trading-checks. publish 이전 주간잔여 재확인.
2. 검사 성공 후 로컬 publish(Win64)→격리 EXE smoke. 실행중 Stock.exe 있으면 정상종료 여부 확인 후 백업/루트와release에 반영. 현재 root EXE는 아직 이전1일 버전이다. GitHub push/release하지 않음.
3. P7 생성 시작 시 sibling stock_avalonia 쓰기 범위를 request_permissions로 요청. 공식 확인: Avalonia.Desktop12.1.5(2026-11-02), ScottPlot.Avalonia5.1.59는Avalonia>=12.0 의존. Core net10이며 StoragePaths는 Windows P/Invoke를 OS가드함. Mac UI/키 보관은 WPF와 분리해야 한다.
4. separate Avalonia .sln/AXAML/DI+MVVM를 만들고 Core/엔진 최대 재사용. 소스 allowlist 복사만(원본 DB/API키/개인config/.git/bin/obj/EXE/artifacts 제외), snapshot manifest 남김. 기존 WPF를 삭제/교체하지 않음. 실제 Mac 실행은 Windows/headless/크로스빌드와 구분. Mac 환경 없으면 그 검증은 미완료로 명시.
5. 각 단위/큰 검증 전 get_usage_limits 확인.40% 이하 중지 시 TASKS/HANDOFF에 수정파일·테스트·다음시작점 기록. 전체 전환 미완료를 완료로 보고하지 않음.

## 실행/도구

Windows PowerShell. STOCK 쓰기는 request_permissions로 허용받았으나 환경 갱신으로 거부되면 exactscope 재요청. 빌드는 require_escalated(SDK registry), auto review 허용. Python PATH 없음: PowerShell/.NET 또는 apply_patch 사용. apply_patch Delete+Add samepath 불가. shell mutations ErrorActionPreference Stop. 새 agent 생성 금지(사용자/AGENTS 위임 지시 없음).

현재 원본Stock.exe80,865,492bytes SHA256 CF50697690477A7C45D9FB4012C1CBBFB426F5D0499DBE96D5A3E50257EDAF37. 직전1일 기능 관련46/WPF687/EXE smoke 성공은 docs/verification/OneDayRange-2026-11-07.md. 사용자DB/API키/저장전략 임의 변경 금지; SQLite 최초손상 미확정/AppData리다이렉션 docs/maintenance/Storage.md. 과거 기록을 현재 blocker로 반복하지 않음.