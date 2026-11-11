# 현재 — v0.1.19 릴리즈 준비

최신 사용자 요청이 새 Windows/Mac 릴리즈를 승인했다. main은3de0fe4, 기존 최신v0.1.18. 기존 미커밋 샘플/표시 수정에 더해9개 공개 JSON을 Stock.Core/Strategies/Samples 임베디드 리소스로 포함하고 StrategySampleCatalog.EnsureInstalled를 실제 앱 시작에 연결했다. 기본 설정을 유지하고 이름 충돌 시 사용자 값 우선, 선택 보존,1회 설치 marker로 이름 변경 후 재추가 방지. 개인 DB에서 추출하지 않았으며 artifacts의 검증 fixture와 파라미터 일치 검사를 추가했다.

앱 버전0.1.19. Windows workflow는 추가 샘플 ZIP+체크섬을 게시하고 공개 재다운로드 해시/기본탑재 시작 결과를 검사한다. Mac은 기존 macos-release.yml을 Windows 완료 후 같은 소스에서 build_tag=macos-v0.1.19-r1로 실행한다. docs/avalonia/RELEASE_NOTES_v0.1.19.md 준비. 작업 중에는 성공/게시를 확정하지 않는다.

다음은 로컬/격리 Git 대상 빌드·전체 검사·manifest/설명서 JSON 검증 후 소스 commit/push, Windows 수동 workflow, 완료 시 Mac workflow, 최종 자산 다운로드·해시 확인이다. 시작 잔여23%, 사용자 잔여15% 보존 유지. 기존 설치 앱·개인DB/키/실제기록은 접근·교체하지 않는다.
