# 최신 상태 — 2026-10-31 GitHub v0.1.4 Release 게시 완료

`v0.1.4` 태그와 Release를 GitHub에 게시했다. Actions run `41174068757`은 Build, 전체 오프라인 회귀 1,077개, self-contained 단일 EXE 게시, 게시 EXE smoke, SHA256 작성, Release 업로드를 모두 통과했다. Release URL은 `https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.4`다.

Release 자산은 `Stock.exe` 80,792,975 bytes와 `Stock.exe.sha256` 77 bytes다. 공개 Release에서 두 파일을 다시 내려받아 SHA256을 독립 계산했으며 `3E8C82F178AD3537CFEB069F4E9697DEB1BCBC8D3A1E2B912B1766C9CF1C303D`로 일치했다. API 키·계좌 DB·로컬 EXE는 Git 추적/Release에 포함하지 않았고 실제 브로커 네트워크 호출·계좌 거래도 수행하지 않았다.

v0.1.1~v0.1.2의 실패는 GitHub 가상 화면에서 버튼 템플릿 좌표가 3px 달라지는 테스트 문제, v0.1.3의 실패는 760px CI 화면에서 1040px 창도 같은 높이로 잘리는 물리 해상도 의존 테스트 문제였다. 최종 테스트는 스크리닝 소스 행/스크롤 영역의 44px 고정 높이와 차트의 `*` 가변 행·Stretch 배치를 구조적으로 검사해 화면 해상도와 무관하게 실제 의도를 검증한다.

# 이전 상태 — 2026-10-31 GitHub Actions Release WPF 레이아웃 회귀 수정

첫 `v0.1.0` Release Actions에서 GitHub Windows runner의 가로 스크롤바가 관심종목 소스 행을 3px 키워 `watchlist groups never push screening controls downward (361.1 → 364.1)` 검사가 실패했다. 테스트 허용치를 완화하지 않고 `MainWindow.xaml`의 스크리닝 소스 선택 행과 관심그룹 ScrollViewer에 스크롤바 공간을 포함한 44px 고정 높이를 예약했다. 로컬 검사는 `362.1 → 362.1`로 유지된다.

Release 빌드 경고0/오류0, UI smoke 401개, 전체1077개를 통과했다. 새 게시본 `release/Stock.exe`는 80,759,921 bytes, SHA256 `DA990294DE55DD9C51639DD1B8E8D64AF616CA11E42252E3C0289CC02C693D1D`이며 smoke는 Success/NoBrokerCalls=true다. 현재 `v0.1.0`은 실패한 이전 커밋을 가리키므로 이 수정은 커밋한 뒤 새 `v0.1.1` 태그로 push해야 한다. 실제 브로커 네트워크 호출·계좌 거래는 수행하지 않았다.

루트 `Stock.exe`도 같은 최신 게시본으로 교체했다. 이전 2026-10-24 파일은 `artifacts/Stock-before-actions-layout-B29DA7CD.exe`에 보관했고, 새 루트 EXE는 80,759,921 bytes / SHA256 `DA990294DE55DD9C51639DD1B8E8D64AF616CA11E42252E3C0289CC02C693D1D`이며 단독 smoke가 ExitCode=0, Success/NoBrokerCalls=true로 통과했다.

# 최신 상태 — 2026-10-24 전체 지정봉 분·시간 선택과 범위 통일

스크리닝과 종목/차트 전략 백테스트의 `지정 봉` 입력을 공통 `분봉/시간봉` 단위 콤보와 숫자 콤보로 통일했다. 분봉은 1~59, 시간봉은 1~12만 고를 수 있다. 단위를 바꾸면 기존 값은 새 범위 안으로 보정하고, 예전 저장값의 틱/알 수 없는 단위는 분봉으로 마이그레이션한다. 새 화면에서는 틱 지정봉을 선택하지 않지만 과거 스크리닝 snapshot과 원본 틱 차트는 읽기 호환을 유지한다.

`TimeBarSelection`이 UI 목록, 저장값 정규화, `BarSpecification` 생성을 한 곳에서 담당한다. 백테스트는 분·시간 완성봉 신호를 다음 완성봉 시가에 체결하고, 실시간 다중전략은 KRX 09:00 기준으로 N분/N시간봉을 닫은 뒤 다음 실제 체결에서만 신호를 내보낸다. 실시간 분봉과 시간봉 경계는 각각 독립 fixture와 다중전략 가짜 피드로 확인했다.

Release 빌드 경고0/오류0, 확장136개, WPF UI401개, 전체1077개를 통과했다. 1분봉 20거래일 7,800개 부하 검사는 1,639ms, 총 할당338.1MB, 결과 보유 증가22.6MB였다. 게시본 `artifacts/timebar-final-20261024/Stock.exe`, 루트 `Stock.exe`, EXE만 둔 `artifacts/timebar-clean-exe-20261024-b29da7cd/Stock.exe`는 80,759,684 bytes, SHA256 `B29DA7CDD67E132182A8904268E5FE022058D0ADEFF35363791ABCA1E1B8EA81`로 일치한다. 루트와 단독 EXE smoke는 Success/NoBrokerCalls/RawNativeRendered=true다. 이전 루트는 `artifacts/Stock-before-timebar-52A330B3.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-24 전략 백테스트 지정 분봉·부하 검증

종목/차트의 전략 백테스트에 `일봉`과 `지정 분봉`을 선택하는 UI를 추가했다. 지정 분봉은 온라인 국내 증권사 연결과 공식 분봉 범위 조회를 요구하며 1~390분을 지원한다. 일봉 경로는 그대로 유지된다. 분봉 백테스트 결과에는 정확한 `BarSpecification`이 저장되어 결과·기록·차트 복원 시 `5분`처럼 동일한 주기를 사용한다.

분봉 전략은 완성된 Day N 분봉 종가까지의 정보만 판단에 사용하고 다음 가용 분봉 시가에 체결한다. 기존 낙주 전략의 지표, 급락 탐지, Support Provider/Cluster, 분할매수, 손절·익절·성과 계산을 재사용한다. 기간 계산은 봉 수이며 최대 보유·재진입은 KRX 거래일 수다. 실제 분봉 OHLC 내 High/Low 순서를 알 수 없으므로 같은 봉에서 손절과 익절이 모두 가능한 경우 기존 보수 정책대로 손절을 우선한다. 키움 분봉에는 공식 거래대금 필드가 없어 가격×거래량 추정값을 사용한다.

Release 빌드 경고0/오류0, 낙주75개, WPF UI396개, 전체1067개를 통과했다. 1분봉 20거래일 7,800개를 기본 장기 지표와 Support Provider로 실행한 부하 검사는 1,646ms, 총 할당339.8MB, 결과 보유 증가24.4MB였고 UI는 `Task.Run`과 취소 토큰을 사용한다. 실제 브로커 네트워크를 호출하지 않은 가짜 연결 UI 회귀에서 5분봉 78개, 다음 5분봉 시가 체결, 기간·표식·결과 보존을 확인했다.

게시본 `artifacts/minute-backtest-final-20261024/Stock.exe`, 루트 `Stock.exe`, EXE만 둔 `artifacts/minute-backtest-clean-exe-20261024/Stock.exe`는 모두 80,758,816 bytes, SHA256 `52A330B3EB0E8CD5565097D8CDE4FA679A8D0B14525927681EC4212872789FDF`로 일치한다. 루트와 EXE 단독 smoke는 Success/NoBrokerCalls=true다. 이전 루트는 `artifacts/Stock-before-minute-backtest-82D8B600.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-23 스크리닝 기간 경계·국내 시장 선택·1시간봉 조회 최적화

스크리닝 상세 차트의 봉과 거래 표식은 실행 snapshot의 시작일 00:00 이상, 종료일 다음날 00:00 미만으로 함께 제한된다. 첫 화면은 그 기간 전체이며 이전/이후 체결이 가장자리 봉에 잘못 붙지 않는다.

