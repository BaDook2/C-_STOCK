# Raw Tick WPF 성능·구현 보고 (2026-09-30)

## 결과와 결정
기본 MaxVisibleRawTicks=100,000, MaxLoadedTicks=300,000으로 보수적으로 설정했다. 화면 상한은 원본 DB와 백테스트 입력에 전파하지 않는다. 상한은 측정한 규모 중 가장 작은 메모리/좌표 준비 비용을 선택한 값이며 '최대 가능한 틱 수'나 60FPS 보장이 아니다.

ScottPlot 실제 렌더 p95는 100,000틱 3.87ms, 2,000,000틱 4.95ms였다. 반면 WPF 갱신 요청부터 Background dispatcher 재개까지는 pan p95 약 101~105ms, zoom 약 111~112ms였다. 따라서 전체 화면 입력 지연이 16/33ms 이하라는 목표는 이 측정에서 달성하지 못했다. 숨겨진 창/비활성 창의 WPF 합성·스케줄링 영향이 가능하지만 원인을 확정하지 않았다. 물리 마우스를 사용하는 가시 창에서의 체감 성능은 미검증이다.

## 환경과 방법
AMD Ryzen 5 5600 (6 cores / 12 logical), RAM 68,603,543,552 bytes, NVIDIA GeForce RTX 4060, Windows 10.0.26200, .NET 10.0.12, 1920x1080 화면, WPF 창 1200x700 DIP. ScottPlot.WPF 5.1.59 / SkiaSharp 3.119.0.

Stock.App/TickPerformanceProbe.cs에서 실제 WpfPlot SKElement PaintSurface / RenderFinished를 측정했다. Window.Show, ShowActivated=false; 프로세스는 Hidden으로 시작했다. 네이티브 합성 화면의 실제 표시 상태는 독립 검증하지 않았다. SavePng만으로 대체한 테스트가 아니다.

단일 프로세스에서 7규모를 작은 것부터 1회씩 생성, 각 규모마다 자동 축 이동 24회 + 확대 24회. RenderExecution은 RenderStarting~RenderFinished, 요청 지연은 Refresh~RenderFinished 후 Dispatcher.Background 재개다. 렌더 횟수는 규모별 98회(내부 추가 렌더 포함). 10ms DispatcherTimer의 예정 간격 초과분도 기록한다. 샘플 사이 8ms 대기, 단계 시작 시 명시적 GC 후 단계 내 GC 수 측정. Process.Refresh 후 메모리 수집. CPU는 단계별 누적 CPU 시간이다. 최초 JIT/캐시 영향 때문에 규모별 생성/최초 렌더 값을 절대 성능 순위로 해석하지 않는다.

합성 MarketEvent의 불규칙 원본 시간/가격을 double 배열로 변환한다. 이 그래프 POC에는 DB 읽기와 전체 메인 화면/마커 수천 개 비용이 포함되지 않는다. 원본 DB/전체 엔진 검사는 별도로 수행했다. 원본 측정 JSON: ../../artifacts/tick-performance-final/measurements.json.

## 그래프 실측 (ms)
| 틱 수 | 좌표 준비 | 시리즈 생성 | 최초 WPF | Render p50/p95/max | Pan p50/p95/max | Zoom p50/p95/max |
|---:|---:|---:|---:|---:|---:|---:|
| 100,000 | 22.01 | 1.021 | 26.10 | 1.79/3.87/14.82 | 99.03/101.39/103.13 | 110.12/112.37/118.19 |
| 250,000 | 22.55 | 0.036 | 39.11 | 1.81/2.50/4.16 | 99.81/101.92/109.72 | 110.67/111.82/112.63 |
| 500,000 | 51.85 | 0.005 | 46.26 | 2.02/2.58/3.11 | 100.12/101.97/109.04 | 109.92/111.79/111.92 |
| 750,000 | 54.61 | 0.006 | 67.83 | 2.24/2.92/3.57 | 100.52/103.08/103.48 | 110.19/111.56/112.49 |
| 1,000,000 | 76.65 | 0.007 | 44.46 | 2.53/3.32/4.46 | 101.30/103.72/104.59 | 110.38/111.78/111.89 |
| 1,500,000 | 109.09 | 0.006 | 14.45 | 3.30/5.75/6.47 | 93.07/103.96/109.28 | 109.68/110.75/114.66 |
| 2,000,000 | 146.15 | 0.006 | 14.29 | 3.53/4.95/6.77 | 101.15/105.34/106.53 | 109.71/110.84/111.18 |

