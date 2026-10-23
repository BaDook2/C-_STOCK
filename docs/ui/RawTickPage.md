# 원본 틱 화면

## 목적
종목·차트의 '원본 1틱' 또는 '원본 틱' 탭에서 SQLite에 저장된 실제 개별 체결을 시간 순서대로 탐색한다. 일봉/분봉에서 가상의 체결을 생성하지 않는다. 정기매수·기존 캔들 기능은 그대로 유지한다.

## UI 기능
| UI | 기능 | 실행 코드 |
|---|---|---|
| 소스 선택 | 해당 종목에 실제 저장된 소스를 선택 | RawTickChartModel.SwitchSource |
| 이전/다음 구간 | 원본 행 순서로 양방향 이동, 기존 구간 최대 90% 유지 | Move |
| 드래그 / 휠 | ScottPlot 기본 pan/zoom, 경계 가까이에서 비동기 preload | RawTickChartView.Navigate |
| 최신 / 초기화 | 마지막 저장 시점 기준 24시간 요청, 상한 초과 시 기간 축소 | Reset |
| 최신 체결 따라가기 | 3초마다 저장 완료된 체결 조회; 마우스 조작 시 자동 해제 | FollowLatest |
| 매수 ▲ / 매도 ▼ | 정확한 체결 위치 선택, 부분 체결 상세 표시 | SelectAtPixel |
| 이 거래 확대 | 진입 전 30초~청산 후 30초 요청; 상한 초과 시 진입 중심 축소 | ZoomTrade |
| 십자선 | 원래 한국시간, 가격, 체결량, sequence/DB 행 번호 표시 | Mouse |

조회는 비모달 '구간 조회 중…'으로 표시한다. 저장 자료가 없으면 '데이터가 없습니다' 안내를 표시한다. 휴장 공백은 가장 가까운 저장 구간으로 이동한다. 처음/마지막에서는 종료 안내를 표시한다.

## 동작 과정
버튼/축 변경 → RawTickChartModel → TickChartWindow → RawTickRepository → SQLite 읽기 전용 연결 → 백그라운드 좌표 변환 → ScottPlot SignalXY.

전체 자료는 보존하고 화면은 기본 100,000틱까지 보여 준다. 화면 및 캐시의 원본 행 수 합계는 300,000 이내다. 이 수는 일시적 조회 배열·좌표 배열·렌더러 메모리까지 포함한 바이트 상한은 아니다.

거래 상세는 원본 시간(100ns 표현 정밀도), 가격, 수량, 주문번호, 진입/청산 이유, 보유 시간, FIFO 기준 수수료 반영 손익을 표시한다. 제공처의 실제 타임스탬프 해상도는 별개다. 같은 위치에 여러 거래가 있으면 선택 메뉴를 연다. 기간 이전 진입이나 미청산은 미확정으로 표시한다. 날짜만 있는 매매는 정확한 틱 마커로 표시하지 않는다.

## 관련 코드
Stock.App/RawTickChartView.cs, RawTickChartModel.cs, RawTickWorkspace.cs
Stock.Core/Research/RawTickRepository.cs, TickChartWindow.cs, TickTradeDetails.cs
성능 및 제한: ../performance/RawTick.md