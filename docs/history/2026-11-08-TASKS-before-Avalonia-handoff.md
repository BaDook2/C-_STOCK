# 진행 — 범용 당일매매 엔진 + 전략 D

요구사항 docs/requirements/StrategyD-DayTrading-20261107.md. 기존 설명서 docs/strategy/과매도_낙주반등_전략_상세설명서.md. 기존61개/전략ID/실행 보존. 실계좌는 별도 명시 활성화 전 실행 금지. GitHub 게시 요청 없음.

- [x] P0 실제 호출·데이터 공급자·61개 설정 감사, 차이/재사용/위험/단위별 계획.
- [x] P1 버전 설정·Timeframe·위험/당일/재진입/익절 정책·전략D 프리셋·호환 검증/저장.
- [x] P2 표준 이벤트·진행봉·일봉MA/급락·정밀/근사 매물대·중첩 snapshot.
- [x] P3 후보episode·저점·신규반등·멱등신호·같은Zone 재진입.
- [x] P4 공통실행·3손절/3익절·부분체결·재동기화·당일청산·공유한도(가상 우선).
- [x] P5 replay·신호동일성·미래자료차단·체결모형·공유계좌/성과(원본/확정봉 근사 명시 분리).
- [x] P6 WPF 1차 설정/연구/가상 실행·상태/차트/기록·긴급중지·회귀·사용안내(실계좌 미연결·원본 가상20종목 제한 명시).
- [~] P7 (최신 추가, P0~P6 이후) 기존 STOCK 유지. 같은 상위 폴더 C:/Users/wkdru/Desktop/C#/stock_avalonia에 별도 Avalonia 프로젝트로 전환, 공통코어 재사용·macOS 개발 지원·키/DB/개인설정 복제 금지·빌드/가능 검증.

## 작업 상태 기록

2026-11-08 P6 검증 완료: 전체 회귀1651(artifacts/strategy-d-regression.log) 성공. 최종 주말 거래일 통계 보정 후 신규192 + WPF13 재검증 성공(artifacts/strategy-d-final-core.log, strategy-d-final-ui.log), Release0경고0오류, 기존 핵심6파일 SHA256 불변. Publish 단일EXE smoke Exit0/Success/NoBrokerCalls=true(artifacts/strategy-d-exe-smoke/result.json). 루트/release Stock.exe 갱신 및 이전 파일 백업; 해시/크기/백업은 artifacts/strategy-d-local-exe.json. GitHub 게시 없음. P7별도 경로 쓰기 허용받음. 아직 sibling 소스 생성 전. 주간잔여71%(직전 조회).

2026-11-08 최신: P6 UI/앱 수명 연결·관련191+WPF13 성공. 전체 회귀 실행 중(unified exec session14689, artifacts/strategy-d-regression.log; 테스트PID1644는 실행 당시 값). 최신빌드0경고0오류. 새 UI/서비스/기록·표식 설명 docs/ui/StrategyD.md. 회귀 성공 후 추가 변경 여부를 확인하고 publish+격리 EXE smoke→루트 EXE 갱신; 현재 루트 EXE는 아직 D 이전1일 기능 버전이다. P6 실제 broker 어댑터 미연결/가상 원본 구독20종목/strict 품질 차단은 명시 제한. P7 아직 생성 전, 공식 Avalonia.Desktop12.1.5 및 ScottPlot.Avalonia5.1.59(최소Avalonia12) 확인. 현재 주간잔여72%(직전 조회), 40% 이하면 기록 후 중지.

최신 P5: IntradayReplay/ClosedBarReplay + 공통 IntradayReportBuilder/IntradayBacktestService 완료. 원본 receive 순 replay와 스트리밍의 신호·체결 동일성, 다음 이벤트/다음 시가 체결, 미래/누락/불확실 자료, 손절 우선, 비용/공유현금/cache 격리 포함 새181개 성공(artifacts/strategy-d-p5.log), Release0경고0오류. 100종목10만 합성 이벤트 코어 실행 2.23→1.13초, 할당875→543MB(누적 할당), 실제 API/UI 속도 아님. P6 시작: 앱 수명 bounded DayTradingSessionService 추가 중(아직 검사 전). 현재 주간잔여75%. P7는 P6 뒤. 실제 주문 어댑터 미연결, D는 아직 제품 UI 미노출.

