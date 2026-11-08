# Apple Silicon Mac 배포

현재 작업: v0.1.16과 같은 앱 소스를 Apple Silicon(M1 이후)용 self-contained Stock.app으로 빌드한다. macOS 14 이상을 대상으로 하며 별도 .NET 설치는 필요 없다. DMG/ZIP은 Mac에서 생성해 실행 권한과 번들 구조를 보존한다. Intel용은 이번 게시 범위에 포함하지 않는다.

## 설치

릴리즈에서 Stock-AppleSilicon-버전-r번호.dmg를 받아 열고 Stock.app을 Applications로 옮겨 더블클릭한다. ZIP은 Mac에서 압축을 풀어 같은 Stock.app을 실행한다. Windows Stock.exe와 다른 실행 파일이다.

ad-hoc 서명은 무결성 확인용이며 Apple Developer ID 서명·공증을 뜻하지 않는다. 첫 실행이 차단되면 공식 릴리즈의 SHA256을 대조하고 [Apple의 앱별 실행 허용 안내](https://support.apple.com/102445)를 따른다. 전역 보안을 끄거나 격리 속성을 제거하는 명령은 제공하지 않는다.

## 빌드/검사 흐름

- 일반 main push로 배포하지 않는다. 사용자가 Mac 게시를 요청한 경우에만 새 macos-v버전-r번호 태그를 생성한다. 기존 Windows v태그/자산은 덮어쓰지 않는다.
- macos-release.yml은 macos-15 ARM64 호스트 여부, 기존 Windows 릴리즈 존재 및 Stock.Core/Stock.Avalonia 소스 일치를 먼저 검사한다.
- Core/화면/디자인 및 고유 테스트 서비스의 더미 Keychain 저장·복원·갱신·삭제를 검사한다. 사용자 계좌 키는 읽지 않는다.
- 앱호스트·런타임·SQLite/Skia/Avalonia native ARM64 파일과 실행 권한, JIT entitlement, 코드 서명 무결성을 확인한다.
- LaunchServices로 실제 Stock.app을 실행한다. 원본 번들, ZIP 재추출, DMG 읽기 전용 마운트에서 시작하고 실제 공개자료 삼성전자/SK하이닉스/애플 차트를 검사한다.
- 모두 성공한 뒤에만 현재 릴리즈에 Mac DMG/ZIP/체크섬을 추가한다. 실제 돈 주문은 실행하지 않는다.

사용자 Mac의 DPI·트랙패드·권한 승인과 실제 증권사 API는 CI의 ARM64 실행 검사와 구분한다. 계좌·전략·관심종목은 앱 밖의 사용자 저장소에 별도로 저장하고 키는 Keychain을 사용한다. 기존 Windows WPF 자료는 자동 이관하지 않는다.

## 근거

[GitHub ARM64 macOS runner](https://docs.github.com/en/actions/reference/runners/github-hosted-runners), [.NET macOS 지원](https://learn.microsoft.com/en-us/dotnet/core/install/macos), [.NET macOS 서명/JIT](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos), [Avalonia 앱 번들 배포](https://docs.avaloniaui.net/docs/deployment/macos/).

## 현재 결과

준비·검증 진행 중. 실제 게시 자산/해시/CI 결과는 완료 후 기록한다.

r1 CI에서 ARM64 빌드0/0·Core192·Avalonia275(실제 더미 Keychain4개 포함)·디자인18은 성공했다. 패키징 중 평면 Contents/MacOS의 관리 DLL이 서명되지 않아 앱 본체 서명에서 실패했으며 자산을 게시하지 않았다. leaf 파일을 모두 먼저 서명하고 본체를 마지막에 서명하도록 수정했다. 무결성 검증을 완화하지 않았다. 후속 r태그는 기존 태그를 덮어쓰지 않는다.

r2는 서명 무결성과 DMG/ZIP 생성까지 성공했다. 검증 도구의 lipo 인자 순서를 수정했다(입력 파일 뒤에 -verify_arch arm64). 검사 항목은 유지하며 실패 명령의 stderr도 기록한다. 아직 실제 앱 실행·공개 게시를 통과한 것으로 표시하지 않는다.