스크리닝 시장 선택은 `코스피`, `코스닥`, `코스피+코스닥`만 제공한다. 단일 시장은 선택한 시장에서 상위 N개만 받고, 통합은 KOSPI와 KOSDAQ에서 각각 상위 N개를 받는다. 종목 검색의 국내/해외 선택은 별도 기능이라 유지했다.

키움 1시간봉은 `ka10080`의 공식 `tic_scope=60`을 직접 사용한다. 같은 종목·봉·기간·브로커 범위는 2분 동안 최대 128개 캐시하고, 공개 목록이 이미 제공한 업종을 다시 조회하지 않는다. 하단 완료 문구는 자료조회와 전략계산 평균을 분리해 다음 실제 실행에서 병목을 바로 확인할 수 있다. 브로커의 직렬 REST 요청과 650ms 최소 간격 때문에 40종목이 각 한 요청만 해도 간격 하한이 약 26초다. Rust/C++ 네이티브 코드로 옮겨도 이 네트워크 대기는 줄지 않아 DLL을 추가하지 않았다.

Release 빌드 경고0/오류0, 대상 UI 219개, UI smoke 394개, 전체 1060개를 통과했다. 게시본 `artifacts/screening-market-performance-final-20261023/Stock.exe`와 루트 `Stock.exe`는 80,651,838 bytes, SHA256 `82D8B600B6A49E7D8E6986A5587816481AFDBEAF6A2AD135D4B7F9F4F5D57393`로 일치하고 양쪽 smoke를 통과했다. 이전 루트는 `artifacts/Stock-before-screening-market-performance-EAA06D41.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-23 스크리닝 지정봉 전체보기·우클릭 탐색 전환

스크리닝 목록과 `스크리닝 결과 > 종목별 조건·체결`에서 여는 상세 창은 동일한 `OpenScreeningStock → ScreeningChartDialog → CandleChart` 경로를 사용한다. 상세 창은 immutable screening snapshot의 `BarSpecification`을 첫 주기로 명시적으로 선택하며, 시간봉도 장중 주기로 판별해 시간 축과 실제 체결 시각을 포함하는 봉에 매수·매도 표식을 배치한다.

첫 화면은 선택 기간의 모든 봉을 보여주는 잠금 상태다. 이때 휠, 좌클릭, 드래그, 체결 선택은 동작하지 않고 원하는 봉에서 우클릭만 허용한다. 첫 우클릭은 포인터 봉을 중심으로 `min(500, max(25, ceil(전체 봉/4)))` 범위를 표시하며 휠 확대·축소, 좌우 드래그, 표식 클릭을 활성화한다. 이 상태의 최대 축소 범위는 최초 집중 범위를 넘지 않는다. 두 번째 우클릭은 전체 기간으로 복귀하고 다시 입력을 잠근다. 데이터와 매매 내역은 줄이거나 다시 계산하지 않는다.

Release 빌드 경고0/오류0, 전체 1044개를 통과했다. 시간봉 지정, 전체 잠금, 우클릭 집중, 최대 축소 제한, 두 번째 우클릭 복귀, 결과 창 진입 경로와 공통 매매 표식을 검증했다. 화면은 `artifacts/screening-review-ui-final-20261023/screening-chart-focused-hour.png`다. 게시본 `artifacts/screening-review-final-20261023/Stock.exe`와 루트 `Stock.exe`는 80,649,488 bytes, SHA256 `EAA06D410CB8ABFE168FF69F07F84FB8292D31BB3B978BC3DBAAF5788D930B65`로 일치하며 양쪽 smoke를 통과했다. 이전 루트는 `artifacts/Stock-before-screening-review-DEE1F31D.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-22 시장별 상위 선택값·장기간 지정봉 이동 성능

스크리닝의 시장별 상위 선택값은 `10, 20, 50, 100`이며 선택한 수를 KOSPI와 KOSDAQ에 각각 적용한다. 기존 25 선택은 20으로 교체했다.

`CandleChart`의 장기간 분봉·시간봉 이동 병목을 줄였다. 축소 상태에서 실제 봉 폭이 1px 미만이어도 그 폭에 비례해 드래그 거리를 계산하고, 드래그 중 무효화 요청은 Dispatcher 렌더 프레임당 한 번만 보낸다. 이동평균·일목 값, 펜과 브러시는 이동마다 다시 만들지 않는다. 여러 봉이 같은 픽셀에 겹칠 때는 화면 표시 단계에서만 OHLCV 열로 묶으며 원본 봉은 확대·마우스 정보·매매 표식·백테스트를 위해 그대로 유지한다.

Release 빌드 경고0/오류0, WPF 211개, 전체 1036개를 통과했다. 1만 개 합성 1분봉 중 2천 개 표시, 이동평균·일목 활성, 12회 연속 이동 렌더는 최적화 전 1,096ms에서 최종 회귀 787ms였으며 지표 캐시는 한 번만 생성됐다. 게시본 `artifacts/screening-pan-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,648,597 bytes, SHA256 `DEE1F31D41637F742A65CBCAE52F546E2A0D133209DE785AFFDF6738CDD4D350`으로 일치한다. 게시본·루트 smoke는 Success/NoBrokerCalls/ScreeningSnapshotVerified=true다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-22 스크리닝 온라인 지정봉·전체 기간 상세 차트

오프라인 스크리닝은 일봉만 사용할 수 있으므로 지정봉 선택 영역을 숨기고, 저장되거나 코드로 남은 틱/분/시 설정도 실행 시 일봉으로 강제한다. 온라인 연결 후에는 `틱봉`, `분봉`, `시간봉`을 명시적으로 선택한다.

일반 종목 차트는 빠른 첫 화면을 위해 최근 분봉 3페이지만 유지하지만, 스크리닝 상세는 별도 `IRangedMinuteHistoryBroker` 경로를 사용한다. 키움 `ka10080` 요청의 `base_dt`를 스크리닝 종료일로 지정하고, 시작일을 포함하는 페이지에 도달할 때까지 최대 30페이지를 이전 방향으로 조회한다. 화면은 정확한 시작일~종료일만 집계하며 임의로 일봉으로 대체하지 않는다.

`ScreeningChartDialog`는 지정 기간의 집계봉 전체를 첫 화면에 맞춘다. 2026-10-01~2026-10-20 1시간 fixture에서 완성봉 84개 전부가 표시됐고, 휠 확대 뒤에도 원본 84개가 유지됐다. 증거 화면은 `artifacts/screening-range-ui-final-20261022/screening-hour-full-range.png`다.

Release 빌드 경고0/오류0, 전체 1033개, 최종 WPF 379개를 통과했다. 게시본 `artifacts/screening-range-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,645,331 bytes, SHA256 `04FB712C5372CA2414CD0F983C6D914639C6DA7C9A450270B7C4C31DB73C6874`로 일치한다. 게시본·루트 smoke는 Success/NoBrokerCalls/ScreeningSnapshotVerified=true다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-22 거래번호·포인터 기준 확대·스크리닝 지정봉 상세

백테스트 매수·매도 표식에 `OversoldResult.Trades`의 1부터 시작하는 거래번호를 전달한다. 거래상세목록의 한 거래에 속하는 분할매수와 부분매도는 같은 번호를 유지하고 일반 표식, 클릭 상세, 축소 집계 배지 모두 `#번호`를 표시한다. WPF 렌더 증거는 `artifacts/numbered-marker-ui-final-20261022/zoomed-out-trade-counts.png`다.

`CandleChart`의 휠 확대·축소는 포인터 X 위치를 봉 인덱스로 환산해 확대 전후 같은 봉이 같은 화면 위치에 남도록 offset을 보정한다. 따라서 더 이상 오른쪽 끝만 향해 확대되지 않는다.

