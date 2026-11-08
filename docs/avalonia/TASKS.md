# 진행 — 별도 Avalonia 전환·WPF 기준 디자인 반영

원본 STOCK은 유지한다. 원본 당일매매/전략D P0~P6 검증과 Stock.exe 반영을 먼저 마쳤다. 개인 DB/키/config/.git 복제 없음. 사용자 정책(2026-11-08 최신 답변): 사용량 조건을 무시하고 계속 진행, 이전 주간/월간40% 중단 기준 해제. 실제 주문 금지, GitHub 게시 명시 요청 때만.

- [x] A0 격리 경계·공식 의존성·Core80개 source manifest.
- [x] A1 .NET10/Avalonia12 solution·DI/MVVM·portable192.
- [x] A2 검색/공개·연결 시세/일주월·분시간/1초 pump/차트.
- [x] A3 기존61개·D 설정·JSON·별도 저장·정밀도/입력 편집.
- [x] A4 공통 백테스트/당시 fill·support/이전다음popup/표식병합·마우스·우클릭 전체/상세·기록1차 이식/검사.
- [x] A5 일반 스크리닝·진행 중 열람/funnel/조건 상세·실시간 서비스/가상 체크/긴급중지. 일봉 가상 자동 실행/복원은 E4에서 추가 검증. 해외 실시간 자동 주문은 지원으로 표시하지 않음.
- [x] A6 관심그룹·시장 지수·정기매수·통합 기록.
- [x] A7 계좌 인증·조회·OS 보안 저장/기본 연결·메모리 재사용/초기화·실제 수동 주문/취소·확인 체결·관심그룹 공통 실행 UI 및 기존 실전 adapter 연결. 지원/검증 범위는 E1~E4; 실제 Mac/실제 증권사 검증은 [!] 별도.
- [~] A8 Release0/0·Core192·AXAML 기능199/디자인18·최신 Windows 일반 EXE 실제 입력/재실행4회 및 백테스트/드래그/상세 선택/달력/전체시장 애플 검색·Mac arm64/x64 cross-publish 완료. 실제 Mac 실행은 미검증.

## 후속 실행 가능한 단위

- [x] D3a 익절 빨강/손절 파랑 통일·최종 검수·Stock.exe 후보 생성. 실제 손익 우선/병합 순손익/미확인0원 방지. build0/0·Core192/UI207/디자인18/실제공개28. 두 일반 앱7탭/버튼 호버99·콤보14, 후보 실제 검색·재실행4회/백테스트/팬·클릭·행 선택/달력/익절 빨강322픽셀·손절 파랑333픽셀 성공. 루트 Avalonia와 후보258F8CB6...C095E/107330359bytes. docs/RELEASE_READINESS_20261108.md.
- [~] E0 사용자 답변 수신: "미완료 기능을 먼저 완성". 아래 단위부터 완료하고 기능 검증 후에만 기존 EXE 교체/조건부 GitHub Release. 실제 주문 테스트는 하지 않는다. 기존 WPF E433...1934 및 공개v0.1.14/main fbeed251은 유지한다.
- [x] E1 키/기본 계좌 영구 저장: 기존 DPAPI 형식/원본 wrapper 재사용, 별도 Windows/Mac Keychain adapter, 인증+계좌 확인 후 저장·기본 연결·초기화 확인·입력 취소/늦은 응답 방지. 더미 키13검사 성공. 실제 Mac Keychain 권한/잠금은 [!] 미검증.
- [x] E2 실계좌 수동 주문/취소·체결: RemoteTradingSession/OrderGuard 재사용, 명시 활성화+건별 확인/한도/최종 중지·계좌 식별자/WAL·취소 대사/확인 체결·현재 계좌 차트. 가짜 계좌14 및 최종 전송7검사 성공. 실제 주문0.
- [x] E3 실계좌 전략 adapter: 기존 LiveStrategyBatch·Kiwoom 피드·동일 전략/원장·종목별 상태·중복/긴급중지·팝업수명 독립. 가짜 피드7검사 성공. 키움 국내만, 현재 종목/위험·시장 세션·실제 거래대금 입력 필요; 모의 가정/추정 거래대금 차단. 전략 D 실전 연결을 새로 활성화하지 않음.
- [x] E4 일봉 가상 실행/복원: N종가→다음 거래일 새 관측 이후 주문/후속 관측 보수적 체결, 장외/주말 대기, checkpoint v2·현금/수량 대사·원장/표식 원자 저장·paused 복원·보호 재개. 합성16+앱 호스트5 성공. 원본 틱/관측 캐시 저장 없음. 신규/추가 매수는 복원 시 OFF, 이전 신호 소급 주문 안 함.
- [~] E5 통합 검수/배포 판단: 공통85 소스(로컬 바이트 동일·Git용 UTF8/LF 정규화 SHA256) 동기화. build0/0·Core192·UI273·디자인18, 원본WPF1652, 실제공개28. 최종 v0.1.15 C162D6C5...D8EC23 후보 시작/종료·일반 검색/재실행4회 성공, Mac 양RID 교차빌드 성공. 원본 불변을 확인하는 표 렌더 검사에서 기본 양방향 가격열이 원본0/null을 쓰는 결함을 재현·모든 표시 열 OneWay로 수정했다. 최종 일반 백테스트/색/드래그 검수와 GitHub CI/다운로드 검증 남음. 실제 Mac/API·주문·서명/공증 미검증. 원본 EXE 보존 중.

