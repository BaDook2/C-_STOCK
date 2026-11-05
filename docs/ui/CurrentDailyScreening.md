# 현재 일봉 스크리닝과 장중 차트

## 화면 목적
스크리닝은 기존 검색 일봉으로 현재 기술적 후보를 평가한다. 종목·차트는 선택 종목의 새 시세를 1초 단위로 화면에 반영한다.

## 동작과 제한
- 일봉 스크리닝은 별도 과거 JSON 없이 급락·RSI·거래량·이동평균·지지·반등을 계산한다. 확인되지 않은 종목 상태·평균 실제 거래대금·위험은 미확인으로 표시하며, 기술적 후보는 자동주문 허가가 아니다.
- 과거 백테스트의 시점별 자료 검증은 유지한다. 당일 거래대금을 과거 평균으로 사용하거나 종가×거래량으로 실제 거래대금을 만들어내지 않는다.
- 선택 종목 조회 → SelectedQuotePump → 공급자 간격으로 최신 관측 → DispatcherTimer 1초 → 현재가/등락/당일 OHLCV/시각 표시. 매초 전체 일봉을 다시 내려받지 않는다.
- 일반 공개 조회는 응답 간격을 따르고, 선택 종목·공개 실시간 감시/모의매매는 장중 1초 조회를 사용한다. 화면 1초 반영은 매초 새 체결이나 가격 변동 보장이 아니다. 연결된 키움 체결은 기존 수신 경로에서 병합한다.
- 15:30 일봉 축 기준과 실제 시세/서버 관측 시각을 구분한다. 진행봉은 확정 백테스트 봉에 포함되지 않는다. 종목 변경 및 종료 시 취소하고 늦은 응답을 무시한다. 공개시세 실패는 재시도 간격을 늘리며 마지막 값을 표시한다.
- 장중 공개시세와 키움 읽기 전용 연결은 2026-11-05 검증 기록을 참고한다. 이번 UI 변경의 온라인 선택·체결은 가짜 공급자 검사로, 오프라인 1초 현재가와 시간봉·실시간 표는 실제 장중 공개시세로 확인했다. 휴장에 가짜 가격 변화를 만들지 않는다.

## 관련 코드
Stock.Core/Research/CurrentMarketSnapshot.cs, PublicCurrentMarketData.cs, Oversold/CurrentOversoldScreening.cs, Stock.App/LiveDailyWorkspace.cs, OnlyOversoldWorkspace.cs, OversoldScreeningWorkspace.cs, CandleChart.cs.

## 검사
Stock.Tests/CurrentDailyTests.cs 및 CurrentDailyUiTests.cs. UI 검사는 Program의 --oversold-ui와 전체 UiSmoke에서 모두 실행한다. 최종 수치와 실행파일 해시는 TASKS/VALIDATION 참조.
