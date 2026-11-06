# 작업 시작 안내

## 1분 안에 현재 상태 파악

1. 루트 `AGENTS.md` → `TASKS.md` → `HANDOFF.md`만 먼저 읽는다.
2. 필요한 기능의 문서·소스만 연다. 아래 지도에서 출발한다.
3. 과거 채팅·누적 문서는 현재 상태와 충돌할 때만 확인한다. `docs/history/`는 과거 조사 당시의 기록이며 확인되지 않은 추정도 포함한다.

| 작업 | 우선 읽을 문서 | 핵심 소스 |
|---|---|---|
| DB 오류/백업/재시작 | [저장소 운영](maintenance/Storage.md) | `TradingStore`, `StorageDiagnostics`, `StorageWorkspace`, `App` |
| 전체 기능 점검 | [기능 점검표](verification/FeatureAudit-2026-11-06.md) | `Stock.Tests/Program.cs`, `ButtonAuditTests` |
| 시세·차트 | `ui/ChartPage.md`, `api/NaverPublic.md`, `api/Kiwoom.md` | `LiveDailyWorkspace`, `IntradayChartWorkspace`, `CandleChart` |
| 일반 스크리닝 | `ui/ScreeningResults.md`, `ui/ScreeningBacktestPopup.md` | `OversoldScreeningWorkspace`, `ScreeningNavigation` |
| 실시간 스크리닝 | `ui/ScreenerPage.md` | `RealtimeScreeningWorkspace`, `PublicRealtimeScreeningWorkspace`, `RealtimeOversoldScreening` |
| 전략/백테스트 | `ui/StrategyPage.md`, `ui/OversoldBacktest.md` | `OnlyOversoldWorkspace`, `OversoldBacktest`, `OversoldMinuteBacktest` |
| 자동 주문/가상 계좌 | `ui/MultiTrade.md`, `ui/PublicPaperTrading.md` | `MultiTradingWorkspace`, `LiveStrategyBatch`, `RealtimePaperStrategyBatch` |
| 키 저장/초기화 | `ui/ConnectionDialog.md`, `ui/CredentialReset.md` | `BrokerCredentialStore`, `CredentialResetService` |
| 빌드/배포 | [검증 명령](maintenance/Verification.md) | `Win64.pubxml`, `.github/workflows/release.yml` |

## 증거 기록 원칙

- 실제 사용자 DB의 절대 경로를 보고서에서 확인한다. 격리된 셸의 `LOCALAPPDATA`가 Windows 앱과 같다고 가정하지 않는다.
- 정상/실패 검사, fixture/실제 API/실계좌 주문 여부, 로컬 EXE/GitHub 배포본을 구분한다.
- 테스트 개수만으로 모든 기능을 검증했다고 말하지 않는다. 기능/버튼별 점검표의 근거를 확인한다.
- 루트 TASKS에는 현재 작업과 실제 미해결 항목만, HANDOFF에는 다음 작업에 필요한 현재 사실과 파일 위치만 남긴다.
- 오래된 완료 기록을 다시 실행하지 않는다. 상세 기록은 날짜별 보고서로 분리한다.
