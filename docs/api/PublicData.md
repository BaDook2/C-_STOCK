# 공개 시세 데이터

## 목적 / 인증
인증키 없이 국내·미국 종목을 한글 이름으로 검색하고 과거 일봉과 업종을 조회한다. 별도 인증·쿠키를 사용하지 않으며 접근 제한을 우회하지 않는다.

## 연결 경로
- 네이버 검색: ac.stock.naver.com/ac, query q / target=stock / st=111.
- 국내 목록: m.stock.naver.com/api/stocks/marketValue/{KOSPI|KOSDAQ}.
- 미국 목록: api.stock.naver.com/stock/exchange/{NASDAQ|NYSE|AMEX}/marketValue.
- 국내 업종: 종목 integration의 industryCode → stocks/industry의 groupInfo.name.
- 미국 업종: stock/{reutersCode}/basic 또는 목록의 industryCodeType.industryGroupKor.
- 국내 일봉: fchart.stock.naver.com/sise.nhn, timeframe=day / count=1500 / symbol. EUC-KR 문자 인코딩 지원.
- 미국 일봉: query1.finance.yahoo.com/v8/finance/chart/{symbol}, range=5y / interval=1d.

## 처리
StockItem과 PriceBar 공통 모델로 변환한다. 차트는 당일 미확정 봉도 표시한다. PriceBar.IsComplete와 BarDataset.CompletedBars로 백테스트용 확정봉을 구분한다. 국내 15:40 한국시간, 미국 16:10 현지시간(DST 적용)을 보수적인 확정 기준으로 사용하며 조기 폐장일에도 이 시각까지 기다린다. null OHLCV·거래정지 0가격 봉을 제외하고 OHLC 유효성을 검증한다. 출처와 수집 시간을 보존하며 재다운로드 실패 시 캐시임을 표시한다. 시각 표시는 +09:00이며 미국 일봉 원본 타임스탬프는 보존해 변환 표시한다.

## 제한 / 오류
공개 인터페이스는 비공식이며 제공처 변경·호출 제한·중단이 가능하다. 요청 타임아웃 20초, 스크리닝은 순차 조회 및 간격을 사용한다. 429는 별도 안내한다. 실패 종목은 실패 건수에 포함하며 합성 가격을 만들지 않는다. 실제 체결 틱·L2·전체 시장 보장·세금·배당 정보는 제공하지 않는다.

## 관련 코드
Stock.Core/Research/PublicMarketData.cs, Stock.App/WorkspaceViewModel.cs. 공개 네트워크 읽기 검증과 가짜 데이터 검증은 별도 결과 파일로 남긴다.

## 시장 현황
- GET https://m.stock.naver.com/api/index/KOSPI/basic 및 KOSDAQ/basic
- 국내 종목: GET https://m.stock.naver.com/api/stock/{symbol}/basic
- 미국 종목: GET https://api.stock.naver.com/stock/{providerCode}/basic
closePrice, compareToPreviousClosePrice, fluctuationsRatio, localTradedAt, marketStatus를 읽는다. 시장 현황 탭 활성 중 5초 간격이며 중복 요청은 생략한다. 오류 시 이전 값을 유지하되 갱신 실패와 각 값의 실제 시각을 표시한다. 제공처 지연 가능성이 있고 5초 조회가 5초 이내 시세 보장을 뜻하지 않는다. 모든 화면 시각은 한국시간이다. 일평균 거래량은 이미 가져온 최근 확정 20거래일 자료를 사용한다.

## 지수 추이와 해외 지수
GET https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=1d&interval=5m
심볼: ^KS11(코스피), ^KQ11(코스닥), ^DJI(다우), ^IXIC(나스닥), ^GSPC(S&P 500).
meta.regularMarketPrice, chartPreviousClose, regularMarketTime으로 지수·등락을 계산한다. timestamp와 indicators.quote.close 배열에서 실제 값만 추출하고 결측은 건너뛴다. 국내 차트는 최대 1분마다 요청하고 국내 현재 지수 값은 기존 네이버 경로를 유지한다. 해외 카드는 5초 요청에서 값과 추이를 함께 얻는다. 데이터 제공처의 지연과 휴장 시각을 그대로 표시하며 체결 실시간을 보장하지 않는다. 2026-09-28 공개 읽기에서 다섯 심볼 모두 관측값을 확인했다.
