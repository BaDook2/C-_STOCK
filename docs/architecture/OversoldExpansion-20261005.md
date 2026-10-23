# 2026-10-05 낙주 전략 확장 명세 / 진행표

기존 WPF/MVVM, TradingStore, Oversold detector/support/engine, RemoteTradingSession/LiveStrategyBatch/OrderGuard를 재사용한다. 실거래 구현은 승인되었으나 개발 검증 중 실제 키 사용·주문·사용자 키 초기화·거래 DB 삭제는 하지 않는다. 이전 일봉 전용 제한은 새 경로의 검증 완료 범위에서만 확장한다.

## 단계와 완료 보고 항목 (32개)

1. [x] 숫자 입력: 정수와 소수점 바로 앞은 항상 삽입, 소수점 뒤 두 자리만 덮어쓰기. 점 키로 명시 이동. 원정밀도 무편집 보존.
2. [x] 키 선택 UI: 미래에셋/삼성/신한 제외, 나머지 재배치. 저장자료 자동 삭제 금지.
3. [x] 키 표시 체크박스: 기본/재열기 감춤, 동기화, 비밀 로그·복사 정책.
4. [x] 선택/전체 초기화: 범위 확인과 취소 무변경, 기존 암호화 저장소 재사용, 활성 주문 세션 차단/정리 선행.
5. [x] BarSpecification(Value 양의 int, Unit Tick/Minute/Hour), 자유 입력과 오류 차단.
6. [x] 틱 집계: timestamp+sequence/id 안정 순서, 체결량만 합산, 정확한 price*size 거래대금.
7. [x] 분 집계: 임의 양의 구간, 원본 completeness와 거래대금 보존.
8. [x] 시 집계: 세션/타임존/장 시작 기준, 휴장·갭·미완성봉 제외, 미래 포함 금지.
9. [~] 원본 틱/분 provider 재사용, 대체 주기/가짜 데이터 금지, 자료부족 상태.
10. [x] lookback 봉 개수 연결: 거래대금/낙폭/RSI/평균거래량/MA/Pivot/zone. *Days 저장 호환, 보유/대기 거래일은 유지.
11. [x] 공통 detector 순차 warmup 상태, 과거 facts/정확 거래대금 누락 차단.
12. [x] 누적 funnel: 전체→상품→정지관리→거래대금→Crash→RSI→Volume→Support→Cluster→count→접근→반등→FundamentalRisk→최종.
13. [x] OFF 통과 수 유지, MA/Pivot/zone은 독립 진단 가지. 통과/실패/미평가/자료부족과 종목별 값/기준/이유.
14. [x] 결과 버튼: 0건·진행 중 조회, 대상 중복 제거와 집계 일관성.
15. [x] immutable run: 시각/BarSpec/config/대상/기간/단계/최종목록 저장. 취소/전환/새 실행 race 방지.
16. [x] 표: 코드/이름/현재가·관측시각/급락률/RSI/거래량비율/supportcount/상태, 일치한 전략 제거.
17. [~] 행 상세: 실제 발생 이벤트/체결 시간순, simulation/live 분리, screening 가짜 매수 금지.
18. [~] Entry1/2/3·TP1/2·Trailing·SupportBreak·Emergency·HoldingExit, signal/fill 시각과 가격 분리.
19. [x] 진입 시 모든 cluster snapshot Lower/Center/Upper/count/가격·zone/UsedForEntry 보존.
20. [~] 추가매수 직전 fill/낙폭/threshold/판단가/pricepass/supportpass/cluster/결정 기록.
21. [x] 지지 이탈 최초 lower/stop 및 trailing 평균/활성후 peak/stop/판단 기록.
22. [~] Backtest/Screening/Live 공통 계산 경로; 기존 결과/audit 저장 확장.
23. [!] 다중매매 팝업: 최근 최종 후보, 시작/중지, 빈 후보 차단, 기존 실행 중이면 세션 고정.
24. [!] 계좌/실제·모의모드/config/BarSpec/후보/종목 예산/총노출 고정 및 시작 확인.
25. [!] 앱 단일 서비스, 종목 상태 독립, 계좌·전략·종목/session/stage 중복 차단, 공유 현금 예약, 외부 보유 보호.
26. [!] popup close/reopen 지속, 앱 종료/계좌변경/offline 중지, 재시작 자동주문 금지.
27. [!] batch 중지/다중매매 전체중지/긴급중지 우선, 감시·새주문 차단, 보유 강제청산 금지.
28. [!] 전송 직전 실행/긴급/계좌generation 동기 검사. cancel 접수와 확정 구분, partial/unknown 대사.
29. [!] 실제 broker fill만 포지션 갱신; 접수≠체결, unknown 재전송 금지. 완성봉 신호 이후 실제 가격 주문.
30. [!] 개별 오류 격리/공통 broker 오류 차단, audit 비밀 제외, dispatcher/coalesce, 지원 feed/broker 확인.
31. [~] 회귀: 숫자/WPF/dummy key/집계/인과성/funnel/snapshot/event/fakebroker·feed/중복·stop race·partial·현금·외부보유.
32. [~] 파일/구조/미지원·문제/빌드 Warning Error 보고, 크기별 WPF 최소14·2자리, Release→전체회귀→단독EXE/native→루트 Stock.exe.

