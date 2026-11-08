# 네이버 공개 KRX 시세

키 없이 공개 응답을 읽으며 개인 토큰이나 쿠키를 전달하지 않는다.

- 국내 현재 관측: `https://polling.finance.naver.com/api/realtime/domestic/stock/005930` (최대 20종목 코드 묶음 지원)
- 미국 현재 관측: `https://polling.finance.naver.com/api/realtime/worldstock/stock/AAPL.O`
- 공개 1분봉: `https://api.stock.naver.com/chart/domestic/item/005930/minute?startDateTime=202610280900&endDateTime=202611041530`
- 현재 종목 상태: 기존 `m.stock.naver.com/api/stock/{symbol}/basic`

공개 응답 계약은 변경될 수 있으며 공식 증권사 주문 API가 아니다. 일반 조회는 제공처 pollingInterval을 따르고 오류에는 지수 백오프를 적용한다. 선택 종목과 공개 모의매매는 `IFastCurrentMarketData`, 공개 실시간 스크리닝은 `IFastPublicCurrentBatchData`를 사용해 장중 1초 간격으로 읽는다. 20종목 묶음 구조와 HTTP 오류 쿨다운은 유지한다. 장 마감에는 제공처 간격을 따른다. 2026-11-05 12:48:23~27 KST의 1초 간격 네이버 응답에서 서버 관측시각이 매초 전진했다. 가격은 이 표본에서 동일했다. 이는 새 원본 체결이나 가격 변동이 매초 보장된다는 뜻은 아니다. 원본 응답은 pollingInterval=7000을 안내했다.

분봉 localDateTime은 KST 시작시각, accumulatedTradingVolume은 해당 분봉 거래량으로 처리한다. 봉 종료시각은 시작+1분이며 현재시각 이전에 종료된 봉만 전략에 사용한다. 더 넓은 봉은 기존 집계기를 사용한다. 근사 거래대금은 모의 전용으로 표시하며 원본 체결로 저장하지 않는다.

현재가 응답의 최상위 `time`은 서버 관측시각이다. 실제 최종 체결시각과 동일하다고 주장하지 않는다. 국내 `itemCode`, 미국 `reutersCode`로 응답 행을 맞춘다. `closePriceRaw`, 전일 대비 방향·변동액, OHLC, 누적 거래량/거래대금, `pollingInterval`을 읽는다. 미국은 `localTradedAt`을 체결시각으로 표시하고 제공처가 명시한 `delayTime`을 표시한다. 프리/애프터마켓 응답은 별도 가격·시각이 더 최신일 때만 사용한다. 과거 미국 일봉도 아래의 네이버 실제 일봉 경로를 사용한다.

StockViewer가 쓰는 국내/미국 공개 폴링 경로와 같은 형식이다. 2026-11-04 장 마감 후 실측은 미국 AAPL.O 7초, 국내 삼성전자 70초 갱신 간격을 응답했다. 장중 간격은 제공처 응답에 따라 달라진다. 공개 데이터는 거래소의 무지연 원본 체결·호가가 아니고 API 형식·허용량도 바뀔 수 있다. 20종목 묶음은 오프라인 실시간 스크리닝의 요청 수를 제한한다.

## 해외 종목/지수 구성 목록 (2026-11-06)

StockViewer의 검색/현재가 경로를 참고해 USA 검색 결과의 opaque `reutersCode`를 그대로 사용한다(AAPL.O, BRKb 등의 접미사를 임의 생성하지 않는다). 현재가는 공개 realtime/foreignStock 폴링, 시간외 등락 기준도 StockViewer와 같다. 온라인 연결 증권사가 선택한 해외 시장을 지원하지 않으면 화면 시세는 공개 제공자로 읽고 해당 증권사에 미국 주문을 전송하지 않는다.

일봉은 `https://api.stock.naver.com/chart/foreign/item/{reutersCode}?periodType=month&range=120`의 localDate/openPrice/highPrice/lowPrice/closePrice/accumulatedTradingVolume을 읽는다. month 범위는 일봉이고 year 범위는 주봉으로 축약되므로 대체하지 않는다. 미국 Eastern 시간대(DST 포함) 거래일과 정규장 마감 기준을 유지한다. 없는 가격을 만들지 않는다.

S&P500 `.INX`, 나스닥100 `.NDX`의 `https://api.stock.naver.com/index/{code}/stocks?page=N&pageSize=100`을 끝까지 읽는다. 합집합은 시장+티커로 중복 제거하고 각 지수 편입 정보를 보존한다. 현재 구성 목록이며 과거 유니버스가 아니다. 분봉 OHLCV를 제공하지 않는 가격 표본은 실제 분봉으로 가장하지 않는다.