스크리닝 결과 상세는 실행 snapshot의 `BarSpecification`과 시작일~종료일을 공통 `ScreeningBars` 경로에 전달한다. 첫 시리즈가 스크리닝 당시의 정확한 틱/분/시/일봉이고, 일봉은 사용자 선택 봉을 대체하지 않는 보조 자료다. 키움 `ka10079/ka10080`의 현재 진행 중인 불완전 행 한 건을 엄격 검증하다 전체 정상 봉까지 버리던 것이 `가격 데이터 형식이 올바르지 않습니다`의 원인이었다. 원본 틱은 체결가 한 점으로 O/H/L/C를 맞추고, 분봉은 잘못된 진행 행만 건너뛰며 완성된 행을 유지한다. 모든 행이 잘못된 경우에만 구체적인 데이터 오류를 반환한다.

Release 빌드 경고0/오류0, `--oversold-ui` 207개, 전체 1014개를 통과했다. 게시본 `artifacts/number-zoom-screening-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,712,184 bytes, SHA256 `76602D9E0F20AE1A52E7DC01A5C7045387CC86636D47CED0E47085469F477C6B`로 일치한다. 게시본·루트 smoke 모두 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 실제 키움 네트워크와 실계좌 주문은 실행하지 않았고, 공식 형식의 가짜 응답과 임시 DB로 파서·온라인 화면 경계를 검증했다.

# 이전 상태 — 2026-10-22 축소 매매 표식 집계·모바일식 차트 이동

실제·모의·백테스트·정기매수 체결을 공유하는 `CandleChart`와 ScottPlot 원본 틱 차트에 화면 구간 기반 표식 집계를 적용했다. 축소해 표식이 겹치는 경우 약 92px 폭마다 매수와 매도를 따로 묶어 `매수 N건`, `매도 N건`으로 표시한다. 표시는 묶지만 원본 체결 시각·가격·수량과 저장 자료는 그대로 유지하며, 원본 틱 차트의 집계 표식을 누르면 같은 묶음의 개별 체결을 선택할 수 있다. WPF 실제 렌더는 `artifacts/marker-cluster-ui-final-20261022/zoomed-out-trade-counts.png`다.

봉 차트는 마우스를 누른 위치와 관계없이 포인터를 캡처해 좌우 드래그한다. 오른쪽으로 밀면 과거, 왼쪽으로 밀면 이후 시점을 보여주는 직접 조작 방식이며, 봉 폭이 큰 확대 상태에서도 12px 이내 단위로 움직여 한 봉 전체 폭을 끝까지 끌 필요가 없다. 4px 이내 움직임은 기존 단일 클릭으로 유지된다. 스크리닝 백테스트 팝업도 같은 `CandleChart`를 사용하므로 동일하게 동작한다.

Release 빌드 경고0/오류0, WPF 205개, 원본 틱 30개, 전체 1004개를 통과했다. 게시본 `artifacts/chart-marker-pan-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,710,099 bytes, SHA256 `6B2E25C99061FF33D5ECDF7352D4A7C761F2C842E16206161F695CA463EA13D3`로 일치한다. 게시본과 루트 smoke 모두 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 최신 상태 — 2026-10-22 백테스트 매매 표식 실제 화면 검증

삼성전자 `백테스트 검증 1 · 1일 보유`를 2025-10-22~2025-11-30으로 실제 루트 WPF에서 실행했다. 완결 5회, 승률 60.00%, 최종 평가자산 10,050,300원, Profit Factor 2.05가 표시됐고 `백테스트 매매 내역 표시`를 OFF하면 표식이 사라지고 다시 ON하면 실행 기간과 `매수`/`매도` 표식이 함께 복원되는 것을 확인했다. 화면은 `artifacts/backtest-marker-visible-20261022/backtest-trade-markers-verified.png`다.

실제 확인에서 1초 현재시세 갱신이 백테스트 종료일 이후 일봉을 다시 붙여 표식을 압축하는 문제를 수정했다. 표시를 OFF했다가 ON할 때도 선택한 `OversoldResult`의 From/To를 다시 적용한다. 따라서 자동 표시와 수동 재선택 모두 같은 테스트 구간을 유지한다.

Release 빌드 경고0/오류0, 표식 WPF 202개, 전체 1001개, 루트 smoke를 통과했다. 게시본 `artifacts/backtest-marker-toggle-final-20261022/Stock.exe`와 루트 `Stock.exe`는 190,775,540 bytes, SHA256 `962609E9B504ECEE917DB98CC528152FD0D9365609C79CE039CA9BFDF3BDB619`로 일치한다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-21 스크리닝 명칭·온라인 1시간봉·백테스트 표식·전면 검증

상단 탭과 관련 문서를 `스크리닝`으로 통일했다. 온라인 지정봉 스크리닝은 연결된 브로커의 공식 1분봉을 읽어 사용자가 지정한 N분/N시간봉으로 집계한다. 키움 분봉에 정확한 체결대금 필드가 없으므로 거래대금은 대표가격×거래량 근사이며 화면과 snapshot에 이 한계를 남긴다. 과거 시점의 종목 분류·거래정지/관리·Fundamental Risk가 없으면 해당 사실은 `자료부족`으로 유지하지만, Crash/RSI/거래량/Support 기술 조건은 관측 자료만으로 계속 평가한다. 이 연구용 평가는 실제 주문 허가로 사용하지 않는다.

저장된 기본 키움 계정의 읽기 전용 경로에서 삼성전자, 지정봉 1시간, 2026-10-14~2026-10-21을 실제 WPF로 확인했다. 결과는 `전체 1개 중 1개 완료 · 조건 일치 0개 · 조회 실패/봉 없음 0개 · 완료 · 00:01 (1.45초/종목)`이었고 기술 단계가 평가됐다. 따라서 이전의 1시간봉 `데이터 없음` 원인은 지정봉 스크리닝이 브로커 분봉 이력을 사용하지 못한 것이었으며, 수정 뒤에는 봉 결손이 아니라 전략 불일치와 과거 상태 자료 결손을 구분한다.

종목/차트 백테스트가 성공하면 `백테스트 매매 내역 표시`를 자동 선택하고 차트 데이터/기간을 실행 범위로 맞춰 매수·매도 표식이 즉시 보인다. 표식 또는 표식이 있는 봉을 한 번 클릭하면 source, 매수/매도, 한국시간, 가격, 수량을 별도 팝업으로 표시한다.

실제 다량 실행 창에서 시작 조건 실패가 하단 문구와 별도 `다량 전략 실행 안내` 팝업에 함께 보이는 것을 확인했다. 장 종료 뒤 실제 피드에서는 새 모의 체결을 만들 수 없었고, 결정론적 FakeFeed 회귀에서 지원 틱/분의 시작→PaperBroker 가상 체결→차트 표식→전체 중지 전 과정을 확인했다. 실제 브로커 주문 호출은 0건이다.

Release 빌드 경고0/오류0, 전체 회귀 998개 통과. 게시본 `artifacts/screening-hour-final-20261021/Stock.exe`와 루트 `Stock.exe`는 190,775,540 bytes, SHA256 `C79DC0A55AC4C0BF7A32B2E6D26D53F7FA8A3FFE8E7B8B0293C0A89A1F0A0FF4`로 일치한다. 게시본 및 루트 smoke는 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 이전 루트는 `artifacts/screening-hour-final-20261021/Stock-before-20261021-192312.exe`에 보관했다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-21 전일 종가 등락·저장 전략 동기화·다량 실행 안내 팝업

종목·차트 헤더의 등락 금액과 등락률은 틱·분·일·주·월봉 선택과 무관하게 전일 일봉 종가를 기준으로 한다. 실시간은 현재 시세의 가격/등락으로 복원한 전일 종가를 사용하고, 과거 화면은 마지막 표시 일봉보다 앞선 가장 최근 거래일 종가를 사용한다. 주봉·월봉끼리 비교해 헤더가 바뀌던 문제를 수정했다.

