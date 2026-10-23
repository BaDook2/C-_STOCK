# 국내 과매도·낙주 반등 구현 보고

새 전략은 기존 C#/WPF/MVVM과 SQLite를 사용하는 **단일 국내 종목 일봉 백테스트 전용**이다. 기존 실제 계좌 다중매매·틱/파이프라인·정기매수 기능은 유지한다. 아래 수치는 Config의 초기 검사값이며 수익성 검증이나 추천값이 아니다.

## 1. 추가/수정 파일
추가 Core: `Stock.Core/Research/Oversold/OversoldConfig.cs`, `HistoricalFacts.cs`, `SupportProviders.cs`, `OversoldBacktest.cs`, `OversoldResults.cs`.

추가 App: `OversoldWorkspace.cs`, `OversoldParameterPanel.xaml(.cs)`, `OversoldResultDialog.cs`. 추가 Tests: `OversoldTests.cs`, `OversoldUiTests.cs`.

수정: Core `Research/WorkspaceModels.cs`(기록 참조), `Trading/ParameterMetadata.cs`(전용 도움말 적용범위); App `WorkspaceFeatures.cs`, `MainWindow.xaml`, `StrategyEditorViewModel.cs`(선택지 한국어), `PublishedSmoke.cs`; Tests `Program.cs`, `UiTests.cs`. 문서: TASKS, VALIDATION, 이 보고서, `oversold-rebound-spec.md`, `docs/ui/OversoldBacktest.md`, `docs/api/OversoldHistoricalFacts.md`, architecture.

## 2. 전체 실행 흐름
검색으로 얻은 완료 일봉과 당시 facts → Config/자료 검증 → 시작 전 warmup → 시장일별 대기 주문 시가 체결 → 현재 확정봉 지표/지지 후보 → 보유 청산 또는 진입 판단 → 일별 순자산 → 성과 → SQLite snapshot/테스트 기록. UI는 background engine을 호출하고 결과를 표시한다. 실제 증권사 주문 경로를 호출하지 않는다.

## 3. Config 구조
`OversoldConfig` 한 곳에 기간/낙폭/RSI/거래량/지지/반등/자본/분할/손절/익절/기간/비용을 둔다. 모든 public 입력 속성의 ParameterAttribute가 Label/Tooltip/Popup/범위를 제공한다. `ParameterField`와 기존 공통 도움말을 재사용한다. 6개 접이식 그룹, 저장/복원, 개별 범위와 비율합/목표순서/예산 교차검증을 적용한다. 새 일봉 전략은 기존 Standard/Advanced 틱 전략의 값을 덮어쓰지 않는다.

## 4. Universe Filter
해당 날짜·판단 시각의 KOSPI/KOSDAQ/Common, Suspended=false, Administrative=false만 진입 가능하다. ETF/ETN/SPAC/Other/Unknown 제외. 이전 20 **관측 완료 일봉**의 실제 거래대금 평균 >=3,000,000,000원. 현재 분류를 소급하거나 종가×거래량으로 거래대금을 만들지 않는다. 자동 과거 facts API가 없어 현재는 JSON 시점 입력이다. 해당 자료가 없으면 날짜별 이유와 함께 보류한다.

## 5. Crash Trigger
`(현재 Close / 기간 내 최고 High - 1) × 100`. 현재 완료봉 포함 20봉 -20% 또는 3봉 -15%. 해당 window가 준비된 경우만 인정하며 두 조건은 OR. 3일 전 Close 기준을 쓰지 않는다.

## 6. RSI/Volume 옵션
RSI는 첫 14변화량의 평균 상승/하락으로 시작해 Wilder smoothing. 상승/하락 모두 0이면 50, 하락만 0이면 100. 기본 RSI<=30. 거래량은 현재 Volume / **현재를 제외한** 이전 20봉 평균 >=2. 활성 조건 모두 AND, OFF는 필수 판단에서 제외. 준비 미완료/0평균은 통과하지 않는다.

## 7. MA Support
120/240/480/999 기본. 전체 제공 과거 완료봉을 warmup에 사용하고 준비된 기간만 지지 후보로 제공한다. 준비 부족은 개별 후보 제외와 안내. 공개 제공처의 약 1,500봉 제한을 무시하고 MA999가 준비됐다고 주장하지 않는다.

