# v0.1.17 Windows·Apple Silicon Mac 릴리즈

2026-11-10 사용자가 현재 개발 버전을 Windows와 Mac용으로 GitHub에 게시하도록 요청했다. 이 문서는 준비·실제 CI·공개 파일 확인을 구분한다.

## 준비 완료

- 원격main 시작점 d172d5ac079ef3734dbf1dea50c5e3f60976be76와 로컬 기반 일치, 공개 최신v0.1.16, 새v0.1.17 및 macos-v0.1.17-r1 사용 예정.
- 현재 전략 통합·배치/클릭·봉 단위·MA240/저점선·JSON 오류 처리·Rider .app 흐름을 배포 대상에 포함한다. RELEASE_NOTES_v0.1.17.md에 사용자가 볼 최종 변경과 지원 한계를 정리했다.
- 버전 변경 후 로컬 Release0경고0오류/Core192/Avalonia629/디자인18 성공. 이 중 JSON303. core manifest88개 일치 및 변경파일에 개인 DB/키/자격증명 파일·실제 토큰이 없는지 확인한다.
- HTTPS Git 인증과 SSH publickey 인증이 없고 GitHub 연결의 Git Data API 쓰기도403으로 거부됐다. 기존 Safari 업로드도 아직 소스 게시로 이어지지 않았다. 공식 Homebrew GitHub CLI를 설치해 기기 인증 화면을 준비하고 새 저장소/워크플로 권한에 대한 사용자 승인을 요청했다. 사용자 본인 재인증 완료 후 BaDook2 CLI 로그인(keyring) 및 Git push dry-run을 확인했다. 현재 릴리즈·태그는 생성 전이며 CI를 진행한다. Actions의 contents:write 배포 권한 자체는 기존과 같다.
- Windows win-x64 단일 EXE 교차 publish 성공. 실제 Windows 실행은 CI에서 확인한다. 로컬 Mac ARM64의 v0.1.17 앱·ZIP·DMG 실행·공개 차트·서명 무결성 검증 Success=true, 실제 돈 주문0. 이 로컬 산출물은 Git-only CI 최종 배포본과 구분한다.
- 수동 요청의 Windows 태그는 전체 검사/배포 EXE smoke 통과 후에만 생성한다. Mac 태그는 Windows 릴리즈·동일 앱 소스·Mac 검사 확인 뒤 만든다. 원래 태그 push 진입도 유지하며 일반main push로 릴리즈하지 않는다.
- Windows 공개 EXE는 빌드 파일/체크섬/GitHub digest/버전 대조 후 Windows에서 다시 실행한다. Mac 공개DMG/ZIP도 원본/체크섬/GitHub digest 대조 후 실제Mac에서 추출·마운트·실행한다.

## Git-only 사전검사 추가

소스 커밋bdf9d38을 git archive로 새 임시 경로에 추출해 Release0/0/Core192/Avalonia629/디자인18을 다시 확인했다. Windows WPF 호환 Stock.sln도 EnableWindowsTargeting=true로0경고0오류 교차 빌드했다. 이 검사는 Windows의 실제 실행·전체 회귀를 대신하지 않는다. 이후 사용자가 GitHub 본인 재인증을 완료해 CLI의 보안 저장소 로그인이 정상 확인됐다. Git-only 검증된 앱 소스는 변경하지 않고 소스 업로드·CI·게시를 계속한다.

## 진행 중

Windows Git-only 전체 검사·게시, Mac ARM64 CI·게시, 최종 공개 다운로드 검증은 아직 완료 전이다. 통과한 CI run과 소스 commit·다운로드 해시를 최종 기록에서 확정한다. 기존 Windows/Mac 자산·태그는 유지한다.

실제 계좌·사용자 DB·키를 읽거나 실제 돈 주문을 실행하지 않는다. D 실계좌 adapter 미지원, Mac Developer ID/공증 없음, 물리 입력·개인 Keychain 승인과 자동 검사를 구분한다.

## 첫 Windows CI 실패와 수정

CI42838779384는 복원·Release 빌드 및 대부분 기존 회귀 후 ProgressiveScreeningTests의 완료 기록 포함 검사에서 실패했다. 로그시각2026-11-10T15:11Z는 한국11월11일00:11이다. 기록은KST 날짜로 필터하지만 기본 조회To가 호스트DateTime.Today(UTC11월10일)여서 새 기록이 범위 밖이었다. WPF·Avalonia 기록 기본From/To를 기존KoreaClock.Today로 통일했다. WPF 검사도 완료상태·조회5회·기록 포함으로 나눠 실패 이유를 각각 표시하고 기본KST범위를 확인한다. 조건을 생략하거나 테스트 호스트 시간대를 바꿔 우회하지 않았다.

새 Avalonia 검사4개는 UTC 환경에서 기본 기간, 한국 자정의 포함·이전/다음날 제외, 표시 날짜, 편집기 재생성 후 저장 기록을 확인한다. UTC 전용4개·로컬 Core192/Avalonia633/디자인18과 양 솔루션0/0 성공. Windows에는 --progressive-screening 전용 CLI 검사 단계를 전체 회귀 앞에 추가했다. 전체 검사는 그대로 수행한다. 첫 실패에서 태그·릴리즈는 생성되지 않았다.

## Windows 게시 완료·Mac 실행기 대기 전환

Windows42842992293가 모든 검사를 통과했다: 기록선행11/WPF1655/D13/Core192/Avalonia631/디자인18. 배포 EXE와 공개 EXE를 각각 실행했고 공개 파일·빌드·체크섬·GitHub digest가 일치한다. Stock.exe107543358bytes, SHA25643a5673c21f78c1ba82381a0d424daa3bea133cd15c3c211ee403bf2c053a1b2. Win 태그/앱 소스 d067be25553d502a3a1c16316164115bbd8e7cbc.

Mac42846537829는 macos-15 실행기가 할당되지 않은 queued 상태가 지속되어 취소했다. 공식 GitHub 실행기 표의 macos-26 ARM64를 선택할 수 있게 수동 입력을 추가해 동일 앱 소스로 재시도한다. 기존 태그 push의 macos-15 기본값과 모든 검사·서명·공개 다운로드 확인은 유지한다. 자료: https://docs.github.com/en/actions/reference/runners/github-hosted-runners. 앱 코드 변경이 아니므로 Windows 전체 검사를 반복하지 않는다. Mac 게시/검증은 아직 완료 전이다.
