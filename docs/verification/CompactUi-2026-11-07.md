# 표식·1분봉 처리·화면 정리 (2026-11-07)

## 사용자 요청과 구현

1. CandleChart의 매매 글자는 기존 화면 폭 bucket 뒤에 실제 FormattedText Rect 충돌 검사를 적용한다. 인접 bucket/오른쪽 가장자리/매수와 매도 라벨도 겹치면 원본 체결 목록을 합쳐 횟수·거래번호 범위·확인된 매도손익을 표시한다. 매 render에서 화면 좌표로 다시 계산하므로 확대하면 개별 기록으로 분리된다. 원본 Time/Price/TradeNumber, 선택 이벤트, 기록은 바꾸지 않는다. Main/스크리닝/기록 차트가 같은 구현을 쓴다. 숨긴 RawTickChart 화면은 새로 노출하지 않는다.
2. 일봉·지정봉 공통 SupportProvider의 근사 매물대를 SortedDictionary 반복 조회 대신 유한 dense bin 배열로 계산한다. 각 bar별 배분·누적 순서와 마지막 bin decimal 잔액은 기존과 같다. MA는 정확한 decimal 덧셈/뺄셈이 보장되는 양수 가격·자릿수·길이 범위에서 prefix를 사용하고 고정밀 가격은 원래 순서 합산으로 돌아간다. 후보 조건·봉·원본 자료를 축약하지 않는다.
3. 일반 스크리닝의 최대 동시 작업4개는 유지하되 4개가 모두 끝나야 다음 묶음을 시작하던 barrier를 없앴다. 하나가 끝나면 다음 종목을 시작한다. UI에서 완료 순서대로 결과를 공개하며 취소/실패 시 대기 작업도 정리한다. 완전히 과거인 조회 범위는 같은 종목/봉/기간/broker/세션 달력 기준으로 프로세스 메모리 cache에서 재사용한다. 최근·실시간 조회 TTL은2분, 최대128개 제한은 유지한다. DB 시세 캐시/원본 틱 저장을 다시 켜지 않는다.
4. 키움 연속조회가 첫 요청 날짜의 일부 페이지에서 멈추던 조건은 실제 timestamp와 시작일00:00을 비교하도록 보완했다. 시작일 오후 자료를 받았다는 이유만으로 시작일 오전 자료를 놓치지 않는다. 계좌 조회/주문 endpoint 및 제한은 변경하지 않았다.
5. 전략 카드는 MinHeight145 제거, 내용에 맞춰 약78px(줄바꿈 시 증가).32px 입력 높이 유지, 일반130px/목록150px 폭. 허용 범위/단위는 입력 오른쪽, boolean ON/OFF·enum 내부 값 설명은 화면에서 생략하되 ? 도움말은 유지한다. TextBoxView가 적용하는 Padding을 template Grid에서 다시 적용하던 문제와 content host의 수직 크기를 수정해 글자 잘림을 제거했다. 숫자 입력 동작·정밀도는 변경하지 않는다.
6. 온라인 전용0개 범위는 화면에서 숨기고 모델의3범위/61필드 및 저장 호환성을 유지한다. 파라미터가 추가되면 빈 범위가 아닌 경우 자동으로 표시한다.
7. 공통 DataGridCell ContentPresenter가 가로/세로 가운데 정렬을 실제 사용하도록 template을 정리했다. 세로 padding4px로48px 행 안에 입력 컨트롤이 들어간다. GridText와 표 안 TextBox도 가운데 정렬. 개별 화면의 표를 별도 엔진으로 만들지 않았다.
8. 계좌 실행 창 제목은 `실제 계좌 전략 실행` / `가상 계좌 전략 실행`, 실제 진입 버튼은 `실시간 실제 전략매매 설정`. 관심그룹 편집은 공통 파랑 전경/옅은 파랑 배경. 계좌 모드 왕복 시 AccountInfo에 안내를 덧붙이던 코드를 제거했다. 기존 account summary만 재사용하며 주문 전 계좌 가드는 유지한다.

## 수정 파일