## 8. Confirmed Pivot High
중심 High가 좌20/우20 모든 High보다 엄격히 높으면 확정. 같은 높이는 제외. 여러 과거 확정 고점을 보관하고 cluster에서 현재가 주변 후보를 선택한다. 별도 breakout 조건이나 임의 중요도 점수를 추가하지 않았다.

## 9. Pivot 미래 방지
오른쪽 확인 봉이 도착한 시점에서만 후보를 추가한다. 발생일과 확정일을 별도 저장. 아직 도착하지 않은 전체 배열 suffix를 provider에 전달하지 않는다. 중요도 확장은 ISupportProvider 구현 교체로 가능하다.

## 10. ApproximateVolumeZone
최근 240완료봉의 Low~High가 차지하는 bin에 각 봉 거래량을 균등 배분하고 누적한다. 마지막 bin에 잔여 소수를 더해 총량을 보존한다. 최대 bin 거래량의 70% 이상인 bin을 선택하고 인접/1% 거리 구간을 병합한다. 실제 틱 volume profile이 아님을 표시한다.

## 11. Price Bin
기본 KRW 100원 폭, 호가 단위 이상으로 올림. MaxBins=512보다 세분화되지 않도록 필요 시 폭 확대. 양끝 가격의 floor(price/폭) bin을 포함한다. Low=High는 하나의 bin. 실제 사용 폭은 지지 후보 설명에 보관한다. 대표가격은 누적 거래량 가중 bin 중심.

## 12. Cluster 알고리즘
후보 ID 중복 제거 → 대표가격 오름차순 → `(오른쪽 대표 / 왼쪽 대표 - 1)×100 <= 5`인 인접 후보를 연결한다. 전체 폭을 5%로 제한하지 않으므로 연결식 군집의 전체 폭은 더 넓을 수 있다.

## 13. Cluster 경계
Lower=후보 하단 min, Upper=후보 상단 max, Center=중복제거한 후보 대표가격의 단순평균. 후보 목록/종류/개수/ID/발생일/확정일을 함께 저장한다. 기본 최소 후보 2개이며 bin 개수를 후보 수로 세지 않는다.

## 14. Support 접근
확정 Close가 `[Lower×(1-distance), Upper×(1+distance)]` 범위 안이면 접근. 기본 distance=3%. 여러 군집이 겹치면 현재 Close와 중심이 가장 가까운 군집을 선택하고 동률이면 낮은 중심을 먼저 사용한다.

## 15. Rebound 상태관리
기본 OFF. ON이면 최초 유효 접근을 마감에 확인하므로 그날 과거 Low 대신 접근 Close로 저점을 시작한다. 이후 Low를 추적하고 Close>=저점×1.03을 요구한다. 필터가 잠시 미충족인 날의 Low도 추적한다. 동일 후보 ID 구성/중심 변경 허용 범위(5%)/후보 수명20시장일을 벗어나면 새 접근으로 초기화한다. 진입에는 그날의 나머지 조건도 필요하다.

## 16. N→N+1 체결
N 마감에 신호를 기록하고 다음 거래 가능 Open에 정수주식으로 체결한다. SignalTime/ExecutionTime은 한국시간 개장·마감 입력으로 분리. 거래정지·누락봉은 대기, 종료일 대기는 미체결로 남긴다. 당일 최종 Volume으로 시가 주문량을 제한하지 않는다. 다음 시가 진입 시 상태/위험도 다시 확인한다.

## 17. 분할매수
예정 총투자금의 40/30/30, 최대3회. actual 모형 시가와 슬리피지/수수료/현금으로 살 수 있는 정수주수를 내림한다. 현금 부족은 가능한 주수만, 0주면 체결과 횟수 증가 없음. 사용하지 못한 회차 예산을 임의로 다음 비율에 합산하지 않는다. 일봉에서 실제 시장 유동성에 의한 부분체결을 복원하지 않는다.

## 18. 추가매수 AND
현재 Close가 직전 실제 매수가 대비 -5% 이하 AND 유효 군집 접근(기본 ON) AND 당시 위험 false/일반주식 상태. 추가매수는 최초 급락/RSI/Volume을 새 필수조건으로 강제하지 않는다. 익절이 실제 체결된 뒤에는 신규 분할매수 금지.

