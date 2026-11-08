# STOCK Avalonia

Windows 단일 실행 파일은 GitHub의 **Releases → Assets → Stock.exe**에서 받는다. 설치된 .NET은 필요 없다. `Stock.exe.sha256`은 다운로드 무결성 확인용이며 프로그램 실행에 필요한 파일은 아니다.

Apple Silicon Mac(M1 이후, macOS14+)은 [DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.16/Stock-AppleSilicon-v0.1.16-r3.dmg) 또는 [ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.16/Stock-AppleSilicon-v0.1.16-r3.zip)을 받는다. Stock.app을 응용 프로그램으로 옮겨 실행하며 .NET 별도 설치는 필요 없다. Mac ARM64 CI에서 시작/공개 차트/더미 Keychain/ZIP·DMG를 검사했다. Apple Developer ID·공증은 없으므로 [설치 안내](docs/avalonia/MACOS_APPLE_SILICON.md)를 확인한다.

원본 WPF 소스는 `Stock.App`/`Stock.sln`에 유지한다. 새 Windows/Mac UI는 `Stock.Avalonia`이며 `STOCK.Avalonia.sln`을 열어 개발한다. `Stock.Core`의 전략·백테스트·주문 엔진을 공유한다. 시작 프로젝트는 `Stock.Avalonia`이고 Tests는 테스트용 콘솔 프로젝트다.

```powershell
dotnet run --project Stock.Avalonia
./avalonia-scripts/verify.ps1
./avalonia-scripts/publish-windows.ps1
```

Mac 개발: .NET10 SDK를 설치하고 `dotnet run --project Stock.Avalonia`. 로컬 앱 번들은 `bash avalonia-scripts/publish-macos.sh osx-arm64` 또는 `osx-x64`로 만든다. 두 RID의 교차 빌드는 확인했으며 실제 Mac 실행·트랙패드·Keychain 권한·Developer ID 서명/공증은 별도 검증이 필요하다. Windows EXE를 Mac에서 실행하는 방식이 아니다.

사용자 자료는 실행 파일 옆에 넣지 않는다. OS의 LocalApplicationData 아래 `Stock.Avalonia`에서 설정/관심종목/거래 원장/테스트 기록을 보관한다. Windows 키는 DPAPI CurrentUser, Mac 키는 로그인 Keychain에 저장한다. 원본 WPF 계좌키/DB를 자동 복사하지 않으므로 새 앱에서 계좌를 연결해야 한다. 실제 계좌 주문은 기본 잠김이며 활성화·수동 주문·실제 전략 시작 시 각각 명시 확인이 필요하다.

오프라인 가상 실행은 공개시세 일봉 모형이다. 종가 신호는 다음 거래일의 신선한 관측 이후 주문하며 원본 틱 또는 공식 시가 체결로 해석하지 않는다. 저장된 가상 실행은 정지 상태로 복원되고 보호 감시 재개도 신규/추가매수 OFF다. 실계좌 전략 실행은 기존 체결 대사가 가능한 키움 국내 계좌와 검증된 시점별 종목/위험/시장 세션/거래대금 자료가 필요하다. 다른 지원 브로커의 수동 주문과 구분한다. 실제 주문 테스트는 자동 검사에서 하지 않는다.

구현·API 제한·파일 지도: [이식 완료 설명](docs/avalonia/COMPLETION_E1_E4_20261108.md). 검증/미검증 정본: [작업 상태](docs/avalonia/TASKS.md), [인수인계](docs/avalonia/HANDOFF.md). CI는 WPF 회귀, 공통/새 UI 검사, Release 빌드와 격리 EXE 시작 검사를 모두 통과한 뒤에만 새 태그의 Windows Release를 만든다.