- [x] D2 차트/백테스트/달력/온라인 지정봉 UI 및 기본 시장 전체: backing history 유지·실시간 새 봉 추가 시 팬 유지, 한글 배지·실측 충돌 배치/병합·단일 클릭·거래 행 선택,202×201 달력/날짜 잘림·인증 취소 모드 표시·지원 지정봉 버튼 경로 수정. 최종Core192/UI199/디자인18/빌드0·0, 루트EXE 검색/재실행4회·백테스트/팬/클릭/달력·기본 전체에서 실제 애플790ms 조회 확인. 최종78CB5E2E...FBA02D/107330359bytes, Mac 양RID 교차 빌드. 실제 증권사 인증·Mac 실기는 아래 [!] 범위. docs/D2_NATIVE_CHART_CALENDAR_20261108.md. 사용량40% 중단 조건은 최신 사용자 답변으로 해제.

- [x] D1 차트 상호작용 동등성 후속: 안내선 누락/ScottPlot 기본 XY 줌을 제거하고 Sequential 봉/포인터15봉 줌/자동Y·동기 거래량/4px 드래그·복귀·경계/툴팁·우클릭 전체↔상세/매매 버킷 매핑을 원본과 대조·수정. 실제 두 일반 앱 일봉 마우스 비교·십자선 화면 픽셀 가로274/세로176 확인. 오프라인에서 숨긴 다른 봉은 강제 활성화하지 않고 격리 검사로 구분. 빌드0/0·Core192/UI172/디자인16·배포EXE 입력/재실행4회 성공. 최종1D7783...7CEB0D,107318071bytes. Mac 양RID cross-publish만 완료. docs/CHART_INTERACTION_PARITY_20261108.md. 원본/Core 보존, 실제 주문/GitHub 없음, 주간잔여45%.

- [x] D0 원본 WPF·Avalonia 일반 실행 앱의 7탭/버튼 호버108/콤보14/펼침144 상태/공개20종목 표3상태/마스킹 계좌 창·전략D 설정을 실제 마우스와 화면 픽셀로 비교·수정했다. ControlStates로 Fluent 내부 회색 상태를 분리하고 화살표 hit·선택색·정렬·스크롤·분야 UI·편집 콤보를 수정. 최종 EXE C475123C...69BF7F, 빌드0/0·Core192/기능142/디자인16·루트EXE 검색/재실행4회 성공. Mac 양 RID cross-publish, 실기 미검증. 상세 docs/INTERACTIVE_UI_PARITY_20261108.md. 일반 명령 일괄 누름은 승인 검토 거절로 실행하지 않았고 안전한 UI만 실제 클릭. 원본/Core80개 유지, GitHub 게시 없음. 주간잔여49%.