다량 전략 실행의 등록 전략 목록은 전략 설정의 `Presets`를 그대로 읽는다. 추가·JSON 불러오기·이름 변경한 저장 전략이 팝업에 나타나며 과거 모의, 실시간 모의, 실계좌 경로 모두 종목별 선택 전략의 `oversold-preset:<이름>` Config를 실제로 사용한다. 실계좌의 비상 손절 필수 보호는 유지한다.

다량 창의 명령이 지원 주기·계좌·시장 세션·검증 원본 등 시작 조건에서 실패하면 기존 하단 상태와 함께 `다량 전략 실행 안내` MessageBox를 표시한다. 전략 수식은 임의의 N개 봉을 처리하지만 실시간 실행은 인과적으로 닫힌 봉과 검증 원본을 보장하는 틱/분만 연결돼 있다. 일봉은 장중 미완성이고 시간봉은 준비 자료/경계 검증이 아직 연결되지 않았다.

Release 빌드 경고0/오류0, 낙주/현재가 UI 198개, 다량 UI 25개, 전체 오프라인 회귀 992개 통과. 게시본/루트 smoke 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 게시본 `artifacts/header-strategy-popup-final-20261021/Stock.exe`와 루트 `Stock.exe`는 80,702,235 bytes, SHA256 `3231B86179C4BD5C79FD9DC1BFDA6E942EC402EC016F88305BFCCCFED8F4B69F`. 이전 루트는 `artifacts/Stock-before-header-strategy-popup-33F0AC33.exe`. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-21 1초 차트·기본계좌 자동연결·온라인 백테스트·가상계좌 주문

실시간 이벤트를 1초 Dispatcher 주기로 모아 틱·분·일·주봉의 현재가와 진행 중 마지막 봉을 함께 갱신한다. 분봉은 내려받은 이력의 마지막 구간을 같은 시간대 실시간 체결 집계로 교체해 과거 배열이 새 가격을 가리지 않는다. 제공처가 새 값을 보내지 않을 때 가격을 임의 생성하지 않으며 전체 이력을 1초마다 다시 받지 않는다.

온라인 투자는 저장·검증된 기본계좌가 있으면 키 설정 창 없이 연결하고 라디오 옆에 증권사·마스킹 계좌를 표시한다. 기본계좌가 없을 때만 기존 연결창을 연다. 온라인 상태에서도 저장 전략 백테스트와 정기매수 백테스트를 그대로 사용할 수 있다.

종목·차트 오른쪽 도구는 `실제 계좌 주문 / 가상 계좌 주문 / 전략 백테스트 / 정기매수 백테스트` 4탭이다. 오프라인에서는 실제 탭을 숨기고 가상 탭을 기본 선택한다. 가상 탭의 실시간 전략 버튼은 다량 창을 모의+실시간으로 잠그고, 실제 탭의 다중전략 버튼은 실제 모드로 잠근다. 팝업을 닫아도 Application-level 실행 상태를 유지하는 기존 구조와 주문 직전 중지 검사를 재사용한다.

Release 빌드 경고0/오류0, 일봉·주봉/UI 194개, 다중전략 UI 24개, 전체 오프라인 회귀 987개 통과. 게시본/루트 smoke는 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 게시본 `artifacts/live-account-final-20261021/Stock.exe`와 루트 `Stock.exe`는 80,700,174 bytes, SHA256 `33F0AC33EA0DA515C57B46AE56CA4C8D37564DBB63AD231AE1A1636655B35947`. 이전 루트는 `artifacts/Stock-before-live-account-3AAA0573.exe`. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-21 백테스트 표식·분봉 응답·긴급중지 취소

종목·차트에 `백테스트 매매 내역 표시`를 추가했다. 선택한 단일 종목 백테스트의 정확한 실행 내역을 우선 사용하고, 구형 결과는 저장 Fill을 공통 `TradeMarker`로 변환한다. 실제·모의·정기매수 표식과 서로 덮어쓰지 않고 합쳐지며 세 체크는 독립적으로 저장·복원된다.

키움 국내 `ka10080` 분봉 차트가 `cont-yn=Y`를 최대 100페이지까지 기다리다 약 1분 뒤 실패하던 원인을 고쳤다. 차트 이력은 최근 3페이지를 부분 성공으로 반환하고 화면에는 15초 제한을 둔다. 계좌·잔고·주문은 기존 전체 페이지/fail-closed 정책을 유지한다. 실패 시 `데이터 없음`으로 뭉개지 않고 취소·시간 초과·증권사 오류 원인을 표시한다.

우측 상단 긴급중지는 Busy 래퍼 밖의 독립 명령이다. 차트 로딩 중에도 활성화되며 전경 작업과 시세·관심종목·시장현황·틱/분/일봉 API 공통 토큰을 취소하고 로딩 오버레이를 즉시 지운다. 이후 명시적인 일반 명령을 시작할 때 API 요청을 다시 허용한다. 기존 정책대로 신규 주문/전략은 중지하지만 보유 종목을 자동 청산하지 않는다.

Release 빌드 경고0/오류0, WPF UI 349개, 전체 오프라인 회귀 980개 통과. 최종 루트 smoke 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true. 게시본 `artifacts/backtest-minute-emergency-final-20261021/Stock.exe`와 루트 `Stock.exe`는 80,698,922 bytes, SHA256 `3AAA05739D536323EF791407A20219F7A0A90C971E433771B2CF37ECF3CD90EA`. 이전 루트는 `artifacts/Stock-before-backtest-minute-emergency-D2990AD4.exe`. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았고 키움 연속 페이지는 공식 형식의 가짜 HTTP로 검증했다.

# 이전 상태 — 2026-10-20 차트 범위·키움 틱/분봉·모의표식·Git 보호

종목·차트의 1/3/6개월·1/3/5/10년을 공통 RadioButton 선택 그룹으로 바꿔 선택색을 유지한다. `CandleChart`는 일목 선행 구름을 실제 봉 범위에 클리핑하고 미래 슬롯을 예약하지 않아 확대·팬 후 오른쪽 빈 띠가 생기지 않는다. 일·주·월봉과 7개 기간의 21개 조합을 WPF 렌더로 확인했다.

연결 계좌가 지원하는 데이터 API에 따라 틱·분 선택기를 활성화한다. 키움 국내는 `ka10079` 원본 1틱과 `ka10080` 1분봉 REST 이력을 사용하고 N틱/N분으로 로컬 집계한다. 틱을 골라도 원본 틱 탭으로 이동하지 않고 중앙 차트를 갱신한다. 실제 체결과 모의 체결 체크는 분리했으며 수동/실시간 모의 체결은 공통 marker로 틱·분·일·주·월봉에 매핑된다.

`.gitignore`는 EXE/빌드/게시/로그/DB/WAL/SHM/로컬 설정/DPAPI 보호 키를 제외하고 공식 명세 JSON은 추적 가능하게 둔다. `docs/security/GitPrivacy.md`에 첫 커밋 전 확인 및 이미 추적한 비밀 파일 처리 원칙을 기록했다. 프로젝트 자체는 아직 Git 저장소가 아니어서 임시 저장소로 ignore 규칙을 검증했다. 비밀 문자열 검사는 테스트용 가짜 자격 증명 `Stock.Tests/ConnectionTests.cs`만 탐지했다.

Release 빌드 경고0/오류0, 전체 회귀 962개, 확장 UI 345개, 게시본/루트 smoke 종료0. Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true. 게시본 `artifacts/chart-api-final-20261020/Stock.exe`와 루트 `Stock.exe`는 190,689,012 bytes, SHA256 `D2990AD476CC5CE2DE5B2B67287A0E8BD26C1BA780915A9CD84096A1EC0932A9`. 이전 루트는 `artifacts/Stock-before-chart-api-76B2AB54.exe`. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았고 키움 이력은 공식 스키마 가짜 HTTP로 검증했다.

