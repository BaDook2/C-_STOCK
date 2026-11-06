# 완료 — 2026-11-06 스크리닝 지정봉 체결·선택

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