- [x] C0 재차 직접 실행 요구 후 Windows 일반 EXE의 실제 키보드·마우스 검색으로 **내부 조회 완료/실제 로딩 프레임 잔류**를 재현했다. 별도 RenderTargetBitmap 검사가 놓친 표시 문제였다. Win32 RedirectionSurface+ShouldRenderOnUIThread로 수정하고, 수정 전 실제 화면 검사 실패/수정 후 루트EXE4회 성공을 확인했다. 상세 docs/NATIVE_SEARCH_FIX_20261108.md. Mac 실기는 여전히 미검증.
- [x] C1 오프라인/온라인 지원 기능 점검·시간 초과 오인/종목별 취소 전파/활성 감시 종료 교착/무응답 조회 수정. Core192/기능UI116/디자인16/실제공개28/동일코어 원본계약965 성공. 루트EXE actual-public·synthetic 각각Exit0. 상세 docs/MODE_AUDIT_20261108.md. 실제Mac/실제증권사 인증은 미검증, 실제 주문 없음.
- [x] B0 원본 WPF XAML·스타일·격리 비교 PNG 분석, 메인7탭·공통 컨트롤·우측2×2도구·설정61개·계좌 타일·일목/MA 준비자료 이식. UI74+추가16/빌드0·0/Windows 새EXE 확인. 상세 docs/UI_PARITY.md.
- [!] B1 실제 Mac의 폰트·DPI·트랙패드·창 장식과 픽셀 비교는 호스트 미제공으로 미검증. 원본 custom chart/ScottPlot 눈금·자동 범위 차이는 남음. 픽셀 완전 동일로 보고하지 않는다.
- [x] 일봉 종가 신호→다음 거래일 관측 가상 실행·미체결/보유 복원은 E4 구현·검사 완료. 아래 옛 기록의 미지원 표시는 과거 상태다.
- [!] 실제 Mac 호스트가 없어 Mac native 실행·파일 선택·폰트·트랙패드·SQLite·Keychain 권한/잠금은 확인하지 못함. Keychain adapter 구현/교차 빌드와 실제 실행 검증을 구분한다.
- [!] 실제 증권사 read-only API 검사는 진짜 키로 수행하지 않음. 실계좌 주문은 명시 활성화 전 실행하지 않는다. 서명/공증도 미수행.

## 작업 상태 기록 — 2026-11-08

최종 새 소스는 빌드0경고0오류, portable192/Headless74 성공. Windows self-contained EXE2회 각각 Exit0/93봉/2회quote/NoBrokerCalls. Mac osx-arm64/x64 apphost와 필수native 의존성 생성 확인; 실제Mac실행 아님. `artifacts/avalonia-final-build.log`, `avalonia-core-checks.log`, `avalonia-ui-checks.log`, `native-smoke-summary.json`, `macos-audit.json`.

루트 `Stock.Avalonia.exe`107,158,327bytes SHA256EFCAFD0B9DCF7B0853664A64136B69BB3B5447B53EE2DC7F0B3453687F719C3B. Mac bundle은 artifacts/macos/<RID>/Stock.Avalonia.app. 원본WPFexe E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934 보존. Core80개 source 원본/manifest 차이0. GitHub 게시 없음. 최근 주간잔여70%; 재개 때 새로 조회하고40% 이하면 중지.

수정 파일 지도 docs/SOURCE_MAP.md, 단위별 구현·제한 docs/IMPLEMENTATION.md, 검사 docs/VERIFICATION.md. 다음 작업을 시작하기 전에 이 미완료 범위가 사용자의 우선순위와 맞는지 최신 요청을 우선한다. 원본 DB 손상 이력은 현재 blocker로 재사용하지 않으며 이번 범위에서 DB 교체/삭제하지 않았다.

추가 이식까지 마감: 관심그룹 공통 체크/종목별 등록 전략·예산/실행중 편집잠금·팝업수명·중복실행 방지, 기존 저널의 당시 지지 snapshot,10K 전체→우클릭1,200봉 상세/휠축소한도/전체복귀 검증. 최종74개. 큰 저널을 새 관측 없는250ms heartbeat마다 쓰지 않도록 조정. 성능 fixture는 artifacts/avalonia-ui/candle-navigation-performance.json(Windows 합성 자료, Mac 측정 아님).

