# 현재 작업

2026-11-06 DB 근본 원인 조사·전체 기능 점검·문서 정리 및 검증 성공 후 GitHub 릴리즈.

- [x] 사용자 요청에 따라 AGENTS/검증 문서에 필수 검사 전체 통과 시 커밋·push·GitHub Release·공개 EXE 재검증을 완료 루틴으로 추가.
- [x] v0.1.11/44bc68b 게시 완료. Actions 42151757915: 전체1,257개·빌드 경고0/오류0·publish/smoke/Release 모두 성공. 공개 EXE 재다운로드→체크섬/GitHub digest/실제 해시 일치→실행 종료0/Success/NoBrokerCalls 확인→루트/release Stock.exe 교체 완료. 근거: docs/verification/Release-v0.1.11.md.

- [x] 실제 사용자 DB 98MB 원본/사이드카 보존, 과거 실패 사본4개 비교, full integrity/index/모든 JSON 검사. 현재 손상 없음. SQLite 3.50.4(Python)/3.53.4(배포 EXE)에서 독립 검증.
- [x] 장기·풀링 연결 대신 작업별 비풀링 연결/직렬화·명시적 종료로 수정. 파일 교체 후 stale handle, dispose 후 늦은 callback, 실제 깨진 페이지 오류11/JSON불량 분리/비밀 미기록 검사.
- [x] 오류/확장코드와 독립 연결 무결성 증거를 private 보고서에 기록. 실패한 쓰기 자동 재시도/빈 DB 초기화 없음. 테스트 기록의 저장소 점검과 --storage-check CLI 추가.
- [x] 오프라인/가짜 온라인 전체 1,257개, WPF 버튼 Invoke 33회·팝업/전략/계좌/중지 검사, 빌드 경고0/오류0, 게시 EXE smoke 성공.
- [x] 실제 온라인 계좌 읽기·20종목 피드·확정봉 분석·HLB 차트15회 모형거래·선택 유지·중지·정상 종료 확인. 이후 DB full integrity/JSON 정상. 온라인 자동매매 체크박스 표시 갱신 누락도 수정·회귀40개 확인.
- [x] 누적 TASKS/HANDOFF를 docs/history에 보존하고 시작 안내·저장소 운영·검증 명령·기능 점검표 추가.
- [x] 사전 로컬 빌드(publish/win-x64) 80,823,213 bytes/SHA256 DE30F5EBB80BFC6396FD17539B0FA53BF4F1B0752C853B2677295A1359AC1DF9 반복 smoke2회 및 최종 실제 DB CLI 정상. 이후 루트/release는 아래의 검증된 GitHub 빌드로 갱신했다. 검증 앱 종료/실주문0. 실행 가능한 잔여 구현 없음.
- [!] 09:18/09:20 최초 SQLite11의 유발 사건은 재현/확정되지 않았다. 모든 현존 사본/부하 검사는 정상이다. 새 failure 보고서 없이 파일 손상/하드웨어/SQLite 버그로 단정하거나 DB를 재구축하지 말 것.

## 현재 제한

- 실제 돈이 드는 주문·실제 API키 삭제/변경은 이번 점검에서 실행하지 않는다. 해당 기능은 격리 DB/더미 키/가짜 broker로 검사했다.
- 시점별 종목 상태/위험 자료 미확인은 전략 일치/자동주문으로 통과시키지 않는다. 오프라인 공개 스크리닝은 신호 표시만이다.
- 국내390분 정규장 내 7~12시간 완성 봉은 생성할 수 없다. 다른 봉으로 대체하지 않는다.
- 최신 배포: [v0.1.11](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.11). 루트/release/공개 Stock.exe: 80,854,787 bytes / SHA256 2BEA9661999FAD1C4702BB11C60CDCF11D97E34C546E658CDD394EF5163D93E3. 사전 로컬 publish 파일과 빌드 메타데이터/해시가 다름은 정상이다.

## 재개 순서

AGENTS.md → 이 문서 → HANDOFF.md → docs/START_HERE.md에서 관련 항목만 읽는다.
상세 근거: docs/verification/StorageAudit-2026-11-06.md, FeatureAudit-2026-11-06.md.
과거 누적 기록(당시의 미완료/추정 포함): docs/history/TASKS-through-20261106.md. 과거 체크박스를 새 작업으로 재실행하지 않는다.
