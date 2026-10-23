# 국내 과매도 일봉 시점 자료

## 사용 목적
현재 종목 목록이나 현재의 위험 여부를 과거에 소급하지 않기 위한 입력이다. `IHistoricalUniverseProvider`, `IFundamentalRiskProvider`, `IDailySessionProvider`를 `HistoricalFacts`가 구현한다. 공개 일봉 제공처는 실제 과거 거래대금, 당시 보통주/관리/정지 분류와 위험 여부를 모두 제공하지 않으므로 이 자료의 자동 API 어댑터는 아직 없다. 누락된 항목을 추정하거나 뉴스 위험을 자동 판정하지 않는다.

전략 설정 → 국내 과매도·낙주 반등 → 시점 자료 JSON 읽기, 또는 편집란에 입력 → 적용 및 저장. 실제 계좌 키는 필요 없다. `KnownAt`은 그 사실을 당시 알 수 있었던 시각이고, `Date`는 해당 사실을 적용하는 한국 날짜다. 해당 날짜 입력 중 `KnownAt <= 판단 시각`인 마지막 값을 사용한다. 다음 시가의 진입도 그때의 상태와 위험을 다시 확인한다.

## 형식
아래는 **형식 설명용** 예시이며 실제 시장 자료가 아니다. 프로그램이 예시를 자동 적용하지 않는다. 실제 출처와 시점을 확인한 데이터만 사용해야 의미 있는 결과가 나온다.

```json
{
  "Source": "자료 출처와 수집 방법",
  "MarketSessions": [
    { "Date": "2025-01-02", "Open": "2025-01-02T09:00:00+09:00", "Close": "2025-01-02T15:30:00+09:00" }
  ],
  "StatusRecords": [
    { "Symbol": "005930", "Date": "2025-01-02", "KnownAt": "2025-01-02T08:50:00+09:00", "Board": "Kospi", "Kind": "Common", "Suspended": false, "Administrative": false }
  ],
  "TradingValues": [
    { "Symbol": "005930", "Date": "2025-01-02", "KnownAt": "2025-01-02T15:40:00+09:00", "Value": 4000000000 }
  ],
  "RiskRecords": [
    { "Symbol": "005930", "Date": "2025-01-02", "IsRisk": false, "KnownAt": "2025-01-02T08:50:00+09:00" }
  ]
}
```

- `Board`: Unknown / Kospi / Kosdaq / Other. `Kind`: Unknown / Common / Etf / Etn / Spac / Other.
- `Suspended` / `Administrative`: true / false / null(모름). 둘 다 명시적 false이고 일반주식인 경우만 진입한다.
- `IsRisk`는 반드시 명시한 bool이다. 키를 생략하면 JSON 검사에서 거부한다. 해당 날짜 행이 없으면 미지다.
- `TradingValues.Value`: 실제 거래대금, 원 단위, 0 이상. 종가×거래량으로 대체하지 않는다. JSON 숫자는 쉼표 없이 입력한다. 화면의 일반 숫자 표시에는 천 단위 쉼표를 쓴다.
- 평균 거래대금은 현재 봉 이전 설정된 수의 관측 완료 일봉에 대응하는 실제 거래대금으로 계산한다. 거래정지로 봉이 없는 날은 이 봉 평균에 포함되지 않는다. 보유/재진입 기간은 아래 시장일 기준이다.
- `MarketSessions`: 테스트 구간의 전체 시장 거래일을 포함해야 한다. 평일/선택 종목 일봉으로 달력을 자동 생성하지 않는다. 이 입력 자체의 실제 휴장일 정확성을 외부와 대조해 인증하는 기능은 없다. 사용자가 확인한 캘린더를 사용한다.
- 이전 평균 기간의 거래대금·상태, 선택 기간의 상태와 위험, 전체 거래일을 함께 제공한다. 빈 목록은 허용하지만 충분한 사실이 없는 날에는 진입하지 않는다.
- 동일 Symbol/Date/KnownAt 중복, 음수 거래대금, 잘못된 enum, 역전된 시장 개장/마감 시각, null 행을 거부한다. 일봉 확정 시각이 달력의 마감보다 늦으면 해당 자료 조합은 거부한다.

## 저장과 재현
기존 `workspace.db`의 state 테이블을 사용한다. 현재 설정은 `oversold-config-v1`, 현재 facts는 `oversold-facts-v1`, 결과는 `oversold-run:<id>`, 테스트 기록 참조는 `chart-run:oversold-run:<id>`다.

실행마다 facts 및 전체 완료 일봉을 각각 `oversold-facts-snapshot:<SHA256>`, `oversold-bars-snapshot:<SHA256>`에 보관한다. 같은 내용은 동일 키로 중복을 줄인다. 결과에는 두 해시, 출처, Config, 신호/체결 시각, 판단과 거래 상세를 저장하므로 이후 현재 입력을 수정해도 이전 실행 근거가 남는다. 키/계좌 정보는 이 자료에 포함하지 않는다.

## 관련 코드
- `Stock.Core/Research/Oversold/HistoricalFacts.cs`
- `Stock.Core/Research/Oversold/OversoldBacktest.cs`
- `Stock.App/OversoldWorkspace.cs`

실제 API 연결, 과거 전체시장 생존편향 제거 universe, 상장폐지 가격 보정, 기업행동·배당·분할의 통합 정규화는 이 입력 어댑터에서 제공하지 않는다. 데이터 출처의 조정주가 여부를 확인해야 한다.
