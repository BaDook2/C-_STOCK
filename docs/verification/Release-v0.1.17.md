# v0.1.17 Windows·Apple Silicon Mac 릴리즈

2026-11-10 사용자가 현재 개발 버전을 Windows와 Mac용으로 GitHub에 게시하도록 요청했다. 이 문서는 준비·실제 CI·공개 파일 확인을 구분한다.

## 준비 완료

- 원격main 시작점 d172d5ac079ef3734dbf1dea50c5e3f60976be76와 로컬 기반 일치, 공개 최신v0.1.16, 새v0.1.17 및 macos-v0.1.17-r1 사용 예정.
- 현재 전략 통합·배치/클릭·봉 단위·MA240/저점선·JSON 오류 처리·Rider .app 흐름을 배포 대상에 포함한다. RELEASE_NOTES_v0.1.17.md에 사용자가 볼 최종 변경과 지원 한계를 정리했다.
- 버전 변경 후 로컬 Release0경고0오류/Core192/Avalonia629/디자인18 성공. 이 중 JSON303. core manifest88개 일치 및 변경파일에 개인 DB/키/자격증명 파일·실제 토큰이 없는지 확인한다.
- HTTPS Git 인증과 SSH publickey 인증이 없고 GitHub 연결의 Git Data API 쓰기도403으로 거부됐다. 기존 Safari 업로드도 아직 소스 게시로 이어지지 않았다. 공식 Homebrew GitHub CLI를 설치해 기기 인증 화면을 준비하고 새 저장소/워크플로 권한에 대한 사용자 승인을 요청했다. 사용자의 계속 진행 지시를 받아 승인을 진행했으며 GitHub 추가 본인 재인증을 기다리고 있다. 현재 게시·태그는 만들지 않았다. Actions의 contents:write 배포 권한 자체는 기존과 같다.
- Windows win-x64 단일 EXE 교차 publish 성공. 실제 Windows 실행은 CI에서 확인한다. 로컬 Mac ARM64의 v0.1.17 앱·ZIP·DMG 실행·공개 차트·서명 무결성 검증 Success=true, 실제 돈 주문0. 이 로컬 산출물은 Git-only CI 최종 배포본과 구분한다.
- 수동 요청의 Windows 태그는 전체 검사/배포 EXE smoke 통과 후에만 생성한다. Mac 태그는 Windows 릴리즈·동일 앱 소스·Mac 검사 확인 뒤 만든다. 원래 태그 push 진입도 유지하며 일반main push로 릴리즈하지 않는다.
- Windows 공개 EXE는 빌드 파일/체크섬/GitHub digest/버전 대조 후 Windows에서 다시 실행한다. Mac 공개DMG/ZIP도 원본/체크섬/GitHub digest 대조 후 실제Mac에서 추출·마운트·실행한다.

## Git-only 사전검사 추가

소스 커밋bdf9d38을 git archive로 새 임시 경로에 추출해 Release0/0/Core192/Avalonia629/디자인18을 다시 확인했다. Windows WPF 호환 Stock.sln도 EnableWindowsTargeting=true로0경고0오류 교차 빌드했다. 이 검사는 Windows의 실제 실행·전체 회귀를 대신하지 않는다. 사용자 계속 진행 지시로 GitHub CLI 기기 권한 승인을 진행했으나 GitHub Confirm access의 추가 패스키/인증 앱 본인 확인에서 대기 중이다. 인증 토큰은 아직 발급되지 않았고 원격 소스·태그·릴리즈 변경은 없다.

## 진행 중

Windows Git-only 전체 검사·게시, Mac ARM64 CI·게시, 최종 공개 다운로드 검증은 아직 완료 전이다. 통과한 CI run과 소스 commit·다운로드 해시를 최종 기록에서 확정한다. 기존 Windows/Mac 자산·태그는 유지한다.

실제 계좌·사용자 DB·키를 읽거나 실제 돈 주문을 실행하지 않는다. D 실계좌 adapter 미지원, Mac Developer ID/공증 없음, 물리 입력·개인 Keychain 승인과 자동 검사를 구분한다.