# 이전 상태 — 2026-10-14 다중 전략 실시간 모의매매·전 구간 체결 표식

다량 전략 창에 별도 `실시간` 체크를 추가했다. `모의 ON + 실시간 OFF`는 기존 과거 종목별 백테스트, `모의 ON + 실시간 ON`은 키움 실제 체결·호가를 사용하는 가상 체결, `모의 OFF`는 기존 선택 키움 국내 실계좌 보호 경로다. 실제 모드는 항상 실시간이며 주문 직전 모드·중지·계좌·현금·노출 검증을 유지한다.

새 `Stock.Core/Trading/RealtimePaperStrategyBatch.cs`는 `OversoldLiveStrategy`, `DataQualityGate`, 시점별 facts/warmup을 재사용하지만 종목별 `PaperBroker`에만 주문한다. 실제 브로커 submit을 호출하지 않고 신호 다음 실제 이벤트부터 가상 체결한다. 중지는 신규 가상 주문 차단과 가상 미체결 취소만 수행하며 보유분을 청산하지 않는다. 공용 WebSocket을 선택 차트와 공유하고 체결은 source `실시간 모의매매`인 `TradeMarker`로 저장된다. 동일 체결시각·가격이 틱·분·일·주봉에 매핑되고 온라인 차트에서 다시 로드된다.

Release 빌드 경고0/오류0. `--multi-mock` 5, `--expansion-checks` 126, `--current-daily-checks` 99, `--multi-ui` 23, `--oversold-ui` 193, 전체 회귀 915개 통과. 최종 안내 문구 반영 뒤 Release 빌드와 다중 UI 23개도 재통과했다. 전체 회귀는 가짜 브로커·피드/임시 DB만 사용했고 계좌 거래가 없었다. 게시본 `artifacts/realtime-paper-final-20261014-b/Stock.exe`와 루트 `Stock.exe`는 80,693,800 bytes, SHA256 `76B2AB5464FF5457A7D909A0A3A159F6233F8B6528490565498B4A8903AF4B37`. 게시본·루트 smoke 모두 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 이전 루트는 `artifacts/Stock-before-realtime-paper-49FD7DDA.exe`. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-14 전략 선택·스크리닝 성능·차트 조작·Stock.exe 통합

종목/차트의 전략 백테스트와 종목 골라보기에 공통 저장 전략 ComboBox를 추가하고 실행 버튼을 `백테스트`로 바꿨다. `OversoldScreeningRun.StrategyName`이 실행 당시 선택을 보존한다. 전략 설정의 중복 `국내 과매도·낙주 반등 · 일봉 백테스트` 래퍼와 중복 저장 버튼은 제거했다.

스크리닝 기본 기간은 최근 1주다. 목록 설명과 종목 평가는 최대 4개씩 병렬 처리하고, 현재 상태·현재가를 함께 읽으며, 공개 현재가 cadence/cache는 symbol별로 분리했다. 10분 이내 일봉을 재사용하고 SQLite 실행 snapshot은 8종목 단위로 저장한다. 합성 8종목 0.468초, 공개 KOSPI/KOSDAQ 각10개 합계20종목은 9.52초/9.55초였다. 보고서: `artifacts/public-screening-performance-20261014`, `artifacts/public-screening-performance-repeat-20261014`.

가격 차트는 좌우 드래그 pan을 양방향으로 제한 보정했다. 매수/매도 표식은 14 DIP이며 단일 클릭 및 표식 봉 전체 클릭으로 가장 가까운 거래 상세를 연다. 전역 DataGrid 셀/헤더는 가로·세로 중앙 정렬이고 스크리닝 DatePicker와 관심그룹 행 높이를 맞췄다.

Release 빌드 경고0/오류0, 현재가99, 대상 WPF193, 전체 WPF305, 전체 회귀904 통과. 게시본 `artifacts/strategy-screening-final-20261014/Stock.exe`와 루트 `Stock.exe`는 80,755,858 bytes, SHA256 `49FD7DDA43D5518F53B700ED7B02C9F64F62344EAF1F708818532241B59C7BDE`. 게시/루트 smoke 모두 Success/NoBrokerCalls/RawNativeRendered/ScreeningSnapshotVerified=true. `Stock-검증완료-20261013.exe`는 삭제했고 이전 루트는 `artifacts/Stock-before-strategy-screening-8F97D6F7.exe`에 보관했다. 실제 키·실계좌 주문·사용자 운영 DB 삭제 없음.

# 이전 상태 — 2026-10-13 낙주 백테스트 0건 수정·검증 전략 3개·스크리닝 백테스트 팝업

기존 `OversoldBacktest`가 `HistoricalFacts.MarketSessions`가 비어 있으면 완성 일봉이 있어도 즉시 끝나 판단/전이가 모두 0이던 문제를 수정했다. 실제 완성 일봉 날짜에서만 KRX 세션을 파생한다. 엄격 기본 전략은 과거 상태·위험·실제 거래대금 누락을 계속 차단한다. 별도 `UsePriceOnlyBacktest`는 검증 프리셋만 사용하며 실시간 주문/스크리닝 우회가 아니다.

일반 전략 목록에 `백테스트 검증 1 · 1일 보유`, `2 · 초단기 익절`, `3 · 지지이탈`을 동명 전략이 없을 때 자동 저장한다. 삼성전자 네이버 공개 일봉 1,500개(2020-08-26~2026-10-13)로 각각 MaxHoldingDays, TP1, SupportBreak를 확인했다. 보고서: `artifacts/samsung-oversold-verification-final/samsung-oversold-verification.json`.

종목/차트와 스크리닝 팝업은 `IOversoldBacktestService` 하나를 공유한다. 스크리닝 후보 더블클릭/백테스트 보기는 같은 `OpenScreeningStock` 경로이며, 팝업에 실행 당시 snapshot, 일봉 성과, 거래표, ▲/▼, Support Zone, 상세, 이전/다음, 취소, 조건 해시 cache를 연결했다. 지정 틱/분/시는 현재 일봉 체결 엔진의 범위 밖이므로 검증 봉과 저장 체결만 보여주며 가짜 체결을 만들지 않는다.

최종 검증: Release 경고0/오류0, Core70, WPF186, 삼성 공개6, 전체896 통과. 독립 EXE smoke 종료0, NoBrokerCalls=true. 게시본 `artifacts/backtest-popup-final-20261013/Stock.exe`, SHA256 `802EE3EAA7EF81C11937E8C57BFC0761C70BFA1A239B96604E5FC72E6829A31F`, 80,683,012 bytes. 기존 루트 `Stock.exe`가 실행 프로세스에 잠겨 교체하지 못해 같은 위치에 `Stock-검증완료-20261013.exe`로 staging했다. 사용자가 앱을 닫은 뒤 게시본/검증완료 파일을 루트 `Stock.exe`로 교체하고 해시를 재확인하면 된다. 실제 키·사용자 DB·실계좌 주문은 건드리지 않았다.

# 최신 상태 — 2026-10-11 전략 진단 프리셋 추가

전략 설정 시작 시 `전략 진단` 프리셋이 없을 때만 자동 등록한다. 설정은 RSI/거래량/Pivot/근사 매물대/반등 확인 OFF, 이동평균 MA5, 최소 지지 1개다. 별도 상태 확인 UI나 실행 차단은 없으며 일반 저장 전략과 같은 선택·수정·스크리닝·백테스트·실시간 경로를 사용한다. 사용안함은 OFF 조건, 자료부족은 필요한 자료 결손, 미평가는 앞 단계 제외 또는 미처리라는 기존 의미를 유지한다.

