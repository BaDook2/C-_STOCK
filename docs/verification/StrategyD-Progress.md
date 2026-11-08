# 범용 당일매매 엔진 + 전략 D — 단계 검증

기준 요구사항 ../requirements/StrategyD-DayTrading-20261107.md, 분석/경계 ../architecture/StrategyD-Plan.md. 실제 주문 및 사용자 DB/설정 수정 없이 격리 fixture로 진행한다. 단계 구현과 제품 UI 연결 완료는 구분한다.

## P0 — 감사 완료

- 기존61개 모델·schema1·공통 detector/보유규칙/스크리닝·실시간·API 공급 경로를 대조했다. 변경 필요/재사용/위험 표 및 P0~P6 작은 실행 단위를 StrategyD-Plan.md에 기록했다.
- artifacts/strategy-d-p0: legacy-61-baseline.json, legacy-source-baseline.json, baseline.log.
- 기존 확장177개 통과. 직전WPF687/EXE smoke 성공 기준 보존.
- 실제 API의 과거틱 보관기간/완전성·호가 이력·시점별 수정주가와 특수 세션은 확인하지 않았다. 코드의 최근 bounded 틱 PriceBar를 정밀 replay라고 간주하지 않는다.

## P1 — 설정/호환성 완료

- 추가: Stock.Core/Research/DayTrading/DayTradingConfiguration.cs, StrategyDefinition.cs, Stock.Tests/DayTradingConfigurationTests.cs. 검사 연결 Stock.Tests/Program.cs.
- schema1/krx-oversold-rebound/61개는 기존과 동일 의미. D만 schema2/strategy-d-intraday-v1, 공통61 값+새 timeframe/급락·지지·재진입·익절·세션·위험·품질 정책. 구 import는 D를 거부해 잘못된 구 엔진으로 실행하지 않는다.
- 실제일봉MA240/OnTick/같은Zone새신호/당일청산/1회진입은 **검증 전 실험 프리셋**이다. 사용자의 활성/실계좌 기본 전략을 바꾸지 않는다.
- 새 timeframe은 체결 수와 초를 구분하며 일봉은1거래일, 지표별 override는 독립 모델이다. 고정/추적/TP 수치는 공통61의 단일 원본을 참조해 중복 필드를 만들지 않는다.
- JSON 버전/ID·필수항목·알 수 없는 enum·기간·급락·필수MA/매물대/반등·상충익절/추적/위험 검증. .015 수수료를 원정밀도로 유지한다.
- Release 빌드0경고0오류, **39개 검사 통과**. artifacts/strategy-d-p1-build.log, strategy-d-p1.log. 기존 SQLite State 계약의 격리 DB roundtrip 포함.
- 제한: 설정만 추가한 단계이므로 UI/실시간/백테스트에는 아직 D 선택을 노출하지 않는다. 이후 P2~P6에서 실제 계산/실행에 연결한 기능만 표시한다. 구61·구 엔진 파일은 수정하지 않았다.

## P2 — 데이터/지표 완료

- 추가 CausalMarketInput.cs(품질/초·체결·분·시간·일 진행/확정봉), VolumeProfileAndSupport.cs(증분체결분포/근사 모드/이종지지), StrategyDDataEngine.cs(KnownAt 준비자료/독립기준MA/지표·급락/현장자료).
- 기존 MarketEvent/Instrument/StrategyBar/HistoricalFacts/TickCoverageTracker/근사bin 배분을 재사용한다. 잘못된 종목·미래수신·역순·중복·지연/누적량 누락을 검사하고 미확인 시각은 관찰하되 엄격 진입은 금지한다.
- 초·분봉의 경계 체결은 다음 진행봉에만 포함한다. 신호 이전 봉의 종가를 뒤따르는 경계 틱 가격으로 바꾸지 않는다. 미관측 빈 구간/짧은 장마감 구간을 완성봉으로 만들지 않는다.
- 전일 확정240일 MA/현재까지 받은 고가/rolling 기간을 분리한다. future suffix/미래 위험 사실은 현재 snapshot에 반영하지 않는다. 완성봉 지표는 봉 추가 시만 갱신하며 MA/근사매물대를 version cache한다.
- 체결 매물대는 실제 개별량의 rolling 증분+제거, 최대bin에 맞춘 폭 확대/제한된 재구축. 자료 이력 부족·버퍼 초과는 근사로 몰래 대체하지 않는다. 근사는 기존 OHLCV 균등배분을 명시한다. MA+유효VolumeZone/최소거리/최대폭을 함께 검사한다.
- 한계: 엄격 시각 품질 기본값에서는 현재 키움 parser의 `KST 날짜 추정` 입력은 신규 진입을 차단한다. 과거틱 PriceBar만으로 정밀분포의 연속성을 인증하지 않는다. 실제 공급자 규칙 검증/제품 UI 노출은 이후 단계다.

