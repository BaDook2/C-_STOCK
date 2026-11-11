# v0.1.19 Windows / Apple Silicon 릴리즈 완료

2026-11-11 사용자 요청에 따라 [v0.1.19](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.19)에 Windows EXE·Mac DMG/ZIP·샘플 JSON/설명서 ZIP과 각 체크섬 총8개 자산을 공개하고 재다운로드 검증을 완료했다. 기존v0.1.18의 자산 ID/hash/크기는 그대로다. 개인 DB/키/실제 기록은 배포하지 않았고 실제 자동주문을 활성화하지 않았다.

## 소스와 검사

두 태그 v0.1.19 / macos-v0.1.19-r1은 모두 `c2b52811cb7b45ffc5978d64d2143cbea7fc94cb`다. Mac workflow도 Windows와 동일 Core/Avalonia 소스임을 확인했다. 이후 문서 기록 커밋은 앱 소스·태그를 바꾸지 않는다.

| 대상 | 성공 CI | 검사 |
|---|---|---|
| Windows x64 | [43039584970](https://github.com/BaDook2/C-_STOCK/actions/runs/43039584970) | Windows338/Inje363/Bin187/Avalonia1007·디자인·manifest105·설명서JSON15·EXE/공개 다운로드 실행·샘플 검증 |
| Mac ARM64 | [43041052949](https://github.com/BaDook2/C-_STOCK/actions/runs/43041052949) | Inje363/Bin187/Avalonia1009·디자인·manifest105·JSON15·Keychain·앱/ZIP/DMG·공개 다운로드 실행·공개 차트 |

OS별 자격증명 검사가 UI 수 차이를 만든다. 로컬 Avalonia/WPF 교차 빌드는0경고/0오류, UI1009/Inje363/Bin187/manifest105/JSON15 검사를 통과했다. 로컬 Mac 격리 시작도 Success/StrategySettingsVerified/StrategySamplesVerified/NoBrokerCalls true, 샘플9개/601봉/3quote였다. staged41개 소스·문서에 개인 DB/키/바이너리가 없음을 확인하고 게시했다. CI는 해당 Git 소스에서 다시 빌드/전체 검사/공개 실행을 했다.

macos-15 서버 배정이 지연되어 대기 실행43040473470만 취소했다. 이전 릴리즈에서 성공했던 macos-26 ARM64로 전환한43041052949가 성공했다. 취소된 실행은 자산을 게시하지 않았다.

## 샘플 기본 포함과 보존

Inje4개/Bin5개 전체 프로필 JSON을 공개 합성 검증 fixture에서 만들고 앱 리소스에 포함했다. 최초1회 추가 시 같은 이름의 사용자 값·기존 프로필·현재 선택을 보존하며, 이후 이름 변경 시 다음 시작에서 샘플을 다시 만들지 않는다. 새 설치는 기본 설정1개+샘플9개다. 샘플 모든 파라미터를 기존 기능 검증 fixture와 비교했고, 설치·이름 충돌·재시작·합성 원본 이벤트 미삽입 검사를 추가했다.

공개 샘플 ZIP의9개 JSON을 임베디드 원본과 필드 전체 대조하고, 설명서가 한 파일이며 저장소 정본과 일치함을 확인했다. 합성 시세/거래 기록을 개인 DB에 추가하지 않는다. 실제 시장 수익성 검증으로 해석하지 않는다. 상세 설명/전체JSON은 기존 한 파일 가이드에 모았다.

## 공개 파일

API digest·배포 체크섬·실제 다운로드 SHA256/크기를8개 자산 모두 대조했다. Windows는 공개 EXE 버전0.1.19.0·격리 시작·샘플9개 탑재를 확인했다. Mac CI는 생성 앱/ZIP 추출/읽기 전용 DMG와 공개 재다운로드 앱을 실행하고 공개 차트까지 검사했다. 사용자 Mac의 물리 입력·실제 증권사 API/계좌·실제 돈 주문 검사는 수행하지 않았다.

| 파일 | bytes | SHA256 |
|---|---:|---|
| Stock-AppleSilicon-v0.1.19-r1.dmg | 53190135 | `27aed15da777bfa1eb920880746006c1b2622d174834f36c8ce248730fa7813e` |
| Stock-AppleSilicon-v0.1.19-r1.zip | 46947805 | `46bcc9f91e4a1e765599db790b2b154fa9d5583f685c7a5451b1bf70f9bf834b` |
| Stock-Strategy-Samples-v0.1.19.zip | 75002 | `30e85f0fcd5f051d7d8c8b97b42781ca4c3f9b3f94d9c2f0da8c1fc583a06b17` |
| Stock.exe | 108065086 | `b1a1ffeb8adc6c98c935c8748f3d5bb1f8c22f548f676f2a8d1f16aabf7e6a67` |

Bin 실시간 가상 주문/중단 후 복원은 미연결로 시작 차단, 실계좌 자동 전략 주문은 잠금 유지다. 원본 틱/호가·당시 확인 자료가 없으면 MissingData이며 공개 일봉을 틱으로 만들지 않는다. Mac은 ad-hoc 서명이며 Apple Developer ID/공증은 포함하지 않는다.

Git 제외 증거는 artifacts/releases/v0.1.19-verification/final-verification.json·양 CI 결과/태그·공개8개 자산·이전 릴리즈 대조 자료다. 로그는 저장소 외부 ../work/release-0.1.19-*.log다. 설치 개발 앱/개인 DB/키는 이번 릴리즈에서 교체·접근하지 않았다. 원본 프로필9개를 이전 요청으로 사용자 앱에 저장했던 작업과 이번 배포를 구분한다. 별도 .app 사본을 개발 폴더에 만들지 않았다.

주간 잔여는 시작23%, 단계별22%/21%/20%/19% 확인. 사용자 지시인 잔여15% 보존을 유지하며 완료했다.
