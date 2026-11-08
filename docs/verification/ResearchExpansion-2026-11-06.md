# 근접 후보·미국 일봉·테스트 기록 확장 (2026-11-06)

## 변경 동작

| 요청 | 구현/근거 |
|---|---|
| 전략에 근접한 후보 | 전략 설정의 공통 → 스크리닝 후보에 `근접 후보 표시` 기본 ON, `후보 표시 최소 접근도` 기본60.00점. 저장 전략·JSON·스크리닝 snapshot에 포함한다. 일반/실시간 목록이 같은 ScreeningCandidatePolicy를 사용한다. |
| 실제 전략 매매 표시 | 기존 IOversoldBacktestService/EntryDetector를 재사용한다. 근접 후보는 매수 승인이 아니다. 당시 설정/가격으로 실제 발생한 모형체결과 저장 실제·가상 체결만 출처를 구분해 표시한다. 체결이 없으면 표식을 생성하지 않는다. |
| 장외 팝업 | 시작 전 KRX 정규장 시각 검사. 공개 공급자가 CLOSE를 반환해도 준비봉 로딩 전 장 외 시간 팝업. 세션/주문을 만들지 않는다. |
| 미국 종목 | 애플 등 검색 결과의 Naver reutersCode를 유지하고 StockViewer와 같은 공개 현재가 경로를 사용한다. 과거 가격도 Naver 실제 일별 OHLCV로 변경. 온라인 연결 계좌가 다른 시장이면 공개 현재가를 사용하며 다른 시장의 피드를 구독하지 않는다. |
| 해외 시장 선택 | S&P500, 나스닥100, S&P500+나스닥100. 사용자가 나스닥100으로 확인했다. 현재 구성 종목 전체/중복 제거, 미국에서 국내 상위N을 숨긴다. |
| 테스트 기록 | 기존 SQLite state의 백테스트/스크리닝 실행을 한 번당 한 행으로 모은다. 실행일(KST) 기간 필터, 더블클릭/상세 버튼 → 기존 결과/차트. 일반 및 중지·종료한 실시간 스크리닝도 저장. |

## 계산·저장

- 기존 접근도 계산을 재사용한다. 활성 비진단 조건의 평균이며 통과1/미지0, Crash는 실제 낙폭/기준, RSI는 100-RSI/100-최대RSI, Volume은 실제/기준을0~1로 제한한다. 확률이 아니다. 확정된 위험·상품 제외·정지/관리 제외는 근접 후보로 우회하지 않으며 주요 지표 준비 부족도 표시 후보로 통과시키지 않는다.
- 후보 표시 조건과 실제 Enter/체결을 분리했다. OFF면 기존 조건 후보만 표시한다. 거래대금 등 미충족이 남은 근접 후보는 `근접 후보`로 명시한다. 실제 매수는 기존 엔진과 위험/사실 검사를 통과해야 한다.
- 미국 일봉은 Eastern 날짜/16:00 마감/DST, 다음 관측 거래일09:30 체결 모형이다. 국내 KST 날짜로 다음 날로 밀리지 않으며 차트 체결 범위도 해당 거래소 날짜다.
- 새 SQL 테이블/DB 초기화 없이 기존 state를 재사용했다. `screen-data:Hash` 원본은 중복 제거, 실행·종목·봉의 `screen-bars:` 포인터, `screen-facts:` 종목별 사실, `screen-chart:` 최초 차트/체결 snapshot. 메인 백테스트는 기존 `oversold-bars-snapshot:DataHash`를 재사용한다.
- 과거 기록은 현재 시세로 재계산하지 않는다. 원본이 없으면 저장 요약과 원본 부재를 안내한다. 스크리닝 차트 첫 계산도 당시 보관한 원본을 우선한다. 저장 당시 이후 실제 체결을 과거 결과에 섞지 않는다.
- 실시간 중지 상태를 먼저 적용한 후 원본/기록을 저장해 긴급중지 차단이 DB 저장을 기다리지 않게 했다. 기존 보유 보호 정책을 유지한다.

## 수정 파일 지도

- 전략: OversoldConfig, OversoldParameterLayout/Metadata, ScreeningCandidatePolicy, CurrentOversoldScreening, OversoldScreening, RealtimeOversoldScreening.
- 해외: ForeignPublicMarketData(신규), PublicMarketData, PublicCurrentMarketData, HistoricalFacts, ResearchMarketSession(신규), SupportProviders/DailyDates, OversoldBacktest/Results, LiveDailyWorkspace.
- 화면/기록: ResearchHistoryWorkspace(신규), ScreeningMarketClock(신규), WorkspaceViewModel, OnlyOversoldWorkspace, OversoldScreeningWorkspace, PublicRealtimeScreeningWorkspace, RealtimeScreeningWorkspace, ScreeningNavigation, MainWindow.xaml(.cs).
- 검사: ResearchExpansionTests(신규), Program, CurrentDailyTests, OversoldTests의 fixture 기본값, FixedEditorTests, ParameterScopeUiTests, ScreeningUiTests, UiTests, ButtonAuditTests, PublishedSmoke.
- UI/API/검증 문서와 TASKS/HANDOFF도 갱신했다. AGENTS/Verification의 사용자 요청 시만 릴리즈 정책을 유지했다.

