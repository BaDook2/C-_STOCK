# Apple Silicon Mac 배포

현재 배포: v0.1.18 Windows와 같은 앱 소스를 Apple Silicon(M1 이후)용 self-contained Stock.app으로 빌드했다. macOS 14 이상을 대상으로 하며 별도 .NET 설치는 필요 없다. DMG/ZIP은 Mac에서 생성해 실행 권한과 번들 구조를 보존한다. Intel용은 이번 게시 범위에 포함하지 않는다.

## 설치

릴리즈에서 Stock-AppleSilicon-버전-r번호.dmg를 받아 열고 Stock.app을 Applications로 옮겨 더블클릭한다. ZIP은 Mac에서 압축을 풀어 같은 Stock.app을 실행한다. Windows Stock.exe와 다른 실행 파일이다.

ad-hoc 서명은 무결성 확인용이며 Apple Developer ID 서명·공증을 뜻하지 않는다. 첫 실행이 차단되면 공식 릴리즈의 SHA256을 대조하고 [Apple의 앱별 실행 허용 안내](https://support.apple.com/102445)를 따른다. 전역 보안을 끄거나 격리 속성을 제거하는 명령은 제공하지 않는다.

## 빌드/검사 흐름

- 일반 main push로 배포하지 않는다. 사용자가 Mac 게시를 요청한 경우에만 새 macos-v버전-r번호 태그를 생성한다. 기존 Windows v태그/자산은 덮어쓰지 않는다.
- macos-release.yml은 선택한 macOS ARM64 호스트 여부(기본macos-15, 수동macos-26 지원), 기존 Windows 릴리즈 존재 및 Stock.Core/Stock.Avalonia 소스 일치를 먼저 검사한다.
- Core/화면/디자인 및 고유 테스트 서비스의 더미 Keychain 저장·복원·갱신·삭제를 검사한다. 사용자 계좌 키는 읽지 않는다.
- 앱호스트·런타임·SQLite/Skia/Avalonia native ARM64 파일과 실행 권한, JIT entitlement, 코드 서명 무결성을 확인한다.
- LaunchServices로 실제 Stock.app을 실행한다. 원본 번들, ZIP 재추출, DMG 읽기 전용 마운트에서 시작하고 실제 공개자료 삼성전자/SK하이닉스/애플 차트를 검사한다.
- 모두 성공한 뒤에만 현재 릴리즈에 Mac DMG/ZIP/체크섬을 추가한다. 실제 돈 주문은 실행하지 않는다.

사용자 Mac의 DPI·트랙패드·권한 승인과 실제 증권사 API는 CI의 ARM64 실행 검사와 구분한다. 계좌·전략·관심종목은 앱 밖의 사용자 저장소에 별도로 저장하고 키는 Keychain을 사용한다. 기존 Windows WPF 자료는 자동 이관하지 않는다.

## 근거

[GitHub ARM64 macOS runner](https://docs.github.com/en/actions/reference/runners/github-hosted-runners), [.NET macOS 지원](https://learn.microsoft.com/en-us/dotnet/core/install/macos), [.NET macOS 서명/JIT](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos), [Avalonia 앱 번들 배포](https://docs.avaloniaui.net/docs/deployment/macos/).

## 현재 v0.1.18 결과

[DMG 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.dmg) · [ZIP 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.zip) · [Mac CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42963164613) · [전체 검증](../verification/Release-v0.1.18.md).

- macos-v0.1.18-r1과 Windows v0.1.18은 소스2c1891c8de46172e7060b47004a38ad712bef211을 사용한다.
- macOS26.7.2 ARM64 CI에서 Inje363/Bin187/UI861/디자인17·격리 Keychain·앱/ZIP/DMG·공개 재다운로드 실행을 통과했다. 실제 공개 차트1500/1500/2513봉 렌더 확인, 실제 돈 주문0.
- 공개 DMG53366698bytes / SHA256 `95739ba764260b83370ac00263a3546eae70011bbf3c07c1296e0aac78667132`.
- 공개 ZIP46939247bytes / SHA256 `baea97434f786acfcef453158d0ae16c3f0001ad3333238959b2e6026db18143`.
- 사용자 Mac macOS27.2에서도 공개 ZIP의 서명·버전0.1.18·격리 시작을 확인했다. 개인 DB/키를 사용하지 않았으며 검사 앱은 미색인 .saved-app으로 보관했다.

## 이전 v0.1.17 결과

[DMG 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.dmg) · [ZIP 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.zip) · [성공한 Mac CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42848351841) · [전체 릴리즈 검증](../verification/Release-v0.1.17.md).

- macos-v0.1.17-r1 / 빌드17a297b67ac5cfca0de2d07a19237fb8221e01bf. Windows v0.1.17(d067be2)과 앱 소스 동일.
- macOS26.7.2 ARM64, .NET10.0.14. 빌드0/0, Core192/UI633/디자인18 및 더미 Keychain 통과.
- 원본·ZIP·DMG와 공개 재다운로드 앱 모두 네이티브 시작601봉/2quote 성공. 공개 앱 삼성전자1500/SK하이닉스1500/애플2513봉 차트 성공. 실제 돈 주문0.
- 공개 파일·체크섬·GitHub digest·빌드 SHA256 일치. 기존 Windows/이전 릴리즈 자산 유지. ad-hoc 서명 무결성 검증이며 Developer ID/공증은 없음.
- DMG53,145,790bytes / SHA256 `18d34f3cc2da104e2928e19e298a747bce03219c552a243a449bad120f45984d`.
- ZIP46,839,840bytes / SHA256 `087f8e70d152ed503d8d05e71aba8629c3afc5294b403687615a2d4e87eff32c`.

## 이전 v0.1.16 결과

[DMG 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.16/Stock-AppleSilicon-v0.1.16-r3.dmg) · [ZIP 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.16/Stock-AppleSilicon-v0.1.16-r3.zip) · [성공한 Mac CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42487371788).

- 빌드 태그 macos-v0.1.16-r3 / 패키징 commit a2ba9d8c8991a0d60ab6bce06ddf8b4bacd87478 / 앱 코드 db1ee44e3083118b05982f4574446a8ba8f7a787(기존 v0.1.16과 동일).
- macOS15.8.2 ARM64, .NET10.0.14. 빌드0경고0오류, Core192/UI275/디자인18 및 실제 더미 Keychain4개 성공.
- ARM64 native 파일19개·실행 권한·JIT entitlement·서명 무결성·self-contained 확인, 앱 내부 개인자료/테스트 파일0개.
- LaunchServices의 원본 Stock.app, ZIP 재추출, DMG 읽기전용 마운트에서 각각 시작 성공: 601봉·2quote·NoBrokerCalls.
- 실제 공개자료 삼성전자1500/SK하이닉스1500/애플2514봉, 데이터와 렌더링 봉 수 일치. 이미지에서 한글/캔들/가격을 확인했다. RenderTargetBitmap은 Mac 사용자 실화면·물리 입력 검증으로 과장하지 않는다.
- 공개 DMG/ZIP/체크섬 재다운로드가 Mac CI 산출물 및 GitHub asset digest와 정확히 일치했다. Windows Stock.exe/체크섬의 자산 ID·해시는 유지했다. 실제 돈 주문0.

| 파일 | bytes | SHA256 |
|---|---:|---|
| Stock-AppleSilicon-v0.1.16-r3.dmg | 53,161,455 | 3d06c06bd8166dd0ef8e2c7b47299215eab08a0df8fe8ddbd320d05a90e0cbbe |
| Stock-AppleSilicon-v0.1.16-r3.zip | 46,914,659 | 99a40a6265290a7ab788c5ac0e20b2336916fa3403b32cfb9f3be7d98e055ed3 |

로컬 증거: STOCK/artifacts/macos-r3-ci.log, artifacts/macos-r3-verification/stock-macos-release/audit/verification.json와 PNG, artifacts/releases/v0.1.16-macos-r3-public/download-verification.json. 설치 파일과 검수 자료는 Git 제외다. 계좌 키/원본 사용자 DB를 읽거나 복제하지 않았다. 사용자 Mac의 DPI·트랙패드·승인/잠금·실제 증권사와 정식 Apple 서명/공증은 별도다.

### 이전 시도

r1 CI에서 ARM64 빌드0/0·Core192·Avalonia275(실제 더미 Keychain4개 포함)·디자인18은 성공했다. 패키징 중 평면 Contents/MacOS의 관리 DLL이 서명되지 않아 앱 본체 서명에서 실패했으며 자산을 게시하지 않았다. leaf 파일을 모두 먼저 서명하고 본체를 마지막에 서명하도록 수정했다. 무결성 검증을 완화하지 않았다. 후속 r태그는 기존 태그를 덮어쓰지 않는다.

r2는 서명 무결성과 DMG/ZIP 생성까지 성공했다. 검증 도구의 lipo 인자 순서를 수정했다(입력 파일 뒤에 -verify_arch arm64). 검사 항목은 유지하며 실패 명령의 stderr도 기록한다. 아직 실제 앱 실행·공개 게시를 통과한 것으로 표시하지 않는다.