Release 경고0/오류0, 대상 WPF 184개와 전체 891개 통과. 게시본 `artifacts/diagnostic-strategy-final-20261011/Stock.exe`, 루트 smoke `artifacts/diagnostic-strategy-root-smoke-final-20261011/result.json`은 종료0이며 Success/NoBrokerCalls/ScreeningSnapshotVerified=true다. 루트 `Stock.exe` SHA256 `8F97D6F75474DBBD02EAEB8C7EB6C75233BDC6C83C4091F827016263D3C2FC5B`, 80,670,957 bytes. 이전 루트는 `artifacts/Stock-before-diagnostic-strategy-5ACA27C1.exe`; 중간 게시본은 `artifacts/Stock-before-diagnostic-final-40439E72.exe`. 사용자 운영 DB·키·실계좌 주문은 건드리지 않았다.

# 최신 상태 — 2026-10-11 전략 설정 라이브러리 완료

전략 설정 상단은 이름 목록 선택, `추가`, `이름 변경`, `설정 저장`, `복구`, `불러오기`, `다운로드`, `기본값`으로 구성한다. 실행 시 마지막 적용 설정과 마지막 선택 전략을 자동 복원한다. `복구`는 선택 전략의 마지막 저장값으로 미저장 입력을 되돌린다. `불러오기`는 `OversoldPresetEnvelope` JSON의 schema/strategy/config를 검증하고 기존 전략을 덮어쓰지 않는 고유 이름으로 새 전략을 등록한다. 선택 변경은 저장된 낙주 설정을 즉시 적용하며 이름 변경은 이전 SQLite state 키를 제거한다.

Release 경고0/오류0, `--oversold-ui` 182개, 전체 889개 통과. 게시본 `artifacts/strategy-library-final-20261011/Stock.exe`, EXE-only smoke `artifacts/strategy-library-smoke-result-20261011/result.json`, 루트 smoke `artifacts/strategy-library-root-smoke-20261011/result.json`은 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified가 true다. 게시본과 루트는 80,670,041 bytes, SHA256 `5ACA27C1CE116C7D4D11A40912D578D1A6E889CF86DE98F40E0E93970E9FDFF0`. 이전 루트는 `artifacts/Stock-before-strategy-library-4D6816F6.exe`. 실제 키·증권사 네트워크·실자금 주문·사용자 운영 DB 삭제는 수행하지 않았다.

# 최신 상태 — 2026-10-11 툴팁·컨트롤 중앙 정렬·스크리닝 상태 설명 완료

공통 `Styles/TossInspired.xaml`의 ToolTip을 흰 배경·짙은 글자·옅은 테두리와 그림자로 변경했다. Button/TextBox/RadioButton/DatePicker 입력 내용은 세로 중앙 정렬이며 기본·Secondary·TextLink 사용자 지정 버튼 템플릿도 정렬 속성을 그대로 사용한다.

`ScreeningResultDialog` 단계별 집계 상단에 자료부족과 미평가의 의미를 표시한다. 자료부족은 필요한 과거 봉·시점별 종목 상태·거래대금·지표 준비값이 없다는 뜻이고, 미평가는 아직 처리 전이거나 앞 단계 탈락 또는 결손 때문에 뒤 조건을 실행하지 않았다는 뜻이다. 최초 자료부족 한 건과 후속 미평가 여러 건은 같은 종목에서 함께 집계될 수 있다.

Release 경고0/오류0, `--oversold-ui` 178개, `--ui-smoke` 290개, 전체 885개 통과. 최종 설명 문구 반영 뒤 `--oversold-ui` 178개도 재통과했다. 화면은 `artifacts/tooltip-alignment-full-20261011-final`, 게시본은 `artifacts/tooltip-alignment-final-20261011/Stock.exe`, 게시/루트 smoke는 각각 `artifacts/tooltip-alignment-final-smoke-20261011/result.json`, `artifacts/tooltip-alignment-final-root-smoke-20261011/result.json`이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified가 true다. 루트와 게시본은 80,738,541 bytes, SHA256 `4D6816F6020297443E75B2CCD6F0049E093C7DECFBD4A70177E263B84E184EA5`. 이전 루트는 `artifacts/Stock-before-tooltip-alignment-C8F17B3D.exe`. 실제 키·증권사 네트워크·실자금 주문·사용자 DB 삭제는 수행하지 않았다.

# 최신 상태 — 2026-10-11 관심등록·스크리닝 상세 차트·다량 전략 편집 완료

관심등록은 현재 선택 그룹에 등록된 종목이면 파란색 채움 별(★)을 표시한다. 비버튼 ListBox/ComboBox 항목과 탭의 커서는 화살표로 복구했다. 종목 골라보기 상단 TextBox/ComboBox/DatePicker/RadioButton과 버튼 높이는 40px로 통일했다.

후보 표의 파란 종목 이름 클릭과 스크리닝 결과 `종목별 조건·체결` 행 더블클릭은 메인 종목·차트 탭을 바꾸지 않고 `ScreeningChartDialog`를 연다. 일봉은 항상 표시하고, 검증된 원본 체결 또는 연결 계좌 분봉이 있으면 1분봉을 추가한다. 틱 기준 실행이며 원본 구간이 검증되면 당시 N틱봉도 선택할 수 있다. 일봉 실행은 당시 run/facts/config로 기간 모형 체결을 계산하고, 저장 체결과 함께 marker 및 한국시간·가격·수량·사유 표에 표시한다. 원본이 없으면 다른 주기로 대체하지 않는다.

다량 전략 창은 새 수동 추가 행에 첫 등록 전략을 자동 지정한다. 종목별 예산은 표 안 TextBox로, 전체 투자 한도와 일 손실 한도는 실행 전 항상 편집한다. 모의 체크도 오프라인에서 조작 가능하되 검증된 온라인 계좌가 없으면 실제 실행 명령은 비활성이라 주문 경계는 유지된다. 창 높이는 880px로 늘려 편집 행을 보이게 했다.

Release 경고0/오류0. --oversold-ui 175개, --multi-ui 18개, 전체 881개 통과. 검증 화면은 artifacts/latest-ui-check-2 및 artifacts/latest-multi-check-2. 단일 게시본은 artifacts/screening-popup-final-20261011/Stock.exe, smoke 결과는 artifacts/screening-popup-smoke-result-20261011/result.json과 artifacts/screening-popup-root-smoke-20261011/result.json이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified가 true다. 게시본과 루트 Stock.exe는 80,668,018 bytes, SHA256 C8F17B3DF79B745913EA7F24CE0025F4BF848172A7EC6D5ED81111C1424B8223. 이전 루트 실행 파일은 artifacts/Stock-before-screening-popup-F28481D6.exe에 보관했다. 실제 키·증권사 네트워크·실자금 주문·사용자 DB 삭제는 수행하지 않았다.

# 이전 상태 — 2026-10-11 토스증권 참고 UI 정리 완료

토스증권 웹의 현재 시각 체계를 참고해 복제하지 않는 범위에서 앱 공통 디자인을 정리했다. Stock.App/Styles/TossInspired.xaml에 Canvas/Surface/본문/보조문/선/파란 주요 동작/위험 색과 Button, TextBox, ComboBox, CheckBox, RadioButton, DatePicker, ListBox, DataGrid, Tab, Card 스타일을 중앙화했다. App.xaml은 이 ResourceDictionary를 병합한다. 기존 MVVM·명령·입력기·주문 경로는 변경하지 않았다.

MainWindow의 양쪽 고정 패널을 190/350px로 조정해 최소 1160×760에서도 중앙 차트를 234px 유지한다. 1540×1040에서는 514px다. 다량 전략 창은 검색/예산, 종목 표, 실행 제어를 카드로 나누고 실행 외 버튼은 보조 동작으로 구분했다. 기존 소수 두 자리 입력, 32px 전략 입력칸, 오른쪽 최소14, 선택 동기화와 주문 차단 검사는 유지된다.

