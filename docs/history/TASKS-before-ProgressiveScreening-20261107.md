# 진행 — 2026-11-07 중간 스크리닝 결과·애플 차트

- [x] 일반 스크리닝 완료 종목부터 즉시 반영, 진행 중 결과/차트 열람. 설정·중복 시작 잠금 유지. 관련352개/전체1342개·빌드0경고·0오류.
- [!] 애플 검색/일봉 정상. 실제 AAPL 캐시 DB 손상 재현. 사용자 실제392MB 원본 보존, 복구 검토 DB 구조/JSON 정상. state13개·틱6건 정상 복구 미확인으로 실제 DB 교체 질문 대기 중. 승인 전 원본 변경 금지.
- [ ] 관련 검사/회귀/빌드/로컬EXE 갱신, 문서 완료 기록. GitHub 게시하지 않음.

중요: 현재 앱의 진단은392,929,280bytes/손상인데 도구에서 같은 논리 경로를 열면 Codex LocalCache의144,805,888bytes/정상 파일로 리다이렉트된다(GetFinalPathNameByHandle 확인). 이전의 정상 검사로 현재 앱 DB가 정상이라고 판단하면 안 된다. UI/실제 앱 문맥에서 파일을 보존해 검증한다. 실제키 변경/주문/DB 초기화 금지.

## 이전 완료

# 완료 — 2026-11-06 근접 스크리닝·미국 일봉·테스트 기록

- [x] 전략별 `근접 후보 표시`/`후보 표시 최소 접근도`(기본 ON/60.00점), 일반·실시간 후보 필터/JSON/실행 snapshot 연결. 실제 매수 엔진·주문 조건은 유지.
- [x] 장외 실시간 시작 시 팝업 안내, 공개 CLOSE 응답도 준비봉 조회 전 차단.
- [x] StockViewer 네이버 미국 조회 방식 참고, 애플 등 검색/일봉/현재가 보완. 사용자 확인한 S&P500/나스닥100/합집합 구성 종목 선택·중복 제거.
- [x] 백테스트·일반/실시간 스크리닝 통합 기록 표, 실행일 기간 필터, 더블클릭 당시 상세/차트. 기존 데이터 보존, 원본 해시 중복 제거.
- [x] 최종 빌드0경고·0오류, 전체1,333개/전용16개/관련343개/실제 공개 조회2개 성공. 실제 마우스로 기록·매수 상세·설정·해외 UI 확인. publish/루트 EXE smoke 종료0/Success/NoBrokerCalls=true.
- [x] 루트Stock.exe와 release/Stock.exe 갱신·체크섬 대조.80,849,464 bytes / SHA256 `01D27CE34EC32CBD2BC9175CEE8F80F1275FBCAB18F3DFB5B1DD34A297F45A0A`. 이전 파일은 artifacts/before-research-expansion-20261106-234544에 보존.

## 검증·인계

- docs/verification/ResearchExpansion-2026-11-06.md, HANDOFF.md에 파일 지도/동작/검사/제한 기록.
- GitHub push/태그/릴리즈는 실행하지 않았다. 최신 사용자 정책에 따라 명시 요청 시에만 Release. 공개 v0.1.14에는 이번 기능이 포함되지 않는다.
- 이전 작업 상태 기록은 docs/history/TASKS-before-ResearchExpansion-20261106.md 및 기존 검증 문서에 보존.

## 유지할 제한·미확정

- [!] 이전 최초 SQLite11 유발 원인은 재현/확정되지 않았다. 읽기 전용 검사에서 기존5백테스트/2스크리닝과5개 차트 원본 유지. 정상 개인 DB를 초기화·수리·삭제하지 않는다. docs/maintenance/Storage.md 참고.
- 미국은 일봉 연구, 금액USD/현재 지수 구성 목록 기준. 과거 편입·편출 유니버스/해외 분·시간 실시간 자동매매는 미지원. 가격자료 검증 모형과 실제 주문을 구분한다.
- 실제 주문/실제 API키 변경/개인 DB 삭제 없음. 실제 해외 공개 조회와 fixture 가짜 broker 검사를 구분해 보고했다.

## 작업 상태 기록

요청 작업과 로컬 단일 EXE 갱신 완료. 남은 구현/검증 작업 없음. 다음 요청 전 검사·태그·릴리즈를 반복하지 않는다.

## 2026-11-07 작업 상태 기록

코드 구현/관련352·전체1342 검사 성공. StoragePaths/PhysicalDatabasePath로 AppData 리다이렉션 파일 대조를 보완했다. 사본 복구와 전체 컬럼 비교 완료(정상1070state/11933events/30runs 동일). 원본 SHA5f9875..., artifacts/storage-user-20261107-original/{inspection,index-scan,recovery-review,application-review,exact-record-comparison,native-review}.json 확인. 실제파일 교체 여부에 대한 사용자 질문 대기. Publish/루트 EXE 갱신과 문서 마무리 진행 중. GitHub 게시 없음.
