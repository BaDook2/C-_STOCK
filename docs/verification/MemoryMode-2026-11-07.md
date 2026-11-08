# 메모리 시세·모드 기준·표시 개선 — 2026-11-07

## 변경과 데이터 보존

- 제품 WorkspaceViewModel/MainViewModel의 기본 TradingStore는 memoryMarketData:true. SessionMarketData가 bars:/live-ticks:/verified-tick-coverage:/catalog와 원본 MarketEvent를 메모리로 처리한다. 캐시 LRU128개/JSON32M문자 제한, 이벤트 중복 제거·시각/sequence 정렬. 앱 종료 시 폐기. 예전 DB 캐시/틱 행은 읽거나 삭제하지 않는다.
- 전략·관심종목·체결·주문 가드와 테스트 기록의 당시 봉/판단 snapshot(screen-data:, oversold-bars-snapshot: 등)은 보존한다. 과거 자료 재조회나 전략 변경으로 기존 테스트 차트가 달라지지 않도록 하기 위한 기록이다. API키는 기존 DPAPI 별도 파일이며 변경하지 않았다.
- 오프라인 기준은 일봉, 온라인은 지정봉(분1~59/시간1~12)·일봉. 일반/실시간 스크리닝, 차트, 다량 전략 팝업에 적용했다. 주/월/틱 표시 선택기는 제거하고 내부 틱 실행은 유지한다. BarUnit.Day는 검증된 장 시작~마감 전체이며 미완성/미래 일봉을 준비 자료로 쓰지 않는다. Day 팝업도 공통 일봉 백테스트 엔진·캐시를 재사용한다.
- ConnectionLifetime은 최초 검증 broker/session을 오프라인에서 보관하고 온라인 복귀 시 재사용한다. 오프라인 실제 주문 경로·피드는 꺼진다. 키 설정/초기화/종료 시 폐기, 주문 직전 계좌/한도/중지/미확인 주문 검증은 유지한다. 오프라인 긴급중지도 보관된 세션을 로컬 상태로 차단하며 API를 호출하지 않는다.
- DisplayNumber.Price/SignedPrice는 내림 정수 표시다. 국내·미국 헤더/표/차트축/hover/체결 상세에 적용하고 원본 OHLC/체결/주문/전략 계산 decimal은 그대로다. %는 소수 둘째 자리다.
- 매도 표식·클릭 상세에 확인된 실현손익을 표시한다. 축소 묶음은 모든 매도 손익이 확인될 때 합계를 표시한다. 실계좌 원가/비용이 없어 모르는 손익은 미확인으로 표시하며 만들지 않는다.
- TossInspired 공통 ScrollBar는 얇고 둥근 회색 Thumb/hover/drag 및 가로·세로 Track/페이지 이동을 지원한다. 모든 ScrollViewer/DataGrid/TextBox/팝업에 적용한다. 우측4탭은 UniformGrid2열×2행/본문 상단 정렬, 좁은 지정봉 입력은 WrapPanel로 감싸 잘림을 방지했다.

## 파일 지도

저장: Stock.Core/Trading/{SessionMarketData,TradingStore}.cs, Stock.App/{WorkspaceViewModel,MainViewModel}.cs.
연결: Stock.App/{ConnectionLifetime,WorkspaceFeatures,WorkspaceViewModel}.cs, Stock.Core/Trading/RemoteTradingSession.cs.
봉/실행: Stock.Core/Research/Oversold/{BarSpecification,OversoldLiveStrategy,RealtimeWarmup}.cs 및 Stock.App의 기존 일반/실시간 스크리닝·PublicPaper·MultiTrade·OversoldBacktestService·ScreeningNavigation·ResearchHistoryWorkspace·ChartWorkspace·ScreeningChartData.
표시: Stock.Core/Research/{WorkspaceModels,TradeMarker,TickTradeDetails}.cs, CandleChart/MainWindow.xaml.cs/ScreeningNavigation/ScreeningResultDialog/OversoldResultDialog/RecurringWorkspace/RawTickChartView.
UI: MainWindow.xaml, MultiTradeDialog.xaml, ScreeningChartDialog.cs, Styles/TossInspired.xaml.
검사: MemoryModeTests 및 Program/ResearchTests/UiTests/ConnectionTests/UiRevisionTests/ScreeningUiTests/RealtimeScreeningUiRevisionTests/MultiTradeUiTests.

## 검증

- 최종 Release 빌드 경고0/오류0: artifacts/memory-mode-build-final.log.
- 전용24개 성공: 메모리 가격·틱 정확값/중복 제거/DB 미기록/기존 행 보존/재시작 초기화/테스트 snapshot 보존, 가격 내림·원본 불변, 일봉 완성 시점/Day 공통 엔진·검증, 오프라인 기준, 반복 재연결/긴급중지 가드, 실제 WPF4탭/양방향 스크롤바/본문 배치. artifacts/memory-mode-checks.log.
- 최종 전체1366개 통과/종료0/실제 broker 통신·계좌 거래 없음: artifacts/memory-mode-full-final.log. 직전 UI smoke605개도 통과했다. 옛 주/월/틱/오프라인 분봉 기대는 새 요구대로 갱신했으며 분봉 매매 fixture는 온라인 지정봉을 명시해 기존 엔진 체결을 계속 검사한다.
- fixture 모드 왕복3회 약1.6~3.4ms, 추가 인증0/계좌조회0: mode-switch-timing.json. 실제 네트워크 측정이 아니다. 만료 토큰/연결 실패는 기존 broker 처리·주문 전 재검증을 따른다.
- publish 및 루트 EXE smoke 모두 종료0/Success=true/NoBrokerCalls=true: artifacts/memory-mode-{publish-smoke,root-smoke}/result.json.
- 렌더 확인: memory-mode-checks/{daily-memory-tools,sell-profit-markers}.png. 정렬된4탭/입력 잘림 없음/본문 상단/익절·손절 손익 표시 확인.
- 실제 사용자 Explorer에서 새 루트 Stock.exe 실행 성공. 오프라인 해외→애플 검색/일봉/342달러 정수 가격/오프라인 일봉 전용 UI를 확인했다. 현재가·가격축 모두 소수점이 없다. 공개 API 조회이며 가짜 broker 검사와 구분한다. 실제 계좌 연결/주문은 실행하지 않았다. 앱은 애플 차트로 열어 두었다.

## 로컬 배포와 제한

publish/win-x64/Stock.exe, 루트Stock.exe, release/Stock.exe:80,855,594bytes / SHA256 0CA8A9913AC9F7EE881CBC5903F359874089EE7E8F0F833C08D8C4021F7284FA. release 체크섬 갱신. 이전 파일 artifacts/before-memory-mode-20261107-180705 보존. GitHub commit/push/tag/Release 없음.

메모리 시세/원본 틱은 앱을 닫으면 사라진다. 예전 DB 데이터를 삭제하지 않아 기존 파일 용량을 축소한 것은 아니다. 테스트 재현용 당시 봉은 계속 기록한다. 일봉 실시간 판단은 확정 봉 기준이며 과거 신호를 소급 주문하지 않는다. 장외 시작 제한/정확 거래대금·종목상태·위험 미확인 시 주문 차단은 유지한다. 기존 DB 손상 유발 사건 미확정과 실제 AppData 대조 절차는 Storage.md/ProgressiveScreening-2026-11-07.md를 따른다.
