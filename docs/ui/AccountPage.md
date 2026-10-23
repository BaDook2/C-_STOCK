# 계좌 · 주문

## 화면 목적
현재 선택한 실제 계좌 하나의 잔고·주문 상태를 조회하고 미체결 주문을 취소한다. 저장된 다른 증권사 계좌에는 요청하지 않는다. 키움은 국내·미국, 한국투자·메리츠는 국내주식을 지원한다.

## UI 기능

| UI | 기능 | 실행 코드 |
|---|---|---|
| 계좌 새로고침 | 잔고와 오늘 주문 상태 조회 | RefreshAccount → RemoteTradingSession.Refresh |
| 선택 주문 취소 | 현재 계좌 확인 후 미체결 취소 요청 | Cancel → IAccountBroker.CancelAsync |
| 미확인 주문 외부 확인 | 증권사 앱에서 확인한 뒤 신규 주문 차단 수동 해제 | AcknowledgeReview |
| 보유 종목 표 | 수량·평균가·현재가·손익·통화 표시 | Holdings |
| 주문 표 | 접수·부분체결·완료·취소 상태 표시 | Orders / SelectedOrder |

## 동작 과정
사용자 조회 → 현재 선택한 IAccountBroker REST → 응답 검증 → 증권사 이름·마스킹 계좌·잔고·주문 표. 온라인 모드의 기본 계좌 지정과 변경은 [계좌 연결창](ConnectionDialog.md)에서 한다.
주문은 차트 탭에서 수량 입력 → 증권사 가격 자동 조회 → 실제 계좌 확인창 → 최신 가격 재검사 → 위험 검사 → write-ahead 저널 → 주문 POST.

접수는 체결 완료가 아니다. 응답 불명 주문을 자동 재전송하지 않는다. 성공한 키는 증권사별로 Windows 사용자 계정에 암호화 저장한다. 통화와 선택 시장을 합산하지 않는다. 실제 계좌 인증·체결은 개발 중 검증하지 않았다.

## 관련 코드
WorkspaceViewModel.cs, ConnectionDialog.cs, KiwoomBroker.cs, RemoteTradingSession.cs, OrderGuard.cs.
