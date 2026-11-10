# 현재 완료 — Windows·Apple Silicon Mac v0.1.17

2026-11-10의 양OS 릴리즈 명시 요청을 이행했고 한국시간11월11일 공개 파일 검증을 마쳤다. 진행 중인 구현·게시 작업은 없다. 새 명시 요청 없이 태그/릴리즈를 반복하지 않는다.

- [x] 전략 설정 통합·배치/펼침·사용 체크, 독립 봉 단위, 검은 MA240/보라색 급락 지점선, JSON 매칭/오류 검증 및 Rider .app 흐름을 포함했다.
- [x] Git-only 사전검사·개인정보 제외 후 소스를 커밋/push했다. 첫 Windows CI에서 발견한 KST/UTC 기록 기본 날짜 불일치를 수정하고 관련 검사를 추가했다.
- [x] [Windows CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42842992293): WPF1655/D13/기록선행11/Core192/Avalonia631/디자인18, 배포 EXE 및 공개 EXE 재다운로드/실행 성공.
- [x] [Mac ARM64 CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42848351841): Core192/Avalonia633/디자인18(더미 Keychain 포함), 앱/ZIP/DMG 시작·공개 차트 성공. 공개 Mac 파일 재다운로드·ZIP 추출·DMG 마운트·네이티브 실행도 성공.
- [x] 공개6개 자산의 크기·SHA256이 빌드·체크섬·GitHub digest와 일치한다. 기존 v0.1.16 자산 및 Mac 추가 전 v0.1.17 Windows 자산 ID/해시는 유지했다.
- [x] [v0.1.17 릴리즈](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.17) 게시와 안내·검증 기록 완료. 앱 소스 `d067be2`, Mac 빌드 태그 `macos-v0.1.17-r1`은 `17a297b`(앱 소스 동일).
- [x] 현재 전략 설명서 하나에 기존/D ON 전체 JSON2개를 수록했다. [정본](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md); 개인 저장값이 아닌 코드 기본값이다.

실제 사용자 DB/키/계좌와 설치된 개발 앱은 이번 릴리즈 작업에서 교체하지 않았다. 실제 돈 주문0. Mac은 Apple Silicon용 ad-hoc 서명이며 Developer ID/공증은 없다. 사용자 물리 입력·Keychain 권한/잠금·실제 증권사 API와 D 실계좌 자동 주문은 검증/지원 범위를 구분한다.

상세: [릴리즈 검증](docs/verification/Release-v0.1.17.md), [인수인계](HANDOFF.md). 이전 진행·개발 완료 기록은 [이력](docs/history/TASKS-before-v0.1.17-complete-20261111.md)에 보존했다.
