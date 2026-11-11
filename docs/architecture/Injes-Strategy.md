# Inje 구조 — 2026-11-11 로컬

사용자 최신 지시는 기존 전략과 D를 제거하는 것이다. 원래 첨부의 D 보존 조항보다 우선한다. 현재 정의/파라미터/실행은 `Stock.Core/Strategies/Injes`에 있으며 두 UI가 같은 Core를 사용한다. GitHub 업로드는 금지되어 있다.

## 공통 책임

| 파일 | 책임 |
|---|---|
| Parameters / Definition | 공통14개·전용10/12/11/13개, 타입/범위, Standard6개 매핑, family별 등록, strict JSON |
| EditorModel / PresetStore | UI 중립 필드, 소수 정밀도·정수 검증, 모드 변경·Advanced 원본 유지, 네 기본 프리셋·이름변경 |
| MarketFeatures | 시장/수신/KnownAt·source/sequence, US 단계/DST, 중복/역순/지연 거절, 증분 관측 창, 같은 경과시간 RVOL |
| StrategyRuntime / SupportZones | 독립 네 상태 기계. 완료·가용 일봉, 확정 Pivot, 거래량 보존 Bin, 독립 근거/군집·진입 snapshot |
| Execution | IStrategyExecutionAdapter 공유, 기존 PaperBroker 원장, 진입 승인과 주문/체결 분리, 공통 보호·부분체결·취소·대사 |
| Replay / ResearchBridge | 같은 세션의 가용시각 순 리플레이·주문 없는 스크리닝, 기존 표/차트 DTO로 투영 |
| StreamService | 앱 소유의 bounded 원본 스트림·독립 시계, 상태만 저장하는 저널, 복원/보호 재개·종료 처리 |
| TradingSettingsAdapter | 기존 계좌/목록 DTO의 transport 연결. 구전략 자동 실행을 활성화하지 않음 |

신규 family는 별도 체계를 가져야 한다. Bin은 `Stock.Core/Strategies/Bins`에 독립 Core로 추가했으며 Inje registry/UI에 혼합하지 않는다. unknown family/ID를 Inje로 자동 해석하지 않는다. 신규 Inje 알고리즘은 IStrategyFactory/IStrategyRuntime 등록으로 추가한다. 주문 어댑터·계좌 원장·UI 이벤트에 새로운 알파 조건을 중복 구현하지 않는다.

## 인과성과 원장

`MarketEvent.StrategyReference / TradingSession / AvailableAt`은 공급자가 확인한 당시 자료 계약이다. 뉴스 부재·거래정지 미확인·권한 미확인을 safe/0으로 바꾸지 않는다. US 달력 계산은 providerConfirmed가 기본 false다. 허용 주문 유형도 공급 자료로 확인한다. L2는 깊이·정렬·L1 일치를 확인한다.

관측 창은 선언한 기간별 queue·누적량·고저가 deque를 사용한다. RVOL 과거 자료는 단계/경과초별 인덱스를 사용하고 같은 날짜를 여러 세션으로 중복 계산하지 않는다. 지지 자료는 일봉 배열/유효 틱/새 가용시각이 변할 때 재계산한다. 데이터 공백·원천/세션 변경 시 후보와 관측 창을 다시 준비한다.

수신시각이 같은 이벤트는 TradingStore.ReadEventsInArrivalOrder로 원래 삽입 순서를 보존한다. 기존 일반 시세 조회의 거래소시각 정렬과 분리한다. 원본 틱과 재조회 캐시는 프로세스 메모리에 두는 기존 정책을 유지한다. 주문/체결·설정·판단 근거·필요한 연구 차트 snapshot은 DB에 보관한다.

InjesSession은 고정된 정의를 snapshot한다. Enter와 RiskApproved와 OrderAllowed를 분리하며, 체결 전 보유로 바꾸지 않는다. 공통 손절·시간청산은 신규 진입 필터 탈락과 독립이다. 계좌 노출·위험예산·보유 자리는 미체결 매수도 예약한다. 개별 중지는 다른 종목의 새 진입을 막지 않고 자신의 보유 보호를 유지한다. Unknown 주문은 대사 전 재무장하지 않는다.

PaperBroker는 수동적 지정가의 queue를 모른다. 같은 이벤트/호가 터치/같은 가격 체결로 채우지 않고 후속 엄격 통과 체결량의 참여율로만 채운다. 취소 요청과 확인을 분리한다. 복원 때 현금·수량·평균가·미배분 매수비용·최초 체결시각을 체결 원장과 대조한다. 실제 계좌 adapter는 native session 생성 단계부터 거절한다. 원장 저장 실패는 신규 진입을 잠그고 미체결 매수 취소를 요청하며 메모리 원장을 보존한다. 실패를 빈 저장 계좌로 바꾸지 않고, 긴급중지/종료에서 예외로 앱을 종료시키지 않는다. 저장 실패는 명시 점검·복구 전 자동 해제하지 않는다.

## 구전략 경계

D 전용 runtime/data/signal/portfolio/replay/editor/window 소스는 삭제했다. 구형 chart/scalping/box/rebound/oversold/pipeline 주문 알고리즘도 실행 로직을 제거하고 구형 계약 호출에 명시 오류를 반환한다. 차트·수동 계좌·실제 감사 기록 역직렬화에 쓰이는 DTO/기술지표 helper는 남긴다. 구형 BarPaperAccount의 수동 주문/다음 봉 체결·평가 기능은 유지한다.

TradingStore.RetireLegacyStrategies는 앱 시작 때 한 번 구전략 프리셋/설정/연구/가상 전략 namespaces만 정리한다. 실제 runs/audit/WAL은 실제 계좌 안전 근거일 수 있어 삭제하지 않는다. 사용자 DB를 개발 도구로 직접 초기화하지 않는다. 구전략 설정 JSON을 신전략에 자동 이식하지 않는다.

전체 수식·설계 결정·실행 JSON은 [단일 전략 설명서](../strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md)에 있다.
