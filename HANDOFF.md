# 현재 인수인계 — v0.1.18 릴리즈 완료

사용자 2026-11-11 명시 요청으로 Windows/Apple Silicon v0.1.18을 게시하고 공개 재다운로드/실행/해시 검증까지 완료했다. 태그 v0.1.18과 macos-v0.1.18-r1은 소스2c1891c8de46172e7060b47004a38ad712bef211을 가리킨다. 문서 정리 커밋은 태그/앱 소스를 바꾸지 않는다. 이 요청은 완료됐으므로 새 명시 요청 없이 추가 태그·릴리즈를 반복하지 않는다.

## 결과

릴리즈 https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.18
Windows CI42962568324 성공(WPF338/Inje363/Bin187/UI859/디자인17), Mac CI42963164613 성공(Inje363/Bin187/UI861/디자인17·격리 Keychain). 양쪽0경고/0오류, manifest104/설명서JSON6개 PASS. 배포본과 공개 재다운로드본 시작, Mac ZIP/DMG/공개 차트까지 확인했다. 사용자 Mac macOS27.2에서도 공개 ZIP0.1.18의 서명/격리 시작 성공.

공개 자산6개가 체크섬/GitHub digest/CI결과와 일치한다. Stock.exe107884862bytes sha256 dbdc9441a21a980343de5f0dfdf408d62537b45eb5bbe607443b304661d79d4c. DMG53366698bytes sha256 95739ba764260b83370ac00263a3546eae70011bbf3c07c1296e0aac78667132. ZIP46939247bytes sha256 baea97434f786acfcef453158d0ae16c3f0001ad3333238959b2e6026db18143. 이전 v0.1.17 자산6개 ID/hash/크기 그대로다.

최종 검증 정본 docs/verification/Release-v0.1.18.md. 증거/공개 파일은 Git 제외 artifacts/releases/v0.1.18-verification에 final-verification.json/public-verification.json/Windows·Mac CI 결과/local-macos-verification.json으로 보관. 로그는 ../work/release-0.1.18-*.log, 실행 상태 release-0.1.18-state.json. Mac CI는 큰 .app을 풀지 않고 검증 JSON만 추출했다. 로컬 공개 ZIP 검사 사본은 /private/tmp/stock-public-v0.1.18-ae37lh06.noindex/Stock.saved-app으로 보관하고 등록을 해제해 설치 앱1개만 검색된다.

## 현재 제품과 제한

전략설정은 Inje/Bin 두 상위 그룹·단일 사용 체크·실제 파라미터·전체 JSON 프로필이다. Inje4개 알고리즘/Standard/Advanced, Bin3가족/5패턴의 독립 Core와 백테스트/일반 스크리닝을 포함한다. 도움말 중복과 StockViewer NXT/미국 시간외·데이마켓 공개 가격·1초 목표 조회/재시도를 반영했다. 한 파일 설명/전체 JSON은 docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md.

Bin 실시간 가상 주문/원장 복원은 미연결로 시작 차단, 실계좌 자동 전략 주문은 잠금 유지. 원본 자료가 없으면 MissingData이며 공개 일봉/가격을 틱·실제 체결로 만들지 않는다. Mac Developer ID/공증은 없고 ad-hoc 서명이다. 사용자 물리 입력/권한·실제 증권사 검증은 자동 검사와 구분한다.

릴리즈 작업에서는 개인 DB/키/실제 기록을 읽거나 삭제하지 않았고 설치 개발 앱은 교체하지 않았다. /Applications/Stock Development.app은13:36 KST 로컬 개발본0.1.17이며 공개0.1.18과 구분한다. 이전 앱 복구본·중복정리 백업·다른 StockViewer 저장소·기존 ZIP/DMG 보존. 직전 로컬 상태는 docs/history/*before-Release-v0.1.18-20261111.md에 있다.
