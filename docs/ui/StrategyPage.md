# 전략 설정 — Inje / Bin

2026-11-11 로컬 Avalonia 소스 기준. GitHub에 게시하지 않은 개발본이다. 상위 그룹은 **Inje’s Strategy / Bin’s Strategy** 두 개다. 구전략·D 설정·공통 조건의 옛 상위 그룹은 화면에서 제거했다. 백테스트 가정과 공통 조건은 해당 전략 안에 있다.

두 이름 바로 옆에 `사용` 체크박스를 둔다. 파라미터를 접어도 보이고, 하나를 켜면 다른 하나는 꺼진다. 둘 다 끄면 새 실행을 거절한다. 이 체크는 전략 선택이며 실계좌 주문 권한을 활성화하지 않는다. 진행 중인 세션은 고정 설정을 보존하고, 선택 변경 시 신규 진입 감시를 중지한다.

Inje 내부의 네 알고리즘 선택기와 Standard/Advanced 편집은 각 알고리즘의 값을 보존한다. 공통14개, 체결·가상 현금, 강도6개 또는 전용 파라미터를 편집한다.

Bin은 이름, 가족 선택, 시장 분류, Stage1, 다섯 진입 패턴, 우선순위5개, 공통 매도·위험·재진입, 시세 품질·주문 체결, 거래소, 한국/미국 프로필과 기간별 비용표를 편집한다. 기간에는 초·분·시·일, 봉 크기에는 초·분·시·거래일 봉 단위를 붙인다. 초기 가상 현금은 KRW/USD를 따로 보관한다. 비용표는 시작/종료일·당시 확인 시각·출처를 포함하며 추가·삭제할 수 있다. 비어 있는 예산과 비용은 확인된 값으로 꾸미지 않는다. 지정가·추가매수 금지·실계좌 잠금 등 명세상 고정 정책은 보이되 편집하지 못한다.

상단 선택기는 전체 설정 프로필이다. 저장·추가·이름 변경·복구·기본값·불러오기·다운로드를 재사용한다. 저장 JSON은 `kind=stock-strategy-profile`, `schemaVersion=1`이며 두 체계의 모든 설정, Inje 네 알고리즘, 사용 중인 체계를 함께 담는다. 비활성 체계의 값도 보존한다. 단독 Inje/Bin JSON도 받으며 다른 체계의 값은 유지한다. 중복/알 수 없는 키·누락·잘못된 단위·범위는 저장 전에 거절한다. 기존 Inje 설정은 프로필에 복사하며 기존 계좌/주문/실제 기록을 삭제하지 않는다.

실행은 선택한 체계만 `OversoldConfig.Injes` 또는 `.Bins`로 전달한다. 서로 다른 두 체계를 동시에 담은 실행 설정은 거절한다. 백테스트·일반 스크리닝은 두 엔진을 연결했다. Bin 스크리닝은 신호·위험 승인만 평가하며 가상 주문도 만들지 않는다. 원본 체결/호가·확인된 시장 세션/가격 규칙이 없으면 MissingData다. 일봉에서 틱을 만들지 않는다. **Bin 실시간 가상 주문과 재시작 복원은 아직 연결하지 않아 시작을 차단한다.** Inje 가상 세션/원장 복원은 유지하며 실계좌 자동 전략 주문은 계속 잠겨 있다.

주요 코드: `Stock.Avalonia/Views/StrategyPanel.axaml`, `Views/SettingInput.cs`, `ViewModels/StrategyEditor.cs`, `Stock.Core/Strategies/StrategyProfile.cs`, `Strategies/Bins/EditorModel.cs`, `Strategies/Bins/ResearchBridge.cs`.

검사: `Stock.Desktop.Tests`의 StrategySettings/StrategyJson/Workflow/InjesWorkflow/Design, `Stock.Bins.Tests/BridgeChecks.cs`, Inje 및 Bin Core 회귀. 전체 설정 JSON과 설명은 `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`에 모았다.

## 도움말·JSON 재검증 — 2026-11-11 후속

물음표 도움말에서 같은 short/detail 설명을 두 번 연결하던 문제를 고쳤다. 다른 상세 설명과 허용 범위는 남기고 같은 문장은 한 번만 표시한다. 실제 물음표 버튼과 열린 팝업의 TextBlock을 검사했다.

JSON 구조는 현 파라미터와 일치한다. 최상위 필수 항목은 schemaVersion/kind/name/selectedSystem/selectedInjeId/injeStrategies/bin/binInitialCashUsd/binInitialCashKrw다. Inje 배열은 독립 알고리즘4개의 commonParameters/strategyParameters/standardParameters/effectiveAdvancedParameters/executionPolicy를 보관한다. Bin은 전체 typed config와 duration/value·unit, 국가별 프로필/날짜별 비용표를 보관한다. 비활성 체계의 값도 함께 저장한다.

`StrategyValueChecks`가 Inje166개·Bin172개 입력값을 각각 변경해 JSON 왕복을 검사하고, 패턴 우선순위·실제 설정 저장·다운로드 파일·불러오기·편집기 재시작까지 검사한다. 이름 중복 접미사와 편집 시각 외의 모든 프로필 leaf를 비교해 비활성/내부 정책 값도 유지됨을 확인한다. 누락 파라미터·잘못된 시간 단위·엉뚱한 알고리즘 필드·동시 사용 선택·사용 체크 불일치·숫자 형식 오류는 저장을 바꾸지 않고 거절한다.

## 샘플 검증 — 2026-11-11 후속

Inje4개/Bin5패턴 샘플9개를 strict JSON 불러오기부터 실제 연구·스크리닝 명령까지 검사했다. 전체 JSON은 기존 한 파일 전략 설명서 마지막에 모았다. 합성 원본 이벤트로만 기능을 검증하며 공개 일봉을 틱으로 만들지 않는다. 프로필만 불러와서는 샘플 이벤트가 사용자 앱에 생성되지 않는다. 결과는 docs/verification/Strategy-Samples-2026-11-11.md다.

후속 사용자 요청으로 실제 설치 앱에도9개를 각각 저장했다. 상단 선택 목록의 `샘플 · Inje · …`4개와 `샘플 · Bin · …`5개를 고를 수 있다. 기존2개를 포함해 총11개이며 기존 선택/파라미터는 보존했다. 사용자 로컬 프로필 추가이며 모든 설치본에 샘플을 자동 생성하는 변경은 아니다.

## v0.1.19 기본 샘플

배포 앱은 StrategySampleCatalog의 임베디드 JSON9개를 최초1회 저장한다. 기존 동일 이름 값과 현재 선택을 덮어쓰지 않고, 이후 사용자가 이름을 바꾼 샘플을 다시 생성하지 않는다. 새 설치는 기본 설정1개+샘플9개, 기존 설치는 기존 프로필에 없는 샘플만 추가한다. 시세·합성 거래 기록은 추가하지 않는다. App 시작 단계에서 수행하며 UI 파라미터나 주문 권한을 자동 변경하지 않는다.
