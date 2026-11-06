# 저장소 진단과 안전한 운영

## 저장 경로와 데이터

앱의 DB는 `MainViewModel.StateDirectory/workspace-v2.db`이다. 현재 사용자 실제 경로는 `C:\Users\wkdru\AppData\Local\StockPaperTrading\KiwoomQuant\workspace-v2.db`다. 계좌 키는 같은 폴더의 `broker-keys/*.protected`에 Windows DPAPI로 따로 보관한다. 키·DB·실제 진단 결과는 Git에 올리지 않는다.

`state`는 전략/관심그룹/자료캐시/체결표식/실행 snapshot, `events`는 원본 시세 이벤트, `runs`/`audit`는 실행·판단 기록이다. 지원 과거 DB를 임의로 지우거나 빈 DB로 교체하지 않는다.

## 점검 방법

- 앱 `테스트 기록 → 저장소 점검`: 전체 `integrity_check`, 모든 알려진 JSON 컬럼의 UTF-8/JSON 형식, 테이블별 행 수를 확인한다. 원본에는 쓰지 않는다.
- CLI: `Stock.exe --storage-check <DB 절대경로> <보고서 JSON 절대경로>`. 정상 종료0, 검사상 오류2, 잘못된 실행/보고서 실패3. 브로커 연결/주문 없이 EXE 내부의 SQLite를 사용한다.
- 보고서의 `DatabasePath`, `Bytes`, `Engine`, `SourceId`, `Integrity`, `Rows`, `JsonIssues`, `Healthy`를 확인한다. 구조 정상과 JSON 정상은 별개다. `quick_check`만으로 데이터 손상 없음이라고 단정하지 않는다.

## 오류가 발생했을 때

`TradingStore`는 작업별 새 연결(`Pooling=false`)을 열고 작업·트랜잭션이 끝나면 닫는다. 인스턴스 내 쓰기는 gate로 직렬화한다. 원본 틱의 별도 읽기 연결도 비풀링이다. `DELETE` journal과 `synchronous=FULL`은 유지한다. 일반적인 프로세스 종료만으로 DB가 손상된다는 이전 설명은 근거가 부족하므로 적용하지 않는다.

SQLite 오류를 빈 결과나 성공으로 바꾸지 않는다. 기존 예외는 `StorageException.InnerException`에 남기고, 원본 DB 폴더의 `storage-diagnostics/failure-*.json`에 작업 종류·오류/확장코드·프로세스/런타임·독립 읽기 연결의 무결성 결과를 저장한다. SQL 파라미터 값, JSON 본문, 키 이름, 계좌 키는 기록하지 않는다. 실패한 쓰기/주문은 자동 재시도하지 않는다. 차트 체결 조회 실패는 별도로 안내하며 가짜 체결로 대체하지 않는다.

실제 파일 손상과 장기 연결의 캐시/핸들 상태 문제는 구분해야 한다. 새 연결에서도 무결성 오류가 있으면 물리적 손상 증거다. 새 연결은 정상이라면 파일 재구축을 먼저 하지 말고 해당 실패 보고서/재현 동작/동시 접근을 조사한다. 현재 작업은 후자의 가능성을 예방했지만 2026-11-06 09:18/09:20 최초 오류를 발생시킨 정확한 사건은 확정하지 못했다.

## 백업/수리 절차

1. 자동매매/수집 중지 → 앱 정상 종료 → Stock 프로세스가 없는지 확인한다.
2. DB와 존재하는 journal/WAL/SHM을 함께 날짜별 폴더에 보존하고 해시를 기록한다. 앱 실행 중 파일 복사로 일관된 백업을 만들었다고 가정하지 않는다. 실행 중 백업은 SQLite backup API를 사용해야 한다.
3. 사본으로 무결성·JSON·행 수·기존 오류 쿼리를 검사한다.
4. 손상이 확인된 경우에만 사본에서 복구한다. 인덱스만 손상되었는지, 본문이 손상되었는지 구분한다. 복구 전후 행 수와 주요 레코드 해시를 대조하고 손실 내역을 기록한다.
5. 파일 교체는 모든 연결 종료 후 원본을 보존한 상태로 수행한다. 손실/계좌 관련 판단이 필요하면 사용자 확인을 받는다.

이번 원본 백업은 `diagnostics-20261106-102905/`다. 현재 DB와 과거 실패 사본, Python SQLite 3.50.4와 앱 SQLite 3.53.4의 전체 무결성 검사 결과는 모두 정상이다. 필요 없는 수리/초기화/VACUUM을 하지 않았다. 자세한 근거는 [DB 조사](../verification/StorageAudit-2026-11-06.md)를 참조한다.

공식 근거: [SQLite 손상 원인](https://www.sqlite.org/howtocorrupt.html), [무결성 검사](https://www.sqlite.org/pragma.html#pragma_integrity_check), [Microsoft.Data.Sqlite 동시 사용](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors). 작업별 연결은 원인 확정의 대체물이 아니며 실패 보고서와 재현 검사가 필요하다.
