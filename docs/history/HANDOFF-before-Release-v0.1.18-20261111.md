# 현재 인수인계 — 도움말·StockViewer 가격·JSON 검증 완료

2026-11-11 사용자 요청 세 가지를 완료했다. 물음표 도움말 중복을 없애고, 오프라인 공개 가격에 StockViewer의 NXT/미국 시간외·데이마켓 선택·1초 목표 조회·재시도·Retry-After·취소 방식을 포팅했다. 현재 전략 JSON 구조와 UI 필드를 대조하고 실제 변경값 저장/다운로드/불러오기/재시작을 검사했다. GitHub commit/push/tag/Actions/Release 및 실계좌 주문/실제 기록 삭제 없음.

## 구현

- 도움말: `Stock.Avalonia/ViewModels/StrategyEditor.cs`의 LegacyParameter.Help에서 같은 short/detail을 한 번만 표시.
- 가격: `Stock.Core/Research/PublicCurrentMarketData.cs`, `PublicMarketData.cs`, 새 `UnitedStatesDayMarket.cs`, `CurrentMarketSnapshot.cs`, `WorkspaceModels.cs`. StockViewer 원본 경로는 `/Users/jang-gyeongbin/Documents/Codex/2026-11-09/https-github-com-badook2-c-stock`; 읽기만 했고 외부 경로에 앱 런타임 의존 없음. 자체 익명 방문자 세션만 쓰며 개인키/브라우저쿠키/계좌/주문 API 미사용. 네이버·토스 공공 제공처의 원본 시각/최신 가격을 선택하고 실패는 이전 값/UNKNOWN으로 표시. 공개 가격에서 틱·호가·없는 OHLCV를 만들지 않는다. 온라인 계좌의 기존 재시도는 유지한다.
- 화면: `Stock.Avalonia/ViewModels/MarketWorkspace.cs` 가격 시각에 제공처/세션 표시.
- JSON 구조는 변경할 필요가 없었다. 전체 프로필은 두 체계/비활성 값/Inje4개/Standard/Advanced/Bin기간·단위·비용표를 포함한다. 신규 `StrategyValueChecks.cs`가 Inje166/Bin172 입력값 변경과 실제 파일 라이프사이클을 검사한다. `PublicPriceChecks.cs`는 합성 공개 시세 계약18개, `StrategySettingsChecks.cs`는 실제 도움말 팝업을 검사한다.

## 검증

Avalonia 전체861/Inje363/Bin187 PASS. Avalonia Release와 WPF 교차 빌드 모두0경고/0오류(Windows 실행 검사는 아님). Core manifest104파일 일치, 설명서 JSON6개 strict import PASS. Mac ARM64 패키지 합성 시작/전략 화면 및 삼성전자·SK하이닉스·애플 실제 공개 차트 렌더 PASS. 13:33 KST 별도 동시 비교에서 StockViewer/현재 앱 삼성전자와 애플 가격 동일(국내시각3ms차이, 애플시각동일). 가격은 해당 관측시점의 비교다.

로그는 저장소 형제 `../work/viewer-*.log`, `public-price-comparison.json`, 최종 UI 화면·격리 DB는 `artifacts/viewer-final-ui`, 네이티브 검사는 `artifacts/viewer-native-smoke`/`viewer-native-public`. 실제 사용자 DB·키를 도구로 열거나 수정하지 않았다.

## 설치

13:36:51 KST `/Applications/Stock Development.app`을 새 검증본으로 교체했다. 앱이 실행 중이 아님을 확인했고 사용자 DB로 일반 앱을 자동 실행하지 않았다. bundleID `com.stock.desktop.avalonia`, 표시버전0.1.17/source revision `local-viewer-price-json-20261111`이며 동명 공개 릴리즈와 다르다. Core hash `5267ee8e135611d6c01b1571fdfd589358e76dcf674ad2892490d8f7be5abb8f`, UI hash `73b6ef1c988bd33431170083ef6be7507d42da389205a383a53930592e64aa4e`. 서명 strict 통과·mdfind 검색 결과 설치 앱1개.

직전 앱은 `~/Library/Application Support/Stock App Backups.noindex/2026-11-11-viewer-price-json/Stock-before-viewer-price-json.saved-app`에 보존. 설치 manifest는 해당 폴더와 `../work/viewer-install-result.json`. 이전0.1.16과 중복정리13개 백업, 다른 StockViewer 저장소와 공개 ZIP/DMG는 건드리지 않았다.

최신 검증 문서 `docs/verification/Viewer-Price-JSON-2026-11-11.md`, API `docs/api/NaverPublic.md`, UI `docs/ui/StrategyPage.md`/`ChartPage.md`. 사용자용 전략 설명과 전체 JSON은 기존 한 파일 `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`에 유지한다. 기존 두 상위 전략/단일 사용 체크·Bin 백테스트/일반 스크리닝 연결은 보존한다. **Bin 실시간 가상 주문/원장 복원은 미연결로 시작 차단**, 실계좌 자동 전략 주문 잠금도 유지한다. 이전 상태는 `docs/history/*before-viewer-price-20261111.md`에 있다. 모든 이전 미커밋 변경을 보존하며 게시하지 않았다.