### 후속 디자인 작업 마감 — 2026-11-08

사용자 최신 요청은 stock_avalonia를 원본 STOCK 디자인과 같게 맞추는 것. 메인7탭/팝업/스타일/차트 표현을 조정했고 소수·API키 파생 TextBox 테마 누락, 패널 겹침, 달력 아이콘을 실제 렌더 검사로 수정했다. 원본/Core 변경 없음. 자세한 수정 파일·한계는 docs/UI_PARITY.md.

최신 우선 산출물: 루트 Stock.Avalonia.exe107,264,823bytes SHA256EF972F6F7D4EF48E928607554CF2DAA78E771B7587853E9E82DFCF6F9FC6B897. 앞의 EFCAF 해시는 이전 전환판 기록이다. Release0경고0오류/코어192/UI74/디자인16/루트Windows native Exit0·93봉·quote2 성공. Mac 양 아키텍처 cross-publish 갱신; Mac 실기 미검증. GitHub 작업 없음. 주간잔여 마지막54%,40% 하한 유지; 재개 시 다시 조회.

### 기능 전체 점검·오류 수정 — 2026-11-08

최신 우선 산출물은 Stock.Avalonia.exe107,277,111bytes SHA25640290DEBD1FD75A46A403058EEF4821A2AC9424540A6C0ECBD29433C599B04C6. 위 EF972/EFCAF는 이전 이력. 원본WPFexe E433834...1934/Core80개 차이0. 원본 DB·키 변경/삭제/가져오기 없음.

검사: solution0경고0오류, Core192·Avalonia112·디자인16·실제공개28 성공. shared Core의 기존 계약/위험/파싱/모의965도 fixture만으로 재검사했고 WPF UI는 호출하지 않음. 실제 공개 입력/클릭/Enter/국내·애플·기간/백테스트/정기매수/스크리닝/지수/해외목록 성공. 최종 루트 EXE 실제공개61/61/65봉이 실제 RenderedCandles와 일치, 합성 시작도Exit0. 네트워크 실패와 실제 사용자 취소를 구분한다. 실시간 일부 종목 시간 초과는 나머지 감시를 중단하지 않으며, 활성 감시 Dispose 시 UI 컨텍스트 교착을 수정했다.

로그/파일/지원별 한계/재현 명령: docs/MODE_AUDIT_20261108.md. --verify-public <폴더>는 개인 DB/계좌 대신 격리 공개조회 검사다. 새 StartupProbe가 정상 기본 저장경로를 바꾸지 않는다. 아직 사용자보고 자체는 미재현이다. 비동기 질문의 환경·오류 답변이 필요하다. 실제Mac/실제키원격API/실제돈 주문은 검사하지 않았다. GitHub 게시 없음. 주간최근54%, 재개 전 갱신·40% 이하 중지.

### 무한 로딩 증상 후속 — 2026-11-08

사용자는 오류 문구가 아니라 로딩 중 멈춤처럼 보였다고 설명했다. 완료·취소에 반응하지 않는 제공처에서는 기존 메인 조회가 검색 명령을 계속 점유할 수 있어 AppServices.QueryTimeout(기본45초)과 MarketWorkspace.Run의 WaitAsync 제한/취소를 추가했다. 지연 응답은 기존 세대/토큰 검사로 무효화한다. 실제 화면의 작업 취소 버튼을 눌렀을 때 무응답 Task 종료를 기다리지 않고 로딩·검색 명령이 해제되고, 제한 후 재검색이 성공함을 검사했다. 계좌 잔고 시간 초과도 이전 값 유지·오류 안내·주문0을 추가 확인했다.

최종 빌드0경고0오류, Core192/UI116/디자인16/실제공개28. 루트EXE C302B966E94CE1D00426A626633EC24A7994C7B8047C75D19E1A7C0CF52C65F5(107,277,111bytes), 실제 공개 삼성전자61/SK61/애플65봉이 RenderedCandles와 일치/Exit0. 합성 시작·종료도Exit0. 이전40290판은 artifacts/before-unbounded-query-fix-20261108에 보존. Mac 양아키텍처 cross-publish 갱신, 실제Mac 미검증. 원본WPFexe/Core80개 hash변경0. 최신 로그 loading-final-verification.log, live-public-loading-final.log, loading-native-public/result.json. 사용자 환경·종목은 아직 모름; 그 환경의 정확한 원인 확정 아님. GitHub 게시/실제계좌 인증·주문 없음. 주간잔여54% 유지.

