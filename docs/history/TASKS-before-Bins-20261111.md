# 현재 — Inje’s Strategy 로컬 교체 (2026-11-11)

사용자 정본: `docs/requirements/Injes-Strategy-20261111.txt`. 최신 정정으로 기존 전략/D 설정·전략 전용 기록을 제거했다. UI의 공통 구조를 유지하고 새 파라미터와 로직을 연결했다. **GitHub 업로드 금지: commit/push/태그/Actions/Release 없음.** 이번 작업은 2026-11-11 06:00 Asia/Seoul 전에 종료하며 이후 자동 재개하지 않는다.

- [x] 첨부 전체와 기존 시장자료/전략/체결/저장/UI 경로 확인.
- [x] family/registry로 분리한 Inje 네 전략, 공통14개·전용10/12/11/13개·Standard6개 매핑·Advanced 보존·엄격 JSON 구현.
- [x] 원본 틱/호가·수신/가용 시각·증분 지표·동일 경과시간 RVOL·당시 가용 지지 근거와 독립 네 상태 기계 구현.
- [x] 기존 PaperBroker/공통 실행 어댑터를 공유한 위험·수량·부분체결·취소·청산·원장·리플레이·성과 지표 구현.
- [x] 기존 Avalonia/WPF 설정·백테스트·스크리닝·가상 실행·복원/보호 재개에 연결. D 전용 창/버튼/알고리즘 삭제, 구전략 실행 거절. 전용 기록 정리는 격리 DB로 migration 경계 검증.
- [x] 저장 실패 시 진입 잠금과 마지막 정상 저장본·메모리 원장 보존, 응답 없는 피드의 유한 종료, 복원 원장 대사와 확인된 체결 표식 복구.
- [x] Avalonia Release 및 WPF 교차 빌드 모두 경고0/오류0. Core363개, Avalonia263개, 디자인17개 검사 통과. 설명서5개 전체 JSON·Core manifest89파일 검증.
- [x] 최신 Mac ARM64 로컬 ZIP 생성·재압축 해제 후 strict 서명 검증·실제 실행 파일 시작 검사 성공. 설치 앱 교체/개인 DB 초기화/실제 API·주문 없음.
- [x] 단일 상세 설명서에 전체 JSON 통합, 구조/UI/검증 문서와 현재 인수인계 갱신.

## 다음 요청 때 확인할 제한

- [!] 실제 미국 공급자의 TickSize·정지·뉴스·과거 RVOL·시장 단계/권한·L2 자료를 확인하고 부족한 어댑터를 연결해야 한다. 현재는 누락 자료를 MissingData로 차단한다. 원본 자료 없는 일봉 백테스트는 실행하지 않는다. 합성 검사는 실제 시장 수익성의 검증이 아니다.
- [!] Windows에서 WPF UI/파일 선택·저장·실행을 직접 검증해야 한다. 이 Mac에서는 교차 빌드만 확인했다. 새 Windows EXE/원격 CI는 실행하지 않았다.
- [ ] Bin’s Strategy는 별도 요구사항을 받은 뒤 독립 등록한다. 현재 미구현이며 Inje로 대체하지 않는다.

현재 전달본: `artifacts/Injes-Strategy-local-macos-arm64-20261111.zip`. 사용자 설명서: `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`. 상세 근거/한계: `docs/verification/Injes-2026-11-11.md`. 실제 자동 전략 주문은 잠금 유지. 기존 `/Applications/Stock Development.app`과 공개 v0.1.17은 이번 작업으로 교체하지 않았다.
