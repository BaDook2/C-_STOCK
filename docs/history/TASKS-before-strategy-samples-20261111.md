# 현재 — v0.1.18 Windows / Apple Silicon 릴리즈 완료

2026-11-11 사용자 명시 릴리즈 요청을 이행했다. [GitHub 릴리즈](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.18). 실제 자동주문·사용자 DB/실제 기록 삭제 없음.

- [x] 누적 Inje/Bin/UI/StockViewer 가격·도움말/JSON 개선을 소스2c1891c로 commit/push.
- [x] Git-only 빌드0/0·Inje363/Bin187/UI861/디자인17/manifest104/JSON6개 통과.
- [x] Windows CI42962568324 성공: WPF338/Inje363/Bin187/UI859·배포 EXE·공개 재다운로드 실행/버전/hash.
- [x] 동일 소스 Mac CI42963164613 성공: Inje363/Bin187/UI861·Keychain·앱/ZIP/DMG·공개 차트·공개 재다운로드 실행.
- [x] 공개 EXE/DMG/ZIP과 체크섬6개를 다시 내려받아 크기·SHA256·API digest·CI hash 대조. 이전 v0.1.17 자산6개 보존.
- [x] 사용자 Mac에서도 공개 ZIP의 버전·서명·격리 시작 확인. 검사 사본 미색인 보관·등록 해제, 기존 설치 앱1개만 검색.
- [x] 릴리즈 노트/설치 안내/README/검증 문서 갱신.

정본: docs/verification/Release-v0.1.18.md. 배포 태그 v0.1.18/macos-v0.1.18-r1은2c1891c를 가리킨다. Bin 실시간 가상 주문/복원은 아직 시작 차단, 실계좌 자동 전략 주문도 잠금 유지. 이번 게시 요청은 이행 완료했으며 다음 추가 릴리즈에는 새 사용자 요청이 필요하다.