### 일반 창 직접 입력에서 화면 멈춤 재현·수정 — 2026-11-08

사용자 요청대로 일반 루트 EXE를 진단 옵션 없이 열고 실제 입력칸/버튼을 조작했다. C302판은 내부 UIAutomation 상태가 조회 완료여도 실제 화면이 로딩 프레임에 남았고, 화면 봉 표본 빨강0/파랑15로 검사 실패했다. 앞선 코드 직접 호출/RenderTargetBitmap 결과만으로 표시 정상이라고 판단한 검증 공백을 확인했다. Program.cs의 Win32PlatformOptions에 RedirectionSurface와 ShouldRenderOnUIThread=true를 함께 지정해 표시 경로를 수정했다. GPU 기본 선택·Mac 옵션·Core·데이터/전략 로직은 유지한다.

새 scripts/verify-native-search.ps1는 인수 없는 일반 EXE, 실제 키보드/마우스/Enter, 화면 캡처 봉 픽셀을 확인한다. 수정 전 실패 근거 native-search-before, 수정 빌드4회 및 새 루트EXE4회 모두 성공(native-search-final). 삼성전자→SK(Enter)→삼성전자 반복→정상 종료/재실행 후 삼성전자. 검색·캡처 포함 약1.3~2.0초. UIAutomation 값만으로 성공 처리하지 않는다. 직접 만든 검사 프로세스만 닫았고 실계좌/키/주문은 조작하지 않았다.

최신 루트EXE F86BCC3FF384669B3C85A8A5B698DEAC9C9BA03303B5309A8D463DF9FDD6F09A,107,277,111bytes. 이전C302는 artifacts/before-native-presentation-fix-20261108에 보존. 전체 빌드0경고0오류/Core192/UI116/디자인16 성공(native-search-final-regression.log), 배포EXE 시작smoke Exit0. Mac 두 아키텍처는 같은 소스로 cross-publish하며 실제Mac검증 아님. 원본WPF/코어는 수정하지 않음. 문서 NATIVE_SEARCH_FIX_20261108.md가 이전 미재현 기록보다 우선. GitHub 게시 없음, 주간잔여54% 확인.

사용자 확인용으로 최종 루트 EXE를 한 번 더 일반 실행하고 실제 키보드/마우스로 삼성전자를 검색했다. 화면의 로딩 해제와 실제 봉을 직접 캡처해 확인했고 앱은 그 상태로 열어 두었다(이 실행 PID19664, 이후 종료 여부는 다시 확인). 화면 증거 C:/Users/wkdru/Documents/Codex/stock-actual-fixed-live-window.png. 최종 원본Core80개와 원본WPFexe E433...1934 해시도 보존 확인.
### 실제 상호작용 비교 수정 마감 — 2026-11-08

최신 산출물 C475123C8BF6575AE0212F4473F2AA83B1B3BB02A6770732FB29A27F8D69BF7F /107,305,783bytes. 이전F86판 보존. 일반 두 앱의 버튼·콤보·펼침·채운 표·마스킹 계좌 창을 실제 입력과 화면 픽셀로 비교했다. Fluent 내부 상태/화살표 빈 hit/콤보 이중 focus/표 선택·정렬·스크롤/누락 분야 UI를 수정했다. 파일 목록과 전체 검증은 docs/INTERACTIVE_UI_PARITY_20261108.md, HANDOFF 및 artifacts/interactive-* 참고.

