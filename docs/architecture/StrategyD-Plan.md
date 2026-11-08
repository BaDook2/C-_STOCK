# 범용 당일매매 엔진/전략 D — 단계별 계획과 P0 감사

기준: 2026-11-07 현재 소스. 요구사항 `docs/requirements/StrategyD-DayTrading-20261107.md`, 설명서 `docs/strategy/과매도_낙주반등_전략_상세설명서.md`. 문서의 기능 설명은 소스와 대조했다. 이 문서는 아래 구현 단위의 완료 표시를 대신하지 않는다.

## 현재 호출과 차이

| 영역/실제 코드 | 현행 동작 | 재사용/필요 변경 | 주의 |
|---|---|---|---|
| OversoldConfig / OversoldParameterMetadata / Layout | 61개 scalar 설정·한국어 설명·행/범위 | 구 모델/기본값 그대로 보존, D 설정 별도 버전 | 이름의 Days를 일봉으로 몰래 재해석 금지; 수수료0.015 보존 |
| OversoldPresetExport / OnlyOversoldWorkspace | schema1/krx-oversold-rebound, 이름별 DB 설정 | 기존 import/export 유지, 새 정의를 명시적으로 분기 | 새 ID를 구 엔진으로 실행하지 않음; 사용자의 활성 전략/JSON 자동 덮어쓰기 금지 |
| BarSpecification / StrategyBarAggregator | Tick/Minute/Hour/Day, 세션 정렬/완성봉 | MarketEvent/StrategyBar/세션 모델 재사용, 초·진행봉 추가 | 틱=초 아님, 원본 누락을 0거래 봉으로 채우지 않음 |
| MarketEvent / Instrument / TickCoverageTracker | 원본/수신 시각·sequence·호가·개별 체결량·연속성 | 새 경로도 기존 모델을 공통 입력으로 사용 | 공개 폴링 관측을 원본 틱으로 위장 금지, 미래 수신 자료 제외 |
| OversoldIndicatorCache / SupportProviders | 선택 봉 기준 MA/급락/근사bin/확정Pivot | Wilder/확정Pivot·근사bin 원리 재사용, D의 전일 확정 일봉MA 독립 | 1분 선택 시 240분MA가 되면 D 실패; 공급자 수정주가의 시점성 미확인 |
| SupportClustering | 같은 종류 여러MA로도 count 통과, 인접 연결 | D는 MA+VolumeZone 교차 중첩/Zone폭 독립 판정 | 기존 군집 결과를 변경하지 않음 |
| OversoldEntryDetector | 확정봉Close 최초 저점, 매봉 계속 true 가능 | D의 첫 실제틱/저점·상향 통과·episode ID 별도 | 후보/신호/주문/체결 상태 구분 |
| OversoldPositionRules | 종가 지지이탈 상시, 비상/추적 옵션, 거래일 보유/더낮은군집 재진입 | 기존 규칙 보존, 새 틱 보호/당일/재진입 정책 | MaxHoldingDays=1은 당일청산 아님 |
| OversoldBacktest / OversoldMinuteBacktest | 종가 신호 다음 봉 시가, 독립 단일계좌 | 그대로 유지; D는 공통 이벤트 런타임을 replay | 틱 신호를 완성 일봉Low에서 체결했다고 만들지 않음 |
| OversoldBacktestService | 메인/스크리닝 팝업 공통 진입점·hash cache | 새 전략 discriminator로 요청/결과 명시적 분기 | v1 cache/config/facts/hash 재현 보존 |
| CurrentOversoldScreening / OversoldScreening / CandidatePolicy | 기간 중 정확/근접후보, 근접도 주문허가 아님 | Universe/대상 목록/UI 정책 재사용 | 연구 가정이 실행 승인으로 전이되지 않음 |
| OversoldLiveStrategy / RealtimeOversoldScreeningService | 완성봉 공통 detector; 신호 이후 시세에서 준비 | D 신호 엔진을 research/실행/replay가 공유 | 기존 엔진의 비공개 fields에 D 상태 끼워넣지 않음 |
| LiveStrategyBatch / RealtimePaperStrategyBatch | 중앙 상태·주문pending·실행ID 대사·중복/중지 가드 | 실행 수명/체결기록/어댑터 원리 재사용 | paper는 종목별 독립자금, D 공유계좌와 구분; 15:20 종료는 청산 서비스 아님 |
| PaperBroker | 신호 이후 이벤트·latency·spread·부분/거부·수수료 | D replay/가상 공통 체결 어댑터로 재사용 | 다종목 체결 유동성/예약현금 확인, 0주 익절 완료 금지 |
| MultiTradeWorkspace / PublicPaperWorkspace / RealtimeScreeningWorkspace | 앱 수명 서비스·팝업과 무관, 실제주문 별도 확인 | 기존 UI/긴급중지에 새 경로 연결 | 자동매수 해제와 기존 포지션 보호 종료를 구분 |
| HistoricalFacts / RealtimeWarmup | KnownAt·일자별 상태/위험, 공급 자료만 준비 | 엄격한 진입/facts snapshot 재사용 | 미확인=false 금지; 현재 상위/지수구성의 생존편향 한계 |
| StrategyDiagnosticPreset | 엔진 검사용 완화 프리셋 | 그대로 회귀 기준 | D의 수익성 증명/실계좌 기본값으로 사용 금지 |

## 공급자 지원 — 현재 코드로 확인한 범위

