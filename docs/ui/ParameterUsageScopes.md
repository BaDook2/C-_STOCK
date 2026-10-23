# 전략 파라미터 사용범위

현재 설정의 59개 파라미터를 용도에 따라 분류한다. 59번째 `가격자료 검증 모드`는 공개 일봉만으로 백테스트 엔진을 진단하는 전용 가정이며 실시간 주문에는 적용되지 않는다.

| 사용범위 | 개수 | 항목과 하위 분야 |
|---|---:|---|
| 백테스트 전용 | 7 | 초기 현금, 편도 수수료, 매도 거래세, 슬리피지 / 일봉 감지 정책 / 연 무위험수익률, 연율화 거래일 |
| 공통 전략 조건 | 51 | 대상·급락·RSI·거래량 / MA·확정 고점·근사 거래량 구간·군집·반등 / 예정 투자금·분할 진입 / 손절·익절·추적 손절 / 보유기간·재진입 |
| 온라인 투자 전용 | 0 | 전용 파라미터는 없으며 공통 낙주 설정을 검증된 틱·분 실시간 경로에서 사용. 주문 보호 한도는 다량 실행 창에서 별도 지정 |

‘공통’은 전략 판단 기준을 의미한다. 현재 연결 경로는 일봉 백테스트와 마지막 확정일 스크리닝뿐이다. 실제 계좌 자동주문 또는 수동 모의매매에 이 설정이 적용된다는 뜻이 아니다. 수동 주문 수량과 1회 매수 한도는 기존 종목·차트 → 실제 계좌 주문에서 설정한다. 복제하거나 이동하지 않았다.

## 중앙 메타데이터와 화면

- `Stock.Core/Research/Oversold/OversoldParameterMetadata.cs`: 사용범위 이름·설명·상태, 각 속성의 명시적 소속과 하위 그룹, 실제 사용 도움말. 중복 키는 dictionary 구성에서 실패하고 새 미분류 속성은 설명 생성 시 오류로 탐지한다.
- `ParameterMetadata`: optional UsageScope / UsageDescription 추가. 기존 일반 전략 메타데이터에는 영향 없음. Label·Tooltip·상세 Popup이 동일 Catalog 결과를 사용한다.
- `OversoldWorkspace.BuildOversoldGroups`: 기존 ParameterField를 한 번 만들고 사용범위 및 하위 그룹으로 투영한다. 저장용 OversoldGroups와 화면용 OversoldUsageGroups가 같은 필드 객체를 참조한다.
- `OversoldParameterPanel`: 세 사용범위 카드 + 하위 Expander. 온라인 빈 그룹에도 상태와 위치 안내 표시. 각 필드의 도움말은 공통 ParameterHelpDialog에서 ‘사용범위’를 추가 표시한다.
- 값은 기존 `oversold-config-v1`, `oversold-preset:<이름>` 등의 SQLite state에 그대로 저장한다. DB migration 없음.

## 코드 참조에 근거한 분류

| 항목 | 실제 참조 |
|---|---|
| InitialCapital | OversoldBacktest의 초기 Cash 및 PerformanceCalculator 기준 자산; OversoldConfig.Validate의 InitialCapital ≥ PlannedInvestment 관계 검사 |
| CommissionPercent | 다음 시가 모형 매수 가능 정수 수량, 매수·매도 수수료 |
| TaxPercent | 모형 매도 비용 |
| SlippagePercent | 모형 매수/매도 체결가를 시가보다 불리하게 조정 |
| TouchPolicy | 보유 중 비상손절·익절·추적 손절용 low/high를 종가로 대체할지 선택. 최초 지지 하단 이탈은 정책과 관계없이 종가 판단 |
| AnnualRiskFreePercent / AnnualTradingDays | PerformanceCalculator의 Sharpe 통계. 신호·체결 변경 없음 |
| 나머지 51개 | OversoldIndicatorCache / SupportProviders / OversoldBacktest의 종목·진입·보유·청산 조건 |

TouchPolicy는 도달 여부를 과거 일봉에서 해석하는 **모형 가정**이고 EmergencyStopLossPercent·TakeProfitPercent 등은 **전략 임계값**이다. 장중 경로/실제 체결을 생성하거나 온라인 감지 정책을 추가하지 않는다.

## 스크리닝에서의 실제 사용

ScreenOversold는 같은 OversoldBacktest를 마지막 확정일 하루 구간으로 실행한다. 이전 봉은 지표 준비에만 쓰고 포지션은 빈 상태로 시작한다. 따라서 마지막 날짜 Buy 신호가 대기 상태로 남으며 다음 날 체결은 실행하지 않는다. 백테스트 자금·비용·감지·통계 값은 그 신호 선별을 바꾸지 않는다. 단, **전체 Config 검증은 유지**하므로 초기 현금 ≥ 예정 투자금 관계나 다른 범위 오류는 스크리닝 실행도 막는다. 공통 분류 내 추가 매수·보유·청산·재진입 조건 또한 이 하루 신규 진입 검사에서 실행되지 않는다. 미래에 스크리닝 기간/보유 상태를 변경하면 이 설명과 테스트도 함께 변경해야 한다.

## 검증

ParameterScopeUiTests는 전체59개 누락/중복·8/51/0 분류, 기존 필드 참조 공유, 저장·재시작·프리셋 복원·기본값, 서로 다른 그룹 사이 검증, 잘못된 값의 저장 방지, 0.015 → 표시0.02 → 편집/저장0.015, 메타데이터 도움말, 하루 스크리닝 가정 변화 불변성, 1,540/1,160 폭 카드·스크롤을 검사한다. 기존 오른쪽 최소14 및 구형 전략 실행차단 검사도 유지한다. 실계좌·물리 마우스·다중 모니터 DPI 검증은 수행하지 않는다.

2026-10-03 최초 검증은 7/51/0이었다. 2026-10-13 가격자료 검증 모드 추가 후 현재 분류는 8/51/0이며 Release·WPF·단독 EXE에서 다시 확인했다. 실제 주문은 수행하지 않았다. 최신 로그와 배포 해시는 VALIDATION.md를 참고한다.

## 2026-10-04 명시적 행과 입력 종류
명시 소속/분야의 원본은 OversoldParameterLayout.Rows다. 사용범위8/51/0이며 null 빈칸을 포함한26행으로 표시한다. ParameterMetadata.InputKind가 decimal/integer/ratio-list/MA-list를 구분한다. 기존 focus 원정밀도 표시는 전략 편집에서 고정2자리로 대체하되 Source Value는 무편집 보존한다. 51개 공통 전략 라벨과 세부 분야 이름을 사용자 요청대로 유지한다. 상세: FixedDecimalParameters.md.
