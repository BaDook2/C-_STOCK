# v0.1.18 Windows / Apple Silicon 릴리즈 완료

2026-11-11 사용자 명시 요청에 따라 [v0.1.18](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.18)의 Windows EXE·Apple Silicon DMG/ZIP·개별 체크섬 총6개 자산 공개와 재다운로드 검증을 완료했다. 실제 자동주문·개인 DB/실제 기록 삭제는 하지 않았다. 기존 v0.1.17 자산6개의 ID/hash/크기도 보존됐다.

## 확정 소스와 CI

두 태그 `v0.1.18` / `macos-v0.1.18-r1`은 모두 `2c1891c8de46172e7060b47004a38ad712bef211`을 가리킨다. Mac workflow가 Windows와 동일 Core/Avalonia 소스임을 먼저 검사했다. 게시 후 문서 기록 커밋은 앱 소스/태그/기존 자산을 바꾸지 않는다.

| 대상 | 성공 CI | 검사 |
|---|---|---|
| Windows x64 | [42962568324](https://github.com/BaDook2/C-_STOCK/actions/runs/42962568324) | WPF338/Inje363/Bin187/Avalonia859/디자인17·manifest104·설명서JSON6개·단일 EXE·공개 재다운로드 실행 |
| Apple Silicon | [42963164613](https://github.com/BaDook2/C-_STOCK/actions/runs/42963164613) | Inje363/Bin187/Avalonia861/디자인17·manifest104·JSON6개·격리 Keychain·앱/ZIP/DMG/공개 다운로드 실행·공개 차트 |

양 플랫폼 Release 빌드0경고/0오류. Avalonia 검사 수 차이는 OS별 자격증명 검사 차이다. 배포 전에 Git 추적/추가 예정579파일만 새 임시 경로로 복사해 실제 staged 실행 소스와 byte 일치·로컬 파일 비의존을 확인했다. Git-only 빌드0/0·Inje363/Bin187/UI861/디자인17/manifest104/JSON6개도 통과했다. 새 빌드에서 드러난 합성 HTTP fixture 미초기화 경고2개는 명시적 기본값 초기화로 제거했으며 제품 동작/전략 수치는 바꾸지 않았다. 실제 키/DB/바이너리를 소스 커밋에 포함하지 않았다.

## 공개 배포본 검증

- Windows10.0.26100/win-x64에서 배포 EXE와 공개 재다운로드 EXE의 격리 시작 Success/StrategySettingsVerified/NoBrokerCalls/SyntheticOnly true, 601봉/3quote. 버전0.1.18.0과 SHA256을 빌드/체크섬/API digest에 대조했다.
- macOS26.7.2 ARM64에서 원본 번들·ZIP 추출 앱·읽기 전용 DMG 앱을 LaunchServices로 실행했다. 공개 재다운로드 파일에도 같은 검사를 다시 적용했다. 시작601봉/3quote, 실제 공개 차트 삼성전자1500/SK하이닉스1500/애플2513봉과 렌더링 수 일치, 실제 돈 주문0.
- Mac native ARM64 의존성·self-contained 런타임·실행 권한·코드서명 strict·JIT entitlement·개인자료0을 확인했다. 로컬 macOS27.2에서도 공개 ZIP의 버전/서명/격리 시작을 확인했다. 검사 사본은 미색인 .saved-app으로 바꾸고 등록을 해제해 기존 설치 앱1개만 검색된다.
- 공개6개 자산을 로컬에 다시 내려받아 크기/SHA256/체크섬/API digest를 대조했다. Windows·Mac CI가 기록한 공개 파일 hash와도 일치한다. 이전 릴리즈 자산 ID/hash/크기는 변하지 않았다.

| 파일 | bytes | SHA256 |
|---|---:|---|
| Stock.exe | 107884862 | `dbdc9441a21a980343de5f0dfdf408d62537b45eb5bbe607443b304661d79d4c` |
| Stock-AppleSilicon-v0.1.18-r1.dmg | 53366698 | `95739ba764260b83370ac00263a3546eae70011bbf3c07c1296e0aac78667132` |
| Stock-AppleSilicon-v0.1.18-r1.zip | 46939247 | `baea97434f786acfcef453158d0ae16c3f0001ad3333238959b2e6026db18143` |

[Windows 다운로드](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock.exe) · [Mac DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.dmg) · [Mac ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.zip).

## 지원 범위와 증거

Inje/Bin 상위 설정·단일 사용 체크·전체 JSON·독립 엔진·백테스트/일반 스크리닝, 도움말 수정, StockViewer NXT/미국 시간외·데이마켓 공개 가격 조회를 포함한다. [릴리즈 변경 사항](../avalonia/RELEASE_NOTES_v0.1.18.md)과 [한 파일 전략 설명/전체 JSON](../strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)을 따른다.

Bin 실시간 가상 주문/중단 후 원장 복원은 미연결로 시작 차단한다. 실계좌 자동 전략 주문은 잠겨 있다. 원본 틱/호가·당시 자료가 없으면 MissingData이며 일봉을 틱이나 실제 체결로 만들지 않는다. Mac은 ad-hoc 서명이고 Developer ID/공증은 없다. 사용자 하드웨어 물리 입력·Keychain 권한·실제 증권사 API는 자동 검사와 구분한다. 이번 릴리즈가 실제 주문/개인 기록 삭제 승인은 아니다.

로컬 증거는 Git 제외 `artifacts/releases/v0.1.18-verification/final-verification.json`, public-verification.json, windows-run.json/macos-run.json, Windows CI 공개 EXE 결과, Mac CI built/public verification JSON, local-macos-verification.json 및 공개6개 자산이다. 큰 Mac CI 자료에서는 검증 JSON만 골라 추출해 개발 폴더에 .app 사본을 만들지 않았다. 로그는 저장소 밖 `../work/release-0.1.18-*.log`. 기존 설치 개발 앱/개인 DB/키는 릴리즈 작업에서 교체/접근하지 않았다. 이전 작업의 검증은 각 작업 문서와 구분한다.