최종0경고0오류, Core192/기능142/디자인16, 새 루트EXE 실제 키보드·버튼·Enter·재실행4회 성공. 양 Mac RID cross-publish 성공(실제 Mac 아님). 원본WPFexe E433...1934/Core80개 변경0. 전체 활성 버튼 일괄 누름은 키 초기화/주문 위험으로 자동 승인 검토 거절. 우회하지 않고 호버 및 허용 UI 클릭/격리 pressed 검사를 사용했다. 모든 픽셀 동일·모든 실제 명령 실행으로 보고하지 않는다. Keychain/실계좌·실기 Mac 잔여 범위는 위 [!] 항목과 동일. 현재 수정 중 파일 없음. 주간잔여49%, GitHub 게시 없음.
### 차트 상호작용 검증 공백 수정 마감 — 2026-11-08

사용자가 지적한 십자선 누락과 원본 대비 휠 동작 차이를 실제 일반 앱에서 재현했다. 원본 CandleChart와 다른 날짜 X간격/비율 XY 줌을 Sequential 봉 좌표·15봉 포인터 줌·자동 가격축/동기 거래량·원본 드래그/한도·스크리닝 quarter/500봉 상세 규칙으로 수정했다. Canvas 십자선/툴팁/커서, 단일 거래 봉 매핑, 일·주/분·시간 버킷, 표시 기간 첫날과 날짜 눈금도 수정. 파일 지도 docs/CHART_INTERACTION_PARITY_20261108.md.

최종172 UI검사(차트30)/Core192/디자인16/빌드0경고0오류. 일반 원본·이식 앱의 일봉 좌/중/우 휠 in/out·드래그·거래량·이탈 캡처, 실제 십자선 픽셀274/176, 루트EXE 실제 키보드/검색·Enter·반복·재실행4회 성공. 현재 오프라인에서 주·월·지정봉은 노출되지 않아 원격 계좌 검증으로 보고하지 않았다. Mac은양RID 교차 빌드만 수행.

새 EXE107318071bytes SHA2561D7783EAF7183AA750145524A827298A2BC30EF6C7B74500D3ABC93C137CEB0D. 이전C475판 백업. 원본WPF E433...1934 및Core80개 보존. artifacts/chart-final-regression.log, chart-native-final/, chart-exe-native-search/, chart-publish-* 참고. 중간 네이티브 검사 래퍼의 stale LASTEXITCODE 오판은 성공JSON과 실제 예외로 구분하도록 수정했으며 앱 결함으로 보고하지 않는다. 현재 수정 중 파일 없음. 실제 주문/GitHub 없음. 주간잔여45%,40% 이하 중지 정책 유지.

최종 확인: 새 루트 EXE를 실제로 다시 열어 삼성전자 차트·휠 확대·십자선을 캡처하고 사용자 검토용으로 열어 두었다(PID30024). 화면 C:/Users/wkdru/Documents/Codex/stock-avalonia-chart-crosshair-fixed.png. 마감 주간잔여44%를 확인했으며40% 하한을 유지했다.
### D2 중단 인수인계 — 주간잔여40% 도달, 2026-11-08

사용자 최신 요청: 두 프로그램을 켜서 실제 비교하고 (1) 드래그 불가 (2) 백테스트 표식 깨짐 (3) 날짜/기간 잘림·하얀 조잡한 달력 (4) 온라인 지정봉 비활성까지 수정/검증. 원본WPF32168(기본 계좌 자동연결, 읽기만)/사용자Avalonia33404(오프라인)를 실제로 함께 열었다. 키 읽기·복제·실제 주문 없음.