| 입력 | 현재 코드 | D에 쓸 수 있는 범위/제약 |
|---|---|---|
| 공개 Naver KRX/해외 공개시세 | IPublicMarketData/ICurrentMarketData·시세 폴링 | 일봉 연구/근사 모드. 관측은 원본 체결/호가 연속성 증명이 아님 |
| 키움 현재가 | KiwoomBroker.Current, 수신시각/ExchangeTimestamp=false | 현재가 표시/보조 관측. 자체적으로 원본틱 D 입력으로 승격 금지 |
| 키움 국내 WS | Trading/MarketData의 개별체결·누적량·호가 parser | 엄격한 연속성/시각 품질·세션/facts 확인이 추가로 필요 |
| 키움 과거 분봉 | IMinuteHistoryBroker/IRanged.../IRecent... | 분봉확정 근사 replay 준비 가능, OHLC에서 틱 선후를 복구하지 않음 |
| 키움 과거틱 | ITickHistoryBroker → PriceBar[] 최근 bounded3페이지 | 호가·원본 sequence/수신시각·연속수집 증명 없음. 완전한 정밀 replay 자료라고 간주 불가 |
| 기타 계좌 | 공통 broker 계약은 존재, 자동실행은 현재 키움 국내 가드 | 계좌 연결 성공만으로 D 실시간 기능이 지원된다고 표시하지 않음 |

위 표는 저장소 구현 범위다. 실제 보관기간·전체 세션 과거틱/호가 제공 여부·특수장/거래소 규칙·공급자 수정주가 시점성은 외부 검증 전 미확인으로 남긴다. 이번 개발은 실제 계좌/키 조회나 주문을 하지 않는다.

## 61개 보존/새 설정 연결

`artifacts/strategy-d-p0/legacy-61-baseline.json`에 **실제61개 이름/기본식**, `legacy-source-baseline.json`에 설정·export·구 양 백테스트·진입·보유규칙 소스 해시를 보존했다. 매핑은 다음 원칙으로 적용하고, 누락된 구 필드는 삭제하지 않는다.

- 공통 그대로 재사용: 자본/수수료/세금/슬리피지/통계, 후보표시, RSI/거래량, 분할예산·비율, TP1/TP2/비상·추적 수치.
- 구 의미 유지+별도 D 컨텍스트: 유동성/중단기급락/MA/매물대/군집/반등/보유·재진입. D에서 기간을 바꿀 경우 새 Timeframe과 정책을 명시하고 구 ID에는 적용하지 않는다.
- 구 연구에만 유지: TouchPolicy/UsePriceOnlyBacktest. D 엄격 실행은 별도 데이터 품질/체결 모형이며 구 연구가정으로 매수 허가하지 않는다.
- 단일 진실: D에서도 공통 수수료/예산/TP·고정손절·추적수치는 기존 OversoldConfig 인스턴스의 값을 참조한다. 같은 값의 setter를 새 필드로 복제하지 않는다. 독립 ON/OFF가 기존에 없는 지지손절 등만 새 정책 필드다.

## 실행 단위와 완료 게이트

| 단계 | 작은 작업 단위 | 단계 완료 기준 |
|---|---|---|
| P0 | 호출 감사,61개 baseline,공급자/위험 보고 | 변경 전 expansion177개 및 직전WPF687개 증거 보존 |
| P1 | P1.1 timeframe/정책·검증, P1.2 버전 전략정의/JSON, P1.3 legacy 저장회귀 | 구61 JSON불변·새ID/버전·precision·오입력/미래schema 거부·격리 저장 검사 |
| P2 | P2.1 이벤트 품질/진행·확정봉, P2.2 일봉/장중급락·독립MA, P2.3 매물대/교차중첩 | 미래 suffix가 과거 계산을 바꾸지 않음·실제 체결량 보존·미확인 차단 |
| P3 | P3.1 후보/저점/상향반등, P3.2 stable zone/episode/재진입 | 예시10100→9900→9920→9950 최초1신호·같은Zone 새episode 재진입 |
| P4 | P4.1 체결기반 포지션/익절손절, P4.2 공유한도/주문멱등, P4.3 독립 종료시계/재동기화 | 부분·중복·0주·복수stop·무틱종료·잔여미청산·중지정책 검증 |
| P5 | replay=실시간 공통런타임, 기존PaperBroker 어댑터/공유현금, 결과/성능 | 동일 신호시각/사유, 다음이벤트체결,비용/예약/충돌·자료한계 표시 |
| P6 | 기존WPF 안의 전략유형/간편고급·파일·연구/가상실행·표식/차트·안내 | 연결 없는 선택지 노출 금지, 구UI+신규UI회귀/EXE smoke |

P0→P6 순서. 각 단위마다 소규모 검증 후 진행하며 앱의 구 경로는 유지한다. 계정의 `get_usage_limits` codex10080분 창 잔여를 단위 경계/큰 검증 전에 조회한다. 최초84%, **잔여40% 이하에서는 현황/수정파일/다음 시작점을 TASKS/HANDOFF에 남기고 중지**한다. null/조회실패는 미확인으로 기록하며 임의 잔여율을 만들지 않는다.

## P0 결과

- 전략/지정봉/스크리닝/실시간 가상 회귀: **177개 성공**, artifacts/strategy-d-p0/baseline.log. 실제 broker 호출/주문 없음.
- 직전 코드 기준 WPF687/관련46/publish와root EXE smoke 성공은 OneDayRange-2026-11-07.md.
- 순수 기존 엔진은 변경 전 상태. 새 전략 ID/공통 런타임의 additive 구현부터 진행한다. 실계좌 라우팅은 명시적인 사용자 활성화 전 차단을 유지한다.