2026-11-07: 첨부 보존 및 문서/실제 코드 감사 중, 전략D 소스 수정 전. get_usage_limits codex의10080분 창 usedPercent16→주간잔여84%. 각 단위 완료/큰 검증 전에 재조회, 잔여40% 이하는 기록 후 중지. null은 미확인 처리. 직전1일 기능 완료 docs/verification/OneDayRange-2026-11-07.md, docs/history/*complete-OneDayRange*.md.

- [!] 실제 증권사 주문은 사용자 별도 명시 활성화 전 실행하지 않는다.
- [!] 이전 DB 최초 손상 사건 미확정. 보존 원본/승인 복구 docs/maintenance/Storage.md.
P1 완료: DayTradingConfiguration.cs/StrategyDefinition.cs/DayTradingConfigurationTests.cs/Program.cs. 별도 schema2 + strategy-d-intraday-v1, 기존 schema1/61개 roundtrip·SQLite 저장·정밀도·검증 39개 성공, Release0경고0오류. 새 설정은 아직 제품 UI에 노출하지 않아 구 엔진으로 오실행되지 않음. 다음 P2 공통 데이터/지표 구현. 기존 엔진 소스 baseline 해시는 유지.
P2/P3 완료: CausalMarketInput/CausalBarBuilder, VolumeProfileAndSupport, StrategyDDataEngine/StrategyDSignalEngine 및 관련 검사. 설정 포함100개 성공(artifacts/strategy-d-p3.log), Release0경고0오류. 미래 일봉/당일 고가·시각 추정/체결 누락·MA-only·매물대 폭/중첩·첫틱 저점/새 상향교차·동일Zone 새episode 재진입/중복을 검증. UI/실주문에는 아직 신규 기능 노출 없음. 다음 P4: 공통 실행/가상 체결 어댑터·체결기반 포지션/위험·세션독립 청산·공유계좌 한도. 상세 docs/verification/StrategyD-Progress.md. 주간잔여81%(직전 조회).
P4 가상 실행 단위 완료: IntradayPositionRules/DayLiquidationPolicy, IDayTradingSignalRuntime/StrategyDRuntime, IDayTradingExecutionAdapter/기존PaperBroker 어댑터, DayTradingPortfolioEngine(공유계좌·확인체결·부분/분할매수·TP·원가/위험·독립Clock·멱등/미확인/대사·긴급중지·종목격리). 총145개 새 핵심 검사 성공, Release0경고0오류(artifacts/strategy-d-p4*). 실제 broker 구현/호스트UI 피드 연결은 P6까지 미노출. 실제주문 어댑터는 별도 승인과 전송 직전 재검증 계약이며 현재 실제IAccountBroker 어댑터가 연결되지 않아 실주문 없음. 다음 P5: 동일 StrategyDRuntime+PortfolioEngine의 raw replay/성과·cache·원본자료 가용성·다종목/신호재현성/성능, 이후 P6 UI. 주간잔여79%(직전 조회), 40% 이하 중지 조건 유지.
최신 추가 요청(2026-11-07): 하던 전략D 작업을 먼저 마친 뒤 stock_avalonia를 별도로 만들어 Mac 개발 가능한 Avalonia로 전환. 원본 STOCK 유지. 주간잔여40% 중지 정책은 두 작업 전체에 적용한다. 새 폴더는 STOCK의 상위 C# 폴더 아래 stock_avalonia로 가정. 해당 전환은 아직 시작하지 않았으며, 실제 macOS 실행 검증은 Mac 실행환경 가용성을 확인한 뒤 구분해 보고한다.
