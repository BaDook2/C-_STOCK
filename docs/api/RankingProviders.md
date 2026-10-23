# 검색순위·관심도 자료

가격 조회 API와 분리한 ISearchRankingProvider / ITrendingSymbolProvider를 사용한다.
구현: Stock.Core/Pipeline/RankingProviders.cs. UI 진입: Stock.App/PipelineWorkspace.cs.

현재 ManualInputProvider, CsvImportProvider, 명시적으로 선택하는 MockRankingProvider를 제공한다.
Toss/Kiwoom은 순위 API 미연동 상태이며 UnavailableRankingProvider가 한국어 안내를 반환한다.
인증키·토큰을 이 기능에 입력하거나 저장하지 않는다. 시세·주문 어댑터의 지원 범위와는 별개이다.

CSV의 필수 열은 Symbol,At이다. Symbol은 KRX:005930 또는 NASDAQ:AAPL 형식,
시각은 2026-09-29T09:00:00+09:00처럼 시간대가 필요하다.
선택 열은 SymbolObservation 속성명과 같다. SearchRank,PreviousRank,PreviousRankAt,
MarketCap,DayTurnover,DayVolume,Average20DayVolume,SameTime5MinAverageVolume,Vwap,
PreviousLow,Resistance,RecentHigh,RecentHighDays 등을 제공할 수 있다.
아래 예는 입력 형식 설명용이며 실제 시장 자료가 아니다.

```csv
Symbol,At,SearchRank,PreviousRank,PreviousRankAt,MarketCap
NASDAQ:EXAMPLE,2026-09-29T22:30:00+09:00,5,30,2026-09-29T22:29:00+09:00,"10,000,000"
```

CSV 헤더/형식/범위/과거 순위 시각을 검증한다. 미래 자료는 현재 시점 계산에서 제외한다.
각 종목의 최신 과거 자료를 사용하며 관측 유효기간은 Universe 갱신 주기의 두 배이다.
실시간 평가에서는 UniverseRefreshIntervalSeconds마다 메모리 관측을 새로 읽는다.
외부 순위 API는 연결하지 않았으므로 REST/TR ID/Rate Limit은 해당 사항이 없다.
누락된 입력은 임의 채우지 않고 해당 필터의 자료 없음으로 표시한다.
