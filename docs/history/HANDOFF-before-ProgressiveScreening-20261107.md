# 현재 진행 — 2026-11-07

일반 스크리닝 중 이미 완료한 결과를 열도록 수정 요청. 아직 수정 전이다. Apple 검색도 조사 중이며 실제 검색 목록에는 AAPL 있음; 차트 로드의 state 쓰기와 fallback 읽기에서 SQLite11/11. failure-20261107-013158-{441,725} 진단에392,929,280bytes/Tree12·5 overflow/참조 오류. Python/별도 EXE로 논리 AppData 경로를 열면144,805,888bytes/정상 DB이고 GetFinalPathNameByHandle가 OpenAI.Codex LocalCache로 리다이렉트됨. 실제 사용파일을 사용자 Explorer 문맥으로 사본 보존할 계획. 아직 원본 변경/수리 없음. 사용자 앱 Stock pid11488, UI HWND525796(다시 목록 조회 필요). 데이터계좌 자동실행 없음/작업취소 비활성. API 애플 실조회2검사 정상, artifacts/apple-lookup-20261107.

partial screening 차단: MainWindow WrapPanel327 CanConfigure가 결과 버튼까지 끔, InitializeOnlyOversold의 결과열기 Add 공통Run(executing)·CanConfigure, OpenScreeningStock Busy 반환. 결과창은250ms snapshot 갱신하지만 ItemsSource 교체로 선택 잃음. 완성 종목만 독립 command/차트 token/lifetime으로 열고 현재스캔 취소·매매상태변경 없이 유지해야 함.

# 현재 상태 — 2026-11-06 요청 완료

## 완료 범위

전략 근접 후보·장외 안내·미국 일봉/지수 스크리닝·통합 테스트 기록을 구현하고 로컬Stock.exe까지 갱신했다. 사용자가 나스닥100 구성 종목으로 확인했다. StockViewer는 읽기 참고만 하고 수정하지 않았다.

- 공통 → 스크리닝 후보: ShowNearCandidates=true, MinimumCandidateProximity=60.00. 일반/실시간 목록만 제어하며 EntryDetector의 실제 매수/주문 조건은 바꾸지 않는다. 확정 위험/제외·미준비 주요 지표를 점수로 우회하지 않는다. 총61필드/27행/20빈칸, 사용범위8/53/0.
- 미국: Naver opaque ProviderCode(AAPL.O 등), 실제 일별 OHLCV `chart/foreign/item/{id}?periodType=month&range=120`; year는 주봉이므로 사용하지 않는다. Eastern/DST 현지 날짜/16시 마감, 다음09:30 시가. 다른 시장 연결 계좌에 현재가/피드를 잘못 요청하지 않는다.
- S&P500 .INX / 나스닥100 .NDX 현재 구성 종목을 페이지100개로 모두 읽고 합집합 중복 제거. 실측501/101/516. 국내 상위N은 해외에서 숨긴다.
- 미국은 일봉 연구, USD 단위/환산 없음/과거 구성 변화 미반영. 해외 분·시간 실시간 전략/자동주문은 추가하지 않았다. 미국 백테스트 체결 연구는 UsePriceOnlyBacktest ON이 필요하며 가정을 결과에 명시한다.
- 장외 실시간 시작은 팝업. 공개 제공자 CLOSE도 준비봉 조회 전에 차단한다.
- ResearchHistoryWorkspace가 기존 chart-run/oversold-screen을 한 실행당 한 행으로 모은다. 실행일KST 필터/더블클릭/상세→기존 ScreeningResultDialog/ScreeningChartDialog. 메인 백테스트 원본은 oversold-bars-snapshot 재사용, 스크리닝은 screen-data:Hash + screen-bars:실행:종목:봉 포인터, screen-facts, 최초 screen-chart snapshot. 당시 원본 없는 옛 기록은 현재 가격으로 가장하지 않는다. 실시간 중지/종료도 실행별 기록, 차단을 저장보다 먼저 처리.

## 검증/로컬 실행 파일

- 최종빌드0경고·0오류, 전체1,333개 종료0. 전용16개/관련343개/실제 공개 조회2개. 미국 다음09:30 모형체결·정확 날짜도 전용 검사에 포함.
- 실제 공개 애플2,513일봉(2016-11-07~2026-11-05)/현재가 및 양 지수 구성 조회. artifacts/research-foreign-live/foreign-public.json.
- 실제 마우스(격리DB): 기록 행 더블클릭→저장 차트, BUY 클릭→거래 행·당시 지지 상세 선택, 전략 근접 후보 ON/60.00, 해외 목록/상위N 숨김/USD 안내 확인.
- publish 및 루트Stock.exe smoke 모두 종료0/Success=true/NoBrokerCalls=true. publish/win-x64/Stock.exe, 루트Stock.exe, release/Stock.exe80,849,464 bytes / SHA256 `01D27CE34EC32CBD2BC9175CEE8F80F1275FBCAB18F3DFB5B1DD34A297F45A0A`. release 체크섬 갱신. 이전 EXE artifacts/before-research-expansion-20261106-234544에 보존.
- 로그 artifacts/research-expansion-{build-final,full-final,publish-final}.log, research-expansion-checks.log, research-related-ui.log, research-expansion-{smoke-final,root-smoke}/result.json.
- 첫 회귀/배포 검사는 제거된 옛 기록 버튼 및 이전 파라미터 개수 가정 때문에 실패했으며 실제 새 UI/61개 설정을 검증하도록 수정 후 전체/배포 검사를 통과했다. 검사를 생략하지 않았다.

## 다음 작업 시작 위치

현재 남은 작업 없음. 새 요구 전 동일 검사/게시를 반복하지 않는다. 기능/파일 지도·제한: docs/verification/ResearchExpansion-2026-11-06.md. 새 CLI `Stock.Tests.exe --research-expansion <output>`, `--foreign-public-checks <output>`, 격리 실제UI `--research-demo <output>`. 기존 검증은 docs/maintenance/Verification.md.

GitHub commit/push/tag/Release를 이번에 실행하지 않았다. 최신 정책은 사용자가 명시적으로 요청할 때만 릴리즈. 이전 자동 게시 승인은 철회됐다. 공개 v0.1.14/bb1d510은 이번 새 기능을 포함하지 않는다. 공개 EXE SHA77A647...와 현재 로컬 새 EXE 해시를 혼동하지 않는다.

## 유지할 DB 사실

실제DB C:/Users/wkdru/AppData/Local/StockPaperTrading/KiwoomQuant/workspace-v2.db. 최초09:18/09:20 SQLite11의 유발 사건은 미확정이다. 원본/실패 사본4개/최종DB 무결성·JSON은 정상. 기존 원본 백업 diagnostics-20261106-102905 보존. 이번 읽기 전용 확인:5백테스트/2스크리닝,5개 모두 차트 원본 존재. 정상DB 재구축/삭제/수리·실제키 변경·실계좌 주문 없음. 새 실패 없이 원인을 확정하지 않는다. docs/maintenance/Storage.md 및 기존 StorageAudit/FeatureAudit 참고.
