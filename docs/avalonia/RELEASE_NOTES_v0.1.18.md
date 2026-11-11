# Stock v0.1.18 · Inje / Bin 전략

전략설정을 **Inje’s Strategy / Bin’s Strategy** 두 체계로 교체하고, 오프라인 가격 조회를 StockViewer 방식으로 개선했습니다. Windows와 Mac은 같은 Avalonia·Core 소스를 사용합니다.

## 다운로드

- [Windows x64 · Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock.exe)
- [Apple Silicon Mac · DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.dmg) / [ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.zip)

각 파일에 .NET 실행 환경을 포함합니다. Mac은 M1 이후·macOS14 이상을 지원합니다. 파일별 `.sha256`은 무결성 확인용입니다. Mac 자산은 Windows 릴리즈 검증 후 순차 추가됩니다.

## 변경 사항

- 기존 전략과 전략 D의 실행·설정 화면을 제거하고 Inje 네 알고리즘과 독립 Bin 세 가족·다섯 매수 패턴으로 교체했습니다.
- 각 전략 이름 옆의 `사용` 체크는 접어도 보이고 한 번에 하나만 선택합니다. Inje의 알고리즘별 Standard/Advanced, Bin의 시장 분류·매수·매도·위험·재진입·주문/체결·시간 단위·통화별 예산·비용표를 편집합니다.
- 설정 저장·불러오기·다운로드는 두 체계와 비활성 값을 보존하는 전체 JSON 프로필을 사용합니다. 단독 Inje/Bin JSON도 받을 수 있습니다. 누락·중복·잘못된 필드/범위/단위와 실행값 불일치는 반영 전에 거절합니다.
- 같은 문장이 두 번 나오던 물음표 도움말을 수정했습니다.
- 오프라인 가격 조회에 StockViewer의 국내 NXT·미국 시간외/데이마켓 가격 선택을 반영했습니다. 1초 목표 조회·장 종료 후 재조회·실패 재시도·Retry-After를 적용하고, 이전 실제 가격과 시각을 보존하며 제공처/세션을 표시합니다. 계좌키 없이 공개 가격만 읽습니다.
- 전략 백테스트·일반 스크리닝은 선택한 체계의 엔진을 사용하며, 시점별 원본 자료/주문·체결 근거를 보존합니다. Bin 스크리닝은 가상 주문도 만들지 않습니다. Inje 가상 세션의 보유 보호·원장 복원은 유지합니다.

[두 전략 상세 설명·파라미터·전체 불러오기 JSON](https://github.com/BaDook2/C-_STOCK/blob/v0.1.18/docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)을 한 파일에 모았습니다. 예시는 코드 기본값이며 개인 설정이나 수익성 검증 결과가 아닙니다. 기존 Inje 설정은 새 프로필에 복사하고, 계좌/관심종목/실제 주문·거래 안전 기록은 보존합니다. 구전략 JSON을 다른 알고리즘으로 자동 변환하지 않습니다.

## 지원 범위

- **Bin 실시간 가상 주문과 중단 후 원장 복원은 아직 연결하지 않아 시작을 차단합니다.** Bin은 설정·백테스트·일반 스크리닝에서 사용합니다.
- **실계좌 자동 전략 주문은 잠겨 있습니다.** 이번 릴리즈가 실제 주문을 활성화하지 않습니다.
- Inje/Bin 판단에는 원본 틱·호가와 당시 확인 가능한 시장/위험/가격 규칙 등이 필요합니다. 공개 일봉만 있으면 MissingData로 보류하며 틱이나 실제 체결로 합성하지 않습니다. 공개 가격의 1초 조회는 새 거래가 매초 보장된다는 뜻이 아닙니다.
- Mac은 ad-hoc 서명 무결성을 검사하며 Apple Developer ID·공증은 포함하지 않습니다. [Mac 설치 안내](https://github.com/BaDook2/C-_STOCK/blob/v0.1.18/docs/avalonia/MACOS_APPLE_SILICON.md)를 확인하세요.

배포 CI는 전체 Core/Inje·Bin·Avalonia UI·디자인·JSON 검사를 실행하고, 생성한 배포본의 격리 시작 검사가 통과해야 공개합니다. 공개 파일을 다시 내려받아 해시와 실행 결과를 확인합니다. 실제 계좌·API키·개인 DB는 배포 파일에 포함하지 않습니다.
