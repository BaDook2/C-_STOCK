# 작업 시작 안내

현재 Avalonia UI는 Inje/Bin 두 상위 그룹과 단일 사용 체크다. 전체 설정 JSON·백테스트·일반 스크리닝과 샘플9개 기본 탑재를 연결했다. Bin 실시간 가상 주문/복원은 미연결로 시작 차단한다. v0.1.19 Windows/Apple Silicon 릴리즈를 게시·검증했다. 이전 릴리즈/아래 구기능 문서와 구분한다.

## 1분 안에 현재 상태 파악

1. 루트 `AGENTS.md` → `TASKS.md` → `HANDOFF.md`만 먼저 읽는다.
2. 필요한 기능의 문서·소스만 연다. 아래 지도에서 출발한다.
3. 과거 채팅·누적 문서는 현재 상태와 충돌할 때만 확인한다. `docs/history/`는 과거 조사 당시의 기록이며 확인되지 않은 추정도 포함한다.

| 작업 | 우선 읽을 문서 | 핵심 소스 |
|---|---|---|
| Bin 독립 Core·전체 JSON·22개 결정·A~E 검사 | `strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md`, `architecture/Bins-Strategy.md`, `verification/Bins-2026-11-11.md` | `Stock.Core/Strategies/Bins`, `Stock.Bins.Tests`; 기존 Inje/UI/실제 기록 보존, 실계좌·배포 잠금 |
| 현재 Avalonia Windows·Mac v0.1.19 배포/개발 | `../README-Avalonia.md`, `verification/Release-v0.1.19.md`, `avalonia/MACOS_APPLE_SILICON.md` | `STOCK.Avalonia.sln`, `Stock.Avalonia`, 공통 `Stock.Core`; 아래 WPF 소스는 호환 이력 |
| 현재 로컬 Inje 전략 | `strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`, `architecture/Injes-Strategy.md`, `verification/Injes-2026-11-11.md` | `Stock.Core/Strategies/Injes`, 두 UI의 기존 설정·연구·가상 실행 연결. D 알고리즘/창/전용 기록은 제거 |
| ChatGPT 논의용 현재 전략·전체 JSON | `strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md` | Inje 공통14개·전용10/12/11/13개·Standard6개 매핑·네 전략 전체 JSON과 Standard 예시를 한 파일에 포함. 구전략 설명서는 이력 |
| 메모리 시세·모드 재사용·가격/스크롤바 | `verification/MemoryMode-2026-11-07.md` | `SessionMarketData`, `ConnectionLifetime`, `DisplayNumber`, `TossInspired.xaml` |
| DB 오류/백업/재시작 | [저장소 운영](maintenance/Storage.md) | `TradingStore`, `StorageDiagnostics`, `StorageWorkspace`, `App` |
| 전체 기능 점검 | [기능 점검표](verification/FeatureAudit-2026-11-06.md) | `Stock.Tests/Program.cs`, `ButtonAuditTests` |
| 시세·차트 | `ui/ChartPage.md`, `api/NaverPublic.md`, `api/Kiwoom.md` | `LiveDailyWorkspace`, `IntradayChartWorkspace`, `CandleChart` |
| 일반 스크리닝 | `ui/ScreeningResults.md`, `ui/ScreeningBacktestPopup.md` | `OversoldScreeningWorkspace`, `ScreeningNavigation` |
| 진행 중 결과 열람/애플 DB 복구 | `verification/ProgressiveScreening-2026-11-07.md`, `maintenance/Storage.md` | `OnlyOversoldWorkspace`, `ScreeningNavigation`, `StoragePaths` |
| 실시간 스크리닝 | `ui/Injes-Strategy.md`, `ui/ScreenerPage.md` | `RealtimeScreeningWorkspace`, `RealtimeScreeningHost`, `InjesStreamService` |
| 테스트 기록/실행 재현 | `ui/HistoryPage.md`, `verification/ResearchExpansion-2026-11-06.md` | `ResearchHistoryWorkspace`, `ScreeningNavigation` |
| 전략/백테스트 | `ui/StrategyPage.md`, `ui/OversoldBacktest.md` | `InjesResearchBridge`, `InjesReplayEngine`, 기존 UI 백테스트 서비스 |
| 자동 주문/가상 계좌 | `ui/MultiTrade.md`, `ui/PublicPaperTrading.md` | `InjesSession`, `InjesStreamService`, `IStrategyExecutionAdapter`, 기존 PaperBroker |
| 키 저장/초기화 | `ui/ConnectionDialog.md`, `ui/CredentialReset.md` | `BrokerCredentialStore`, `CredentialResetService` |
| 빌드/배포 | [검증 명령](maintenance/Verification.md) | `Win64.pubxml`, `.github/workflows/release.yml` |

## 증거 기록 원칙

- 실제 사용자 DB의 절대 경로와 열린 핸들의 최종 `PhysicalDatabasePath`·크기·행 수를 대조한다. 격리된 셸의 같은 `LOCALAPPDATA` 경로가 Windows 앱과 같은 파일이라고 가정하지 않는다.
- 정상/실패 검사, fixture/실제 API/실계좌 주문 여부, 로컬 EXE/GitHub 배포본을 구분한다.
- 테스트 개수만으로 모든 기능을 검증했다고 말하지 않는다. 기능/버튼별 점검표의 근거를 확인한다.
- 루트 TASKS에는 현재 작업과 실제 미해결 항목만, HANDOFF에는 다음 작업에 필요한 현재 사실과 파일 위치만 남긴다.
- 오래된 완료 기록을 다시 실행하지 않는다. 상세 기록은 날짜별 보고서로 분리한다.
