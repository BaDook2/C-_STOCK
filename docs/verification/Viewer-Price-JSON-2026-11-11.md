# 도움말·StockViewer 가격·JSON 검증

2026-11-11 후속 사용자 요청 세 가지를 이행했다. 기존 미커밋 Inje/Bin 변경을 보존했으며 GitHub 게시·실계좌 주문·개인 DB/키/실제 기록 삭제를 하지 않았다.

## 결과

1. 물음표 도움말이 같은 ShortDescription/DetailedDescription을 두 번 연결하던 문제를 수정했다. 같은 설명은 한 번만 표시하고 서로 다른 설명/허용 범위는 유지한다. 실제 버튼을 눌러 생성한 팝업의 표시 문장도 검사했다.
2. 사용자 StockViewer 저장소의 공개 시세 선택과 익명 데이마켓 클라이언트를 포팅했다. NXT 대체 필드·프리/메인/애프터, 미국 프리/애프터·공급자 확인 데이마켓, 최신 가격 우선·마감 후 마지막 가격 보존·미확인 상태 UNKNOWN, 같은 캐시의 1초 목표 조회·1/2/4/8/10초 재시도·Retry-After·취소를 반영했다. 공개 가격을 원본 틱/호가나 없는 OHLCV로 만들지 않는다. 온라인 계좌의 기존 조회/재시도는 보존했다. 자세한 계약은 `docs/api/NaverPublic.md` 맨 위에 있다. StockViewer 저장소는 소스 읽기만 했고 이 앱은 그 경로에 런타임 의존하지 않는다.
3. JSON 구조는 현재 두 전략과 일치한다. 전체 프로필·선택된 체계·비활성 값·내부 보호 정책·Inje 네 알고리즘/Standard/Advanced·Bin 기간/단위/우선순위/국가별 비용표를 보존한다. Inje166개/Bin172개 입력값을 실제로 바꾸어 왕복했다. 실제 저장/다운로드/파일 불러오기/재시작 후 이름 중복 접미사·편집 시각 외 모든 leaf 값이 일치했다. 누락·다른 알고리즘 필드·잘못된 단위/숫자·동시 체계 선택·사용 체크 불일치는 저장을 바꾸지 않고 거절했다. 구조를 새로 바꿀 필요가 없었으며 기존 전체 프로필 JSON 예시는 유효하다.

## 검증

| 검사 | 결과 | 로컬 근거 |
|---|---|---|
| Avalonia 솔루션 Release | 경고0/오류0 | `../work/viewer-final-build.log` |
| WPF 호환 소스 교차 빌드 | 경고0/오류0 (Windows 실행 검사는 아님) | `../work/viewer-wpf-build.log` |
| Avalonia 전체 회귀 | 861 PASS | `../work/viewer-final-ui.log`, `artifacts/viewer-final-ui` |
| Inje Core | 363 PASS | `../work/viewer-final-inje.log` |
| Bin Core | 187 PASS | `../work/viewer-final-bin.log` |
| 합성 공개 가격 계약 | 18 PASS (전체 UI 회귀에도 포함) | `Stock.Desktop.Tests/PublicPriceChecks.cs` |
| 소스 검증 목록 | 104파일 일치 | `docs/core-source-manifest.json` |
| 설명서 JSON | Inje5개+전체 프로필1개 strict import PASS | 통합 전략 설명서 |
| Mac 패키지 합성 실행 | Success/StrategySettingsVerified/NoBrokerCalls true | `artifacts/viewer-native-smoke/result.json` |
| Mac 패키지 실제 공개 조회·차트 | 삼성전자1500봉/SK하이닉스1500봉/애플2513봉 렌더, Success true | `artifacts/viewer-native-public/result.json` 및 PNG |
| StockViewer와 동시 공개 가격 비교 | 두 종목 가격 일치 | `../work/public-price-comparison.json` |
| 설치 앱 코드서명·검색 | strict 서명 통과, bundleID 검색1개 | `/Applications/Stock Development.app` |

2026-11-11 13:33 KST 두 클라이언트를 동시에 조회했다. 삼성전자는 같은 NXT 가격205250원, 공급자 시각 차이 약3ms였고, 애플은 같은 데이마켓 가격347.54 USD와 같은 원본 시각이었다. 이는 해당 관측 시점의 비교이며 항상 가격이 일치하거나 매초 새 체결이 생긴다는 보장은 아니다. 별도 임시 비교 프로젝트는 StockViewer 소스를 읽어 링크했으며 계좌/키/주문 API를 호출하지 않았다.

새 JSON 검사는 `Stock.Desktop.Tests/StrategyValueChecks.cs`, 팝업은 `StrategySettingsChecks.cs`다. 테스트의 초기 취소 검사가 UI 스레드를 동기 대기해 멈춘 문제는 테스트에서 Dispatcher를 처리하도록 고쳤고 해당 테스트 프로세스만 종료했다. 운영 앱의 실패나 계좌/기록 변경이 아니다. 중간 검사에서 날짜 시각의 문자열 표기 차이와 기본 패턴 우선순위를 잘못 가정한 기대값을 고친 후 최종 검사는 모두 통과했다.

## 설치

2026-11-11 13:36:51 KST, 기존 앱이 실행 중이 아님을 확인하고 검증 패키지를 `/Applications/Stock Development.app`에 반영했다. bundleID `com.stock.desktop.avalonia`, 표시 버전0.1.17이며 공개 릴리즈와 다른 미게시 개발본이다. source revision은 `local-viewer-price-json-20261111`이다.

- Core SHA256: `5267ee8e135611d6c01b1571fdfd589358e76dcf674ad2892490d8f7be5abb8f`
- UI SHA256: `73b6ef1c988bd33431170083ef6be7507d42da389205a383a53930592e64aa4e`
- 직전 앱 복구본: `/Users/jang-gyeongbin/Library/Application Support/Stock App Backups.noindex/2026-11-11-viewer-price-json/Stock-before-viewer-price-json.saved-app`
- 설치 결과: `../work/viewer-install-result.json`, 백업 폴더의 `install-manifest.json`.

임시 .app은 설치 경로로 이동하고 임시 등록은 해제했다. 최종 mdfind 검색은 설치 앱1개다. 일반 사용자 DB를 사용하는 앱은 자동 실행하지 않았으며 합성 시작/공개 조회는 모두 격리 저장소와 메모리 자격증명 저장소로 수행했다. 이전 중복 정리 백업·다른 저장소 StockViewer·기존 공개 ZIP/DMG는 보존한다.

Bin 실시간 가상 주문/재시작 보호 원장 복원은 기존처럼 미연결로 시작 차단한다. 이번 작업은 해당 범위를 늘리거나 실계좌 자동주문을 켜지 않았다.
