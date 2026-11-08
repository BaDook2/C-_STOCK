# 저장소 진단과 안전한 운영

## 저장 경로와 데이터

앱의 DB는 `MainViewModel.StateDirectory/workspace-v2.db`이다. 현재 사용자 실제 경로는 `C:\Users\wkdru\AppData\Local\StockPaperTrading\KiwoomQuant\workspace-v2.db`다. 계좌 키는 같은 폴더의 `broker-keys/*.protected`에 Windows DPAPI로 따로 보관한다. 키·DB·실제 진단 결과는 Git에 올리지 않는다.

`state`는 전략/관심그룹/자료캐시/체결표식/실행 snapshot, `events`는 원본 시세 이벤트, `runs`/`audit`는 실행·판단 기록이다. 지원 과거 DB를 임의로 지우거나 빈 DB로 교체하지 않는다.

## 2026-11-07 메모리 시세 정책

앱의 기본 TradingStore는 `memoryMarketData:true`다. `bars:`/`live-ticks:`/`verified-tick-coverage:`/`catalog` 및 원본 MarketEvent는 SQLite 대신 SessionMarketData에서만 읽고 쓴다. 예전 동일 키의 디스크 캐시는 읽지 않으며 기존 행도 삭제하지 않는다. 일반 재조회 캐시는 LRU128개/JSON32M문자 이내다. 원본 이벤트는 중복 해시를 제거하고 시각/sequence 순서로 현재 실행 중만 제공한다. 앱 종료 시 메모리 가격·원본 이벤트가 사라진다.

`screen-data:`/`screen-bars:`/`screen-chart:`/`oversold-bars-snapshot:` 등은 재조회 캐시가 아니라 **테스트 기록의 당시 원본과 판단 자료**이므로 계속 보존한다. 과거 API가 수정되거나 이후 전략을 바꿔도 같은 테스트를 다시 열기 위한 데이터다. 원본 이벤트 테이프 자체는 테스트 기록에 새로 쓰지 않는다. 전략/관심종목/주문 가드/체결/안전 상태는 유지한다. 계좌 키는 계속 별도 DPAPI 파일이다.

일반 TradingStore의 기본 인자는 기존 저장 형식 검증·마이그레이션 도구와의 호환을 위해 유지한다. 제품의 WorkspaceViewModel/MainViewModel 기본 생성 경로는 명시적으로 메모리 정책을 켠다. 테스트가 저장소를 직접 주입하면 해당 인자를 명시해 정책을 검증한다. 원본 사용자 DB와 키를 테스트 fixture로 사용하지 않는다.

## 점검 방법

- 앱 `테스트 기록 → 저장소 점검`: 전체 `integrity_check`, 모든 알려진 JSON 컬럼의 UTF-8/JSON 형식, 테이블별 행 수를 확인한다. 원본에는 쓰지 않는다.
- CLI: `Stock.exe --storage-check <DB 절대경로> <보고서 JSON 절대경로>`. 정상 종료0, 검사상 오류2, 잘못된 실행/보고서 실패3. 브로커 연결/주문 없이 EXE 내부의 SQLite를 사용한다.
- 보고서의 `DatabasePath`, `PhysicalDatabasePath`, `Bytes`, `Engine`, `SourceId`, `Integrity`, `Rows`, `JsonIssues`, `Healthy`를 확인한다. 실행 앱과 도구가 같은 파일을 여는지 최종 핸들 경로·크기·행 수를 대조한다. 구조 정상과 JSON 정상은 별개다. `quick_check`만으로 데이터 손상 없음이라고 단정하지 않는다.

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

이전 `diagnostics-20261106-102905/`와 [DB 조사](../verification/StorageAudit-2026-11-06.md)의 정상 판정은 당시 도구가 연 파일 기준이었다. 아래2026-11-07 실제 파일 대조에서 사용자 앱과 다른 리다이렉션 DB임을 확인했으므로 실제 사용자 DB가 정상이라는 근거로 사용하지 않는다.