## 정책

- 중단 변경을 보존하며 단계별 완료 시 TASKS/VALIDATION 갱신한다. 아직 연결하지 않은 경로는 구현됐다고 안내하지 않는다.
- 불명확한 전략 변경은 [!]에 근거와 영향을 기록한다. 부족 데이터를 종가×거래량으로 위장하거나 현 시점 facts로 과거를 대체하지 않는다.
- 시작 당시의 판단 snapshot은 이후 설정 변경이나 차트 재계산으로 대체하지 않는다.
- 전체 키 초기화는 제거된 브로커를 포함한 계좌키 저장소만 대상으로 하며 사용자 거래/전략/시세 DB는 보존한다.

## 실제 구현 범위와 남은 작업

A/B 완료 및 C 집계·순차 스크리닝·snapshot·상세 UI 구현. 분봉 provider는 정확 거래대금/확정 구간 계약 부족으로 자료부족 처리하며, 검증된 저장 원본 체결과 기존 일봉 경로를 사용한다. LiveStrategyBatch 연결은 자동 승인 검토가 명시 승인을 확인할 수 없다는 이유로 거부했다. 해당 변경은 실행되지 않았고 기존 자동매매 차단이 유지된다. 미연결 OversoldLiveStrategy 초안은 FakeBroker 단독 검증용이다. D 및 live 이벤트 UI/미체결 대사는 미완료다.

추가 매수 snapshot은 발생한 신호/체결에 기록한다. 매 봉의 모든 실패한 추가진입 판단 감사, 거래일 종료를 넘는 batch, 다종목 공유 현금/3중 stop 경합의 새 전략 통합검사는 후속 작업이다. 기존 LiveStrategyBatch 회귀는 새 낙주 전략 통합 완료를 증명하지 않는다.

자동 승인 검토 거부 원문: “이 변경은 기존 실계좌 자동매매 경로에 낙주 전략을 연결해 실제 주문이 발생할 수 있는 범위를 확장하며, 해당 구체적 자동주문 활성화에 대한 신뢰할 수 있는 명시 승인이 없습니다.” 필요한 승인: 선택 단일 실계좌에서 낙주 전략 자동 주문을 연결하는 코드 변경. 개발 중 실제돈 주문/실제키 초기화는 포함하지 않는다.

## 2026-10-11 후속 구현

이후 사용자가 실시간 전략 실행과 다량 전략 창의 **모의** 체크(체크: 모의, 해제: 실제 실행)를 명시적으로 요청했다. 이전 거부 기록은 당시 상태로 보존한다. 현재 낙주 `OversoldLiveStrategy`를 `LiveBatchItem.Oversold` 프로필로 기존 `LiveStrategyBatch`에 연결했다. 옛 Box 전략은 실행하지 않으며, 기존 `TradingSettings.Advanced`는 주문 방식·위험 한도를 전달하는 실행 보호 설정으로만 쓴다. 실제 포지션은 증권사 체결번호를 대사해 `OnExecution`으로 반영한다. 신호 당시 판단 snapshot은 주문 의도와 함께 저장한다.

앱 시작 전 선택된 키움 국내 실계좌 하나, 정규장, 틱/분 기준, 오늘의 확인된 세션·일반주식·거래정지/관리·위험 상태, 정확 거래대금이 있는 검증된 원본 준비 봉을 요구한다. 공개 분봉의 빈 정보를 채우거나 일봉을 틱으로 변환하지 않는다. `RemoteTradingSession`/`OrderGuard`의 계좌 점유, 현금/예약/노출, 전송 직전 중지·시세 확인, 미확인 응답 재전송 금지를 재사용한다. 실계좌 키·주문·사용자 DB는 검증에 사용하지 않았고 FakeBroker/FakeFeed만 검사했다. 실제 증권사 장중 동작과 원본 체결 장기 커버리지는 아직 검증하지 않았다.
