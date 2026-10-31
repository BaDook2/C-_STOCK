# GitHub Release로 단일 `Stock.exe` 배포하기

## 사용자가 프로그램을 실행하는 방법

1. 저장소의 [최신 Release](https://github.com/BaDook2/C-_STOCK/releases/latest)를 엽니다.
2. Assets에서 `Stock.exe`를 내려받습니다.
3. `Stock.exe`를 더블클릭합니다.

이 파일은 Windows x64용 .NET 런타임과 앱·Core 의존성을 모두 포함한 단일 실행 파일입니다. Visual Studio, .NET SDK, 소스 코드가 없어도 실행됩니다. 증권사 API 키는 EXE에 포함되지 않으며 앱의 계좌 키 설정에서 해당 PC에만 입력·보관합니다.

`Stock.exe.sha256`도 함께 제공됩니다. PowerShell에서 다음 명령으로 다운로드 파일의 SHA256을 확인할 수 있습니다.

```powershell
Get-FileHash .\Stock.exe -Algorithm SHA256
```

서명되지 않은 개인 배포 앱이므로 Windows SmartScreen 안내가 나타날 수 있습니다. GitHub Release의 체크섬과 비교한 뒤 실행하세요. 널리 배포할 때는 코드 서명을 추가하는 것이 좋습니다.

## 새 버전을 배포하는 방법

코드를 `main` 브랜치에 커밋하고 GitHub에 올린 다음, 새 버전 태그를 push합니다.

```powershell
git tag v0.1.0
git push origin main
git push origin v0.1.0
```

태그 이름은 `v`로 시작해야 합니다. `.github/workflows/release.yml`이 자동으로 다음 작업을 수행합니다.

1. .NET 10 SDK로 솔루션 복원 및 Release 빌드
2. 전체 오프라인 테스트 실행
3. Windows x64 독립 실행형 단일 `Stock.exe` 게시
4. 게시 EXE의 오프라인 smoke test와 브로커 미호출 검증
5. SHA256 파일 생성
6. 해당 태그의 GitHub Release 생성 후 두 파일 첨부

실패하면 Release를 정상 산출하지 않으므로 GitHub Actions 로그에서 실패한 빌드·테스트를 먼저 고칩니다. 같은 태그의 워크플로를 다시 실행하면 기존 Release 파일을 교체할 수 있습니다.

## Git에 포함하지 않는 파일

- 루트와 로컬 publish 폴더의 `Stock.exe`
- 로컬 계좌 키와 비밀 설정
- SQLite DB 및 WAL/SHM 파일
- 로그, 테스트 결과, 빌드 산출물

이 파일들은 `.gitignore`로 제외됩니다. 따라서 저장소 clone은 개발용 소스이고, 일반 사용자는 소스가 아니라 Releases의 `Stock.exe`를 받는 구조입니다.
