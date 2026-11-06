# 현재 상태 — 2026-11-06 스크리닝 지정봉 체결·선택

## 다음 실행

AGENTS → TASKS 순서로 읽는다. 이번 요청은 구현·검증·v0.1.14 공개·루트 EXE 동기화까지 완료됐다. 새 요청 없이 같은 검사나 릴리즈를 반복하지 않는다.

- 코드 bb1d510a2c85a1a33c81b7ed7082bbef4f8ed319. 최신 공개 https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.14
- Actions42183510638 빌드0경고·0오류/전체1,317개/publish/smoke/Release 성공. 전체 검사21분31초, job23분19초.
- 공개 EXE80,868,637 bytes / SHA256 77A647E6F86ECA9C069B991C31FAD2BC35C9EB921F11C6724766422A46F8B7DB. checksum/digest/계산 해시 일치, 다운로드 smoke 종료0/Success/NoBrokerCalls=true. 루트/release EXE 동일 해시 확인.
- docs/verification/Release-v0.1.14.md, artifacts/release-v0.1.14-{ci.log,job.log,job.json,proof.json}, download-v0.1.14-smoke-20261106-171427/result.json에 근거 보존. 이전 공개 EXE도 artifacts에 백업했다. v0.1.12는 과거 취소 태그이므로 재개하지 않는다.
- 문서 완료 기록 커밋은 제품 변경이 아니므로 새 릴리즈를 만들지 않는다.

## 구현과 검증

- 원인: 일반 스크리닝 지정봉 상세에서 체결 엔진 호출이 누락됐다. ScreeningNavigation이 IOversoldBacktestService에 당시 설정/사실/분·시간봉을 전달한다. 준비 봉 정책 RealtimeWarmup.BacktestHistoryStart는 메인 백테스트와 공유한다. 차트 범위는 원래 시작~종료일이고 모형체결은 실제 주문으로 기록하지 않는다.
- CandleChart:25봉 이하처럼 우클릭 전후가 같으면 즉시 휠/드래그. 큰 전체 차트는 기존 잠금/확대 한도 유지. 표식 글자·묶음의 실제 렌더 영역과 체결 봉으로 선택하고 오래된 화면 좌표를 재사용하지 않는다. 오른쪽 표식은 실제 글자 폭으로 배치한다.
- ScreeningChartViewModel.SelectedTrade 양방향 표 선택. 시간/방향/수량/가격을 비교하고 출처/번호/사유를 우선한다. 표식 클릭은 표 선택·스크롤·상세만, 표 행 클릭은 체결 봉으로 이동.
- Release 빌드0경고·0오류, 전용58개/관련328개/전체1,317개, publish·단일 EXE smoke 종료0/Success/NoBrokerCalls=true.
- Windows 실제 마우스:1시간봉6개 우클릭 없이 BUY/SELL 클릭→행/상세 선택, 휠6→5개.1분 전환360봉/2표식 유지, 잠긴 전체의 묶음 클릭→행 선택/360봉 유지. 격리 fixture이고 주문0회.
- 성능2만봉/160표식/30프레임: 전용 P95 26.31ms, 전체 UI 검사 중 P95 54.35ms. 지표/표식 매핑 각1회, 원본 유지. 부하에 따른 차이를 포함해 보고했다.
- 근거 docs/verification/ScreeningTradeChart-2026-11-06.md와 artifacts/screening-fill-{build-final,final,related,full,publish}.log, screening-fill-publish-smoke/result.json, screening-fill-native/.
- 로컬 publish EXE80,835,673 bytes / SHA256 FDD5538DA55E444BC3F5AB42AF27A75543640C6E205D9EEC2F42E89364A915B4. 현재 루트에는 위 GitHub 검증 공개 EXE를 반영했다. 로컬/CI 파일은 각각 검증했다.
- GitHub CLI wrapper artifacts/tools/Invoke-GitHub.ps1은 기존 Git 자격을 메모리에서만 사용한다. 토큰 출력/저장 금지. Windows SDK 레지스트리 접근 때문에 빌드/테스트는 require_escalated가 필요했다.

## 이전 DB 사실 유지

실제 DB C:/Users/wkdru/AppData/Local/StockPaperTrading/KiwoomQuant/workspace-v2.db. 09:18/09:20 SQLite11 최초 유발 사건은 미확정이다. 원본98MB/실패 사본4개/최종145MB DB 무결성·JSON 정상. 원본 백업은 같은 폴더 diagnostics-20261106-102905. 장기/풀링 연결을 작업별 비풀링으로 바꾸고 진단 증거를 추가했지만 새 failure 없이 근본 원인을 확정하거나 정상 DB를 수리하지 않는다. 데이터 삭제/VACUUM/실제 키 변경 없음. docs/maintenance/Storage.md, docs/verification/{StorageAudit,FeatureAudit}-2026-11-06.md 참고.
