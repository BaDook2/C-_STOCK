# v0.1.18 Windows / Apple Silicon 릴리즈 검증

2026-11-11 사용자 명시 요청으로 현재 Inje/Bin·전략설정 UI·StockViewer 공개 가격·도움말/JSON 개선을 새 v0.1.18로 준비했다. 이 문서는 진행 기록이며 아래 CI/공개 자산 검증이 완료되기 전 게시 완료를 의미하지 않는다. 실제 자동주문·사용자 DB/실제 기록 삭제는 하지 않는다.

## Git-only 사전 검사

개발 폴더의 무시된 바이너리/개인 자료에 의존하지 않도록 Git의 추적/추가 예정 파일579개만 새 임시 경로로 복사했다. 실행 소스·워크플로·프로젝트가 staged 소스와 byte 단위로 일치함을 확인했다. 자격증명 패턴/DB/키/실행 바이너리 포함 후보0개다. 첨부 요구사항 Markdown의 두 칸 줄바꿈은 원문을 보존했다.

- Avalonia Release 빌드 경고0/오류0.
- WPF 호환 소스 교차 빌드 경고0/오류0 (실제 Windows 실행은 CI에서 검증).
- Inje363/Bin187/Avalonia861/디자인17 검사 PASS.
- Core manifest104파일 일치, 통합 설명서 Inje5개+전체 프로필1개 strict import PASS.
- 새 빌드에서 드러난 합성 HTTP fixture 미초기화 경고2개는 명시적 기본값 초기화로 제거했다. 제품 동작/전략 수치는 바꾸지 않았다.

로컬 로그는 저장소 밖 `../work/release-0.1.18-gitonly-*.log`, 복사 목록은 `release-0.1.18-gitonly.json`이다. 기존 개별 작업의 실제 Mac/공개 가격 검증은 `Viewer-Price-JSON-2026-11-11.md`에 있다. 릴리즈 산출물 자체 검사는 아래 CI가 별도로 수행한다.

## CI / 공개 자산

Windows는 main의 확정 소스로 수동 배포 workflow를 실행하고 모든 검사·단일 EXE 시작 후 새 태그/Release를 생성한다. Mac은 새 macos-v0.1.18-r1 실행에서 같은 Core/Avalonia 소스임을 대조하고 앱/ZIP/DMG·공개 차트·격리 Keychain·공개 재다운로드 실행을 검사한다. 실패한 단계는 생략하지 않는다. 기존 v0.1.17의6개 자산 ID/해시는 별도 snapshot으로 보존해 마지막에 대조한다.

현재 단계: 소스 게시/Windows CI 시작 전. 최종 커밋·실행 ID·자산 hash·링크는 성공 후 기록한다.

## 고지할 제한

Bin 실시간 가상 주문/중단 후 원장 복원은 미연결로 시작 차단한다. 실계좌 자동 전략 주문은 잠겨 있다. 원본 틱/호가와 당시 확인 자료가 없으면 백테스트/스크리닝은 MissingData이며 일봉을 틱이나 실제 체결로 만들지 않는다. Mac은 ad-hoc 서명이고 Developer ID/공증은 없다. 이번 게시 승인은 실제 주문 활성화나 개인 데이터 삭제 승인이 아니다.
