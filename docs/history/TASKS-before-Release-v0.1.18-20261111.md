# 현재 — 도움말·StockViewer 가격·JSON 검증 완료

사용자 요청 세 가지를 완료하고 2026-11-11 13:36 KST 기존 설치 앱에 반영했다. GitHub 게시·실계좌 주문·실제 거래기록 삭제 없음.

- [x] 물음표 도움말의 같은 short/detail 문장 중복 제거, 실제 팝업 검사.
- [x] StockViewer NXT·미국 시간외/데이마켓 최신 가격·1초 목표 조회·재시도·Retry-After·취소 흐름 포팅. StockViewer 원본 저장소 읽기만 함.
- [x] 현재 파라미터와 JSON 구조 일치 확인. Inje166/Bin172 변경값, 실제 저장/다운로드/불러오기/재시작의 모든 프로필 값 보존 및 잘못된 JSON 차단 검사.
- [x] Avalonia861/Inje363/Bin187 PASS, 빌드0경고/0오류, manifest104파일/설명서JSON6개, Mac 패키지 합성 시작·실제 공개 차트·서명 검사.
- [x] StockViewer와 같은 시점의 삼성전자·애플 공개 가격 일치 확인.
- [x] `/Applications/Stock Development.app` 갱신. 직전 앱은 미색인 `.saved-app` 백업, bundleID 검색은 설치 앱1개.
- [x] API/UI/전략 설명서·검증 문서 갱신.

기존 제한: Bin 실시간 가상 주문/재시작 보호 원장 복원은 미연결이므로 시작 차단. 실계좌 자동주문·GitHub 게시에는 별도 요청이 필요하다. 최신 검증: docs/verification/Viewer-Price-JSON-2026-11-11.md.
