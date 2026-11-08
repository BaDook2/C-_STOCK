# 진행 — 근접 스크리닝·해외 시세·테스트 기록 확장

- [x] 전략별 근접 후보 표시/최소 접근도 설정과 일반·실시간 후보 필터 연결. 매매 판단/체결은 공통 엔진 유지.
- [x] 장외 실시간 시작 시 팝업 안내.
- [x] StockViewer 네이버 미국 시세 경로 재사용, 애플 등 해외 검색/일봉 보완, S&P500/나스닥100/합집합 구성 종목 스크리닝.
- [x] 백테스트·스크리닝 통합 기록 표, 실행일 기간 필터, 더블클릭 상세/차트 재현.
- [ ] 관련·전체 검사/빌드/로컬 EXE 갱신. 사용자 명시 전 GitHub push/태그/릴리즈 없음.

사용자 확인: 나스닥은 나스닥100 구성 종목이다. StockViewer는 읽기 참고만 하고 수정하지 않는다. 실제 주문/키 변경/기존 DB 삭제 금지.

## 이전 완료 기록

# 완료 — 2026-11-06 릴리즈 정책 변경

- [x] 사용자 명시 요청 시에만 GitHub Release를 진행하도록 AGENTS.md와 검증 절차를 변경했다. 이전 자동 릴리즈 승인은 철회됐다. 작업 완료/검사 성공/소스 push 요청만으로 배포 태그·릴리즈 Actions·Release를 실행하지 않는다.
- [x] HANDOFF에 새 정책을 기록했다. 이번 변경은 문서만 수정했고 릴리즈를 실행하지 않았다.

## 직전 완료 — 스크리닝 지정봉 체결·선택

- [x] 일반 스크리닝 분·시간봉 상세에서 공통 백테스트 엔진으로 당시 설정/기간의 모형체결과 지지 snapshot을 계산해 표시. 준비 봉 정책은 메인 백테스트와 공유.
- [x] 우클릭 전후 범위가 같은25봉 이하 차트는 즉시 탐색. 개별/묶음 글자·체결 봉 클릭 시 해당 거래 표 행을 선택·스크롤하고 상세 표시. 오른쪽 표식 잘림 방지.
- [x] 빌드 경고0·오류0, 전용58개/관련328개/전체1,317개, publish·EXE smoke 종료0/Success/NoBrokerCalls=true. Windows 실제 마우스 확인. 실제 주문/키 변경 없음.
- [x] v0.1.14/bb1d510 공개. [Actions42183510638](https://github.com/BaDook2/C-_STOCK/actions/runs/42183510638) 빌드0경고·0오류, 전체1,317개/배포 EXE smoke/Release 성공. 전체 검사21분31초, job23분19초.
- [x] 공개 EXE checksum/자산 digest/실제 SHA256 일치, 다운로드 EXE smoke 종료0/Success/NoBrokerCalls=true. 루트와 release/Stock.exe도 동일 공개 파일로 교체·해시 대조 완료.

## 근거·배포

- docs/verification/ScreeningTradeChart-2026-11-06.md, docs/verification/Release-v0.1.14.md.
- 로컬 검증 EXE: publish/win-x64/Stock.exe, 80,835,673 bytes / SHA256 FDD5538DA55E444BC3F5AB42AF27A75543640C6E205D9EEC2F42E89364A915B4.
- [최신 릴리즈](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.14), [Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.14/Stock.exe). 공개/루트/release EXE80,868,637 bytes / SHA256 77A647E6F86ECA9C069B991C31FAD2BC35C9EB921F11C6724766422A46F8B7DB. 이전 루트 파일은 artifacts에 보존했다.

## 유지할 미확정·제한

- [!] 09:18/09:20 최초 SQLite11의 유발 사건은 재현/확정되지 않았다. 원본/실패 사본4개/최종 실제 DB의 무결성·JSON 정상. 새 증거 없이 정상 DB를 초기화·재구축·수리하지 않는다. docs/maintenance/Storage.md 참고.
- 실계좌 주문/실제 키 변경은 실행하지 않았다. 테스트는 격리 DB/가짜 증권사. 과거 상태·위험 미확인은 실제 주문 승인으로 통과시키지 않는다.
- 체결이 발생하지 않은 결과에 매매 표식을 만들지 않는다. 국내390분 정규장 내7~12시간 완성 봉이나 제공자가 반환하지 않은 분봉을 다른 자료로 가장하지 않는다.

## 완료 기록

현재 요청은 완료됐다. 다음 요청이 없으면 동일 검사/태그/릴리즈를 반복하지 않는다. 코드와 검증은 docs/verification/ScreeningTradeChart-2026-11-06.md, 공개 배포는 Release-v0.1.14.md를 확인한다. 실제 주문/키 변경/정상 DB 수리는 실행하지 않았다.

## 새 요구 작업 상태 (2026-11-06)

전용16개/관련343개/전체1333개/실제 해외 조회2개 성공, 실제 창의 기록 더블클릭·표식 선택/설정/해외 목록 확인. 배포 smoke의 옛 파라미터 개수 검증을 새61개/27행으로 보완했고 다른 시장 피드 요청도 차단해 최종 빌드·전체 회귀 실행 중(세션63015, artifacts/research-expansion-{build-final,full-final}.log). 남은 일: 최종 publish/smoke, 루트와 release EXE 갱신, 문서 완료 상태/해시 기록. 실제DB5백테스트/2스크리닝 보존, API키·주문 변경 없음. GitHub는 게시하지 않는다.
