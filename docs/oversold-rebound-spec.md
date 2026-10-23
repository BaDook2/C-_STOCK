# 국내 과매도·낙주 반등 — 승인 범위와 구현 정책

2026-10-03 사용자 승인: 기존 C#/WPF/MVVM, PriceBar, ParameterMetadata, TradingStore SQLite, 테스트 기록을 재사용하는 **일봉 OHLCV 백테스트 전용** 전략. 실계좌 다중매매 자동 활성화 및 개발 중 실제 주문은 금지. 선행 다중매매/UI 배포를 먼저 완료한다.

## 필수 요구사항
1. 당시 KOSPI/KOSDAQ 일반주식만. ETF/ETN/SPAC/거래정지/관리 제외. 이전 20일 실제 거래대금 평균 최소 3,000,000,000원. 현재 분류 소급 금지. 실제 거래대금·과거 상태·위험 정보 없으면 미지/진입 보류. 종가×거래량을 실제 거래대금으로 위장하지 않는다.
2. 최근 20봉 HIGH 최대 대비 현재 CLOSE -20% 또는 최근 3봉 HIGH 최대 대비 CLOSE -15%. 현재 확정봉 포함. 3일 전 종가 기준으로 바꾸지 않는다.
3. Wilder RSI14<=30, 현재 거래량/이전 20봉 평균>=2.0, 각 독립 ON/OFF; 활성 조건 전부 AND.
4. 지지 provider: MA120/240/480/999(준비 전 제외), 엄격 PivotHigh Left20/Right20(오른쪽 봉 도착 후 확정, 발생/확정일 별도, 여러 과거 후보), ApproximateVolumeZone. breakout 조건 추가 금지.
5. 근사 거래량 구간은 일봉 Low~High에 걸치는 bin에 당일 거래량 균등 분배한다. 실제 volume profile이 아니다. 과거 lookback만 누적, 높은 bin 연속 구간/가까운 구간 병합. 총량 보존·Low=High·경계 검사.
6. 후보 대표가격 오름차순 인접 거리<=5%면 연결식 cluster. 전체 폭 5% 제한 아님. ID 중복 제거, Lower/Center/Upper/Count/Types/Supports 보존, 최소2개. 확장된 [Lower×0.97, Upper×1.03] 안이면 접근.
7. 기본 진입=Crash+활성필터+접근+시점별 FundamentalRisk=false. 반등확인 기본 OFF; ON이면 최초 유효 cluster 접근 뒤 형성된 Low 대비 +3%. crash 이후 low와 혼동 금지. cluster변경/후보수명 관리.
8. N 확정데이터 신호 → N+1 이후 첫 거래 가능일 Open 체결. SignalTime/ExecutionTime 분리. 일봉 Time의 종가시각을 시가로 쓰지 않는다. 당일 최종 거래량으로 시가 주문 수량 제한 금지.
9. 최대3회 예정예산40/30/30. 추가 매수는 직전 실제 매수가 대비 현재 CLOSE<=-5% AND cluster접근(기본 required) AND 위험 false. 체결 때만 횟수 증가. 정수주식·비용·현금·갭·미체결 명시.
10. 최초 진입 근거 cluster LOWER×0.97 이하 CLOSE면 청산. 추가진입 뒤에도 최초 cluster snapshot 유지(손절 늦추지 않음).
11. 비상손절 옵션: 평균 매수가 -15%.
12. TP1 +10% 30%, TP2 +20% 30%, 독립 ON/OFF. 첫 실제 TP 시점까지의 최대 실제 보유 수량을 기준으로 고정. 익절 후 추가매수 중단. 트레일 옵션7%, 활성 시점/최고가 기준 Config.
13. STOP 우선. OHLC 감지 신호도 다음 거래 가능 Open 체결. 당일 HIGH로 trailing 올린 뒤 같은 날 LOW가 나중이라고 확정하지 않음. 이전 확정 최고가 기준 stop 평가 후 당일 high 갱신.
14. 첫 매수 후 시장 거래일20일 최대보유. 재진입 대기5일 AND 새 cluster center가 직전 근거 center보다5% 이상 낮고 모든 조건 재충족. 기본은 cooldown 예외 없음; 명시 옵션으로만 허용.
15. FundamentalRiskRecord Symbol/Date/IsRisk/KnownAt. 시점별 입력·저장. 전역 bool 및 자동 뉴스 추정 금지.
16. 중앙 PerformanceCalculator: 완결 거래수, 승률, 평균 수익/손실률, 평균 보유기간, PF, Expectancy, CAGR, MDD, Sharpe. 비용 포함 일별 평가곡선. 미청산은 완결 거래 통계에서 제외하되 평가자산에 포함. 다종목 독립 결과는 공유자본 포트폴리오가 아니다.
17. 상세기록: Symbol, trigger날짜/종류/두낙폭, 진입신호일/최초체결일, RSI/volume ratio, supports/cluster경계·중심·수, 분할진입 각 날짜·가격·수량·평균, TP1/2/trailing/기타청산 날짜·가격·수량, 실현손익/최종수익률/holdingdays/reason/reentry, 당시 Config/조건 snapshot.
18. Idle/CrashTriggered/SupportApproached/WaitingForRebound/Entry1..Holding/PartialTP/Exited/Cooldown. 종목별 독립 상태.
19. 모든 기간 양수, 하락률(-100,0), RSI[0,100], 배수 양수, 거리/count/risk/entryratios 합100 및 길이, TP/trailing/holding/cooldown 검사.
20. 최소20개 전략 검사 + prefix invariance, warmup무주문, 갭/정지/마지막봉미체결/비용/수량/설정복원/기존전략회귀.
21. 날짜별 indicator/support 캐시, 과거만 순차 반영. 과거 1,500봉 제공 제한·MA999 준비기간 부족을 알리고 해당 MA 후보만 제외. 현재 목록을 과거 전체시장 universe로 주장하지 않는다.
22. 설정저장→백테스트→상세 결과조회 UI/문서/검증/단독 EXE. 완료 보고는 기존 작업 완료와 구분.

