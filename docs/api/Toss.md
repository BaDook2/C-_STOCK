# 토스증권 Open API

## 목적과 구현 범위
실제 계좌 키 설정에 일곱 번째 증권사로 추가했다. 현재 어댑터는 국내주식 잔고·주문·취소·체결 조회와 공식 분봉 조회를 지원한다. 해외주식 및 WebSocket 체결 구독은 이번 구현에 포함하지 않는다. 선택 계좌만 요청하며 다른 저장 계좌로 주문을 배분하지 않는다.

## 인증과 계좌
고정 호스트 https://openapi.tossinvest.com 에 OAuth client_credentials를 client_id/client_secret으로 요청한다. 등록 키·허용 IP·API 이용 신청이 필요하다. 실제 계좌 목록의 BROKERAGE 계좌 중 입력 계좌번호와 일치하는 한 계좌만 선택한다. 미입력이고 계좌가 여러 개면 임의 선택하지 않는다. 이후 X-Tossinvest-Account 헤더에 선택 accountSeq를 사용한다. 연결 성공한 키는 기존 증권사별 Windows DPAPI 저장소에 저장한다.

## REST와 오류 처리
- POST /oauth2/token
- GET /api/v1/accounts
- GET /api/v1/buying-power?currency=KRW, /api/v1/holdings
- GET /api/v1/orders (미체결 및 체결 페이지 조회)
- POST /api/v1/orders, 주문 취소 경로
- 공식 1분봉 API: nextBefore 페이지를 최대 10회 읽고 시각 중복 제거 후 집계

응답 규격은 보관한 공식 OpenAPI 스펙과 대조했다. TR ID 방식은 사용하지 않는다. 호출 간 최소 650ms를 두며 이는 모든 서버별 한도를 보장한다는 뜻은 아니다. 인증·권한·한도 오류는 UI에 안내한다. 리다이렉트를 따르지 않으며 주문 POST는 자동 재시도하지 않는다. 연결 유실·해석 불가 응답은 주문 상태 불명으로 취급한다. 체결 평균가/마지막 시각은 공급된 값만 기록한다.

## 검증과 제한
가짜 HTTP 응답으로 인증 요청 형식, 복수 계좌 선택, 계좌 헤더, 국내 제한, 시장가 요청, 체결 페이지, 응답 유실 시 중복 전송 방지를 검증했다. 실제 고객 키 인증·주문·체결은 실행하지 않았다.

관련 코드: Stock.Core/TossBroker.cs, BrokerConnection.cs, Models.cs, Stock.App/ConnectionDialog.cs, WorkspaceFeatures.cs, Stock.Tests/TossBrokerTests.cs.

공식 자료: https://developers.tossinvest.com/ , https://developers.tossinvest.com/docs/market-data , https://developers.tossinvest.com/docs/trade . 스펙 원본: artifacts/api-references/toss-openapi.json 및 toss-asyncapi.json.
