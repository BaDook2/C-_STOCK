# 현재 상태 — 2026-11-06 DB 조사·기능 감사·GitHub 릴리즈

## 바로 이어갈 위치

AGENTS.md → TASKS.md → docs/START_HERE.md. 이번 상세 보고서는 docs/verification/StorageAudit-2026-11-06.md와 FeatureAudit-2026-11-06.md다. 채팅/옛 누적 기록 전체를 다시 읽지 않는다.

최신 사용자 요청: 검증 성공 후 GitHub 릴리즈를 상시 완료 루틴에 추가하고 현재 변경도 게시한다. AGENTS/Verification에 반영했다. 로컬 전체1,257개·빌드·EXE smoke 통과. 새 태그 게시 → Actions → 공개 다운로드 해시/smoke 확인 → 완료 기록을 이어간다.

## DB 사실과 수정

- 실제 DB: C:\Users\wkdru\AppData\Local\StockPaperTrading\KiwoomQuant\workspace-v2.db. 98,349,056 bytes. 백업: 같은 폴더 diagnostics-20261106-102905.
- 원본/과거 실패 사본4개 full integrity·인덱스·JSON 정상. SQLite3.53.4(앱)와3.50.4(Python)에서 독립 확인. state42 / events73,123 / runs7. 원본 삭제/재구축/수리/VACUUM 없음.
- 09:18/09:20 오류11 최초 유발 사건은 재현되지 않았다. 과거 locale 손상 가설은 근거 없어 제거. 현재 파일 정상과 과거 오류 원인 확정을 혼동하지 않는다.
- 작업별 비풀링 연결 + gate + 명시적 종료로 장기/풀링 핸들 잔류를 제거. 파일 교체 후 새 파일 읽기/늦은 callback 차단/실제 손상 페이지/JSON불량 분리/비밀 미기록 검사 통과.
- StorageDiagnostics는 별도 연결 full integrity와 JSON을 검사. SQLite 실패는 원본 코드를 StorageException에 보존하고 private storage-diagnostics/failure-*.json 기록. 자동 쓰기 재시도/빈 DB 초기화 안 함.
- 테스트 기록의 저장소 점검, EXE --storage-check <절대DB> <절대보고서> 추가. 실제 root 앱에서 버튼→정상보고서 확인.

## 검증·배포

- 전체1,257개 통과, 실제 WPF 버튼 Invoke33회 + 팝업, Release 빌드 경고0/오류0. 주문/키삭제는 임시 DB/더미키/fake broker 검사. 실제 주문0.
- 사본 부하:340저장/660조회/20재연결. 수정 전3.634초/후4.779초, 양쪽 무결성 정상. 부하에서 과거 오류를 재현했다고 주장하지 않는다.
- 최종 root/release/publish EXE SHA256 DE30F5EBB80BFC6396FD17539B0FA53BF4F1B0752C853B2677295A1359AC1DF9 / 80,823,213 bytes 일치. 동일 출력 폴더로 단일 EXE smoke2회 모두 종료0/Success/NoBrokerCalls=true. smoke fixture DB를 실행마다 격리하고 앱 watchdog가 실패 종료코드를 가리지 않도록 수정했다.
- 실제 root 앱에서 네이버 검색/시세·DB점검·기본 키움 연결/계좌 조회·20종목 실제 감시/11:00 완성봉 분석·HLB 차트15회 모형거래·선택 유지/중지/정상 종료 확인. 온라인 자동매매 체크박스의 연결 상태 갱신 누락도 고치고 실제 비활성→활성(OFF 유지) 확인. 관련 WPF40개. 자동매매는 켜지 않았고 실주문0. 검증 앱은 모두 정상 종료했다.
- 최종 실제 DB CLI 종료0/Healthy=true.144,805,888 bytes/state42/events110,238/runs9. 원본 백업의98MB/state42/events73,123/runs7과 구분한다. 이 요청에서 즉시 가능한 구현/검증은 마쳤고 최초 과거 오류의 유발 사건만 미확정 추적 항목이다. 새 failure 증거 없이 정상 DB를 임의 재구축하지 않는다.
- root 이전 백업 artifacts/Stock-before-storage-audit-3B1D6853.exe. GitHub v0.1.10은 이전 배포본이며 최신 사용자 요청으로 새 버전 게시를 진행한다.

## 파일/명령

Storage: Stock.Core/Trading/TradingStore.cs, StorageDiagnostics.cs, RawTickRepository.cs; App: StorageWorkspace.cs, App.xaml.cs, MainWindow.xaml(.cs), StrategyFileDialogs.cs/OnlyOversoldWorkspace; Tests: StorageDiagnosticsTests, ButtonAuditTests, Program/UiTests.
로그: artifacts/storage-audit-{build,core,full,publish,realtime-ui}.log, storage-audit-final-real.json, storage-audit-repeat-smoke/result.json; button-audit-clicks.txt는 storage-audit-full-ui에 있다.
검증 명령/운영: docs/maintenance/{Verification,Storage}.md. 공유 compiler가 멈추면 -m:1 -nr:false -p:UseSharedCompilation=false. 앱 출력 DLL 사용 중이면 정상 종료 후 빌드.

## 기록 보존

과거 누적 HANDOFF432줄/TASKS672줄은 docs/history/*-through-20261106.md에 그대로 보존. 옛 미완료/추정은 현 작업 지시가 아니다. 현재의 실제 제한과 미확정 원인은 TASKS를 기준으로 한다.
