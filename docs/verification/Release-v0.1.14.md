# v0.1.14 배포 기록

## 변경·사전 검증

- 일반 스크리닝 분·시간봉의 공통 백테스트 체결 계산과 표식 표시 연결.
- 우클릭 전후가 같은 짧은 차트는 즉시 탐색 가능. 개별/묶음 표식 한 번 클릭 시 거래 행 선택·스크롤·상세 표시.
- [기능 검증](ScreeningTradeChart-2026-11-06.md): 전용58개, 관련328개, 전체1,317개, 빌드 경고0·오류0, publish 성공.
- 로컬 배포 EXE smoke 종료0/Success=true/NoBrokerCalls=true. 크기80,835,673 / SHA256 FDD5538DA55E444BC3F5AB42AF27A75543640C6E205D9EEC2F42E89364A915B4.
- Windows 실제 마우스로 시간봉 표식 선택과 우클릭 없는 휠 확대, 분봉 전환/묶음 표식 선택 확인. 실제 주문/실제 키 변경 없음.

## 공개 배포

- [GitHub Release](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.14), [Stock.exe 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.14/Stock.exe), [체크섬](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.14/Stock.exe.sha256).
- 소스 bb1d510a2c85a1a33c81b7ed7082bbef4f8ed319, [Actions42183510638](https://github.com/BaDook2/C-_STOCK/actions/runs/42183510638) 성공. 전체1,317개/빌드0경고·0오류/publish/배포 EXE smoke 성공. 전체 검사21분31초, job23분19초.
- 게시시각2026-11-06 17:13:42 KST. 공개 자산80,868,637 bytes / SHA256 `77a647e6f86eca9c069b991c31fad2bc35c9eb921f11c6724766422a46f8b7db`.
- 공개 EXE와 checksum을 다시 다운로드해 checksum/자산digest/실제SHA256/크기를 대조했다. 모두 일치했다.
- 다운로드 EXE smoke 종료0, Success/NoBrokerCalls=true. artifacts/download-v0.1.14-smoke-20261106-171427/result.json.
- 실행 중인 Stock 프로세스가 없는 상태에서 공개 검증 EXE를 루트 Stock.exe와 release/Stock.exe에 반영했고 세 파일의 SHA256이 일치했다. 이전 루트 EXE는 artifacts에 보존했다.
- artifacts/release-v0.1.14-{ci.log,job.log,job.json,proof.json}에 근거 보관. Git에는 키/개인 DB/실제 진단 원문/EXE를 추가하지 않았다.

현재 요청의 구현·로컬 검증·공개 배포 검증은 완료됐다. 실계좌 주문·실제 키 변경은 실행하지 않았다. 과거 SQLite11 최초 유발 사건은 이 변경의 원인으로 단정하지 않으며 기존 미확정 기록을 유지한다.