## 19. Support Break
최초 진입 신호의 cluster snapshot을 고정한다. Close<=최초 Lower×0.97이면 청산 신호. 추가매수 때 더 낮은 지지 후보가 생겨도 최초 기준을 아래로 이동시키지 않는다.

## 20. Emergency Stop
옵션 ON 기본, 평균 실제 매수가 대비 -15%. High/Low 감지 정책에서는 Low, CloseOnly에서는 Close로 감지한다. 체결은 다음 거래 가능 시가이며 손절가격 체결을 보장하지 않는다.

## 21. TP1/TP2
각 독립 ON/OFF, +10%/+20%, 30%/30%. 첫 실제 익절까지의 최대 보유수량을 기준으로 고정, 각 단계 수량은 floor(기준×비율)이며 잔여 수량을 초과하지 않는다. 0주 단계는 가짜 체결 없이 완료 처리해 반복하지 않는다. 같은 봉 두 목표 충족 시 다음 시가에 두 leg, 익절 이후 추가매수 금지.

## 22. Trailing Stop
옵션 7%, AfterEntry 또는 AfterTakeProfit1 선택. 기본은 1차 실제 익절 후. 첫 활성화 시가를 시작 최고가로 두며 이후 **이전 확정 최고가로 stop 판단한 뒤** 현재 High(또는 CloseOnly의 Close)를 반영한다. 1차 익절 미체결/0주는 AfterTakeProfit1을 활성화하지 않는다.

## 23. 같은 봉 TP/Stop
SupportBreak → EmergencyStop → TrailingStop → 최대보유기간 → TP 순. 어떤 감지도 동일 봉 가격으로 소급 체결하지 않는다. 거래정지 동안 미체결 TP가 있어도 새 위험 청산 신호가 생기면 전량 청산 대기로 대체한다.

## 24. MaxHoldingDays
제공된 전체 시장 session 기준, 최초 체결일 포함20번째 마감에 잔여 청산 신호 → 다음 가능한 시가. 종목 봉이 빠져도 시장일은 카운트된다. 입력 달력이 정확한 실제 휴장일인지 자동 인증하는 API는 없다.

## 25. Reentry
전량매도 후5개 시장일이 완전히 마감돼야 새 신호 허용, 기본 예외 없음. 새 중심<=직전 진입 근거 중심×0.95 AND 접근/급락/활성필터/위험 조건. 명시적 early 옵션을 켜면 대기기간만 생략하고 더 낮은 군집과 나머지 조건은 유지한다.

## 26. FundamentalRiskFlag
`FundamentalRiskRecord(Symbol, Date, IsRisk, KnownAt)`. 날짜별 최신 알려진 사실을 사용하며 누락은 미지. JSON에서 IsRisk를 생략하면 거부한다. 다음 시가도 재확인하고 true/unknown은 첫 진입·추가진입을 취소한다. 자동 뉴스 분석은 구현하지 않는다.

## 27. Performance Metrics
첫 매수~전량매도 한 거래. 완결 거래만 거래수/승률/평균수익률·손실률/보유일/PF/기대손익에 반영. PF=양의 실현손익 합/음의 절댓값 합; 이익만 있으면 ∞, 빈 분모는 미산출. Expectancy=완결 거래 평균 손익, 수익률도 별도 평균.

CAGR=(최종평가/초기현금)^(365.25/첫 선택 session Open~마지막 Close 경과일)-1(평가점 최소2개). MDD는 초기현금부터 이어지는 비용포함 equity의 고점 대비 최대 하락. Sharpe는 일별 순자산 수익률-연 무위험수익률의 일 환산 값의 평균/표본표준편차×sqrt(연율화거래일, 기본252). 표준편차0은 미산출. 미청산은 완결 통계에서 제외하되 최종 순자산에 포함한다. 종목별 독립자본이며 공유자본 포트폴리오 성과가 아니다.

## 28. Unit Test 목록
`OversoldTests.cs`: 기본/교차검증, HIGH 기반 중·단기 trigger, Wilder seed/smoothing, 현재 제외 거래량, pivot 확정/동률/여러후보/날짜, MA999 미준비, bin 총량·개수·flat, 연결식 군집/ID중복/경계, 다음 시가 gap/수량, 미청산 통계, 마지막 대기, 활성 RSI/Volume, 위험 누락·미래 KnownAt·생략 JSON, 실제 거래대금 누락·미래 공표, ETF/ETN/SPAC, 정지 대기/next-open risk, 분할3회·support AND·risk AND·첫 lower고정, 비용/세금/슬리피지, TP 두단계/고정수량/반복0주, TP 후추가금지, stop우선/정지중 TP대체, prior-peak trailing, CloseOnly, 보유기간, 반등 저점, 0주, 캘린더 누락/봉시각불일치/누락봉, 재진입 거리·5일·옵션, warmup 무주문, 길이별 prefix invariance, 성과 손계산/빈통계, SQLite snapshot, 취소.