| 틱 수 | CPU ms | Wall ms | Managed MiB | Private MiB | Peak working set MiB | GC0/1/2 | Dispatcher 초과 p50/p95/max ms |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100,000 | 1,359.38 | 5,299.43 | 3.71 | 108.43 | 139.76 | 3/0/0 | 89.18/101.87/108.20 |
| 250,000 | 593.75 | 5,361.86 | 10.35 | 106.94 | 148.34 | 3/0/0 | 90.71/101.59/102.63 |
| 500,000 | 281.25 | 5,352.91 | 14.70 | 115.96 | 149.39 | 3/0/0 | 91.69/101.49/101.97 |
| 750,000 | 562.50 | 5,355.48 | 22.19 | 128.21 | 161.83 | 3/0/0 | 97.65/101.80/102.50 |
| 1,000,000 | 296.88 | 5,344.25 | 29.35 | 142.62 | 177.15 | 3/0/0 | 93.52/101.80/103.05 |
| 1,500,000 | 421.88 | 5,302.80 | 40.90 | 141.74 | 188.24 | 3/0/0 | 82.94/100.72/105.29 |
| 2,000,000 | 593.75 | 5,266.13 | 55.92 | 173.11 | 207.02 | 3/0/0 | 92.01/101.95/104.10 |
## 원본 보존·백테스트 검사
Stock.Tests/RawTickTests.cs: 별도 fixture DB에 2,000,000개 원본, 첫 250,000개는 같은 시각. 최초 저장 약192,432ms. 최종 재검증은 그 DB를 재사용했으므로 재사용 실행의 InsertMs는 입력 처리 성능으로 해석하지 않는다.
최종 전체 keyset paging 6,539ms, StreamingBacktest 21,395ms. chart cap=1,000이어도 엔진 이벤트/Equity 수가 모두 2,000,000. 저장 행 수 동일, 시간/행 순서 유지, 캐시 eviction 후 재조회, 취소/요청경합, 축 pan/마커 pixel hit/부분 체결, 구간 중첩과 누락 방지 검사 포함. Raw Tick 검사 30개 + 기존 회귀 492개 통과.
2M 엔진 검사는 전략을 비활성화하고 per-event 감사 기록을 끈 소비량 검증이다. 실제 모든 전략/감사 기록을 켠 실전 처리량 벤치마크로 주장하지 않는다. 원본: ../../artifacts/raw-tick-checks/raw-results.json.

## 구성과 변경 파일
- Core Research: RawTickRepository.cs(비동기 index/keyset), TickChartWindow.cs(기간 축소·cache·preload), RawTickIngestion.cs(Provider→SQLite), TickTradeDetails.cs(FIFO 부분 체결).
- Core Trading: TradingStore.cs(index/prepared insert/전체 iterator), StreamingBacktest.cs(전체 원본→기존 엔진), MarketData.cs(source/sequence 중복 구분), Fill/BarFill 관련 모델과 PaperBroker.cs(이유·주문번호 보존).
- WPF: RawTickChartView.cs, RawTickChartModel.cs, RawTickWorkspace.cs, MainWindow.xaml, WorkspaceViewModel.cs/WorkspaceFeatures.cs, ConfiguredBacktest.cs/ConfiguredPractice.cs(원본 저장 및 공용 엔진 연결).
- 검사/배포: TickPerformanceProbe.cs, PublishedSmoke.cs, App.xaml.cs, RawTickTests.cs, Program.cs, UiRevisionTests.cs, App/Tests csproj. App/Tests Windows TFM은 net10.0-windows10.0.19041.0으로 의존성과 일치시켜 NU1701을 숨기지 않고 제거.

## cap / cache / 시간축
최초 24시간 요청이 상한 초과면 12→6시간 이하로 연속 축소한다. 매우 밀집된 같은 시각 구간은 (at,id) keyset으로 원본 순서대로 접근한다. 양방향 이동은 기존 최대90% + 인접 자료를 이어 붙인다. 화면+캐시 원본 행 수 300,000 이내, 캐시 최대6페이지, 6시간 preload, 표시 구간 경계20%, debounce140ms. 이전/다음 끝과 휴장 공백을 처리한다. 이 메모리 상한은 renderer/임시 배열 포함 바이트 상한이 아니다.
X는 UTC 일자 기준 상대 초 double이며, 동일 timestamp/표현 충돌은 표시 배열에서만 BitIncrement한다. 원본 long ticks/sequence/decimal 가격은 그대로 보존한다. crosshair는 정렬 X 배열 binary search 후 원본 시간과 수치를 읽는다. BUY/SELL은 별도 marker이며 정확한 원래 체결시간/가격을 사용한다. 누락된 체결시간을 캔들 종가로 대체하지 않는다.

