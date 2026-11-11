# STOCK Avalonia v0.1.19

Windows와 Apple Silicon Mac의 현재 UI는 Avalonia다. Inje/Bin 전체 설정과 검증 샘플9개를 기본 포함하고 스크리닝 USD 소수 가격을 보존한다. [릴리즈 변경 사항](docs/avalonia/RELEASE_NOTES_v0.1.19.md) · [두 전략 설명·전체 JSON](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md).

## 다운로드

- [Windows x64 · Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock.exe)
- [Apple Silicon Mac · DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock-AppleSilicon-v0.1.19-r1.dmg) / [ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock-AppleSilicon-v0.1.19-r1.zip)

.NET을 포함하므로 별도 설치가 필요 없다. Mac은 M1 이후·macOS14 이상이며 Intel Mac은 이번 릴리즈 대상이 아니다. 파일별 .sha256은 무결성 확인용이다. Mac은 ad-hoc 서명이며 Developer ID/공증은 없으므로 [설치 안내](docs/avalonia/MACOS_APPLE_SILICON.md)를 따른다.

## 개발과 저장

.NET10 SDK와 Rider에서 `STOCK.Avalonia.sln`을 열고 `Stock.Avalonia`를 실행한다. WPF `Stock.App`/`Stock.sln`은 호환 소스로 보존하며 Core를 UI별로 복제하지 않는다.

```sh
dotnet run --project Stock.Avalonia -c Release
bash avalonia-scripts/verify.sh
```

Windows의 검증 스크립트는 `avalonia-scripts/verify.ps1`이다. Rider 실행 설정 `Mac · .app 빌드 후 실행`은 `/Applications/Stock Development.app`을 갱신하고 연다. [Rider 앱 생성 안내](docs/maintenance/Rider-Mac-App.md).

설정·관심종목·거래 안전 원장·테스트 기록은 OS LocalApplicationData 아래 Stock.Avalonia에 보관한다. Windows 계좌키는 DPAPI CurrentUser, Mac은 로그인 Keychain을 사용한다. 실행 파일에 개인키/DB를 포함하거나 WPF 자료를 자동 복사하지 않는다.

## 전략 지원 범위

Inje/Bin 사용 체크는 하나만 선택하며 두 체계와 비활성 설정도 전체 JSON에 보존한다. 두 전략 백테스트·일반 스크리닝은 원본 체결/호가와 당시 확인 가능한 자료가 필요하다. 공개 일봉/가격 관측을 틱이나 실제 체결로 만들지 않으며 자료가 없으면 MissingData다.

Inje 실시간 가상 세션의 보유 보호/원장 복원은 유지한다. **Bin 실시간 가상 주문/복원은 아직 연결하지 않아 시작을 차단한다. 실계좌 자동 전략 주문도 잠겨 있다.** 기존 수동 주문의 계좌 확인·명시 활성화·전송 안전 절차는 별개다.

[최종 릴리즈 검증](docs/verification/Release-v0.1.19.md): Windows/Mac workflow에서 전체 검사와 배포본·공개 재다운로드 실행/체크섬을 확인한다. 결과는 검증 기록을 따른다. 물리 입력·개인 Keychain 권한·실제 증권사 API·실제 돈 주문은 자동 검사와 구분한다.