`OversoldUiTests.cs`: command 실행→저장→카드/기록, 사실/일봉 snapshot, 정확한 0.015 수수료, VM 재생성 후 설정·기록 복원, JSON 오류 시 저장값 보존, 두 창 크기 chart 유지, 설정/성과/상세 WPF 렌더. 실제 마우스 입력·타 모니터 DPI 체감 검증과 구분한다.

## 29. Look-ahead 방지
완료봉만/시간 오름차순/동일 날짜 중복 금지, 시작 전 지표만 준비, 날짜별 후보 한 번 계산, 우측 확정 pivot, 날짜/알려진 시각 facts, N신호→다음Open, 당일 전체거래량 시가수량 사용 금지, 최초지지 불변, 과거저점 소급 금지, prior-peak trailing, 마지막봉 강제청산 금지, suffix 추가 전후 신호 비교.

## 30. 일봉 데이터 한계
시가 호가·체결순서·실제 부분체결·당일 고저가 도달순서를 재현하지 않는다. 근사 매물대는 true volume profile이 아니다. 기본 비용은 사용자가 조정할 가정이고 거래세 자동 적용/배당/분할 통합 조정 없음. 공개 히스토리 길이 제한, 과거 universe/실제 거래대금/위험 자동 API 미구현, 현재 상장종목으로 과거 전체시장 생존편향을 해소했다고 주장하지 않는다. 시장일/당시 사실 JSON 없으면 유효 진입을 만들지 않는다.

## 31. 추후 교체할 모듈
실제 틱/분봉 provider, TrueVolumeProfile용 ISupportProvider, 시장 캘린더/당시 분류·거래대금 provider, 위험 provider를 연결할 수 있다. 장중 실행 모형을 도입할 때는 일봉 next-open loop의 체결 부분을 검증된 intraday execution engine으로 교체해야 한다. Config/거래기록/지지군집 인터페이스를 유지할 수 있으나 이 전략을 현재 실계좌 목록에 단순 등록하는 것으로 실제매매 지원이 완료되지 않는다.

## 32. Build 결과
2026-10-03 Release 성공, 전체 621개 검사 통과(기존547+새 Core67+새 UI7). artifacts/oversold-build-final.log, oversold-regression-final.log. Windows win-x64 단일 publish 성공: artifacts/oversold-publish.log. EXE만 있는 별도 폴더에서 종료0, Success=true, OversoldTrades=2, RawNativeRendered=true, RawTicks=2,000, NoBrokerCalls=true 확인: artifacts/oversold-clean-result/result.json. 새전략/기존 정기매수·스크리닝·원본틱 실제 렌더 검사. 전용 추출 폴더에서 libSkiaSharp.dll 존재 확인.

## 33. Warning / Error
중간 검사 중 잘못된 BarEquity 속성명, WPF StackPanel 글꼴 속성, 배열 대상형식, RSI 소수점 정확동등 비교, UI 결과 통화 예상값 오류를 발견하고 수정했다. 최종 빌드는 Warning 0, Error 0이며 publish 경고/오류도 없다. 단독 실행은 합성 TEST_FIXTURE이며 실제 API/실계좌의 동작을 검증한 것으로 간주하지 않는다. 실제 계좌 키 변경·인증·실자금 주문 없음.

검증된 배포 파일: Stock.sln 옆 Stock.exe, 80,560,114 bytes, SHA256 190C9EE9A1364AFA1C3C820BDACD1A245DDAB42A6CBE91CC7391EFB18C70FDE3. AGENTS.md는 같은 폴더에 유지한다. 선행 다중매매/UI 배포와 이번 일봉전용 전략 배포를 모두 완료했으며 실자금 주문은 실행하지 않았다.

