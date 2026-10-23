# 메리츠증권 Open API

공식 기준: https://github.com/meritz-securities/open-api 의 meritz/data/catalog.json, meritz/core/client.py, examples/04_account.py, examples/05_order.py (2026-09-28 확인). 포털 https://openapi.imeritz.com.

운영 호스트 `https://openapi.imeritz.com:9443`. POST /oauth2/token에 form-urlencoded grant_type=client_credentials, client_id, client_secret, scope=login. 유효 토큰 재사용. 이후 Bearer와 실제 접속 장치 mac_address 12자리.
App Key에 계좌가 연결되므로 acno/asno/비밀번호를 보내지 않는다. 계좌번호를 반환하지 않아 저널은 App Key의 SHA256으로 연결을 식별한다. 같은 계좌 키를 재발급하면 새 식별자가 되므로 교체 전 증권사에서 미확인 주문을 확인해야 한다. 키 원문은 저널에 없다.

GET /accounts/v1/me/deposit의 tfam과 /accounts/v1/me/holdings를 읽고 종목별 잔고행을 가중평균 합산한다. 대출·신용 잔고 및 부분 응답은 거래 차단. 현금+국내 잔고 평가에 해외 자산을 섞지 않는다.
GET /trading/v1/orders/history: oder_date, deal_cls_code=0, cntg_cls_code=0, sort_stnd_code=2. rsp_cd=5762이면 tr_cont=1과 tr_cont_key를 **쿼리**로 전달하고 후행 공백을 보존한다. 반복 키·누락·100페이지 초과는 오류.
GET /market/v1/prices: mrkt_div_code=J, iscd=6자리. 반환 종목·양수 가격 검사.
POST /trading/v1/orders/buy, /sell, /cancel: A+6자리 종목, 시장가 oder_cls_code=05, oder_unpr=0, oder_cond_cls_code=0, exch_kind_code=01, 최초 warn_cnfr_yn=N. 경고 자동 확인·재전송 없음.

주문 rsp_cd=0001이어도 warn_cls_code=0과 양수 주문번호가 필요하다. 경고는 미접수. 통신 유실·파싱 불가는 Unknown 저널로 차단. 조회는 공식 성공/자료없음 코드와 필수 숫자·응답 형태를 검사한다. 공식 Python의 시세 손상 코드 허용 예외는 적용하지 않아 보수적으로 실패한다.
고정 호스트, 리디렉션 금지, 650ms 직렬 요청, POST 재시도 없음. 해외주식·WebSocket 미구현. MeritzBroker.cs 및 ConnectionTests.cs에서 가짜 HTTP로 인증·MAC·주문 경고·페이지·응답 유실 검사. 실제 계좌는 사용하지 않았다.
