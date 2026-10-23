# 테스트 기록

## 화면 목적
저장된 과거 백테스트를 선택하고 결과·체결 내역과 작업 로그를 확인한다.

## UI 기능

| UI | 기능 | 실행 코드 |
|---|---|---|
| 결과 요약 | 손익·수익률·최대 낙폭·수수료·보유 수량 | SelectedRun.Summary |
| 가상 체결 표 | 한국시간, 매수/매도, 수량, 가격, 비용, 실현손익, 이유 | SelectedRun.Fills / BarFill.KoreaTime |
| 활동 기록 | 검색·조회 실패·전략 실행 등 한국시간 기록 | Logs / Log |

## 동작 과정
BarPaperAccount → UpdatePaper → Trades / ResultInfo → 화면.
상단 목록은 SQLite의 chart-run: 기록을 최신 저장 순으로 불러온다. 앱을 재시작해도 과거 기록을 선택할 수 있다. 기록 선택은 현재 가상 계좌·차트를 변경하지 않는다. 차트 아래 실행 버튼 바로 오른쪽에는 현재 테스트의 손익·수익률·낙폭·체결 수가 즉시 표시된다. 이전 형식의 저장 기록도 읽으며 손상된 개별 기록은 건너뛴다.

## 관련 코드
MainWindow.xaml, WorkspaceViewModel.cs, ChartStrategies.cs, TradingStore.cs.
