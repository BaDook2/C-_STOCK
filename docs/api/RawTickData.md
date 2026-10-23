# 원본 체결 수집과 저장

## 현재 지원 범위
기존 KiwoomMarketDataProvider의 실제 WebSocket 수신 → 기존 메모리 전략 판단 + 별도 원본 저장 → 원본 틱 화면. 증권사 인증과 실거래 주문 경로를 변경하지 않았다. 새 키를 사용하거나 실제 주문을 실행하지 않았다.

RawTickIngestion.Import(TradingStore, IMarketDataProvider, CancellationToken)는 HistoricalMarketDataProvider 또는 스트리밍 Provider를 받아 512건씩 원본을 저장하는 공용 서비스다. 테스트에서는 명시적 TEST_FIXTURE 소스로만 합성 자료를 사용한다.

새로운 증권사 과거 개별 체결 REST 백필 API는 이번 변경에 포함하지 않았다. 과거 틱 API의 권한·보관 기간·원본 체결 식별자 규격 확인이 필요하다. 공개 일봉 API를 원본 틱인 것처럼 대체하지 않는다. 저장 자료가 없으면 빈 화면이다.

## 저장
기존 workspace SQLite events를 재사용한다. 원본 MarketEvent JSON, ExchangeTime.UtcTicks, Source, Sequence를 보존한다. 기존 동일 JSON digest 중복 방지 규칙을 유지한다. 동일 시각이라도 sequence 또는 다른 원본 내용이 다르면 별도 행이다. index ix_events_raw_tick(market_key, source, kind, at, id), schema user_version 최소 2. 원본 삭제·집계·다운샘플링은 하지 않는다.

차트는 kind=Trade만 읽으며 (at,id) tuple keyset으로 양방향 이동한다. 같은 시각 체결이 상한을 넘어도 행 번호로 다음 자료를 읽는다. 각 읽기는 별도 읽기 전용 SQLite 연결/Task.Run과 CancellationToken을 사용한다. 차트의 마지막 요청 generation만 화면에 반영한다.

실시간 수신은 품질 필터 전에 수집하여 128건 또는 1초 단위로 저장한다. 전략 판단은 기존 메모리 경로를 사용한다. 차트 최신 조회는 저장 완료 자료를 3초마다 읽는 방식이므로 거래소 직접 푸시 화면과 최대 수초 차이가 날 수 있다.

## 백테스트
ConfiguredBacktest → TradingStore.EnumerateEvents → StreamingBacktest → 기존 TradingEngine.
선택된 날짜 전체의 원본 이벤트를 (at,id)순으로 스트리밍한다. 차트 표시 상한이나 캐시 설정이 엔진 입력에 전달되지 않는다. 기존 DataQuality/Risk 규칙은 유지하며, 동일 내용 체결의 중복 판별에서 source/sequence를 구분한다. 정기매수 백테스트는 기존 확정 일봉 경로를 유지한다.

## 제한
과거 원본의 누락을 추정해 채우지 않는다. 같은 시간의 표시 좌표에는 double 최소 증가분을 적용하지만 원본 timestamp/sequence와 마커의 체결 가격은 바꾸지 않는다. 백테스트 결과의 전체 equity/체결 목록은 기존처럼 메모리에 유지하므로 대용량 실행은 차트보다 더 많은 메모리를 사용한다. 실계좌 인증·실체결 정확도는 개발 검사에서 검증하지 않았다.