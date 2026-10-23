# 실제 계좌 키 설정

## 화면 목적
여섯 증권사의 로고와 이름을 눌러 사용할 실전 계좌 한 곳을 선택한다. 저장된 다른 계좌로 주문을 전파하지 않는다.

## UI 기능
| UI | 기능 | 실행 코드 |
|---|---|---|
| 증권사 카드 | 증권사 선택과 지원 안내 | ConnectionDialog.SelectBroker |
| App Key / Secret | 선택 증권사 키 입력·복원 | BrokerCredentialStore.Load |
| 계좌번호 / 상품코드 | 한국투자 계좌 앞 8자리·상품코드 2자리 | BrokerConnection.Validate |
| MAC 주소 | 메리츠 접속 장치 12자리, 연결된 장치가 기본값 | BrokerConnection.MacAddress |
| 온라인 기본 계좌 체크 | 성공 후 다음 온라인 전환의 기본 선택 변경 | BrokerCredentialStore.DefaultBroker |
| 선택한 계좌 연결 | 실전 인증·잔고 확인 후 키 저장 | ConfigureKeys |

## 동작 과정
온라인 선택 → 기본 증권사가 선택된 창 → 사용자 선택 → 실전 인증·잔고 검증 → 활성 세션 하나 생성 → 성공 키만 Windows DPAPI CurrentUser로 암호화 저장. 오프라인에서 키를 설정하면 오프라인을 유지한다.
취소·인증 실패는 오프라인으로 복귀하고 이전 저장 키·기본 계좌를 보존한다. 저장 실패는 연결 성공과 구분해 안내한다. 기본 계좌를 바꾸려면 원하는 증권사에서 기본 계좌 체크 후 연결한다. 체크 없이 다른 계좌를 연결하면 이번 세션에서만 사용한다.
저장 위치: `%LOCALAPPDATA%/StockPaperTrading/KiwoomQuant/broker-keys`. 증권사별 한 계좌의 키를 저장하며 연결 성공 때만 교체한다. 주문 확인창에는 현재 증권사와 마스킹한 계좌 식별자를 표시한다. 메리츠는 계좌번호 대신 계좌별 API 키 해시 일부를 표시한다.

## 연결 범위
키움: 국내·미국. 한국투자·메리츠: 국내주식 인증·잔고·주문·취소. 틱 수신: 키움만 지원. 한국투자·메리츠 해외주식은 미구현이며 국내 API로 잘못 전송하지 않는다.
미래에셋 주식 FIX/API 계약 규격, 삼성 개인 주식거래 API 규격, 신한i indi 설치·로그인 연동은 추가 확인이 필요하다. 세 회사는 로고·안내만 제공하며 키를 저장하거나 연결 성공으로 표시하지 않는다.

## 관련 코드
ConnectionDialog.cs, BrokerCredentialStore.cs, WorkspaceFeatures.cs, BrokerConnection.cs, KiwoomBroker.cs, KisBroker.cs, MeritzBroker.cs.

## 2026-09-29 토스증권 추가
7개 증권사 카드 중 토스증권을 선택할 수 있다. 토스 공식 로고, App key/client ID, secret, 계좌번호를 사용한다. 한국투자 전용 상품코드는 토스 선택 때 숨긴다. 현재 토스 국내주식·분봉 범위를 표시하며 해외 주문을 차단한다. 키는 검증 성공 후 증권사별 DPAPI 저장소에 저장한다. 기본 계좌 변경과 온라인 전환은 선택한 한 계좌에만 연결한다. 실제 인증 검증은 수행하지 않았다. docs/api/Toss.md 참조.
