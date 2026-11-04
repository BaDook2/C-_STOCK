# 네이버 공개 KRX 시세

키 없이 공개 응답을 읽으며 개인 토큰이나 쿠키를 전달하지 않는다.

- 현재 관측: `https://polling.finance.naver.com/api/realtime?query=SERVICE_ITEM:005930`
- 공개 1분봉: `https://api.stock.naver.com/chart/domestic/item/005930/minute?startDateTime=202610280900&endDateTime=202611041530`
- 현재 종목 상태: 기존 `m.stock.naver.com/api/stock/{symbol}/basic`

공개 응답 계약은 변경될 수 있으며 공식 증권사 주문 API가 아니다. 현재가 응답 pollingInterval 이상 간격을 지키고 오류에는 지수 백오프를 적용한다. 2026-11-04 장중 실제 관측시각은 10:09:27 / 10:09:34 / 10:09:41 / 10:09:48 KST였다. 같은 실행에서 준비 분봉 3,885개를 반환했다.

분봉 localDateTime은 KST 시작시각, accumulatedTradingVolume은 해당 분봉 거래량으로 처리한다. 봉 종료시각은 시작+1분이며 현재시각 이전에 종료된 봉만 전략에 사용한다. 더 넓은 봉은 기존 집계기를 사용한다. 근사 거래대금은 모의 전용으로 표시하며 원본 체결로 저장하지 않는다.

공개 현재가 result.time은 서버 관측시각이다. 실제 최종 체결시각과 동일하다고 주장하지 않는다. nv/pcv/ov/hv/lv/aq/aa를 KRX 현재가·전일종가·당일 OHLC·누적 거래량/거래대금으로 읽으며 NXT subtree는 제외한다.
