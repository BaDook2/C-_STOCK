# v0.1.17 Windows·Apple Silicon Mac 릴리즈 완료

한국시간2026-11-11에 [v0.1.17](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.17)의 Windows EXE와 Apple Silicon DMG/ZIP 및 개별 체크섬 총6개 자산 공개·재다운로드 검증을 완료했다. 11월10일 사용자의 명시 릴리즈 요청을 이행한 결과다.

## 확정 소스와 CI

| 대상 | 태그·소스 커밋 | 성공 CI | 검사 |
|---|---|---|---|
| Windows x64 | v0.1.17 / d067be25553d502a3a1c16316164115bbd8e7cbc | [42842992293](https://github.com/BaDook2/C-_STOCK/actions/runs/42842992293) | WPF1655/D13/기록선행11/Core192/Avalonia631/디자인18, 배포 EXE 시작 |
| Apple Silicon | macos-v0.1.17-r1 / 17a297b67ac5cfca0de2d07a19237fb8221e01bf | [42848351841](https://github.com/BaDook2/C-_STOCK/actions/runs/42848351841) | Core192/Avalonia633/디자인18, 더미 Keychain, 앱/ZIP/DMG·공개 차트 |

두 커밋의 Stock.Core/Stock.Avalonia 소스는 동일하다. Mac 커밋 차이는 macos-26 ARM64 실행기 선택과 진행 문서다. 버전0.1.17이며 Git-only 사전검사 후 CI가 원격 소스를 새로 checkout했다. Core manifest88개를 확인했다. 각 CI의 Release 빌드0경고0오류. JSON 관련303개는 Avalonia 전체에 포함되며 플랫폼별 전체 개수 차이가 있다.

## 공개 배포본 검증

- Windows 공개 EXE·체크섬을 다시 받아 빌드 해시·GitHub digest·FileVersion0.1.17.0과 대조했다. Windows10.0.26100의 격리된 실제 EXE 시작: Success/NoBrokerCalls/SyntheticOnly true, 601봉/2quote.
- Mac 공개 DMG/ZIP/체크섬을 다시 받아 빌드·API digest와 대조했다. macOS26.7.2 ARM64에서 공개 ZIP 추출 앱, 재추출 ZIP, 읽기 전용 DMG 마운트 앱 모두 LaunchServices 시작 성공: 601봉/2quote, NoBrokerCalls/SyntheticOnly true.
- Mac 번들의 ARM64 native19개·실행 권한·서명 무결성·self-contained 확인. 번들 개인자료0개. 공개 앱 실제 공개자료 차트 삼성전자1500/SK하이닉스1500/애플2513봉의 데이터·렌더링 개수 일치, 성공true. 이 검사는 개인 계좌 API나 물리 마우스/트랙패드 검사와 구분한다.
- 로컬에서도 공개6개 자산의 크기·SHA256 및3개 체크섬을 최종 대조했다. 기존 v0.1.16의6개 자산과 Mac 추가 전 v0.1.17의 Windows2개 자산 ID/해시는 그대로다. 기존 태그·자산 덮어쓰기 없음.

| 파일 | bytes | SHA256 |
|---|---:|---|
| Stock-AppleSilicon-v0.1.17-r1.dmg | 53,145,790 | `18d34f3cc2da104e2928e19e298a747bce03219c552a243a449bad120f45984d` |
| Stock-AppleSilicon-v0.1.17-r1.zip | 46,839,840 | `087f8e70d152ed503d8d05e71aba8629c3afc5294b403687615a2d4e87eff32c` |
| Stock.exe | 107,543,358 | `43a5673c21f78c1ba82381a0d424daa3bea133cd15c3c211ee403bf2c053a1b2` |

[Windows 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock.exe) · [Mac DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.dmg) · [Mac ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.17/Stock-AppleSilicon-v0.1.17-r1.zip).

## 지연 원인과 해결

첫 Windows CI42838779384는 UTC15:11(KST 다음날00:11)에 새 기록이 기본 날짜 범위에서 누락되어 실패했다. 기록 필터는KST인데 기본From/To는 호스트DateTime.Today였던 기존 문제다. WPF/Avalonia를KoreaClock.Today로 통일하고 UTC 경계4개 및 Windows 기록 선행11개를 추가했다. 관련·전체 회귀를 다시 통과했으며 첫 실패 때는 태그/릴리즈를 만들지 않았다. 전략 계산/JSON 기본값은 바꾸지 않았다.

Mac42846537829는 macos-15 실행기가 할당되지 않아 대기 후 취소했다. macos-26 ARM64 선택지를 추가한42848351841은 모든 검사를 유지하고4분5초에 성공했다. Windows 성공 실행은19분6초였다. 검사를 생략하거나 실패를 성공으로 처리하지 않았다.

## 증거·지원 범위

로컬 증거(큰 바이너리/검수 자료는 Git 제외): `artifacts/releases/v0.1.17-verification/final-verification.json`, `windows-ci/public-windows-release/verification.json`, `macos-ci/public-macos-release/audit/verification.json`, API snapshot·공개파일·CI 로그. GitHub CI artifact는 Windows-42842992293-verification / AppleSilicon-main-verification이다. [준비 당시 기록](../history/Release-v0.1.17-preparation-20261111.md)은 최종 결과와 구분한다.

실제 돈 주문0, 사용자 DB/키/계좌·기존 설치 개발 앱은 변경하지 않았다. Mac은ad-hoc 서명이며 Developer ID/Apple 공증은 없다. 사용자 Keychain 권한/잠금·물리 입력·실제 증권사 API는 별도이며 D 실계좌 자동 주문은 미지원이다. [설치 안내](../avalonia/MACOS_APPLE_SILICON.md), [변경 사항](../avalonia/RELEASE_NOTES_v0.1.17.md), [전략 설명·전체 JSON](../strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md).
