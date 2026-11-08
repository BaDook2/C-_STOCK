# 전략 설정

현재 제공하는 전략 엔진은 **국내주식 과매도·낙주 반등** 하나이며 이 엔진의 설정을 여러 이름으로 저장할 수 있다. 화면 제목과 바깥 래퍼는 일반적인 `전략 설정`으로 표시하고, 불필요했던 `국내 과매도·낙주 반등 · 일봉 백테스트` 중복 제목과 중복 저장 버튼을 제거했다. 급등/박스권/구형 반등/틱떼기/농부매매/Pipeline 선택과 구형 Standard/Advanced 편집은 복원하지 않는다.

| UI | 동작 | 코드 |
|---|---|---|
| 전략 선택 | 저장한 낙주 전략 설정을 선택 즉시 편집 화면과 현재 적용값으로 전환 | OnlyOversoldWorkspace |
| 추가·이름 변경 | 현재 입력값으로 여러 전략 설정을 추가하고 선택한 전략의 이름을 변경 | OnlyOversoldWorkspace |
| 설정 저장·복구 | 현재 입력을 선택 전략에 저장하거나 저장 이후 입력을 이전 저장값으로 복구 | OnlyOversoldWorkspace |
| 불러오기·다운로드 | 버전과 전략 ID를 검사해 JSON을 새 전략으로 가져오거나 현재 설정을 JSON으로 저장; 계좌키·주문기록 제외 | OversoldPresetExport |
| 기본값 | 편집 화면만 기본값으로 변경하며 설정 저장 전에는 저장값을 바꾸지 않음 | OnlyOversoldWorkspace |
| 전략 진단 | 최초 실행 시 전략 목록에 자동 추가되는 일반 프리셋. RSI·거래량·Pivot·근사 매물대·반등 확인을 OFF로 두어 `사용안함` 집계를 확인하기 쉽고, 자료 결손과 앞 단계 제외는 각각 `자료부족`·`미평가`로 표시 | StrategyDiagnosticPreset |
| 사용범위별 그룹 | 백테스트 전용8개 / 공통 전략 조건53개 표시. 온라인 전용0개 범위는 숨김(메타데이터는 유지). 기존 세부 분야는 하위 그룹 | OversoldParameterPanel / OversoldParameterMetadata |
| 각 항목의 ? | 중앙 메타데이터 Tooltip 및 상세 도움말 | ParameterField / ParameterHelpDialog |
| 당시 자료 JSON | 실제 시점의 상태·거래대금·위험·시장일 입력 | OversoldWorkspace |
| 적용 및 저장 | 설정과 자료 검증 후 기존 SQLite에 보관 | SaveOversold |

`OversoldConfig` → 검증·snapshot → `OversoldBacktest` → 결과·근거 저장 순서다. 계산은 Core에서 수행한다. 구형 설정에서 새 설정을 자동 변환하거나 기본값을 복원하지 않는다. 새 설정에는 Standard/Advanced 변환이 적용되지 않는다.

`전략 진단`은 별도 모형이나 가짜 결과를 만드는 기능이 아니라 같은 과매도·낙주 엔진에서 사용하는 설정 프리셋이다. 따라서 다른 저장 전략처럼 스크리닝·백테스트·실시간 실행에 선택할 수 있으며, 선택만으로 주문을 보내지는 않는다.

자세한 사용법과 저장 키는 [OversoldBacktest.md](OversoldBacktest.md), 시점 자료 형식은 [OversoldHistoricalFacts.md](../api/OversoldHistoricalFacts.md)를 참고한다. 실계좌 자동매매는 지원하지 않으며 수동 주문 및 정기매수 도구는 별도로 사용할 수 있다.

사용범위별 항목과 코드 참조, 스크리닝 적용 차이는 [ParameterUsageScopes.md](ParameterUsageScopes.md)를 참고한다. 공통 분류는 실제 계좌 연결을 의미하지 않는다.

## 2026-10-04 입력과 배치
요청한 라벨을 중앙 Metadata에서 교체하고 분야별로 27개의 명시적3열 행을 구성했다. 관련 항목은 같은 행에 놓고 빈칸20개는 보존한다. 당시 입력칸은180×32였다. 현재 배치는 아래2026-11-07 갱신을 따른다. 소수 파라미터는 focus/blur 모두 두 자리이며 원정밀도 값은 무편집 보존한다. EntryRatios는 원소별 소수 목록, MA 기간은 정수 목록이다. 상세 입력 규칙과 붙여넣기 정책은 [FixedDecimalParameters.md](FixedDecimalParameters.md)를 참고한다.

### 2026-10-05 갱신
정수부 및 소수점 바로 앞은 일반 삽입으로 입력하며 소수점 뒤 두 자리만 덮어쓴다. 평균 거래대금·낙폭·RSI·거래량·지지의 lookback은 선택 봉 개수, 보유/후보/재진입 기간은 시장 거래일이다. Backtest와 Screening이 공통 EntryDetector/PositionRules를 사용한다. 새 실제 자동주문 연결은 승인 검토에 막혀 미연결이다. 세부 상태: ../architecture/OversoldExpansion-20261005.md.
## 2026-11-06 스크리닝 후보 설정

`스크리닝 후보` 행에 `근접 후보 표시`(기본 ON)와 `후보 표시 최소 접근도`(기본60.00점)를 추가했다. 두 값은 전략별 저장/JSON/실행 당시 설정에 포함한다. 접근도는 확률이 아니며 표시만 제어한다. 실제 주문은 기존 EntryDetector의 매수 조건을 모두 충족해야 한다. 확정 위험·상품 제외를 근접 설정으로 우회하지 않으며 기준값은0~100으로 검증한다. 총61개 필드/27행/빈칸20개다.

## 2026-11-07 카드와 입력 정렬
항목은145px 고정 높이 대신 내용 높이로 배치하며32px 입력칸의 단위/허용 범위는 오른쪽에 놓는다. 일반 입력130px/목록150px, boolean ON/OFF 범위 설명은 생략하고 상세 ? 도움말을 유지한다. 온라인 전용0개 범위는 숨긴다. TextBox template의 이중 padding을 제거했으며 소수 입력 규칙/원정밀도 저장은 유지한다. 검증: ../verification/CompactUi-2026-11-07.md.