## API와 제한
기존 실제 Kiwoom WebSocket 저장 경로와 공용 IMarketDataProvider Import는 연결했다. 새 과거 개별체결 REST 백필 어댑터는 미구현이다. 저장 자료가 없으면 빈 상태를 표시한다. 합성 자료는 명시적인 성능/검증 폴더에만 생성했다. 실계좌 키 인증·실자금 주문은 하지 않았다. 최신 따라가기는 저장된 체결을 3초마다 조회하며 실제 푸시 화면과 다를 수 있다.
부분 체결은 FIFO로 표시하고 진입이 기간 밖이면 미확정으로 표시한다. 기존 날짜만 있는 체결을 정밀 틱 마커로 속이지 않는다. 백테스트 전체 equity 보존 때문에 대규모 엔진 메모리는 차트 cap으로 제한되지 않는다.

## 공식 API 근거
- https://www.nuget.org/packages/ScottPlot.WPF/5.1.59
- https://scottplot.net/quickstart/wpf/
- https://scottplot.net/cookbook/5/SignalXY/SignalXYQuickstart/
실제 설치 버전의 WpfPlot/RenderManager/SignalXY API로 컴파일 및 WPF 렌더 검증했다.
### ScottPlot 최종 완료 — 2026-09-30
구현/검증/배포 완료. 앞선 중간 체크포인트의 진행 중 상태는 이 기록으로 대체한다.
- 원본 틱 30개 + 기존 회귀 492개 통과. Release 빌드 경고0 오류0, publish 경고/오류 없음.
- 7규모 WPF 재측정 완료: artifacts/tick-performance-final/measurements.json. 실제 렌더와 dispatcher 포함 갱신 대기를 분리해 docs/performance/RawTick.md에 전체 수치·환경·방법 기록. 100,000 표시/300,000 원본 행 캐시+화면 예산 채택. 전체 갱신 p95 약100~112ms로 60FPS/물리 마우스 체감은 보장하지 않음.
- 전체 2,000,000 이벤트 paging/백테스트 입력·equity 검증. chart cap 1,000은 엔진에 영향 없음. 동일 timestamp 250,000개 안정 순서와 구간 overlap/취소/marker hit 검증.
- 루트 EXE 공개 시세 검사 artifacts/raw-public-final/result.json 성공·종료0. 국내9/30 장중 자료, 미국9/29 자료. 원본 틱 미보유는 0건으로 표시. 국내 목록3/20, 미국1/30 조회 실패는 숨기지 않고 표시했다.
- 이후 출처 표시만 보완한 최종 EXE를 루트에 재배포. EXE만 있는 artifacts/raw-clean-final-exe에서 artifacts/raw-clean-final-result/result.json 성공·종료0. RawNativeRendered=true, TEST_FIXTURE 2,000틱 실제 SignalXY 렌더. 별도 raw-native-final 추출 폴더에서 libSkiaSharp.dll 확인.
최종 Stock.exe: 80,467,342 bytes / SHA256 549EC47DF5B75CF7ED9838FF6CF6FF7EF3AC08A9BFC01F4B5E7E00EDCFC2A511.
AGENTS.md는 EXE 옆 유지. 실제 계좌 키 변경/인증/실자금 주문 없음. 사용자 데이터 삭제 없음. 합성 자료는 별도 검사 폴더 전용.
문서: docs/performance/RawTick.md, docs/ui/RawTickPage.md, docs/api/RawTickData.md, docs/architecture/ARCHITECTURE.md.
외부 한계: 새 증권사 과거 개별체결 REST 백필 어댑터는 미구현. 기존 실제 Kiwoom WebSocket 수집 및 IMarketDataProvider Import 구조를 사용하고 저장 자료 없는 경우 임의 생성하지 않음. 실제 가시 창 물리 입력 지연은 미검증. 기존 브로커 규격/사용자 계좌 검증 [!]는 유지하며 임의 진행하지 않는다.