## 검증

- 전용16개 성공: 후보 ON/OFF/점수/위험/검증/JSON, 애플 종목 식별자, 현지 일봉, 지수 중복 제거, 해외 공통 평가/다음 시가, 장외 세션 차단, 기록 즉시 반영/차트/기간/재시작.
- 관련 WPF343개 성공. 첫 전체는 제거된 옛 `낙주기록상세` 버튼을 찾는 감사 검사에서 실패해 새 기록 행·상세 버튼 동작으로 갱신했다. 첫 배포 검사는 예전8/51/0 및26행을 전제한 검증 코드에서 실패해 실제 새8/53/0·27행으로 변경했다. 검사를 생략하지 않았다.
- 전체 회귀1,333개 성공. 마지막 해외 피드 시장 구분과 배포 검사 갱신 후 재빌드/회귀/배포 검사 결과는 아래 최종 확인에 기록한다.
- 실제 공개 API2개 성공: 애플 AAPL.O, 2,513개 일봉(2016-11-07~2026-11-05), 당시 현재가341.44 USD; S&P500501개/나스닥100101개/합집합516개. 복수 클래스 포함으로 지수 명칭과 주식 수가 다르다. 자료: artifacts/research-foreign-live/foreign-public.json.
- 실제 Windows 마우스(격리 DB): 기록 행 더블클릭 → 저장 차트, BUY 클릭 → 거래 행·당시 지지 상세 선택, 근접 후보 ON/60.00 입력 표시 확인. 해외 메뉴 선택 시 상위N 숨김/일봉/USD 안내 확인.
- 실제 사용자 DB 읽기 전용 검사: 기존 백테스트5개 모두 차트 원본 보존, 스크리닝2개. DB/키 변경·실계좌 주문 없음. fixture/가짜 broker 검사를 실제 계좌 주문 성공으로 표현하지 않는다.

## 2026-11-07 저장소 판정 정정

위의 실제 사용자 DB 정상/5백테스트·2스크리닝 보존 주장은 도구 AppData가 Codex LocalCache의 다른 DB로 리다이렉트된 결과였다. 실제 사용자 DB의 근거로 사용할 수 없다. 실제392MB DB 손상 재현, 원본 보존, 사용자 승인 복구와 실제 앱 확인은 [후속 완료 기록](ProgressiveScreening-2026-11-07.md) 및 [저장소 운영](../maintenance/Storage.md)을 따른다. 아래 EXE/검사 결과는11월6일 당시 결과이며 최신 로컬 파일은 후속 기록을 확인한다.

## 지원 제한

- 미국은 현재 일봉 연구만 지원한다. Naver minute 조회는400, day chart는 가격 표본이므로 이를 진짜 분봉 OHLCV로 만들지 않았다. 미국 분/시간 실시간 전략·자동주문은 이번에 추가하지 않았다.
- 미국 금액/투자금/거래대금은 USD, 자동 환산하지 않는다. 국내30억원 설정을 그대로 쓰면 미국에서도30억 USD 기준이므로 해외용 전략 값을 선택해야 한다.
- 현재 지수 구성 목록이다. 과거 편입/편출·상장폐지를 포함한 당시 유니버스 검증은 아니다.
- 미국 일봉 체결 연구에는 명시적인 가격자료 검증 모드가 필요하다. 과거 종목상태/위험의 가정을 결과에 표시한다. 미확인 자료를 실계좌 매수 승인으로 사용하지 않는다.
- 최초 SQLite11 유발 원인 미확정 상태는 유지하며 정상 개인 DB를 재구축/삭제하지 않았다.
- GitHub commit/push/tag/Release는 실행하지 않았다. 사용자 명시 요청 시에만 릴리즈한다.

## 최종 확인

- 최종 Release 빌드: 경고0·오류0, artifacts/research-expansion-build-final.log.
- 최종 전체 회귀:1,333개, 종료0, artifacts/research-expansion-full-final.log. 마지막 피드 시장 구분 보완을 포함한다.
- 최종 publish 종료0, artifacts/research-expansion-publish-final.log. 배포 EXE smoke 종료0/Success=true/NoBrokerCalls=true, artifacts/research-expansion-smoke-final/result.json.
- 루트 교체 후 EXE smoke도 종료0/Success=true/NoBrokerCalls=true, artifacts/research-expansion-root-smoke/result.json.
- publish/win-x64/Stock.exe, 루트Stock.exe, release/Stock.exe가 모두80,849,464 bytes, SHA256 `01D27CE34EC32CBD2BC9175CEE8F80F1275FBCAB18F3DFB5B1DD34A297F45A0A`로 일치한다. release/Stock.exe.sha256도 갱신했다.
- 이전 루트/release 파일은 artifacts/before-research-expansion-20261106-234544에 보존했다.
- 로컬 실행 파일만 갱신했다. GitHub 공개 v0.1.14는 이번 새 기능을 포함하지 않으며 commit/push/tag/Release를 수행하지 않았다.
