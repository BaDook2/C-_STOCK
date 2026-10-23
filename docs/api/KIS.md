# 한국투자증권 KIS

공식 기준: https://github.com/koreainvestment/open-trading-api 의 examples_llm/domestic_stock (2026-09-28 확인), https://apiportal.koreainvestment.com/intro.

기본 생성자는 모의 호스트 `https://openapivts.koreainvestment.com:29443`. live:true일 때만 `https://openapi.koreainvestment.com:9443`. 고정 호스트·리디렉션 금지. POST /oauth2/tokenP에 client_credentials/appkey/appsecret. 데이터 요청은 Bearer, appkey, appsecret, tr_id, custtype=P. CANO 8자리, ACNT_PRDT_CD 2자리. UI는 잔고 조회까지 성공해야 키를 저장한다.

| 용도 | /uapi/domestic-stock/v1/ 아래 경로 | 실전 TR |
|---|---|---|
| 잔고 | trading/inquire-balance | TTTC8434R |
| 당일 주문 | trading/inquire-daily-ccld | TTTC0081R |
| 매수/매도 | trading/order-cash | TTTC0012U / TTTC0011U |
| 취소 | trading/order-rvsecncl | TTTC0013U |
| 현재가 | quotations/inquire-price | FHKST01010100 |

시장가 ORD_DVSN=01, ORD_UNPR=0. 취소는 원주문과 조회한 주문지점으로 요청. 모의 TR의 V 접두사는 보존한다. FK100/NK100·tr_cont 연속조회, 반복 키 및 100페이지 한도는 오류. 650ms 요청 간격, 유효 토큰 재사용, POST 자동 재시도 없음. 원문 응답·키는 로그에 기록하지 않는다. HTTP·응답 유실은 Unknown 저널로 신규 주문을 차단한다. 토큰 발급 빈도 제한으로 짧은 간격 재연결이 거절될 수 있다.

KIS 해외주식·WebSocket은 현재 앱에서 미구현. 해외 종목 주문은 국내 API 전송 전에 차단한다. 공식 미국 매수 예제에는 일반 시장가 코드가 없어 기존 시장가 버튼에 지정가를 임의 대입하지 않았다.
KisBroker.cs, RemoteTradingSession.cs, ConnectionTests.cs. 가짜 HTTP로 호스트·TR·시장가·취소·인증 거부·해외 차단 검사. 실제 키·주문 검증은 수행하지 않았다.
