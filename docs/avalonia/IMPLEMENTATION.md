# 1차 이식 변경 보고

후속 원본 WPF 기준 디자인 조정·수정 파일과 검증은 [UI_PARITY.md](UI_PARITY.md)에 있다. 새 화면/테마·차트 표현과 파생 TextBox 테마 수정이 반영되었으며, 아래 내용은 기능 전환의 기본 구조를 설명한다.

원본 WPF P0~P6의 당일매매 엔진·전략 D 작업을 먼저 끝냈다. WPF 전체1651개 회귀, D 코어192개, WPF D UI13개, Release0경고0오류, 단일EXE 격리 시작 검사 후 원본 `Stock.exe`를 갱신했다. 기존 exe는 원본 artifacts에 백업했다. 기준 해시는 원본 `docs/verification/StrategyD-Progress.md`에 기록했다.

## A0/A1: 프로젝트와 공통 코어

`STOCK.Avalonia.sln`에 Core/App/Portable.Tests/Desktop.Tests를 분리했다. Core80개 소스 snapshot은 manifest와 원본 해시를 대조한다. 원본 DB·키·개인config·.git·bin/obj·EXE를 복제하지 않았다. 앱 이름공간은 `Stock.Desktop`로, Avalonia 이름공간과 충돌하지 않는다. 새 AppServices가 DI 구성과 독립 DB/공급자/공통 연구 서비스를 소유한다.

패키지: Avalonia.Desktop/Fluent/Headless12.1.5, 공식 DataGrid12.1.2, ScottPlot.Avalonia5.1.59. net10.0이며 `UseWindowsForms`/WPF 참조는 없다. DataGrid는 공식 legacy 유지보수 패키지로 향후 교체 검토 대상이다.

## A2/A4: 시세·차트·공통 백테스트

`MarketWorkspace`/`ChartTimeframeWorkspace`/`MarketHistoryService`가 검색·공급자별 시세·분/시간 집계를 담당한다. 공개 시세/가격 캐시는 기존 memory-mode Store를 재사용한다. `SelectedQuotePump`의 취소 세대와 VM의 generation으로 이전 종목/계좌 응답을 차단한다. 현재가 표시와 일봉 진행 상태만 갱신하며 미완성 관측을 전략의 확정 원본 체결로 만들지 않는다.

`StockChart`는 ScottPlot OHLC array/거래량 Bars/불규칙 시간 SignalXY MA를 사용한다. 가격 숫자 내림, 원본 체결 시간·가격 별도 표식, 매매한 봉 한 번 클릭, 축소 시 pixel 간격에 따른 표식 병합과 확대 시 분리, viewport 보존·volume 축 동기화를 지원한다. 여러 binding 변경은 한 번의 redraw로 합친다. 매매 marker와 지지 zone은 별도 층이다. `ChartMarkerLayout`은 표식만 묶고 원본 체결은 수정하지 않는다.

스크리닝 popup은 전체 기간→우클릭 위치의1,200봉 상세→다시 우클릭 전체보기다. 상세에서 휠/드래그를 허용하고 첫 상세 폭 이상 축소하지 않는다. 전체와 상세가 같은 작은 자료는 처음부터 자유 조작한다. 1만봉 전체/1,200봉 상세의 실제 AXAML 입력 검사를 수행했다. hover 때마다 거래량 plot을 불필요하게 다시 그리는 것도 제거했다.

`OversoldBacktestService`는 원본의 UI 비의존 서비스 wrapper를 namespace만 바꿔 옮겼다. 단일 종목/스크리닝/기록 popup은 같은 인스턴스를 사용한다. `ResearchBrowserViewModel`은 스크리닝 당시 설정·봉·기간을 고정하고 하나의 창에서 이전/다음 이동을 제공한다. 취소된 이전 응답은 generation 검사로 적용하지 않는다. 같은 자료/설정/기간 cache는 원 서비스가 관리한다.

## A3: 설정

`StrategyEditor`는 원본 ParameterCatalog와61개 Property/validation/legacy JSON을 그대로 사용한다. 저장 snapshot과 편집값을 분리하고 추가·이름 변경·복구·async 파일 선택을 제공한다. `SettingInput`은 bool/enum/text 중 실제 필요한 editor 하나만 생성한다. 숨겨진 빈 ComboBox가 numeric Text에 null을 쓰던 Avalonia binding 문제를 실제 UI 검사에서 찾아 수정했다.

`StrategyNumberBox`는 정수 부분의 정상 삽입을 유지하며 소수부만 덮어쓰고 커서를 진행한다. .00 표시를 유지하고 수수료0.015는 정확히 보존한다. EntryRatios는 원래 쉼표 문자열 전체를 표시한다.

전략 D의 `StrategyDEditorModel`, 버전2 JSON과 `DayTradingViewModel`을 재사용했다. `DayTradingPreparation`은 독립 일봉 MA seed/분봉/원본 가용성을 구분한다. `StrategyDRuntime`·`DayTradingPortfolioEngine`·`IntradayBacktestService`·`DayTradingSessionService`는 원본과 동일하다. 팝업은 실행 수명을 소유하지 않는다.

## A5: 일반/실시간 스크리닝

