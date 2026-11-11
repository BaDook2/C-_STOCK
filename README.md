# Stock v0.1.18 — Inje / Bin 전략

현재 릴리즈 대상은 두 전략 설정·전체 JSON 프로필·도움말 중복 수정·StockViewer 방식의 공개 가격 조회를 포함한다. [변경 사항과 지원 범위](docs/avalonia/RELEASE_NOTES_v0.1.18.md) · [두 전략 설명/전체 JSON](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md).

[Windows EXE](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock.exe) · [Apple Silicon DMG](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.dmg) · [Mac ZIP](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.18/Stock-AppleSilicon-v0.1.18-r1.zip). 자산은 플랫폼별 검증 후 순차 게시한다. 실계좌 자동 전략 주문은 잠겨 있고 Bin 실시간 가상 주문/원장 복원은 미연결로 시작 차단한다.

전략설정의 상위 그룹은 **Inje’s Strategy / Bin’s Strategy**다. 각 이름 옆 `사용`은 한 번에 하나만 선택한다. Inje 네 알고리즘과 Bin 다섯 패턴의 파라미터·시간 단위·전체 설정 JSON을 편집한다. 백테스트/일반 스크리닝은 선택한 독립 엔진을 사용하며, 실제 계좌/주문 안전 기록을 임의 삭제하지 않는다.

**[현재 전략 상세 설명 및 전체 JSON](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)** 한 파일에 두 체계의 설명·파라미터·Inje 예시·통합 프로필 JSON을 모았다. [Bin Core 구현·22개 결정 추적표](docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md)는 개발 근거다.

## 실행과 개발

.NET 10과 Rider에서 `STOCK.Avalonia.sln`을 열고 `Stock.Avalonia`를 실행한다. macOS와 현재 Windows 배포 UI는 Avalonia이며 `Stock.sln` / `Stock.App`은 Windows WPF 호환 소스다. Core를 UI별로 복제하지 않는다.

```sh
dotnet run --project Stock.Avalonia -c Release
# 로컬 Mac 앱을 동기화 폴더 밖의 임시 산출물로 생성: 설치/업로드하지 않음
bash avalonia-scripts/publish-macos.sh osx-arm64 /private/tmp/stock-local-$(date +%Y%m%d-%H%M%S)
```

기존 탭·창·버튼 배치를 유지했다. 기존 전략 선택, 저장, 복구, 불러오기, 다운로드를 사용하고 Standard/Advanced는 기존 파라미터 카드에서 전환한다. 구전략/D 전용 창과 버튼은 제거했다.

## 현재 전략

| 전략 | ID | 순서 |
|---|---|---|
| 급등주 눌림목 | `momentum-pullback` | 후보 등록 → 이후 눌림 → 저점 안정 → 새 반등·거래량 재유입 |
| 호가 스프레드 틱떼기 | `quote-scalping` | 실제 체결 활동·호가·잔량·비용 → 지정가 → 확인된 수량의 매도 |
| 폭락주 초단기 반등 | `crash-rebound` | 급락 감지 → 위험 확인·저점 안정 → 후속 반등 |
| 저유동성 지지구간 | `support-zone` | 가용 확인 일봉의 독립 지지 군집 → 접근·선택적 반등 |

Inje 한 세션은 한 알고리즘과 고정된 파라미터를 사용한다. 미국 USD 종목 1~20개를 같은 전략으로 감시한다. Bin은 위의 별도 Core이며 이 UI 선택기에는 연결하지 않았다. 실제 계좌 자동 전략 주문은 잠겨 있다.

판단은 원본 미국 체결·호가 이벤트로 수행한다. 공개 일봉이나 현재가 폴링을 틱으로 바꾸지 않는다. 당시 유효 TickSize·거래정지·세션/허용 주문 유형·필요한 RVOL/뉴스/L2가 없으면 MissingData로 진입을 보류한다. 기존 피드 연결이 이러한 자료의 확인까지 의미하지는 않는다. 완료 일봉은 지지 지표와 차트 표시용이다. 합성 검사 결과를 실제 시장 수익성으로 해석하지 않는다.

## 유지하는 공통 기능

국내·미국 종목 검색, 가격·차트, 이동평균선/일목, 관심그룹, 계좌 조회, 승인된 수동 주문, 정기매수, 기록과 주문 안전 장치를 유지한다. 미국 페니스탁 가격은 USD 소수 정밀도를 표시한다. 가상 체결은 실제 주문과 구분하고, 매수 신호만으로 보유 수량을 만들지 않는다.

앱 최초 시작 시 구전략/D 설정·전용 연구 기록·가상 실행 기록을 일회성 트랜잭션으로 정리한다. 계좌 키·관심종목·정기매수·실제 주문 WAL/미확인 주문·실제 감사 기록은 보존한다. 새 Inje 원장은 현금·수량·평균단가·주문/체결·진입 근거를 대사해 복원하며 신규 매수 OFF로 시작한다.

## 검증과 문서

```sh
dotnet build STOCK.Avalonia.sln -c Release
dotnet run --project Stock.Portable.Tests -c Release --no-build
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/injes-ui
```

Windows WPF를 Mac에서 컴파일하려면 `dotnet build Stock.sln -c Release -p:EnableWindowsTargeting=true`를 사용한다. 실제 Windows 화면 실행 검증과 Mac의 교차 빌드를 구분한다.

- [현재 전략 상세 설명서·전체 JSON](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)
- [구조와 데이터 계약](docs/architecture/Injes-Strategy.md)
- [현재 UI 흐름](docs/ui/Injes-Strategy.md)
- [검증 근거와 실제 제한](docs/verification/Injes-2026-11-11.md)
- [작업 시작 지도](docs/START_HERE.md)
- [공개 v0.1.17 기록](docs/verification/Release-v0.1.17.md) — 현재 로컬 변경과 다른 완료 이력
