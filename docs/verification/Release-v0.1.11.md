# v0.1.11 릴리즈 검증 — 2026-11-06

- 소스: `44bc68b83a4f4da927bbaaec23cd02f68f627b03`, 태그 `v0.1.11`.
- [GitHub Actions](https://github.com/BaDook2/C-_STOCK/actions/runs/42151757915).
- 사용자 요청에 따라 검증 성공 후 GitHub 게시를 AGENTS/maintenance/Verification의 완료 루틴에 추가했다. 기존 Actions의 빌드/전체 검사/배포 EXE smoke 성공 조건을 그대로 사용한다.
- 로컬: 전체 회귀 1,257개, 빌드 경고0/오류0, 단일 EXE 반복 smoke2회 성공. DB/실제 기능 점검 근거는 StorageAudit-2026-11-06.md와 FeatureAudit-2026-11-06.md.
- GitHub: 전체 회귀1,257개·빌드 경고0/오류0·publish/배포 EXE smoke/Release 모두 성공. 전체 검사23분4초, 전체job25분22초. 이전 v0.1.10의 전체 검사는14분32초였으므로 로컬 검사 시간과 동일하게 예상하지 않는다.
- 공개 시각: 2026-11-06 12:05 KST. [Release](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.11), [Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.11/Stock.exe).
- 공개 파일: 80,854,787 bytes, SHA256 `2bea9661999fad1c4702bb11c60cdcf11d97e34c546e658cdd394ef5163d93e3`.
- 내려받은 EXE의 계산 해시 = 공개 Stock.exe.sha256 = GitHub 자산 digest. 다운로드 EXE smoke 종료0, Success=true, NoBrokerCalls=true. 이후 루트/release Stock.exe를 같은 파일로 교체했고 해시 일치/실행 중 앱 없음 확인.
- 근거(개인 로컬 자료, Git 제외): artifacts/release-v0.1.11-ci.log, release-v0.1.11-proof.json, download-v0.1.11-smoke-20261106-120608/result.json. 사전 로컬 publish 파일은 별도 보존했다.
- 실제 계좌 주문/키 변경/DB 초기화는 이 릴리즈 작업에서 실행하지 않는다. 과거 SQLite11 최초 유발 사건은 미확정이며 해당 제한을 릴리즈 설명에 공개한다.

공개 EXE는 Actions에서 새 커밋으로 빌드하므로 로컬 사전 검증 파일과 빌드 메타데이터/해시가 다를 수 있다. 비교 기준은 공개 EXE의 실제 SHA256, 함께 배포한 체크섬, GitHub 자산 digest다.