완료된 원인/소스 수정:
- ChartBars가 선택 기간으로 잘려 초기 화면 전체 표시 때 과거로 pan할 이력이 없었다. 전체 backing history + ChartDisplayCount로 표시 수를 분리했다. 실제 일반 앱에서 백테스트 후 같은 위치 날짜가2026.10.21→2026.10.06으로 이동함을 확인(artifacts/d2-native/pan.json,Moved:true).
- ScottPlot 한글 글꼴 지정만으로는 네모 문자가 해결되지 않았다. TradeLayer Canvas의 Avalonia TextBlock/Border 배지로 교체했다. 한글/거래번호/손익 정상 실제 화면 확인(final-trades.png); 측정한 bounds 내 배치/배지 클릭/겹침 위치 조정·빈 차트 정리를 추가했다. ChartMarkerLayout는 합친 번호 범위를 보존.
- 온라인 선택 시 인증 전/취소 후에도 라디오가 온라인처럼 남을 수 있어 실제 Online 상태를 다시 통지한다. 지정봉 부모 전체 비활성 대신 숫자·단위만 비활성화하고 버튼은 기존 오류 팝업을 통해 지원 사유를 알린다. minute-capable fake broker로 실제 버튼 클릭→조회 검사는 통과. 실제 계좌를 사용한 Avalonia 온라인 검사는 아직 안 함(현재 앱에 인증 세션 없음, 원본 키 복제 금지).
- CalendarDatePicker 배경/최소폭/패딩/36px 높이, CompactCalendarDay, Calendar/CalendarItem 크기를 수정 중. 처음 가정한 PART_Root/MonthView는 실제와 달라 팝업이 잘렸고 native screenshot에서 확인했다. 실제 DLL의 이름 PART_ElementRoot/ElementMonthView/ElementHeaderButton/ElementPreviousButton/ElementNextButton으로 후속 스타일을 넣었으나 최종 일반 앱 캡처 calendar-correct-parts.png에는 달력이 열리지 않아 최종 표시 성공을 확인하지 못했다. 이 파일을 성공 증거로 쓰지 말 것.

수정 파일: Stock.Avalonia/MainWindow.axaml; ViewModels/MarketWorkspace.cs,PresentationWorkspace.cs; Views/CandleViewport.cs,StockChart.axaml,StockChart.axaml.cs,ChartMarkerLayout.cs; Styles/ControlStates.axaml,StockTheme.axaml; Stock.Desktop.Tests/Program.cs,ChartInteractionChecks.cs,OnlineDesktopChecks.cs; scripts/NativeUiAuditHelpers.ps1(시작 직후 foreground 획득 4회 재시도). Core 수정 없음.

검사: 최신 소스 build0경고0오류/Core192/UI175/디자인16 성공(artifacts/d2-regression.log). 실제 native 캘린더는 별도 실패/미확인 범위다. 실제 source backtest 캡처 d2-native/final-trades.png, pan before/after 날짜·영상 캡처. 기존 artifacts/backtest-native-before에 두 앱 초기/달력/깨진 표식 근거.

다음 순서: (1) 최신 CalendarItem 스타일의 월/연도·연도 선택·날짜 클릭을 headless와 일반 창에서 다시 확인. 틀린 PART_Root/MonthView 스타일 정리하고 실제 template part 구조에 맞추기. CalendarDatePicker 버튼 PART_Button과 래퍼 버튼을 구분. 작은 popup에 큰 내부 template를 단순히 잘라 넣지 말 것. (2)175 검사 외에 좁은1160/큰1540·DPI/날짜 입력 text bounds/양쪽 실제 팝업을 비교. (3) 실제 백테스트→배지/봉 클릭→행/상세·줌/pan 동작을 native로 재검사하고 배지 밀집 겹침 최종 확인. (4) 온라인 minute-capable broker 정상/미지원/미인증·취소 UI를 구분하여 실제 사용자 키는 채팅으로 받거나 복제하지 않음. (5) 필요 변경 후 빌드/회귀·publish·새 rootEXE native 검색/팬/표식·달력 확인 후에만 로컬 루트 교체. 새 소스 Mac 양RID publish는 아직 하지 않음.

현재 루트 Stock.Avalonia.exe는 이전 검증판1D7783EAF7183AA750145524A827298A2BC30EF6C7B74500D3ABC93C137CEB0D 그대로다. D2 코드는 bin/Release까지만 빌드했으며 publish/루트 교체/깃허브 게시하지 않았다. 사용자 루트 앱33404를 임의 종료하지 않음. 비교용 새 소스14192/원본32168는 아래 정리 시 정상 닫기만 시도. 실제 키·주문·데이터 삭제 없음.

이전 사용자 요청과 AGENTS의 주간잔여40% 하한에 도달해 작업 중지. D2 미완료를 완료로 표시하지 않는다. 재개 전 get_usage_limits 확인,40% 이하면 추가 개발하지 않는다.

### D2 최종 마감 및 시작 시장 전체 — 2026-11-08

