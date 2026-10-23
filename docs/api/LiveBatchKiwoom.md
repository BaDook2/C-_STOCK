# 키움 실계좌 다중매매 데이터 계약

공식 참고: [kt00009 개별 체결](https://openapi.kiwoom.com/m/guide/apiguide/08/kt00009), [kt00007 주문 현황](https://openapi.kiwoom.com/m/guide/apiguide/08/kt00007), [kt00001 예수금](https://openapi.kiwoom.com/m/guide/apiguide/08/kt00001). 조회는 POST /api/dostk/acnt의 api-id로 구분한다. 어댑터 내부 650ms 요청 간격을 유지하고 주문 POST를 자동 재시도하지 않는다.

kt00009: ord_dt, stk_bond_tp=1, mrkt_tp=0, sell_tp=0, qry_tp=1, stk_cd 빈값, fr_ord_no 빈값, dmst_stex_tp=KRX. acnt_ord_cntr_prst_array에서 ord_no/cntr_no/cntr_qty/cntr_uv/cntr_tm/stk_cd/io_tp_nm을 읽는다. 날짜+주문+체결번호 중복을 제거하고 내용 변경·가격 누락·잘못된 수량·시각은 실패 처리한다. 체결시각 HH:mm:ss는 한국시간이다.

kt00001의 entr는 예수금이고 100stk_ord_alow_amt는 100% 현금 주문가능금액이다. 후자가 없으면 자동 실행을 차단한다. 계좌 스냅샷 재조회 후 현금·미체결 예약·종목/전체 예산·보유수량·일/건별 손실을 검사한다. 브로커가 이미 예약을 차감한 금액에 로컬 예약을 추가 반영하므로 보수적으로 제한될 수 있다.

체결은 3초 이상 간격의 reconciliation에서 확인한다(실제 요청 지연만큼 더 늦어질 수 있음). 주문 접수 자체를 체결로 만들지 않고, 가격·수량을 시세나 주문가격에서 추정하지 않는다. 취소 응답도 취소 완료로 보지 않는다. 원주문 체결 누계와 개별 체결 합계가 맞지 않으면 기다린다.

실시간 자료는 기존 KIWOOM WebSocket 체결·호가를 사용한다. 자동 주문은 한국시간 평일 09:00~15:20 연속매매 구간과 최신 실제 체결·호가가 모두 있을 때만 허용한다. 피드 출처·거래/수신 시각을 검사하며 최대 10초 또는 더 짧은 사용자 stale 기준을 적용한다. 거래소 휴일 캘린더/종목별 VI·특별 정지 스케줄 전용 어댑터는 미구현이다. 휴일에는 최신 실제 체결 요건을 충족하지 못한다. 브로커 거절은 그대로 표시한다. 장중 체결 보장/지연 없는 실행을 주장하지 않는다.

필요 자료가 없는 전략을 임의 완화하지 않는다. 해외 feed 시각 및 주문별 체결 대사, 다른 증권사 자동 feed 어댑터, 검색순위·뉴스 provider의 실전 자동 주문 통합은 후속 범위다. 수동 계좌 연결 기능과 자동매매 가능 여부는 다르다.

관련: Stock.Core/KiwoomBroker.cs, BrokerConnection.cs, Trading/LiveStrategyBatch.cs, RemoteTradingSession.cs, OrderGuard.cs. 실키·실주문을 사용하지 않는 LiveExecutionApiTests/LiveBatchTests/MultiTradeUiTests가 계약과 상태전이를 검사한다.