## 모호한 부분의 보수적 기본 정책 (구현 시 동일하게 적용)
- 시장 거래일은 명시적으로 제공된 market session calendar를 사용한다. 단순 평일 생성은 한국 휴일/정지를 해결하지 않으므로 사실로 사용하지 않는다. 선택 종목 관측일뿐이면 그 한계를 표시하고 정확한 시장일 기반 결과로 주장하지 않는다.
- 당시 종목 상태/거래대금/위험은 KnownAt<=판단시각인 최신 해당 날짜 입력만 사용한다. next-open 진입도 당시 거래 가능·risk false를 재확인한다. 과거 사실의 미래 KnownAt은 과거 신호에 사용하지 않는다.
- 평균 거래대금/거래량은 현재봉 이전 lookback 완성봉만 사용한다. RSI는 최초 period 변화량 단순 평균 후 Wilder smoothing. Crash HIGH window는 현재 확정봉 포함.
- BinSize는 KRW 단위(초기 100원), 호가 단위 이상으로 올림. MaxBins 상한을 넘으면 유효 bin폭을 확대하고 결과에 기록. [Low,High] 양끝을 포함한 floor(price/binSize) bin에 균등 분배. 임계치는 최고 bin 거래량 대비 비율. 인접선택 bin은 연속 구간, 추가 병합은 이전 구간 상단 대비 거리. 대표가격은 거래량 가중 bin중심.
- Cluster 인접 거리는 (오른쪽 대표-왼쪽 대표)/왼쪽 대표. 하단/상단은 후보 구간 min/max, 중심은 중복제거된 후보 대표가격 단순평균. 연결식이므로 전체 폭은 5%를 넘을 수 있다.
- 반등 후보는 동일 후보 구성/근접한 cluster를 추적하며 유효 수명과 cluster변경 허용 범위를 Config에 둔다. 새 접근 전에 있던 저점은 사용하지 않는다.
- 주문량은 시가와 비용/현금/회차 예산만으로 정한다. 일봉으로 시가 유동성/부분 체결을 복원하지 않는다. 현금 부족 시 살 수 있는 정수주수까지만 매수, 0주면 체결 횟수 증가 없음. 거래불가 시 다음 거래 가능일로 대기하며 종료일 이후 소급 체결하지 않는다.
- TP 두 수준이 동시에 감지되면 별도 TP leg를 같은 다음 open에서 처리할 수 있다. 각 qty=floor(고정기준qty×ratio), 작은 수량에서0주는 추가 체결을 만들지 않고 해당 단계 완료로 표시한다. 남은 주수는 다른 청산 규칙으로 관리한다.
- High/Low touch 정책과 CloseOnly 정책을 Config에 두며 기본은 보수적 OHLC 감지→next-open. supportbreak는 요청대로 CLOSE 기준. trailing은 이전 확정 high를 먼저 검사. 손절/비상/트레일/시간청산이 TP보다 우선한다.
- 보유기간은 첫 체결일 시장session부터 계산하며 20번째 보유 session 마감에 청산 신호, 다음 가능한 open 체결. cooldown은 전량청산 session 이후 완전히 지난 session수로 검사한다. 재진입의 '새'는 snapshot 중심 거리 기준이며 가격 변화만으로 임의 거래를 생성하지 않는다.
- CAGR=(최종평가/초기현금)^(365.25/실제경과일)-1. Sharpe는 시장일별 순자산 수익률에서 연간 무위험수익률의 일 환산을 뺀 평균/표본표준편차×sqrt(연252,Config). 분모0은 미산출, PF 이익만 존재 시 infinity 별도표시. 평균수익/손실은 완결 거래별 비용포함 수익률, Expectancy는 완결 거래 평균손익 및 평균수익률.

## 구현 상태
2026-10-03 일봉 백테스트 구현·621개 회귀·단독 EXE 검증 및 루트 배포 완료. 상세한 33항목 결과는 oversold-implementation-report.md, 입력 한계는 api/OversoldHistoricalFacts.md에 기록한다. 과거 facts API 자동 연결/실계좌 전략 실행은 포함하지 않는다.

## 구현 중 확정된 보충 정책
- 최초 접근을 확인하는 시점은 마감이므로 그 날 이미 지나간 Low를 반등 저점으로 소급하지 않는다. 최초 저점은 접근 CLOSE, 이후 시장일의 LOW를 조건 일시 미충족 때도 추적한다. 후보 수명/구성/중심 변화 시 초기화.
- 마감 판단에서 청산일 이후 다섯 시장일이 완전히 끝났으면(청산일 제외) 재진입 신호를 허용하며 체결은 다음 시가다. 기본5일을 하루 더 기다리지 않는다.
- 거래정지로 기다리는 TP 주문이 있어도 새 손절/시간청산 신호가 나면 전량 청산 대기로 대체한다. 기존 확정 체결을 취소하지 않는다.
- 입력의 시장 달력 자체를 외부 휴장 캘린더와 자동 대조하는 기능은 없다. 명시 자료를 신뢰하되 중복/시간/날짜를 검사한다.