## P3 — 전략 D 신호 완료

- 추가 StrategyDSignalEngine.cs, StrategyDSignalTests.cs. P2 검사 DayTradingDataTests.cs, 설정 검사와 통합된 **100개 성공**, Release0경고0오류: artifacts/strategy-d-p3-build.log, strategy-d-p3.log.
- 실제 첫 접근 틱10100→저점9900→9920대기→9950첫상향 통과에서 1신호. 신호는 frozen 급락·지지·저점·판단 snapshot과 symbol+instance+episode+sequence 식별자를 가진다.
- 지속 true/중복 관측·미체결/보유는 신규 독립 진입을 만들지 않는다. 실제 청산정보를 제공한 뒤 cooldown/새episode/새반등을 확인하면 같은 물리적Zone 재진입 가능. 작은 provider Zone변화로 저점을 리셋하지 않는다.
- 데이터 누락/필수지지 소멸/상태위험/자료 미확인/일일횟수/종료시각/추격거리/손절별 cooldown을 검증했다. 신호 엔진에는 주문 또는 체결 생성 코드가 없다.
- 제한: 이번 단계의 재진입 context는 검사에서 제공했다. 다음 P4가 실제 확인된 가상 체결로 이 context를 공급하고 위험/공유계좌/독립 마감시계 및 다음이벤트 체결을 검증한다. UI 연결/P5 replay 비교는 아직 미완료다.

## P4 — 범용 가상 실행 코어 완료

- 추가 IntradayPositionRules.cs(3손절/3익절·손익분기·수량·독립 당일시계), DayTradingRuntime.cs(범용 신호/실행 포트 및 D 어댑터), DayTradingPortfolioEngine.cs(직렬 공유계좌/체결·원가·위험·소유/주문 상태). 기존 PaperBroker의 다음이벤트·부분체결·비용을 재사용한다.
- 새 검사 IntradayPositionRuleTests.cs/DayTradingPortfolioTests.cs. **설정부터 실행까지145개 성공**, Release0경고0오류. artifacts/strategy-d-p4-build.log, strategy-d-p4.log.
- 신호 틱에서는 주문 접수만, 다음 실제 공급 이벤트부터 가상 체결. 여러 partial buy fill은 1회차, partial TP1은 누적 목표수량까지 미완료. TP1/TP2는 원래 최대수량 기준, 0주에는 체결/완료를 만들지 않는다.
- 독립 손절/동시3손절1판단·트리거목록, 부분/전량/추적 중심, 손익분기 비용, peak/floor, 분할매수 가격AND지지·보유분 포함 남은 위험예산을 검사한다. 최초 진입 지지하단은 동적으로 내려주지 않는다. 매도 의도별 당시 원가/peak/stop snapshot을 보관한다.
- 자동진입OFF 뒤에도 기존 포지션 TP/추적손절 유지, 뒤늦은ON은 과거 신호 미소급. 계좌 현금/예약/노출/일일손실·횟수와 종목 중복 제약은 중앙 어댑터 기준이다. 주문 미확인은 재전송 없이 재동기화를 요구하고 종목별 오류는 격리한다.
- 새 틱이 없어도 OnTime에서 매수차단/미체결 취소/잔량청산 의도를 생성한다. 실제 실행 가능한 후속 이벤트가 없으면 체결을 만들지 않고 장 종료의 UnexpectedOvernightPosition을 남긴다. 긴급중지는 새 주문/미체결을 중지하며 보유분을 임의 강제청산하지 않는다.
- 실계좌 어댑터 전송 계약은 별도 승인과 전송 직전 재검사다. 검사에서는 IsLive 가짜 어댑터의 전송0/승인 철회 시 전송0을 확인했다. **실제 IAccountBroker 어댑터/앱 화면에는 아직 연결하지 않았다.** P6에서 연결되기 전까지 앱의 기존실행은 그대로다. 현재 Raw/근사 replay 결과 비교·WPF 연결은 P5/P6 잔여다.
# P5 — replay/봉 근사/성과

## P6 1차 UI/앱 수명 연결 (회귀 최종 검증 중)