최신 사용자 지시로 사용량40% 중단 조건을 해제했다. D2의 팬/표식/달력/온라인 상태와 추가 요청인 기본 시장 전체를 반영하고 새 루트 EXE까지 확인했다. 최종78CB5E2E35024702F55F32311241B000F3C8459836E29044406B9DC16AFBA02D/107330359bytes. 이전1D/659/E85판은 artifacts의 각 백업에 보존했다.

최종 build0/0·Core192/UI199/디자인18. 새 EXE 실제 키보드/마우스/Enter·반복·재실행4회 성공. 실제 삼성전자 백테스트4거래/손익+66,000, 팬09/23→09/04와 복귀, 표식1회 클릭→상세, 상세 차트→같은 거래 행/선택색·텍스트, 달력202×201 확인. 새 봉 추가 시 과거 팬이 초기화되는 경우도 회귀에서 재현해 명시 Revision 때만 초기화하도록 후속 수정했다. 기본 전체를 실제 UI에서 확인하고 전체 상태로 애플 검색→AAPL 차트790ms를 확인했다. 실제 공개 백테스트 기록을 생성했으며 삭제하지 않았다.

문서/파일/근거는 docs/D2_NATIVE_CHART_CALENDAR_20261108.md와 간결화한 HANDOFF를 따른다. 기존 HANDOFF 전체는 docs/history/HANDOFF-before-D2-final-20261108.md에 보존했다. 실제 인증 전 모드 오표시는 직접 취소로 확인했고, 실제 증권사 분봉은 더미 계좌만 검증했다. 원본 WPF EXE/Core80개 보존; 원본 키 복제·실제 주문·GitHub 게시 없음. Mac 양RID는 동일 최종 소스 교차 빌드만 성공, 실제 Mac 미검증.

현재 수정 중 파일 없음. A7의 영속 키·실계좌 및 실제 Mac 지원은 별도 미완료로 유지한다. 다음에는 최신 사용자 요청을 우선하고 완료된 D2/옛40% 중단 지점을 다시 시작하지 않는다.

### D3 손익 색상·최종 검수/조건부 릴리즈 — 2026-11-08

익절 빨강/손절 파랑을 공유 chart의 텍스트/배지/가격 marker에 적용하고, 실제 순손익 우선·미확인0원 방지를 추가했다. build0/0·Core192/UI207/디자인18/실제공개28, 두 일반 앱7탭·호버99·콤보14 성공. Stock.exe 이름의 후보도 startup 격리601봉·2관측/주문0, 실제 검색/재실행4회·실제 backtest/팬/클릭·상세 선택/달력과 색 픽셀(익절red322/lossblue333) 검증했다. 루트 Avalonia와 후보 SHA258F8CB67F7E67C90FAE57D5923DC70CCD983D25F9D69E2B120318AF267C095E/107330359bytes. Mac 양RID 교차 빌드만 성공. docs/RELEASE_READINESS_20261108.md가 정본.

[!] 기존 기능 동등성 조건 미충족: 실계좌 주문/취소/체결표시, 키·기본계좌 영구 저장, 일봉 다음날 자동 가상주문/보유 복원이 미완료. 실제 증권사/Mac도 미검증. 사용자에게 미완료 우선/테스트용 사전 공개/누락 수용 후 교체 중 범위를 질문했고 답변 대기. 원본 WPF E433...1934와 GitHub v0.1.14/main fbeed251은 유지. commit/push/tag/Release 없음. 자동 승인 검토가 막은 것이 아니라 사용자가 제시한 기능 검증 조건을 충족하지 못한 상태다.

수정 파일: Views/ChartMarkerLayout.cs,StockChart.axaml.cs; Stock.Desktop.Tests/Program.cs,TradeOutcomeColorChecks.cs; scripts/verify-native-backtest.ps1; AGENTS/TASKS/HANDOFF 및 검수 보고. 원본 코어80개 변경0. 현재 수정 중 코드 없음. 사용량 하한 해제는 계속 유효하나, 미답변을 교체 승인으로 해석하지 않는다. 다음 실행은 사용자 범위를 먼저 확인한다.
