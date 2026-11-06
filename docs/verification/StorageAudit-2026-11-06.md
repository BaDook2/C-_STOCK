# DB 조사 결과 — 2026-11-06

## 확인한 사실

Windows 오류 기록의 09:18:49/09:20:15 `Stock.exe` 종료는 `TradingStore.States` 읽기 중 SQLite 오류11이 처리되지 않은 사건이다. 이전 작업의 예외 격리는 종료 방지였으며 DB 최초 오류 발생 원인을 확정한 조치는 아니었다.

실제 사용자 경로의 DB를 앱 종료 상태에서 `diagnostics-20261106-102905`에 보존했다. 당시 원본은 **98,349,056 bytes**, SHA256 `1D4575A6575651BD4819C89377DC4BD1ADDDF4AFB842BFCADB5B9A308500181E`다. 원본과 비교할 때 셸의 격리된 LOCALAPPDATA를 혼동하지 않도록 도구에 절대 경로를 명시했다.

| 검사 | 결과 |
|---|---|
| 현재 사용자 DB full integrity/index 검사 | ok |
| state 전체 문자열 강제 읽기/PK 순서/prefix/substr | 모두 성공, 42행 |
| JSON UTF-8/문법 | state 42행 / events 73,123행 모두 정상 |
| presets/audit/settings_history | 0행 |
| runs | 7행, result NULL |
| 원본 틱/이벤트 JSON 바이트 | 63,681,625 bytes 정상 |
| 이전 실패/재구축 사본 4개 | 모두 full integrity 및 JSON 정상 |
| 독립 Python SQLite 3.50.4 | integrity ok / state42 |
| 앱 포함 SQLite 3.53.4 | 같은 원본 DB integrity/JSON 정상, CLI 종료0 |

따라서 현재 파일의 물리적 손상, 인덱스 손상, JSON 손상은 확인되지 않았다. 이전 코드 주석의 "한글/locale 손상 때문에 substr가 실패한다"는 추정도 근거가 없어 제거했다. 현재 DB를 재구축/초기화/VACUUM하지 않았다. **과거 두 오류를 최초로 유발한 사건은 이번에도 재현하지 못했다.** SQLite 버그/하드웨어 문제/동시 접근을 확정 원인처럼 보고하지 않는다.

## 수정과 근거

기존 Store는 하나의 SQLite 연결을 객체 수명 동안 유지했고 연결 풀링도 기본 ON이었다. 이 구조에서는 파일 교체·복구 후 이전 파일/캐시 핸들을 계속 유지할 수 있다. Microsoft.Data.Sqlite는 연결 객체의 동시 공유를 권장하지 않는다. 기존 gate는 유지하고 작업별 비풀링 연결과 명시적인 트랜잭션 수명으로 변경했다. 작업 종료 뒤 핸들이 남지 않고 같은 경로 파일이 교체돼도 새 파일을 읽는 회귀 검사를 추가했다. **이는 장기 유지 핸들을 제거하는 방어적 수정이며, 기존 gate가 동시 사용을 허용했다거나 이것이 과거 사건의 최초 원인이었다고 입증한 것은 아니다.**

DB 오류는 `StorageException`으로 원본 SQLite 예외를 유지한다. 실패를 빈 성공으로 바꾸거나 쓰기를 자동 재시도하지 않는다. private failure 보고서에 오류/확장코드·작업·프로세스·런타임·별도 연결 무결성을 함께 남겨 향후 메모리/연결 상태와 실제 파일 손상을 구별한다. 계좌 키·레코드 본문·레코드 키는 기록하지 않는다. UI 저장소 점검과 단일 EXE CLI도 같은 검사기를 사용한다.

## 재현·부하 검사

- 실제 state B-tree 페이지 타입을 깨뜨린 테스트 DB는 오류11을 발생시킨다. 같은 코드 경로가 실패 증거를 기록하며 파일을 초기화하지 않는 것을 확인했다.
- 구조는 정상이지만 JSON만 잘못된 경우를 별도로 탐지한다.
- 사용자 DB 사본에서 4개 worker, 340회 저장·660회 조회·20회 재연결을 수행했다. 수정 전 3,634ms, 수정 후 4,779ms였고 두 경우 모두 무결성/JSON 정상이다. 이는 실행 환경의 단일 관측값이며 수정이 부하 오류를 재현해 고쳤다는 의미는 아니다. 연결 수명 분리 비용은 이 검사에서 작업당 평균 약1.1ms 추가였다.
- 실제 파일 교체 후 새 값 읽기, dispose 후 늦은 callback 차단, JSON/키 미노출, 오류 후 손상 유지 검사를 통과했다.
- 루트 EXE에서 실제 사용자 DB `테스트 기록 → 저장소 점검`을 클릭해 정상 보고서 생성을 확인했다.

## 남은 한계

최종 EXE는80,823,213 bytes/SHA256 `DE30F5EBB80BFC6396FD17539B0FA53BF4F1B0752C853B2677295A1359AC1DF9`이며 루트/release/publish가 같다. 마지막 UI 연결 표시 확인까지 종료한 후 실제 DB CLI는 종료0/Healthy=true,144,805,888 bytes/state42/events110,238/runs9다. 전체 회귀1,257개·관련WPF40개·반복 게시 smoke2회 통과. 자료: `artifacts/storage-audit-final-real.json`.

새 연결 수명으로 실제20종목 피드를 감시하고 HLB 차트를 연 뒤 중지/정상 종료했다. DB는144,801,792 bytes/state42/events110,238/runs8이며 배포 EXE 전체 점검이 정상이다. 당시 최초 백업의98MB·73,123이벤트와 구분한다. 새 실패 진단 파일이나 Stock 종료 오류는 발생하지 않았다.

재발 시 `storage-diagnostics/failure-*.json`과 당시 동작을 조사해야 한다. 정상 검사만으로 과거 사건이 해결됐다고 단정하지 않는다. 새 연결의 검사에서도 오류가 있으면 원본/사이드카를 보존한 후 사본에서 수리한다. 현재처럼 파일이 정상인 상태에서 복구 작업을 더 하는 것은 원인 조사나 검증이 되지 않는다.

로그: `artifacts/db-audit-{real-snapshot,old-0,old-1,old-2,old-3,stress,stress-fixed}.log`, `storage-audit-{core,build,full,publish}.log`, `storage-audit-published-real.json`. 실제 사용자 사본과 진단 JSON은 Git 제외 상태로 유지한다. 절차는 [저장소 운영](../maintenance/Storage.md).
