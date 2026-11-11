# 현재 인수인계 — Bin’s Strategy Core 완료

2026-11-11. 기반 HEAD `ca8ea4a`에서 기존 Inje 미커밋 로컬 변경을 보존하며 Bin을 추가했다. GitHub commit/push/태그/Actions/Release 없음. 실제 계좌 자동주문·실제 거래기록 삭제·사용자DB/키 초기화·설치 앱 교체 없음. 사용자 새 요청은 이전 Inje의06:00 작업 종료 후 들어온 별도 구현 요청이다. 완료 상태에서 자동으로 범위 밖 작업을 이어가지 않는다.

## 구현 진입점

`Stock.Core/Strategies/Bins`의9개 파일: Configuration,MarketData,Indicators,SupportActivation,EntryPatterns,PositionRisk,Execution,BinsStrategyEngine,Replay. 전략ID=bins-strategy-v1, 세가족·5패턴·4분류·Fast/Confirmed2모드. 기존 D/과매도 알고리즘이나 Inje factory로 대체하지 않는다. Inje/Core/UI 소스는 이번 Bin 작업에서 삭제·재작성하지 않았다.

`BinsStrategyConfig`는 init record이며 엔진 시작/새 설정/진입/포지션에 깊은 사본을 사용한다. 전체 camelCase strict JSON, 중복/누락/unknown/schema/ID 거절, SHA256,173개 메타데이터를 제공한다. DurationSpec.Day=24시간; BarTimeframeSpec.TradingDay=N개 공급자 확인 거래일 봉이다. 금액과 비용표는 기본 미설정으로 진입 차단한다. 숨은 자동 수치 보정 없음.

`BinsStrategyEngine`은 계좌 단일 lock으로 원본 시장 이벤트와 OnExecutionReport를 직렬화한다. LoadCompletedBar/LoadProfileTrade는 가용시각 순 입력이며 이후 과거로 시계를 되돌릴 수 없다. 신규 진입/청산은 체결, 분류는 확정봉이다. 원본 없는 OHLC 연구는 UnsupportedPrecision이다. 증분 EMA/ATR은 버퍼 축소 때 재seed하지 않는다.

Stage1 OFF는 MA240을 요구하지 않는다. ON은 실제 지지 접근·새 반등 교차, 독립 에피소드만30분 갱신, 잠긴 하단 붕괴·만료 시 허가 취소다. 보유 청산과 독립이다. 박스도 잠기며 보유 중 붕괴를 기록한다. 소비 박스는 새 독립 반응·확인 후 새 ID로 대체한다. 아직 Range인 첫 Up 후보 전환 중에는 잠긴 RangeUpperBreakout만 예외로 허용한다.

모든 패턴은 하나의 공통 청산/재진입/위험 설정을 쓴다. 보유·미체결/Unknown 동안 가족/포지션 설정 고정, 추가매수 금지. 확인된 TP1 첫 부분체결 후 추적 활성화와 TP 목표 완료를 구분한다. 고정/기술 손절 후 긴 쿨다운은 앞선 부분익절 이익이나 가족 변경으로 줄이지 않는다. KRW/USD 위험·현금/예약·일중 카운터 분리, 한도 자리는 미체결 첫 진입도 예약한다.

`BinsPaperExecution`은 기존 IStrategyExecutionAdapter/PaperBroker를 사용한다. 새 공통 partial `Stock.Core/Trading/PaperBrokerConfirmedFill.cs`에 internal ApplySimulationFill을 추가했다. 기존 manual/Inje broker 매칭 코드는 변경하지 않았다. 신호 동일 이벤트 fill 금지, quote/엄격 통과 trade의 보수적 수량, cancel request/ack, timeout Unknown·조회/대사, broker/execution/signal ID 멱등성을 구현했다. 취소 전 실제 fill의 늦은 통지도 수량에 반영하고 잔량 Unknown을 유지한다. Live 어댑터 생성은 거절한다. 손절 지정가 미체결은 보유/UnclosedPosition으로 남기며 자동 시장가 fallback 없음.

## 검증 근거

- Bin180개, 원문 A01~E10 총53시나리오 ID 모두 포함·통과.
- 기존 Inje363개, Avalonia263개 통과. 합성/격리/가짜 broker이며 실제 API·주문0.
- STOCK.Avalonia.sln Release 및 Stock.sln Windows 교차 빌드 모두 경고0/오류0. 실제 Windows 실행은 안 했다.
- Core manifest99파일 hash/범위 일치, 단일 설명서 전체 JSON의 strict import/모든 실제 기본값 일치.
- `Stock.Bins.Tests`는 기본 Avalonia 솔루션과 verify.sh/ps1에 추가했다. 명령 `dotnet run --project Stock.Bins.Tests -c Release -- artifacts/bins-tests`.
- 로그는 저장소 밖 `../work/bins-*.log`. fixture 결과/metadata/replay는 `artifacts/bins-tests/`로 Git 제외. 실제 기대값/자료는 체크 소스에 있다.

전체 설명·22개 결정 추적표·DecisionLog·173개 파라미터 메타데이터·전체 JSON은 `docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md` 한 파일. 구조 지도는 docs/architecture/Bins-Strategy.md, 검증 근거는 docs/verification/Bins-2026-11-11.md다.

## 실제 제한·다음 요청

현재 UI/불러오기는 Inje다. 이번 요청은 Core 범위로 수행했으므로 Bin 선택기/편집 UI 연결·실제 제공자 어댑터·운영 영속화/재시작 복원·새 앱 패키징은 추가하지 않았다. 기존 ZIP은 이전 Inje 산출물이며 Bin 앱이라고 전달하지 않는다. Core API는 config.Validate→BinsWarmupPlan.For→통화별BinsPaperExecution→BinsStrategyEngine, 재생은 BinsReplayEngine이다.

제공자별 원본 틱·호가·확인 세션/휴장/조기폐장·정지/위험·가격 규칙·날짜별 비용이 없으면 진입 보류다. 미국/한국의 실제 broker 기능·대기열·시장 수익성은 미검증이다. 실제 Windows UI/EXE도 해당 플랫폼 확인이 필요하다. 실계좌·기록삭제·GitHub 게시 요청으로 해석해 자동 확장하지 않는다.

직전 Inje 완료 인수인계는 docs/history/HANDOFF-before-Bins-20261111.md다. 과거 미완료나 공개 릴리즈 이력은 이번 자동 다음 작업의 근거가 아니다.