Release 경고0/오류0. --oversold-ui 171개, --multi-ui 15개, 전체 874개 통과. 로그 artifacts/toss-ui-full-verified-20261011.log, 화면 artifacts/toss-ui-preview-20261011-e, 다량 창 artifacts/toss-multi-ui-20261011. EXE-only smoke artifacts/toss-ui-clean-result-20261011/result.json: Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified true. 최종 게시본과 루트 Stock.exe는 80,657,800 bytes, SHA256 F28481D698A9C75DB5148B7BE947ECDC9BA90691B5D4AA000018086EC4B1132B. 이전 루트 EXE는 artifacts/Stock-before-toss-ui-78954CC5.exe.

실제 키·증권사 네트워크·실자금 주문·사용자 DB 삭제는 수행하지 않았다. 실제 모니터의 물리 입력과 DPI 이동은 별도 미검증이다.

# 이전 상태 — 2026-10-10 작업 재개 후 완료

## 최신 인수인계 — 2026-10-11 낙주 전략 선택 계좌 실시간 경로

TASKS.md 맨 위가 현재 상태다. 이후 사용자의 명시 요청으로 다량 전략 창에 모의 체크를 추가하고, 온라인에서 해제한 경우 선택 키움 국내 실계좌 하나에만 낙주 전략을 `LiveStrategyBatch → RemoteTradingSession → OrderGuard`로 연결했다. 현재 시장 세션/시점별 상태·위험/검증 원본 준비 봉이 없으면 시작 전에 차단한다. 완성봉 뒤 체결 신호와 실제 체결번호 대사를 사용하며, 저장 목록을 자동 재시작하지 않는다. 이전 일봉 전용/자동매매 미연결 설명은 아래 과거 상태 기록이다.

Release 경고0/오류0, 전체 874개, 확장 117개, 다량 팝업 WPF15개 통과. 가짜 계좌에서 선택 종목 한 건만 주문되고 중지 시 계좌 점유가 풀리는 검사를 포함한다. 별도 단일 EXE `artifacts/oversold-live-final-20261011/Stock.exe` SHA256 `78954CC5F7286C10E8160D934212CF687F5B76228C33DE121043BF0B8CF5967B`. `artifacts/oversold-live-final-result-20261011/result.json`은 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified 모두 true. 실계좌 키·주문·사용자 DB는 검증에 사용하지 않았다. 실제 장중 증권사 체결은 미검증이다.

루트 `Stock.exe`는 사용자 실행 프로세스 PID 20632가 잠그고 있으며 기존 SHA256 `470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4` 상태다. 사용자가 앱을 닫은 뒤 프로세스가 사라졌는지와 루트 해시가 그대로인지 확인하고, 최종 별도 EXE를 복사해 해시를 재확인한다. 프로세스를 임의 종료하거나 사용자 DB·인증키를 변경하지 않는다.

## 이전 상태 기록

10/06 작업과 아래에 기록된 컴파일 오류는 해결됐다. Release 경고0/오류0, 전체846/WPF164 검사 및 단독EXE smoke 성공. 당시 루트EXE SHA256: 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4 . 검증 로그는 artifacts/resume-*.log 및 current-daily-clean-a4d0bb1754134b6688c385634487bb28/result/result.json.

아래는 계정 전환 안내와 **완료 전 상태의 이력**이다. 10/06 작업/컴파일 오류를 다시 수행할 필요 없다. 개발 명령과 계정 이전 안내는 계속 유효하다.

## 최신 인수인계 — 2026-10-11 기간 스크리닝·다량 모의 백테스트

TASKS.md 맨 위가 현재 작업 상태다. 기간별 스크리닝, 결과→차트·모형 체결, 마지막 봉 hover, 0건 이유, 전략 JSON 다운로드, 기존 MultiTrade 팝업의 그룹 중복 동기화·선택 종목별 모의 백테스트를 소스에 구현했다. 온라인 자동매매는 현재 전략이 일봉 백테스트 전용이므로 실행을 차단한다. 실제 키·계좌·주문은 사용하지 않았다.

WPF 정체 원인은 테스트 프로세스의 App.OnStartup이 일반 창을 추가로 열던 것이었다. 테스트 호스트가 직접 창을 관리할 때만 ManualWindowStartup을 설정해 사용자 DB/단일 인스턴스 창을 열지 않도록 했다. 진단 로그와 대기 루프 실험 코드는 제거했다. Release 빌드 경고 0/오류 0, 전체 862개·대상 WPF 171개·UI 전체 274개 통과. 별도 게시 EXE만 있는 폴더의 smoke는 종료 0, Success/NoBrokerCalls/RawNativeRendered true. 검증 자료는 artifacts/screening-work-full.log, artifacts/screening-work-ui-all.log, artifacts/screening-clean-result-20261011/result.json.

루트 Stock.exe는 사용자 실행 프로세스 PID 20632가 파일을 잡고 있어 교체하지 못했다. 종료 요청은 사용자에게 전달했다. 기존 루트와 artifacts/Stock-before-screen-range-470A1A6C.exe 해시는 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4. 새 게시본은 artifacts/screening-clean-20261011/Stock.exe (80,654,087바이트, SHA256 13C88F091C9267BC9B25C3BAC7628AE85387BC0477DD05FFD23C3E54FD97C8CB). 사용자가 앱을 닫은 뒤 루트 해시가 여전히 옛 값인지 확인하고 검증된 EXE를 복사한 다음 원본과 해시가 같은지 확인한다. 사용자 DB·실계좌 키·주문은 건드리지 않는다.

# 다른 계정에서 작업 재개하기

작성/확인: 2026-10-10 (한국시간). 이 문서는 채팅 접근 없이 현재 소스에서 재개하기 위한 출발점이다. 과거 체크포인트보다 TASKS.md 맨 위의 현재 상태를 우선한다.

## 처음 읽을 순서
1. AGENTS.md
2. TASKS.md 맨 위의 현재 작업 목록
3. 이 HANDOFF.md
4. docs/architecture/ARCHITECTURE.md, docs/architecture/OversoldExpansion-20261005.md
5. 관련 소스와 VALIDATION.md (과거 통과 기록을 현재 소스 검증으로 취급하지 않는다)

## 계정/컴퓨터를 바꾸는 방법
- 같은 Windows 사용자/컴퓨터에서 Codex 로그인 계정만 바꾸면 이 STOCK 폴더를 프로젝트로 열면 된다. 이전 채팅/작업 ID는 필요 없다. 새 계정의 파일 접근 권한은 다시 설정해야 할 수 있다.
- 다른 컴퓨터/Windows 사용자라면 STOCK 폴더 전체를 복사한다. 최소 Stock.sln, Stock.App, Stock.Core, Stock.Tests, docs, 루트 md, 스크립트, 프로젝트/패키지/PublishProfile 파일이 필요하다. bin/obj는 재생성 가능하다. 검증 근거를 보존하려면 artifacts도 함께 보관한다.
- 이 폴더에는 .git이 없다(확인일 기준). 미커밋/새 파일도 포함된 현재 디스크 내용이 작업 원본이다. 원격 저장소만 받아서는 복원되지 않는다.
- C:\Users\wkdru\Desktop\C#\STOCK는 이전 PC 경로일 뿐이다. 아래 명령은 복사한 프로젝트 루트에서 실행한다.
- 앱 데이터는 소스와 별개: %LOCALAPPDATA%\StockPaperTrading\KiwoomQuant\workspace-v2.db. 키는 같은 루트 broker-keys에 있다. 소스 인수인계에 인증정보는 필요 없다. 앱을 종료한 뒤 DB를 백업하고, 실제 자료를 테스트 fixture로 쓰거나 삭제하지 않는다.
- Windows 사용자에 묶인 암호화 키는 다른 Windows 사용자/PC에서 그대로 사용할 수 있다고 가정하지 않는다. 필요하면 사용자가 앱에서 직접 재등록한다. 키/토큰/계좌번호를 문서나 새 채팅에 붙이지 않는다. 이번 작업은 자료나 인증키를 복사/삭제하지 않았다.