- Stock.App: CandleChart.cs, ChartMarkerAggregation.cs, OnlyOversoldWorkspace.cs, OversoldScreeningWorkspace.cs, OversoldParameterPanel.xaml, StrategyEditorViewModel.cs, Styles/TossInspired.xaml, MainWindow.xaml, WorkspaceFeatures.cs, MultiTradeViewModel.cs, MultiTradeDialog.xaml/.cs.
- Stock.Core: Research/Oversold/SupportProviders.cs, KiwoomBroker.cs.
- Stock.Tests: CompactUiChecks.cs, ScreeningPerformanceChecks.cs, Program.cs, ConnectionTests.cs(Render audit), UiTests.cs, RealtimeScreeningUiRevisionTests.cs, MemoryModeTests.cs, ParameterScopeUiTests.cs, FixedEditorTests.cs, ProgressiveScreeningTests.cs, BrokerTests.cs.

## 성능 근거와 제한

동일 환경의20거래일×390=7,800개 합성1분봉, 기본 OversoldConfig, 마지막5세션 스크리닝. 네트워크 없는 전체 EvaluateObserved wall-clock 및 total allocated bytes:

| 항목 | 개선 전 | 개선 후 |
|---|---:|---:|
| CPU 포함 경과 |2397.13ms|1596.38ms|
| 할당량(현재 점유 메모리 아님) |256,272,096bytes|160,342,192bytes|
| 결과 snapshot SHA256 |03576B79ECD7739F89AADDEE92FCF01FA001525F80C468D26508EB30F9DBA3EB|동일|

약33% 시간/37% 할당 감소다. 단일 실행 측정이므로 항상 같은 비율의 향상을 보장하지 않는다. 로그 artifacts/compact-perf-before.log / compact-perf-final.log, JSON artifacts/compact-perf-{before,final}. API 조회·DB snapshot 쓰기·다종목 총 소요시간을 이 수치로 오인하지 않는다. 실계좌키로 증권사 네트워크를 측정하거나 실제 주문하지 않았다. 긴 기간/1분봉은 더 많은 페이지와 지표 계산이 필요하며 broker의 기존 요청 제한을 우회하지 않는다. 화면 상태바의 종목당 자료조회 초/계산ms로 병목을 구분할 수 있다.

## 검증

- 300개 순차 MA prefix 및15개 근사 매물대 이력은 원래 방식과 decimal 결과가 정확히 동일. 고정밀 MA fallback 확인.
- 새 WPF55개:61필드/범위 옆 배치/글자 caret Rect 잘림 없음, 실제/가상 창명, 채워진 주문·보유·다량 표, 겹치는 긴 손익 라벨 병합 및 가까운 확대에서 개별 원본 체결 선택.
- 전체1429개 성공(artifacts/compact-full.log), broker 경계 추가 후210개, 최종 WPF670개, 실시간 표42개 및 최종 집계 문구/확대55개도 통과했다.
- Native 격리 STOCK 창에서 실제 버튼으로 전략 선택→기간 설정→스크리닝→결과 표(17조건) 확인, 채워진 삼성전자 행과 중앙 정렬/전략 입력 카드 확인. 사용자 DB/설정과 별도 fixture이며 실제 시장 결과가 아니다. 데모는 종료한다.
- 채워진 표21회 캡처/439개 셀의 center 좌표 audit, JSON .tables.json과 PNG는 artifacts/compact-full 및 compact-ui-final. 화면 진입 결과/실현손익/상태/날짜·수량·가격 입력을 포함한다. 실제 API의 모든 응답 조합을 검증했다는 의미는 아니다.

## 최종 상태

최종 Release 빌드0경고·0오류. 전체1429개(주요 구현), 이후 API 경계210개/최종 WPF670개/실시간 표42개/집계 익절·손절 문구 보완 후55개 모두 통과. publish와 루트 smoke는 종료0/Success=true/NoBrokerCalls=true. 루트/release/publish Stock.exe80,861,669bytes/SHA256 `88052CCE842989043934A50825DA01B8C39079EE128CE3D616B0FC345B37AF1A` 동일, release/Stock.exe.sha256 갱신. 이전 root는 artifacts/before-compact-ui-20261107-190205/Stock.exe 보존. Native 새 루트 EXE 시작→오프라인 주 화면 정상 표시 확인. 격리 데모 종료. GitHub commit/push/tag/Release 없음.