## 후속 사용자 변경 — 오늘의 전략만 노출
최초 구현의 '기존 전략 유지'는 계산 코드/과거 기록 보존을 뜻하는 상태로 변경되었다. 현재 UI·프리셋·스크리닝·실행에는 국내 과매도·낙주 일봉 전략만 남긴다. 구형 전략을 다시 저장하거나 자동 재생/틱/다중매매를 통해 실행할 수 없다. 새로운 실계좌 자동매매 승인은 없으므로 지원 없음으로 시작을 차단한다. 선택 계좌 수동 주문과 정기매수 도구는 유지한다.

추가 파일: `Stock.App/OnlyOversoldWorkspace.cs`, `Stock.App/MinimumTextSize.cs`, `Stock.Tests/OnlyOversoldUiTests.cs`. 관련 Workspace partial/View/PublishedSmoke/기존 UI 검사와 문서를 수정했다. Core Oversold 전략 수식이나 임계값은 이번 변경에서 수정하지 않았다. 과거 시점 자료의 자동 API 미지원 및 자료 부족시 진입 보류 제한은 여전히 적용된다.

## 2026-10-03 단일 낙주 전략 / 오른쪽 최소14 최종 완료
- 사용자 선택·등록·스크리닝·백테스트는 국내 과매도·낙주 반등만 노출한다. 구형 프리셋 복원/틱·Pipeline 명령/자동 재생/실계좌 다중매매 우회 실행을 차단했다. 기존 DB와 설정·결과 기록은 삭제하지 않았다.
- 수동 실계좌 주문(선택 단일계좌), 수동 모의매매, 차트·원본 틱·관심그룹 및 정기매수 도구 보존. 새 전략은 일봉 백테스트 전용이고 실제 자동 주문 지원 전략 없음으로 안내한다.
- RightToolsPanel 3탭 최소14 DIP: 템플릿·달력·툴팁·정기매수 상세 도움말 포함. 제목 확대값 유지, 다른 화면 크기 보존, 1540×1040 / 1160×760 스크롤·차트 검사. 결과 안내 줄바꿈 보완.
- 전체 회귀 636개 통과: artifacts/only-oversold-regression-final.log. 이후 정기매수 상세 도움말 최소14를 추가하고 최신 UI43검사 통과: only-oversold-ui-final.log. 이 43개는 겹치는 검사이며 전체 수에 합산하지 않는다. Release 0경고/0오류: only-oversold-build-final.log. Core67검사 포함 기존 계산 회귀 보존.
- publish: artifacts/only-oversold-publish.log. EXE만 복사한 only-oversold-clean-exe에서 smoke 종료0. only-oversold-clean-result/result.json Success=true, OnlyOversold=true, LegacyCommandsBlocked=true, OversoldTrades=2, RecurringSessions=234, RawNativeRendered=true, RawTicks=2000, NoBrokerCalls=true. 새 only-oversold-native/Stock 하위 libSkiaSharp.dll 추출 확인. TEST_FIXTURE 검증이며 실계좌/실시세 증명은 아니다.
- Stock.sln 옆 Stock.exe 배포 및 publish SHA256 일치: 80,564,839 bytes / EAABCEBFD97DC25AFB42DBD3537B413CF1179B19423BD996917426936B8D9BCC. AGENTS.md 옆 유지. artifacts/only-oversold-release.json.
- 수정 핵심: OnlyOversoldWorkspace.cs / MinimumTextSize.cs 신규; WorkspaceViewModel·WorkspaceFeatures·StrategyEditorViewModel·ConfiguredScreening·OversoldWorkspace·MultiTradeWorkspace/ViewModel·MainWindow·RecurringPurchasePanel/Caption·PublishedSmoke·정책/UI 회귀검사. README/StrategyPage/ScreenerPage/ChartTools/MultiTrade/OversoldBacktest/architecture/보고서 갱신.
- 기존 한계 유지: 당시 분류/실제 거래대금/위험/검증 거래일 자동 API 미지원. 시점 JSON 필요, 없으면 낙주 진입·스크리닝 보류. 정기매수 배당 재투자 미지원. 물리 마우스/DPI 이동 검사 미수행. 실제 돈 주문·실키 사용·중요 DB 삭제 없음.
- 승인 범위 완료. 안전하게 계속할 다른 미완료 구현 항목 없음. 기존 [!] 실계좌 검증은 임의 수행하지 않는다. 위 진행 중/배포 대기 체크포인트는 이 완료 기록으로 대체한다.