## 현재 상태: 소스와 실행 파일이 다르다
- 마지막 검증/배포: 2026-10-05 안전 범위, 전체 810검사 통과 기록. artifacts/expansion-release.json 및 TASKS의 최종 기록 참고.
- 루트 Stock.exe SHA256: 406FEBF652280F0230CB75EAE42315AD96EA8C305A7E51F6E97E16BA81C7EB95 (2026-10-10 재확인). 10/06 변경은 아직 이 EXE에 반영되지 않았다.
- 10/06 일봉 스크리닝/장중 갱신 작업은 사용 한도로 중단. 구현 초안과 신규 테스트가 있으나 검증/문서/배포 미완료.
- 10/10 재현 빌드: Core/App 컴파일 성공, Tests의 CurrentDailyUiTests.cs:39에서 CS1525 두 건으로 솔루션 실패, 경고 0. `GetValue(chart)!==27` 및 `!==3` 토큰 경계부터 확인할 것. 로그 artifacts/handoff-20261010-build.log. 전체/대상 테스트는 이 소스에서 실행하지 못했다. 이전 exe로 검사해서 최신 통과로 보고하지 않는다.
- 샌드박스 빌드는 Windows SDK 레지스트리 조회 MSB4184로 먼저 실패했다. 허용된 일반 Windows 환경에서 다시 실행해 위 소스 오류를 확인했다. 필요 시 SDK 접근 권한을 정식 요청한다.

## 바로 이어갈 작업
1. CurrentDailyUiTests.cs:39 컴파일 오류 해결 후 Release 빌드.
2. --current-daily-checks, 전체 검사, 실제 WPF 화면 검사. 새 CurrentDailyUiTests가 테스트 실행 경로에 포함되는지도 확인.
3. 신규 경로의 취소/종목 전환/마감/휴장/지연/미완성봉/팬줌/스트림 중복 구독 점검. 미확인 후보가 자동주문 신호로 바뀌지 않는지 확인.
4. docs/ui, VALIDATION, TASKS 갱신. 아래 별도 publish/smoke 성공 후에만 루트 Stock.exe 교체 및 SHA256 기록.

## 10/06 요구사항과 수정 파일
사용자는 (a) 기존 검색 일봉으로 JSON 수동입력 없이 현재 스크리닝, (b) 장중 선택 종목 가격과 당일 차트를 1초 주기로 갱신하도록 요청했다.
- Core/Research/CurrentMarketSnapshot.cs, PublicCurrentMarketData.cs, PublicMarketData.cs: 시세/갱신 및 일봉 데이터 경로.
- Core/Research/Oversold/CurrentOversoldScreening.cs, OversoldEntryDetector.cs, OversoldScreening.cs: 현재 후보 평가와 과거 검증 분리.
- App/LiveDailyWorkspace.cs: 단일 선택 종목 조회, 1초 Dispatcher 반영, 체결 병합/장 종료 히스토리 새로고침.
- App/CandleChart.cs, ConfiguredScreening.cs, OnlyOversoldWorkspace.cs, OversoldScreeningWorkspace.cs, OversoldWorkspace.cs, WorkspaceFeatures.cs, WorkspaceViewModel.cs: UI 연결 및 생명주기.
- Tests/CurrentDailyTests.cs, CurrentDailyUiTests.cs, Program.cs: 신규 검사(검증 미완료).
위 Core/App/Tests 표기는 각각 Stock.Core/Stock.App/Stock.Tests 폴더를 뜻한다.

완료 조건:
- 현재 스크리닝에서 OHLCV로 계산 가능한 급락/RSI/거래량/지지/반등을 부가 JSON 누락으로 모두 막지 않는다. 실제 없는 상태/거래대금/위험은 미확인으로 유지하고 후보와 주문가능 신호를 구분한다. 종가×거래량을 실제 거래대금으로 위장하지 않는다.
- 과거 백테스트는 시점별 엄격검증 및 미래정보 방지 유지. 현재 종목 상태를 과거에 소급하지 않는다.
- 차트 15:30 축 기준과 실제 관측 시각 분리. 당일 진행봉은 확정 백테스트 데이터와 분리. 가격/등락률/OHLC/거래량을 함께 갱신하고 팬줌 유지.
- 10/06 확인 공개 응답 pollingInterval=7000ms: UI 1초와 제공처 조회 간격은 별개다. 공개시세를 진짜 매초 신규 시세라고 보장하지 않는다. 연결된 키움 체결을 재사용할 수 있으나 실제 장중 수신 검증은 별도다.

## 개발 환경과 명령
Windows x64, .NET 10 SDK 및 Windows SDK/WPF 빌드 도구. App/Tests TFM은 net10.0-windows10.0.19041.0, Core는 net10.0. ScottPlot.WPF 5.1.59. 확인 환경 SDK 10.0.401. 새 환경에서는 먼저 restore(네트워크 필요).

```powershell
dotnet --info
dotnet restore Stock.sln
dotnet build Stock.sln -c Release --no-restore
.\Stock.Tests\bin\Release\net10.0-windows10.0.19041.0\Stock.Tests.exe --current-daily-checks
.\Stock.Tests\bin\Release\net10.0-windows10.0.19041.0\Stock.Tests.exe artifacts\resume-regression
.\Stock.Tests\bin\Release\net10.0-windows10.0.19041.0\Stock.Tests.exe --oversold-ui artifacts\resume-ui
dotnet publish Stock.App\Stock.App.csproj -p:PublishProfile=Win64 -o artifacts\resume-publish
```
Tests는 자체 콘솔 검사이므로 dotnet test 성공만으로 검사 완료라 하지 않는다. 각 종료코드/로그 확인. publish 결과 Stock.exe를 빈 별도 폴더에 복사한 뒤 `--smoke-test <새 결과폴더 절대경로>`로 실행해 result.json Success, NoBrokerCalls, native 렌더를 확인한다. WPF GUI exe 종료까지 기다려 종료코드도 확인한다. `--public-smoke`는 기본 fixture 검사와 다르므로 혼동하지 않는다. 검증 전 루트 EXE 덮어쓰기 금지.

## 유지할 경계 / 남은 큰 작업
- 현재 노출 전략은 국내 과매도·낙주 반등 하나. 기존 전략 UI를 되살리지 않는다. 숫자 정수부 및 소수점 바로 앞은 일반 삽입, 소수점 뒤만 두자리 덮어쓰기. 사용자 지정 한국어 라벨/25행/최소14 유지.
- 임의 틱/분/시간 집계의 실제 원본 adapter, 실패 추가진입 감사, 과거 screening run 선택 UI 등은 TASKS C 후속 참조.
- 실계좌 자동매매/다중매매 신규 연결은 기존 자동 승인 검토 거부로 [!] 상태. CanOpenMulti=false, OversoldLiveStrategy는 미연결 초안/FakeBroker 검사. 승인 거부는 계정 변경으로 해제되지 않는다. 재개 문서는 해당 변경이나 실제 주문의 승인을 뜻하지 않는다. 구체적 구현 범위 승인이 필요하면 사용자에게 확인하며 실제돈 주문은 별도로 실행하지 않는다.

## 새 채팅에 붙일 요청
이 프로젝트의 AGENTS.md, TASKS.md 맨 위 현재 목록, HANDOFF.md를 읽고 10/06 현재 일봉 스크리닝·장중 갱신 작업을 이어서 완성해줘. 먼저 HANDOFF에 기록된 테스트 컴파일 오류를 해결하고 빌드/대상 및 전체 회귀/WPF/단독 EXE를 검증해줘. 검증된 경우에만 루트 Stock.exe를 갱신하고 TASKS/VALIDATION을 수정해줘. 실제 주문·인증키 변경·사용자 데이터 삭제와 [!] 자동매매 연결은 임의로 실행하지 마.