공식 근거: [SQLite 손상 원인](https://www.sqlite.org/howtocorrupt.html), [무결성 검사](https://www.sqlite.org/pragma.html#pragma_integrity_check), [Microsoft.Data.Sqlite 동시 사용](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors). 작업별 연결은 원인 확정의 대체물이 아니며 실패 보고서와 재현 검사가 필요하다.

## 2026-11-07 실제 파일 경로 정정

이전 정상 점검은 Codex 실행 문맥의 AppData 리다이렉션 파일(144,805,888bytes, state42/events110238/runs9)을 검사했다. 사용자가 직접 실행한 앱의 DB(392,929,280bytes, state1083/events11939/runs30)와 다른 파일이다. 같은 논리 절대경로라는 이유로 실제 사용자 DB가 정상이라고 한 이전 결론은 적용할 수 없다. Windows GetFinalPathNameByHandle는 도구 문맥에서 OpenAI.Codex LocalCache 경로를 반환했다. 실제 앱/Explorer에서 보이는 파일 크기와 행 수를 함께 대조해야 한다. 새 StorageInspection.PhysicalDatabasePath와 저장소 점검 화면은 실제 열린 파일 경로도 기록한다.

사용자 앱을 작업 없는 상태에서 정상 종료하고 Explorer 문맥으로 실제392MB 파일을 artifacts/storage-user-20261107-original/workspace-v2.db에 보존했다. SHA256 5F987540CED3935BE404E5E73345D643EF0C6F6EB22DA45B1A88801F78E4E318. 이 사본에서 실제 B-tree/overflow 손상과 AAPL 캐시 행 손상을 재현했다. 애플 검색과 공개 일봉 API는 정상이다. 최초 손상 유발 사건은 여전히 미확정이며 단순 프로세스 종료를 원인이라고 단정하지 않는다.

공식 SQLite 도구3.54.1의 SHA3를 공식 manifest와 대조하고 사본에만 `.recover --ignore-freelist`를 적용했다. 읽을 수 있던 정상1070개 state/11933개 events/30개 runs는 모든 컬럼이 그대로다. state13개는 손상·유효하지 않은 JSON·복구 불확실 항목으로 보존용 격리, 원본 틱6개는3개 읽기 오류/3개 원래 행 부재로 복구 확인 불가다. lost_and_found3개는 어떤 활성 테이블에도 임의 배정하지 않았다. 복구 검토 파일 application-review.db는 SQLite3.53.4 무결성/JSON 정상이다. 원본·SQL 복구 자료·사본을 Git에 올리지 않는다.

사용자가 이 제한을 안내받고 **“복구 DB로 교체하고 원본 보존”**을 승인했다. 앱 정상 종료 상태에서 실제 사용자 Explorer로 원본을 같은 폴더의 `workspace-v2.before-repair-20261107.db`로 보존하고, 검증된 복구 DB를 활성 workspace-v2.db로 적용했다. 적용 전 복구 파일 SHA256은 `387F2CED4046A3F1973AC8732F2CF8D8447AA993F9842CB98706F871060E50CB`다. 계좌 키 파일/도구의 Codex LocalCache DB는 수정하지 않았다.

실제 Explorer에서 갱신된 Stock.exe 실행 → 애플 검색/가격/일봉 표시 성공 → 앱 저장소 점검에서 실제 파일이 정상 AppData 경로임을 확인했다. integrity ok/JSON0/state1071/events11933/runs30, 보고서 inspection-20261107-020937-095.json. 복구 뒤 새 Apple 캐시1개가 추가됐다. 개인 상세 근거는 artifacts/storage-user-20261107-original/native-validation.json, 공개 가능한 구현/검증 요약은 [완료 기록](../verification/ProgressiveScreening-2026-11-07.md)이다. 첫 손상 발생 사건은 여전히 미확정이므로 재발 시 실제 열린 파일과 실패 보고서부터 확인한다.

실제 API/데이터 손실과 DB 저장 실패를 구분하기 위해 차트 캐시 저장 예외를 네트워크 실패로 처리하지 않고, 스크리닝 StorageException은 자료부족으로 바꾸지 않으며 병렬 요청을 취소하고 저장 실패를 알린다.