`ScreeningWorkspace`는 원본 OversoldScreeningEngine을 재사용한다. 최대4개 자료 준비, 완료 순서 게시, 전체/현재 진행 상태,17단계 funnel, 종목별 조건값/기준/사유,0결과 열람, 진행 중 완료한 종목 popup을 지원한다. 목록을 매번 지우지 않아 선택 행을 유지한다. 긴 universe 조회 **전** 시장/전략 이름/설정/봉/기간을 동결한다.

`RealtimeScreeningHost`는 앱 수명 feed coordinator다. `RealtimeOversoldScreeningService`와 `RealtimePaperStrategyBatch`를 재사용하며 새 매매 엔진을 만들지 않았다. 시세 수신/분석은 background, UI는1초 snapshot이다. 종목별 준비·수신 오류를 격리하며 실패를 빈 정상 자료로 위장하지 않는다. 자동 체크는 신규 진입/추가매수 gate, 정상 중지는 기존 보유/미체결 보호 유지, 긴급중지는 실행 중단이며 임의청산하지 않는다. 전역 계좌 모드 변경도 기존 feed를 중지한다.

전략 변경은 이전 표를 초기화하고 재시작을 요구한다. 기존 가상 포지션이 있으면 새 빈 실행으로 덮어쓰지 않는다. 감시100/기존 가상 matcher20 한도, 일봉 신호만·온라인 분/시간 자동 가상, 해외 실시간 전략 미지원은 명시 제한이다. 공개/REST 관측값은 원본 틱으로 보관하거나 실제 체결로 표시하지 않는다.

## A6: 관심·정기매수·시장·기록

`WatchWorkspace`: 그룹 추가·이름·공통 별·중복 제거·스크리닝 대상 저장.
`RecurringWorkspace`: 기존 RecurringPurchaseBacktest와 구매/성과 모델 그대로 사용.
`MarketOverview`: 원본 IIndexChartData의5개 지수·실제 irregular series.
`ResearchHistory`: 백테스트/일반스크리닝/D/정기매수의 가벼운 metadata index, 날짜 조회, 당시 전체 결과/봉/설정 상세 재현. 원본 이벤트/재조회 캐시를 테스트 자료라는 이름으로 무조건 저장하지 않는다.

## A7: 계좌와 안전 경계

`AccountWorkspace`는 기존4개 broker factory를 사용해 인증→잔고 검증 후 연결한다. 하나의 활성 계좌만 시세 경로로 쓰고 검증된 메모리 세션을 재사용한다. 잔고/주문 조회, 입력 가림/보기·복사 차단·선택/전체 초기화, 모드 변경 취소를 제공한다. 입력 키를 파일/SQLite/로그에 쓰지 않는다. 비지원 봉 기준은 비활성화한다.

`VirtualExecutionWorkspace/Dialog`는 관심그룹 선택·중복 종목 체크 동기화·제거·첫 등록 전략 기본 선택·종목별 전략/예산·전체 투자/일 손실 한도 편집을 제공한다. 같은 RealtimeScreeningHost/기존 paper matcher를 사용하므로 팝업 닫기/재열기/스크리닝/긴급중지에 실행 상태가 하나다. 시작 후 편집을 잠그고 중복 실행을 막는다. 전역 전략 편집을 바꿔도 실행 중 종목별 설정은 snapshot으로 유지한다. 가상 체결의 당시 지지·추가매수/청산 근거는 기존 주문 저널의 OversoldSnapshot에서 읽고 현재 지지를 재계산하지 않는다. 새 관측이 없는 heartbeat마다 큰 저널을 반복 쓰지 않도록 coordinator를 조정했다.

macOS Keychain과 실계좌 주문 전송 UI/대사는 아직 이식하지 않았다. UI가 실행 가능한 척하는 주문 버튼을 두지 않았다. 가상 포지션/미체결 종료 전 경고를 표시한다. 실패한 startup은 별도 오류 창과 type/stack 진단으로 남기며, broker 응답/키를 진단에 넣지 않는다.

## A8: 검증·배포

실제 AXAML과 ScottPlot을 쓰는 Avalonia Headless 검사, 공통 portable 검사, Windows native EXE startup-smoke, osx-arm64/x64 cross-publish를 구분해 기록한다. `scripts/verify.*`와 `scripts/publish-macos.sh`, 공식 구조의 Info.plist를 추가했다. 실제 Mac에서의 실행·보안 저장·서명/공증은 확인되지 않았다. 최신 수치/아티팩트는 `VERIFICATION.md`를 따른다.

## 후속 이식·검증

- 실제 Mac: 빌드/실행·파일 picker·한국어·트랙패드·SQLite·native 차트 검증.
- macOS Keychain, 사용자 선택 DB/관심그룹 이관 도구(현재 자동 복제 없음).
- 실계좌 주문 UI/대사 및 broker별 실제 read-only API 검사. 명시 활성화/검증 전 실제 주문 금지.
- 일봉 다음 거래일 가상 주문/보유 복원, 해외 실시간 전략.
- WPF의 일부 고급 chart 설정(일목균형표 등)과 Mac 트랙패드·UI 세부 동등성.

이 목록은 숨긴 오류가 아니라 전환 단계에서 아직 구현/확인하지 않은 범위다. 원본 WPF가 이 프로젝트로 교체된 것은 아니다.