최종: 기존 전체 회귀1651 성공(artifacts/strategy-d-regression.log, Exit0). 이후 새 전략 통계에서 주말 시계 pulse를 거래일 표본으로 포함하지 않도록 보정했고 관련192개+WPF13개를 다시 통과했다. 최종 Release0경고0오류, 기존 핵심6소스 baseline SHA256 불변. 단일파일 publish와 격리 실행 Exit0/Success/NoBrokerCalls=true. 루트/release EXE를 이전 백업 후 갱신했다. 실제 파일 해시/크기는 artifacts/strategy-d-local-exe.json. GitHub는 게시하지 않았다. 이는 문서에 기재한 1차 가상/연구 범위의 완료이며 실제 broker 활성화/20초과 원본 구독/macOS 검증을 의미하지 않는다.

- 새 핵심191개(artifacts/strategy-d-p6-core.log), WPF13개(artifacts/strategy-d-p6-ui.log) 성공. 실제 D 설정0.015 표시/저장·schema2 전용 보관·구61개 불변·공통 백테스트1신호/4체결·당시zone·한번 클릭 행 선택·실제 background 가상 서비스 시작·팝업 종료 후 실행 유지·앱 종료 feed dispose를 격리 fixture로 검증했다.
- DayTradingSessionService의 bounded queue/serial background/독립250ms시계/앱 수명, UI1초 갱신, 신호·체결·보유·미체결 분리, 활성화 이전 신호 소급 금지, 진입OFF 후 보유 보호 유지, 긴급중지/전체중지 연동. 원본 메모리/확인 체결·snapshot 영속 정책을 따른다.
- 전략 D는 기존 전략 설정/종목 백테스트/스크리닝에 별도 진입 버튼으로 연결했다. 기존 전략 콤보박스를 새ID로 오염시키지 않는다. 테스트 기록에 당시 설정/봉/결과를 추가하고 과거 기록을 열 수 있다. 상세 docs/ui/StrategyD.md.
- 실제 broker 주문은 미연결. 원본 감시 호스트는 키움 국내 최대20종목/단일 구독이다. 엄격 timestamp/연속성/당시 위험 미확인은 신규 진입을 막는다. 실제 시장 실행이나 수익성 검증을 수행한 것은 아니다. 공개/과거 OHLC는 명시적인 봉 연구 가정과 구분한다.
- 최신 Release 빌드0경고0오류(artifacts/strategy-d-final-build.log). 전체 기존 회귀는 artifacts/strategy-d-regression.log에서 진행 중이다. 완료 전 로컬 EXE를 갱신하지 않는다.

새 핵심181개 성공, Release0경고0오류(artifacts/strategy-d-p5.log, strategy-d-p5-build.log). 원본 receive 순서 재생은 같은 StrategyDRuntime/DayTradingPortfolioEngine/PaperBroker를 사용하여 스트리밍과 신호 ID/시각/사유/체결을 비교했다. 원본 호가 부재는 호가 체결을 만들지 않는다. ClosedBarReplay는 명시적인 OHLCV 연구 근사이며 첫 접근/저점은 확정 종가에서만 관측한다. 다음 봉 시가+최소지연/가정 유동성, 같은 봉 stop 우선, 현재 봉 고가를 과거 저가 stop에 소급하지 않는 보수 모델이다. 이 입력은 ResearchOnly로 실계좌 포트에 주입할 수 없다. 미확인 종목 상태·위험의 연구가정은 별도 opt-in이고 확인된 위험을 덮어쓰지 않는다.

공유 보고 계산은 IntradayReportBuilder, 단일 연구 진입점 IntradayBacktestService의 typed overload다. cache 키에 자료/전체 설정/facts/비용/기간/모형이 포함되고 요청·반환을 복제한다. 빈 자료/미래기간/중복봉/정밀 매물대 오용은 0 결과로 숨기지 않고 거부한다. 원본 이벤트/시세는 DB 저장하지 않는다.

성능: artifacts/strategy-d-perf-before와 after/performance.json. 10/25/50/100종목 각1000개, 합성100ms 간격 이벤트; 100종목10만2.230→1.131초, 누적할당874,863,784→542,887,712bytes, retained약54.5MB. 지표/근사지지/고점의 확정봉 version cache만 추가했고 입력을 축약하지 않았다. API/렌더링/비동기 queue 성능과 구분한다. 성능 도구의 queue0은 직접 직렬 주입이므로 실제 UI queue 측정값이 아니다.
