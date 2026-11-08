# 현재 상태 — 2026-11-07 메모리·모드·표시 요청 완료

## 제품 변경

- 제품 기본 WorkspaceViewModel/MainViewModel은 TradingStore(memoryMarketData:true). SessionMarketData가 bars:/live-ticks:/verified-tick-coverage:/catalog 및 원본 MarketEvent를 프로세스 메모리에만 보관한다. 캐시 LRU128개/JSON32M문자 제한. 기존 디스크 cache/tick은 읽거나 삭제하지 않는다. 테스트 원본 snapshot·전략·관심종목·체결·주문 안전 기록은 계속 DB에 보존한다. API키는 기존 DPAPI 파일이다.
- 모든 새 분석 기준은 오프라인 일봉/온라인 분·시간 지정봉 또는 일봉. 주/월/틱 표시 선택기는 제거했다. 내부 틱 피드/전략 처리는 유지한다. BarUnit.Day는 검증된 세션 전체, 미완성/미래 일봉 제외, Day 결과는 OversoldBacktestService의 기존 일봉 엔진·캐시로 정규화한다. 과거 테스트 기록 자료는 보존한다.
- ConnectionLifetime은 검증 broker/RemoteTradingSession을 오프라인에서 park한다. 재진입 추가 인증·계좌 조회 없이 재사용하고 주문 전 RefreshCore/가드를 유지한다. 키 설정/초기화/종료 시 revoke/dispose. 오프라인 긴급중지도 parked session의 주문 차단 상태를 보존하며 API를 요청하지 않는다.
- DisplayNumber.Price/SignedPrice는 Floor 정수 표시다. 원본 계산/주문 decimal은 그대로, %2자리 유지. CandleChart 매도/묶음과 클릭 상세에 확인된 실현손익, 모르면 미확인. 공통 ScrollBar, 우측2×2 및 본문 상단·지정봉 WrapPanel 적용.

## 검증·실행 파일

- Release 빌드0경고·0오류, 전용24개/최종 전체1366개 통과. 로그 artifacts/memory-mode-{build-final,checks,full-final,publish}.log. 이전 UI605개도 통과했으며 최종 전체에 마지막 보완을 포함했다.
- fixture 모드 왕복3회1.6~3.4ms/추가 인증·계좌조회0. 실제 네트워크 측정 아님. artifacts/memory-mode-checks/mode-switch-timing.json.
- publish/루트 EXE smoke 종료0/Success/NoBrokerCalls=true. artifacts/memory-mode-{publish-smoke,root-smoke}/result.json.
- 루트Stock.exe, release/Stock.exe, publish/win-x64/Stock.exe80,855,594bytes/SHA256 `0CA8A9913AC9F7EE881CBC5903F359874089EE7E8F0F833C08D8C4021F7284FA`, release 체크섬 일치. 이전 파일 artifacts/before-memory-mode-20261107-180705.
- 실제 사용자 Explorer에서 루트 EXE 실행 성공. 오프라인 해외→애플 검색/일봉/342달러 정수 가격/일봉 전용 UI 확인. 실제 broker 연결·주문·키 변경 없음. 앱은 애플 차트를 표시한 채 열어 두었다. native 근거 artifacts/memory-mode-native.json. 현재 HWND/프로세스는 재사용하지 말고 새로 조회한다.

## 다음 작업/주의

요청 작업 완료. 새 요구 없이 검사·게시·DB 수리를 반복하지 않는다. GitHub commit/push/tag/Release 없음. 사용자 명시 요청 시에만 릴리즈한다.

기존 DB 손상 최초 유발 사건은 미확정이다. 도구 AppData가 실제 사용자 앱과 다른 Codex LocalCache로 리다이렉트되므로 절대경로만으로 같은 DB라고 단정하지 않는다. 앞선 승인 복구 원본 workspace-v2.before-repair-20261107.db/사본을 유지한다. 손상 state13개·틱6건 복구 미확인 내역과 실제 앱 정상 점검은 docs/verification/ProgressiveScreening-2026-11-07.md 및 docs/maintenance/Storage.md에 있다. 이번 작업에서 DB 삭제·초기화·수리는 하지 않았다.

변경 파일/흐름/검사/제한: docs/verification/MemoryMode-2026-11-07.md. 빌드: docs/maintenance/Verification.md. 앞선 미국 일봉·근접 후보·기록/진행 중 결과 기능: ResearchExpansion-2026-11-06.md, ProgressiveScreening-2026-11-07.md. 이전 인계: docs/history/HANDOFF-before-MemoryMode-20261107.md.
