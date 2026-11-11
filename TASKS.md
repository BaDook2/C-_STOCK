# 현재 — v0.1.18 Windows / Apple Silicon 릴리즈 진행

사용자가 현재 프로그램의 GitHub 릴리즈를 명시 요청했다. 이번 소스 commit/push/Actions/새 태그·Release를 진행한다. 실제 자동주문·사용자 DB/실제 거래기록 삭제는 하지 않는다. 기존 릴리즈/자산은 덮어쓰지 않는다.

- [x] 인증·원격 main/최신 v0.1.17·현재 미게시 변경·기존 배포 절차 확인.
- [x] v0.1.18 버전·변경 사항·CI의 Inje/Bin/JSON 검사 반영과 공개 범위/비밀자료 제외 점검.
- [x] Git-only 소스 Release 빌드·전체/디자인/manifest/설명서 검증.
- [~] 소스 commit/push 후 Windows Actions 시작.
- [ ] Windows Actions 전체 검사·단일 EXE 실행·새 v0.1.18 게시·공개 재다운로드 hash/실행 확인.
- [ ] 동일 Core/Avalonia 소스의 macos-v0.1.18-r1 Actions 검사·DMG/ZIP·시작·공개 조회·Keychain·공개 다운로드 검증.
- [ ] 공개6개 자산 hash/기존 자산 보존 최종 확인 및 TASKS/HANDOFF/검증 문서 갱신.

현재 제한은 릴리즈 노트에 명시한다: Bin 실시간 가상 주문/재시작 원장 복원 미연결로 시작 차단. 실계좌 자동 전략 주문 잠금 유지. 원본 이벤트가 없으면 백테스트/스크리닝은 MissingData이며 일봉을 틱으로 만들지 않는다.
