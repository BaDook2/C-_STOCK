# Stock v0.1.19 · Inje / Bin 샘플 전략

검증한 Inje4개와 Bin5개 매수 패턴을 각각 저장된 샘플 전략으로 포함했습니다. 앱을 처음 실행하거나 업데이트하면 전략 설정 상단 목록에 한 번 추가하며, 기존 설정·동일 이름의 사용자 값·현재 선택을 보존합니다. 이름을 바꾼 샘플을 다음 실행에서 다시 만들지 않습니다.

- Inje: 급등주 눌림목, 호가 스프레드 틱떼기, 폭락주 초단기 반등, 저유동성 지지구간.
- Bin: 하락 반등, 상승 고점 돌파, 상승 눌림목 반등, 박스 하단 반등, 박스 상단 돌파.
- 각 샘플은 두 전략 체계의 전체 JSON을 보관하고, 실행할 체계/패턴을 구분합니다. 전략 설정에서 선택한 뒤 백테스트·일반 스크리닝에 사용할 수 있습니다.
- 스크리닝에서 USD 소수 가격을 정수로 버리던 표시와 비율 열 제목을 수정했습니다. Inje 결과에 합성 검증 자료라는 출처 표시를 보존합니다.
- 9개 샘플의 매수/매도·스크리닝·중복 이벤트·자료 누락·미래 참조 방지 검사와 기본 탑재/기존 설정 보존/재시작 검사를 릴리즈 CI에 포함했습니다.

## 다운로드

- [Windows x64 · Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock.exe)
- [Apple Silicon Mac · DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock-AppleSilicon-v0.1.19-r1.dmg) / [ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock-AppleSilicon-v0.1.19-r1.zip)
- [샘플 JSON9개와 한 파일 상세 설명서](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.19/Stock-Strategy-Samples-v0.1.19.zip). 앱에 기본 포함되므로 별도 가져오기는 선택입니다. 각 배포 파일에 SHA256 체크섬을 제공합니다.

[전체 전략 설명과 JSON](https://github.com/BaDook2/C-_STOCK/blob/v0.1.19/docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)을 한 파일에서 볼 수 있습니다. 샘플은 합성 자료로 기능을 검사한 파라미터이며 실제 시장 수익성 검증 결과가 아닙니다. 시세 데이터나 합성 거래 기록을 개인 DB에 추가하지 않으며, 실제 백테스트/스크리닝에는 원본 체결·호가 및 당시 확인 자료가 필요합니다. 공개 일봉만 있으면 자료 부족으로 보류합니다.

Bin 실시간 가상 주문/재시작 복원은 연결 전까지 시작을 차단하며 실계좌 자동 전략 주문은 잠겨 있습니다. Mac은 M1 이후·macOS14 이상, ad-hoc 서명이며 Apple 공증은 포함하지 않습니다. 개인 DB·계좌키·실제 거래 기록은 배포하지 않습니다.
