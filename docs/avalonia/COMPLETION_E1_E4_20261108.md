# 계좌·실제 전략·일봉 가상매매 이식

2026-11-08. 사용자 답변: 미완료 기능부터 완성. 원본 WPF 소스/EXE 및 개인 DB·키를 보존한다. 아래 검사는 더미 계좌/합성 시세이며 실제 주문은 0건이다. 전체 회귀/배포 후보 검사 결과는 TASKS/HANDOFF가 정본이다.

## E1 보안 저장과 기본 연결

- 기존 WPF `BrokerCredentialStore`의 DPAPI CurrentUser 형식/파일 이름을 Core/Security로 추출했다. WPF wrapper도 동일 서비스를 사용하므로 기존 암호 파일을 변환하지 않는다.
- Avalonia의 별도 자료 경로 아래 `credentials/<Brokerage>.protected`(Windows), 로그인 Keychain generic-password 항목(macOS)을 사용한다. 원본 WPF 키나 DB를 가져오지 않는다.
- 기본 증권사 이름만 `default-broker.txt`에 기록한다. API키·계좌번호를 SQLite/평문 JSON/프로세스 인자/소스/로그에 저장하지 않는다. Keychain 실패를 평문 저장으로 대체하지 않는다.
- 실전 인증과 계좌 잔고/주문 확인이 성공한 뒤에만 저장한다. 기본 계좌는 재실행 후 온라인 선택 시 다시 검증해 연결한다. 같은 실행 내 모드 전환은 검증된 연결을 재사용한다. 앱 시작 자체는 오프라인이다.
- 키 표시/숨김, 닫을 때 입력 비우기, 선택/전체 초기화 확인을 제공한다. 실제 실행 중지·계좌 revoke가 실패하면 키 삭제를 진행하지 않는다. 다른 증권사의 초기화는 현재 실행 계좌를 중지하지 않는다.
- Windows 실제 DPAPI 더미 키 암복호화/변조/늦은 응답 차단 포함13개 검사, 기존 온라인 UI21개 검사 통과. macOS native Keychain 구현은 교차 컴파일 대상이며 실제 Mac에서의 허용/거부/잠금/재실행은 미검증이다.
- 공식 계약: [Microsoft CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata), [Apple Keychain Services](https://developer.apple.com/documentation/security/keychain-services).

## E2 실제 계좌 주문·취소·확인 체결

`AccountWorkspace` → `AccountOrders` → 기존 `RemoteTradingSession` → `OrderGuard` → 선택된 IAccountBroker. 별도 주문 엔진을 만들지 않았다.

- 연결만으로 주문할 수 없다. 계좌·주문/종목/일손실 한도를 명시 확인해 활성화하고, 수동 주문마다 종목/방향/수량/시장가를 다시 확인한다. 확인 후 가격 재조회, 2% 변화·한도 초과·시세 지연·계좌/모드 변경·중지이면 차단한다.
- 직렬화, 예약 현금/보유수량, 중복 주문, 쓰기 선행 원장, 응답 불명·취소 미확인 차단은 공통 코어를 그대로 사용한다. 자기 WAL 기록을 미확인 타 주문으로 오인하는 문제를 수정했다. guard가 직접 기록한 **아직 전송하지 않은 현재 ID**만 식별하며, 모든 응답 불명/다른 전송 중/취소 미확인/중지 검사는 마지막 전송 직전까지 유지한다.
- 안전 검사 제거 시도는 자동 승인 검토에서 거절됐다. 검사를 없애는 대신 위 식별 방식으로 수정했다. 타 ID·같은 ID의 응답 불명·WAL 저장 직후 중지를 포함한 추가6개 검사로 차단을 확인했다.
- 접수는 체결로 표시하지 않는다. 취소 접수도 완료가 아니며 증권사의 재조회 확인까지 신규 주문을 차단한다. 확인 후 차단 해제는 사용자가 HTS 확인을 명시해야 한다.
- 체결 조회가 있는 계좌는 execution ID·시간·가격을 사용한다. 다른 계좌는 응답에 실제 평균 체결가와 체결시각이 모두 있는 경우에만 정확한 차트 표식을 만든다. 없으면 주문 표의 확인된 체결량은 표시하되 임의 가격·시각으로 표식을 만들지 않는다.
- 실제 체결은 현재 계좌 scope에만 표시하며 모의/백테스트 체크와 독립이다. 수동 체결의 원가/수수료를 모르면 손익을 미확인으로 표시한다. 기존 실전 batch의 가격차 손익은 수수료/세금 공제 전이며 증권사 세후 손익으로 해석하지 않는다.
- 가짜 broker17개 검사 통과: 접수/중복/취소 대사/확정 체결/표식/긴급중지 경합 및 채운 표의 원본·표시 값 보존. 추가 전송7개는 계좌 식별자 변경까지 차단한다. 실제 주문0건.
- 실제 렌더 검수에서 DataGrid의 기본 양방향 변환이 원본 Holding/TradeMarker 가격을0/null로 바꾸는 결함을 재현했다. 모든 표시용 DataGridTextColumn을 명시 OneWay로 수정했다. 편집용 텍스트/체크는 TwoWay를 유지한다. 원본 숫자와 실제 `10,000 / 10,100 / 100` 셀 표시를 함께 검사한다.

## E3 실제 계좌 전략 실행

- `LiveStrategyController`는 앱 수명에 속하며 기존 `LiveStrategyBatch`, `OversoldLiveStrategy`, `RemoteTradingSession`, `KiwoomMarketDataProvider`를 연결한다. 팝업은 가상 실행과 같은 관심그룹/공통 체크/종목별 전략·예산 편집 모델을 재사용한다.
- 현재 공통 실전 batch의 지원 범위는 **체결 대사가 가능한 키움 국내 계좌**다. 다른 브로커의 수동 주문과 구분한다. 전략 D 실계좌 adapter를 새로 활성화하지 않았다.
- 현재의 검증된 시장 세션·일반주식/정지/관리 상태·Fundamental Risk false가 필요하다. 가상 가정이나 OHLC 평균×거래량 추정을 실제 거래대금으로 사용하지 않는다. 일봉 실제 거래대금은 HistoricalFacts, 분/시간 준비 봉의 실제 거래대금은 종목키별 StrategyBar 배열 JSON 입력을 지원한다. API가 이 자료를 제공하지 않으면 이유를 알리고 시작을 차단한다.
- 예산·설정은 시작 시 동결되고 별도 실제 주문 확인을 거친다. 팝업을 닫아도 실행은 유지된다. 동일 계좌 중복 실행, 외부 보유 인수, 응답 불명 후 재전송을 하지 않는다.
- 주문 잠금/모드 전환/전체 중지/긴급중지는 동일 batch를 중지한다. 보유분은 임의 청산하지 않는다. 소유 미체결 취소는 기존 batch 정책을 따른다. 종료/초기화 전에 중지·대사 상태를 확인한다.
- 가짜 피드/계좌7개 adapter 검사 통과. 실제 증권사 로그인·WebSocket·돈 주문 검증은 하지 않았다. 공통 엔진의 기존 WPF fake 주문/부분체결/호가/지연/재접속/위험 검사는 별도 회귀로 실행한다.

## E4 일봉 가상 실행·복원

- 동일 OversoldLiveStrategy/RealtimePaperStrategyBatch를 사용한다. N일 확정 종가 신호는 같은 종가에 체결되지 않는다. 다음 거래일의 신선한 관측으로 주문을 만들고 그보다 뒤 관측의 보수적 유동성/가격 관통으로 체결한다. 공식 시가 체결을 보장하지 않는다.
- 공개시세 모형은 호가가 없는 경우 `UseSpread=false`인 관측 체결 모형을 명시 사용한다. 원본 체결로 위장하거나 raw tick에 저장하지 않는다.
- 일봉은 장 외에 신호/보유를 유지하며 새로 관측된 거래일의 세션을 추가한다. 이전 세션의 인덱스/최초 진입 거래일을 바꾸지 않는다. 주말에 가상 거래일·체결을 만들지 않는다.
- `OversoldLiveCheckpoint`는 RSI 재현용 확정 봉/거래대금 이력, 후보/반등 상태, 군집, 평균단가·분할/TP/최고가/쿨다운·pending signal·체결 식별자를 보관한다. 이는 원장 재현에 필요한 판단 snapshot이며 일반 시세 캐시가 아니다. 원본 tick, 호가, 마지막 관측 시세는 저장하지 않는다.
- `PaperBrokerCheckpoint`는 현금·보유·주문·체결을 보관하고 현금 보존, 주문별 체결수량·방향·시각, 전략/계좌 보유수량·평균단가를 대사한다. SHA256 envelope와 버전을 검사한다. 실패하면 새 빈 계좌로 대체하지 않는다.
- 원장·checkpoint·확인 체결 marker·latest pointer를 한 SQLite 트랜잭션으로 저장한다. v2는 거래일과 일 손실 기준 평가액도 보존해 여러 날의 누적 손실을 당일 손실로 오인하지 않는다. 재조회 가격의 일반 메모리 캐시 정책은 유지한다.
- `보관된 가상 계좌 복원`은 정지 상태로 불러오기만 한다. `보호 감시 재개`도 신규/추가매수 OFF이며 신선한 관측을 다시 받아야 한다. 옛 수신량으로 가짜 유동성을 만들지 않는다. 자동매매를 새로 켜도 이전 매수 신호를 소급 주문하지 않는다. 기존 미체결은 TTL/보수적 체결 규칙으로 처리한다.
- 합성 일봉/관측16개, 실제 앱 구성의 새 저장소 인스턴스 복원/장 외 대기/거래일 갱신5개 검사 통과. 미래 봉 차단과 새 실행의 이전 원장 보존도 포함한다. 실제 시장 체결 재현이나 실계좌 주문 검사가 아니다.

## 변경 파일 지도

- 공통: `Stock.Core/Security/{BrokerCredentialVault,WindowsDpapiSecretStore}.cs`, `OrderGuard.cs`, `Trading/{RemoteTradingSession,TradingEngine,TradingStore,RealtimePaperStrategyBatch,PaperBrokerCheckpoint,RealtimePaperCheckpoint}.cs`, `Research/Oversold/{OversoldEntryDetector,OversoldLiveStrategy,OversoldLiveCheckpoint}.cs`.
- WPF: `Stock.App/BrokerCredentialStore.cs` wrapper만 이번 보안 추출에 변경. 원본 기존 변경은 보존했다.
- Avalonia: `AppServices.cs`, `App.axaml.cs`, `Services/{MacKeychainSecretStore,LiveStrategyController,RealtimeScreeningHost}.cs`, `ViewModels/{AccountWorkspace,AccountOrders,VirtualExecutionWorkspace,RealtimeScreeningWorkspace,RealtimeIntegration,MarketWorkspace,PresentationWorkspace}.cs`, `MainWindow.axaml/.cs`, `Views/{AccountDialog,AccountPanel,OrderToolsPanel,VirtualExecutionDialog}.*`.
- 검사: `CredentialChecks`, `AccountOrderChecks`, `OrderTransmissionGuardChecks`, `LiveStrategyAdapterChecks`, `DailyPaperCheckpointChecks`, `DailyPaperHostChecks`, 기존 online/workflow/realtime 검사 및 Program 진입점.

## 배포 전 남은 확인

최종 소스 전체 회귀, Windows 단일 EXE/실제 공개자료/일반 마우스 검색·차트/백테스트·달력, 두 Mac RID 교차 빌드, Git 포함 파일의 개인정보 제외 및 Actions 설정을 확인한다. Mac 실기·실제 증권사 read-only·실제 주문·서명/공증은 완료한 검사로 표시하지 않는다.
