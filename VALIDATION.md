# 검증 기록 — 2026-09-28

## 2026-10-24 전체 지정봉 분·시간 선택과 단위별 범위

- 스크리닝과 전략 백테스트의 지정봉 단위가 같은 `분봉/시간봉` 목록을 사용하고, 숫자 목록이 분봉 1~59·시간봉 1~12인지 확인했다. 분봉 59에서 시간봉으로 바꾸면 12로 보정되며 0, 범위 초과, 문자열은 실행 전에 차단한다.
- 가짜 온라인 국내 계좌 UI에서 5분봉 78개 백테스트와 1시간봉 6개 백테스트가 같은 `OversoldBacktestService`를 사용하고 결과/차트의 `BarSpecification`을 각각 보존하는지 확인했다.
- 실시간 엔진에서 KRX 장 시작 기준 1분봉과 1시간봉이 정확한 경계에서 닫히고, 현재 봉이 아니라 다음 실제 체결에서만 진입 신호가 준비되는지 확인했다. 다중전략 가짜 피드의 분봉 신호→주문 경로와 실시간 모의 체결→차트 marker 경로도 통과했으며 실제 브로커 제출은 0건이다.
- 1분봉 20거래일 7,800개 부하 검사는 1,639ms, 총 할당338.1MB, 결과 보유 증가22.6MB였다. Release 빌드 경고0/오류0, `--expansion-checks` 136개, `--ui-smoke` 401개, 전체 회귀1077개를 통과했다.
- 게시본 `artifacts/timebar-final-20261024/Stock.exe`, 루트 `Stock.exe`, EXE 단독 폴더 `artifacts/timebar-clean-exe-20261024-b29da7cd/Stock.exe`는 80,759,684 bytes, SHA256 `B29DA7CDD67E132182A8904268E5FE022058D0ADEFF35363791ABCA1E1B8EA81`로 일치한다. 새 폴더로 분리한 루트 smoke와 단독 EXE smoke 모두 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true다. 실제 키·네트워크·계좌 주문·사용자 운영 DB는 사용하지 않았다.

## 2026-10-24 전략 백테스트 지정 분봉·부하 검증

- 종목/차트 전략 백테스트에 일봉과 온라인 지정 분봉(1~390분)을 추가했다. 공식 범위 분봉 조회 결과를 공통 `OversoldBacktestService`에 전달하며 결과, 기록, 차트 복원에 정확한 `BarSpecification`을 보존한다.
- 5분봉 합성 데이터에서 완성 봉 종가의 신호가 다음 가용 5분봉 시가에만 체결되는지, 마지막 봉 신호가 미체결로 남는지, 미래 접미 데이터를 추가해도 과거 결과가 바뀌지 않는지 확인했다. 분 이외 주기 거부와 취소 토큰도 검증했다.
- 가짜 온라인 국내 브로커 UI 회귀에서 2026-10-20 하루의 5분봉 78개를 불러와 백테스트하고 결과 주기 `5분`, 차트 주기 `5분`, 차트 봉 78개, 다음 완성봉 시가 체결 안내를 확인했다. 실제 브로커 네트워크는 호출하지 않았다.
- 1분봉 20 KRX 거래일 7,800개를 기본 장기 이동평균과 Pivot/근사 매물대 Provider로 실행한 부하 검사는 1,646ms, 총 할당339.8MB, 결과 보유 증가24.4MB였다. 제한 20초/보유 증가256MB 이내이며 엔진은 `Task.Run`과 취소 토큰을 사용해 UI 스레드를 막지 않는다. 실제 API 지연은 이 합성 부하 검사에 포함되지 않는다.
- Release 솔루션 빌드 경고0/오류0. `--oversold-checks` 75개, `--ui-smoke` 396개, 전체 회귀1067개를 통과했다.
- 게시본 `artifacts/minute-backtest-final-20261024/Stock.exe`, 루트 `Stock.exe`, EXE 단독 폴더 `artifacts/minute-backtest-clean-exe-20261024/Stock.exe`는 80,758,816 bytes, SHA256 `52A330B3EB0E8CD5565097D8CDE4FA679A8D0B14525927681EC4212872789FDF`로 일치한다. 루트·단독 EXE smoke 결과는 Success/NoBrokerCalls=true이며 실제 주문은 0건이다.

## 2026-10-23 스크리닝 기간 경계·국내 시장 선택·1시간봉 조회 최적화

- 스크리닝 상세에 전달되는 봉과 저장 체결을 한국시간 기준 `시작일 00:00 <= 시각 < 종료일 다음날 00:00`으로 함께 잘랐다. 기간 밖 체결이 첫/마지막 봉으로 보정되어 나타나지 않는 회귀를 통과했다.
- 공개 시장 목록 가짜 응답에서 코스피 선택은 KOSPI 상위 N 한 번, 코스닥 선택은 KOSDAQ 상위 N 한 번, 코스피+코스닥 선택은 각 시장 상위 N을 독립 요청하는지 확인했다. 스크리닝 선택 목록은 세 국내 항목만 제공한다.
- 키움 공식 형식 가짜 응답에서 1시간봉 요청이 `ka10080`의 `tic_scope=60`을 사용하고 1분봉 전체를 내려받지 않는지 확인했다. 직접 지원하지 않는 임의 간격만 기존 1분봉 집계를 사용한다.
- 같은 종목·봉·기간·브로커의 지정봉은 2분 동안 최대 128개 범위 캐시에서 재사용한다. 종목 목록의 이미 확보된 업종을 사용해 별도 상세 조회도 제거했다. 완료 상태에는 종목당 평균 `자료조회` 초와 `전략계산` ms가 따로 표시된다.
- 키움 REST 경로에는 안전한 직렬 요청과 최소 650ms 간격이 있다. 40종목이 각 한 요청만 사용해도 이 간격만 약 26초이므로 네트워크/API 대기가 주 병목이다. Rust/C++로 전략 계산을 옮겨도 이 대기시간은 줄지 않아 네이티브 DLL을 추가하지 않았다.
- Release 솔루션 빌드 경고0/오류0. 대상 `--oversold-ui` 219개, `--ui-smoke` 394개, 최종 전체 회귀 1060개 통과. 숫자는 서로 겹치는 검사 묶음이라 합산하지 않는다. 가짜 브로커·임시 DB만 사용했고 실제 브로커 네트워크 호출과 계좌 거래는 0건이다.
- 게시본과 루트 `Stock.exe` smoke 종료0. Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 두 파일은 80,651,838 bytes, SHA256 `82D8B600B6A49E7D8E6986A5587816481AFDBEAF6A2AD135D4B7F9F4F5D57393`로 일치한다. 화면은 `artifacts/screening-market-performance-ui-20261023/screening-workspace.png`, 게시본은 `artifacts/screening-market-performance-final-20261023/Stock.exe`, 루트 결과는 `artifacts/screening-market-performance-root-smoke-20261023/result.json`이다.

## 2026-10-23 스크리닝 지정봉 전체보기·우클릭 탐색 전환

- 스크리닝 snapshot의 `1시` 지정봉이 상세 창의 첫 선택이며 `ChartPeriods.Intraday`로 판별되어 시간 축을 사용하고, 실제 체결시각이 속한 시간봉에 매매 표식이 연결되는지 확인했다.
- 4,000개 합성 시간봉에서 첫 화면이 4,000개 전체를 표시하고 휠·드래그를 무시하는지 확인했다. 포인터 봉 우클릭 후 500개로 집중되고 입력이 활성화되며, 반복 축소해도 500개를 넘지 않고, 다시 우클릭하면 4,000개 전체·offset 0·잠금 상태로 복귀했다.
- 스크리닝 목록과 결과 창 `종목별 조건·체결`이 같은 immutable run을 상세 창 콜백에 전달하고, 공통 `CandleChart`의 번호 있는 매수·매도 표식과 거래 상세를 사용하는지 확인했다.
- WPF 렌더 `artifacts/screening-review-ui-final-20261023/screening-chart-focused-hour.png`에서 `1시` 선택, 시간 축, `#1 ▲ 매수 1건`, 집중 상태 안내를 확인했다.
- Release 솔루션 빌드 경고0/오류0, 전체 회귀 1044개 통과. 전체 검사는 가짜 브로커·임시 DB만 사용했고 브로커 네트워크 호출과 계좌 거래는 0건이다.
- 게시본과 루트 smoke 종료0. 게시본 `artifacts/screening-review-final-20261023/Stock.exe`와 루트 `Stock.exe`는 80,649,488 bytes, SHA256 `EAA06D410CB8ABFE168FF69F07F84FB8292D31BB3B978BC3DBAAF5788D930B65`로 일치한다.

## 2026-10-22 시장별 상위 선택값·장기간 지정봉 이동 성능

- 새 ViewModel에서 시장별 선택 목록이 정확히 `10, 20, 50, 100`인지 확인했다. KOSPI/KOSDAQ 각각에 적용되는 기존 실행 방식은 유지했다.
- 1px보다 좁은 장기간 봉에서 드래그 이동량이 실제 화면 봉 폭에 비례하는 수학 검사를 통과했다.
- 1만 개 합성 1분봉 중 2천 개를 보이게 하고 5·20·60·120 이동평균과 일목균형표를 켠 뒤 12번 이동 렌더했다. 최적화 전 1,096ms, 최종 전체 회귀에서 787ms였고 지표 캐시 생성은 1회였다.
- 축소 화면의 OHLCV 열 묶음은 렌더에만 사용하고 `Bars` 원본 수와 순서, hover, 매매 표식 및 확대 데이터가 유지되는지 WPF 회귀로 확인했다.
- Release 빌드 경고0/오류0. `--oversold-ui` 211개, 전체 1036개 통과. 전체 검사는 가짜 브로커·임시 DB만 사용했고 브로커 네트워크 호출과 계좌 거래는 0건이다.
- 게시본 및 루트 smoke 종료0. 게시본 `artifacts/screening-pan-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,648,597 bytes, SHA256 `DEE1F31D41637F742A65CBCAE52F546E2A0D133209DE785AFFDF6738CDD4D350`으로 일치한다.

## 2026-10-22 스크리닝 온라인 지정봉·전체 기간 상세 차트

- 오프라인에서 지정봉 UI가 숨겨지고 저장/코드상의 틱·분·시 선택도 일봉 평가로 강제되는지 확인했다. 온라인에서는 선택 목록이 `틱봉`, `분봉`, `시간봉`으로 나타나는지 확인했다.
- 키움 공식 형식의 `ka10080` 가짜 연속응답을 2026-10-20, 2026-10-10, 2026-10-01 세 페이지로 구성했다. 첫 요청 `base_dt=20261020`, 연속 플래그 유지, 요청 시작일이 포함된 세 번째 페이지에서 즉시 중단되는지 확인했다.
- 2026-10-01~2026-10-20 평일 1분봉을 1시간봉으로 집계해 완성봉 84개가 정확한 첫날/마지막날과 함께 상세 차트에 전달되는지 확인했다. 첫 화면은 84개 전체이며 확대 후 `VisibleBarCount`는 감소해도 원본 `Bars.Count=84`가 유지된다.
- WPF 화면 `artifacts/screening-range-ui-final-20261022/screening-hour-full-range.png`에서 기간 전체 봉 렌더를 확인했다.
- Release 솔루션 빌드 경고0/오류0. 전체 회귀 1033개, 최종 WPF 379개 통과. 전체 검사는 가짜 브로커·임시 DB만 사용했고 브로커 네트워크 호출과 계좌 거래는 0건이다.
- 게시본과 루트 smoke는 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 게시본 `artifacts/screening-range-final-20261022/Stock.exe`와 루트 `Stock.exe`는 80,645,331 bytes, SHA256 `04FB712C5372CA2414CD0F983C6D914639C6DA7C9A450270B7C4C31DB73C6874`로 일치한다.

## 2026-10-22 거래번호·포인터 기준 확대·스크리닝 지정봉 상세

- 백테스트 거래 두 건의 실행을 `[1, 1, 2]` 번호로 변환해 한 거래의 분할 체결이 같은 번호를 유지하는지 확인했다. 실제 WPF 축소 렌더 `artifacts/numbered-marker-ui-final-20261022/zoomed-out-trade-counts.png`에서 `#2 ▼ 매도 1건`, `#3,#4 ▲ 매수 2건` 형태를 확인했다.
- 휠 전후 포인터 아래 봉의 화면상 비율이 유지되는 수학 회귀와 WPF 렌더를 통과했다. 오른쪽 끝에 고정하지 않고 마우스 X 위치를 기준으로 offset을 계산한다.
- 온라인 1시간 스크리닝 fixture에서 상세 첫 시리즈가 `1시`이며 모든 봉이 snapshot 기간 2026-10-20 안에 있는지 확인했다. 틱 snapshot도 지정 N틱봉이 첫 시리즈이고 시작일~종료일 밖 봉이 없는지 확인했다.
- 키움 분봉 응답에 OHLC가 0인 현재 진행 행과 정상 완성 행을 함께 넣었을 때 잘못된 행만 제외하고 정상 행을 반환하는 검사를 추가했다. 원본 틱은 체결가로 O/H/L/C가 동일하다.
- Release 솔루션 빌드 경고0/오류0. `--oversold-ui` 207개, 전체 회귀 1014개 통과. 전체 검사는 가짜 브로커·임시 DB만 사용했고 브로커 네트워크 호출과 계좌 거래는 0건이다.
- 게시본 smoke와 루트 `Stock.exe` smoke는 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다. 게시본 `artifacts/number-zoom-screening-final-20261022/Stock.exe`와 루트는 80,712,184 bytes, SHA256 `76602D9E0F20AE1A52E7DC01A5C7045387CC86636D47CED0E47085469F477C6B`로 일치한다. 루트 smoke 결과는 `artifacts/number-zoom-screening-root-smoke-20261022/result.json`이다.

## 2026-10-22 축소 매매 표식 집계·모바일식 차트 이동

- 최대 축소 WPF 봉 차트에서 가까운 체결이 매수·매도별 `N건` 배지로 렌더되는 화면을 `artifacts/marker-cluster-ui-final-20261022/zoomed-out-trade-counts.png`에 저장했다. 화면상 약 92px 구간을 사용하며 원본 체결은 변경하지 않는다.
- 봉 차트 드래그 방향, 어느 위치에서든 포인터 캡처, 확대 상태의 초기 이동 민감도, 클릭 4px 판정을 회귀 검사했다. 오른쪽 드래그는 과거, 왼쪽 드래그는 이후 시점을 표시한다.
- ScottPlot 원본 틱 차트도 보이는 축 범위의 가까운 표식을 같은 규칙으로 집계하며 Pan/Zoom 종료 후 재계산한다. 같은 집계에 속한 원본 체결은 선택 목록에 남는다.
- Release 빌드 경고 0 / 오류 0. `--oversold-ui` 205개, `--raw-tick-checks` 30개, 전체 1004개 통과. 전체 검사는 가짜 브로커·임시 DB만 사용했고 계좌 거래는 0건이다.
- 게시본과 교체된 루트 `Stock.exe` smoke 종료0. 양쪽 모두 80,710,099 bytes, SHA256 `6B2E25C99061FF33D5ECDF7352D4A7C761F2C842E16206161F695CA463EA13D3`.

## 2026-10-22 백테스트 매매 표식 실제 화면 검증

- 실제 루트 WPF에서 삼성전자 `백테스트 검증 1 · 1일 보유`, 2025-10-22~2025-11-30을 실행했다. 결과는 완결 5회, 승률 60.00%, 최종 평가자산 10,050,300원, Profit Factor 2.05였다.
- `백테스트 매매 내역 표시`를 OFF해 표식이 사라진 뒤 ON해 테스트 구간과 `매수`/`매도` 표식이 복원되는 것을 직접 확인했다. 화면: `artifacts/backtest-marker-visible-20261022/backtest-trade-markers-verified.png`.
- 1초 현재시세 갱신이 테스트 종료일 뒤 일봉을 다시 붙이지 않으며, OFF→ON 재선택도 선택한 Oversold 결과 구간으로 복귀함을 WPF 회귀로 고정했다.
- Release 빌드 경고 0 / 오류 0. `--oversold-ui` 202개, 전체 1001개 통과. 브로커 네트워크 주문·계좌 거래 0건.
- 루트 단일 EXE smoke 종료0. 게시본 `artifacts/backtest-marker-toggle-final-20261022/Stock.exe`와 루트 `Stock.exe`: 190,775,540 bytes, SHA256 `962609E9B504ECEE917DB98CC528152FD0D9365609C79CE039CA9BFDF3BDB619`.

## 2026-10-21 스크리닝 명칭·온라인 1시간봉·백테스트 표식·전면 검증

- Release 솔루션 빌드: 경고 0, 오류 0.
- 전체 자체 회귀: 998개 통과. 브로커 네트워크 주문·계좌 거래 0건.
- 실제 WPF/저장 기본 키움 계정 읽기 전용 확인: 삼성전자, 1시간 지정봉, 2026-10-14~2026-10-21, 관심종목 1개를 1.45초에 완료했다. 상태는 `조회 실패/봉 없음 0개`였으며 Crash/RSI/거래량/Support 기술 조건이 평가됐다. 과거 종목 분류·관리/정지·Fundamental Risk가 없다는 별도 `자료부족`은 사실대로 유지됐다.
- 온라인 Minute/Hour 회귀: 공식 1분봉 정규화, 실제 KRX 세션 파생, N분/N시간 집계, 거래대금 근사 고지, 과거 상태가 없어도 기술 조건 평가, 전체 행 결손만 `조회 실패/봉 없음` 집계하는 동작을 확인했다.
- 백테스트 UI 회귀: 성공 후 표식 체크 자동 활성화, 테스트 기간으로 차트 범위 교체, 표식/같은 봉 단일 클릭 상세 팝업을 확인했다.
- 실시간 모의: 실제 WPF에서 지원 조건 실패가 별도 `다량 전략 실행 안내` 팝업으로 나타나는 것을 직접 확인했다. 장 종료 뒤 실제 시장 체결은 생성할 수 없었다. FakeFeed/PaperBroker 검사는 틱·분 신호→가상 체결→차트 표식→중지를 끝까지 통과했고 브로커 제출은 0건이었다.
- 게시본 smoke `artifacts/screening-hour-final-smoke-20261021/result.json`과 루트 smoke `artifacts/screening-hour-root-smoke-20261021/result.json`: 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 게시본 `artifacts/screening-hour-final-20261021/Stock.exe`와 루트 `Stock.exe`: 190,775,540 bytes, SHA256 `C79DC0A55AC4C0BF7A32B2E6D26D53F7FA8A3FFE8E7B8B0293C0A89A1F0A0FF4`. 이전 루트 백업: `artifacts/screening-hour-final-20261021/Stock-before-20261021-192312.exe`.
- 최종 루트 `Stock.exe`를 직접 실행해 `스크리닝` 탭 렌더를 확인하고 정상 종료했다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 2026-10-21 전일 종가 등락·저장 전략 동기화·다량 실행 안내 팝업

- Release 솔루션 빌드: 경고 0, 오류 0.
- `--oversold-ui`: 198개 통과. 과거/실시간에서 일·주·월봉을 전환해도 헤더 등락이 전일 일봉 종가 기준으로 유지됨을 확인했다.
- `--multi-ui`: 25개 통과. 전략 설정과 다량 실행의 등록 전략 목록 일치, 선택 전략별 저장 Config 사용, 별도 안내 팝업 이벤트, 기존 실시간 모의 무주문과 실계좌 보호 경계를 확인했다.
- 전체 오프라인 회귀: 992개 통과. 브로커 네트워크 호출·계좌 거래 0건.
- 게시본 독립 smoke `artifacts/header-strategy-popup-published-smoke-20261021/result.json`과 루트 smoke `artifacts/header-strategy-popup-root-smoke-20261021/result.json`: 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 게시본 `artifacts/header-strategy-popup-final-20261021/Stock.exe`와 루트 `Stock.exe`: 80,702,235 bytes, SHA256 `3231B86179C4BD5C79FD9DC1BFDA6E942EC402EC016F88305BFCCCFED8F4B69F`. 이전 루트 백업: `artifacts/Stock-before-header-strategy-popup-33F0AC33.exe`.
- 실제 키·실계좌 주문·사용자 운영 DB는 사용하지 않았다. 실제 장중 증권사 데이터의 가용성과 수신 빈도는 사용자 계정 권한과 제공처 응답에 따른다.

## 2026-10-21 1초 차트·기본계좌 자동연결·온라인 백테스트·가상계좌 주문

- Release 솔루션 빌드: 경고 0, 오류 0.
- `--oversold-ui`: 194개 통과. 공통 1초 화면 주기, 현재 일봉과 주봉 마지막 봉 갱신, 이력 재다운로드 없음, 온라인 백테스트와 기본계좌 표시를 확인했다.
- `--multi-ui`: 24개 통과. 가상 탭 진입은 모의+실시간으로 고정되고 실제 주문 API 호출 없이 가상 체결을 기록하며, 실제 탭 경로·전체 중지·팝업 재열기를 함께 확인했다.
- 전체 오프라인 회귀: 987개 통과. 브로커 네트워크 호출·계좌 거래 0건.
- 게시본 독립 smoke와 루트 `Stock.exe` smoke: 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 게시본 `artifacts/live-account-final-20261021/Stock.exe`와 루트 `Stock.exe`: 80,700,174 bytes, SHA256 `33F0AC33EA0DA515C57B46AE56CA4C8D37564DBB63AD231AE1A1636655B35947`. 이전 루트 백업: `artifacts/Stock-before-live-account-3AAA0573.exe`.
- 실제 키·실계좌 주문·사용자 운영 DB는 사용하지 않았다. 실제 기본계좌 인증과 장중 시세 cadence는 사용자가 연결한 증권사 환경의 응답 빈도·권한에 따른다.

## 2026-10-21 백테스트 체결 표식·분봉 응답·긴급중지 취소

- Release 솔루션 빌드: 경고 0, 오류 0.
- WPF UI 회귀: 349개 통과. 백테스트 표식 체크 생성/독립 토글/설정 복원/기존 모의 표식 병합과 로딩 중 긴급중지 활성·취소·오버레이 해제를 확인했다.
- 전체 오프라인 회귀: 980개 통과. 브로커 네트워크 호출·계좌 거래 0건.
- 키움 공식 스키마 fixture: `ka10080`이 계속 연속 응답이어도 차트 요청은 정확히 3페이지에서 부분 성공하고 기존 잔고/주문 완전 조회 정책은 보존됨을 확인했다.
- 게시본 독립 smoke 및 루트 `Stock.exe` smoke: 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 최종 게시본/루트 `Stock.exe`: 80,698,922 bytes, SHA256 `3AAA05739D536323EF791407A20219F7A0A90C971E433771B2CF37ECF3CD90EA`. 이전 루트 백업: `artifacts/Stock-before-backtest-minute-emergency-D2990AD4.exe`.
- 실제 키·실계좌 주문·사용자 운영 DB는 사용하지 않았다. 실제 계정의 장중 분봉 권한·응답은 사용자가 키움을 연결한 환경에서 확인해야 한다.

## 2026-10-20 차트 범위·키움 틱/분봉·Git 보호

- Release 솔루션 및 테스트 재빌드: 경고 0, 오류 0.
- 전체 자체 회귀: 962개 통과. 브로커 네트워크 호출·계좌 거래 0건.
- 확장 WPF UI: 345개 통과. 7개 빠른 기간의 선택색, 일·주·월봉 × 7개 기간=21개 오른쪽 끝 렌더, API 미연결 비활성화, 키움 호환 계좌 활성화, 틱 메인 차트 직접 표시, 1분→5분 집계, 모의 marker 체크를 확인했다.
- 키움 공식 스키마 fixture: `ka10080` 부호 가격/시간순 분봉과 `ka10079` 같은 시각 원본 틱 순서를 확인했다. 실제 키·실계좌·장중 API 호출은 수행하지 않았다.
- 임시 Git 저장소 검사: `Stock.exe`, artifacts, publish, broker-keys, DB, Local 설정은 ignore; `docs/kiwoom-api-spec.json`은 추적 가능. 비밀 문자열 검사는 테스트 가짜 키 파일 한 건 외에 없음.
- 게시본/루트 smoke: 종료 0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 게시본/루트 `Stock.exe`: 190,689,012 bytes, SHA256 `D2990AD476CC5CE2DE5B2B67287A0E8BD26C1BA780915A9CD84096A1EC0932A9`. 이전 루트 백업: `artifacts/Stock-before-chart-api-76B2AB54.exe`.

## 2026-10-14 다중 전략 실시간 모의매매

- Release 빌드: 경고 0, 오류 0.
- `--multi-mock`: 5개 통과. 기존 과거 다종목 모의 백테스트 보존.
- `--expansion-checks`: 126개 통과. 실시간 모의 신호는 가상 미체결만 만들고 다음 실제 이벤트에서 체결되며, 정확한 체결시각·가격 하나가 틱·분·일·주봉에 동일하게 매핑됨을 확인.
- `--current-daily-checks`: 99개 통과. 기존 1초 화면 갱신/공개 제공처 cadence 경계 보존.
- `--multi-ui`: 23개 통과. 실계좌 FakeBroker 경로 1건 이후 실시간 모의를 시작해도 브로커 요청 수가 증가하지 않고, 가상 체결 표식이 온라인 선택 종목 차트에 로드되며 전체 중지 시 브로커 청산이 없음을 확인.
- `--oversold-ui`: 193개 통과. 기존 종목·차트, 스크리닝, 전략 UI 회귀 없음.
- 전체 회귀: 915개 통과, 출력상 broker network call/account transaction 없음. 실제 키·실계좌 주문·사용자 DB는 사용하지 않음.
- 게시본 독립 smoke와 교체된 루트 `Stock.exe` smoke: 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true.
- 최종 안내 문구 반영 뒤 Release 빌드와 `--multi-ui` 23개를 재통과했다.
- 게시본/루트: 80,693,800 bytes, SHA256 `76B2AB5464FF5457A7D909A0A3A159F6233F8B6528490565498B4A8903AF4B37`. 이전 루트는 `artifacts/Stock-before-realtime-paper-49FD7DDA.exe`.

## 2026-10-11 전략 진단 프리셋 검증

- 신규 DB와 기존 전략 목록에서 `전략 진단`이 일반 프리셋으로 추가되고 동명 저장값을 덮어쓰지 않는 구조를 확인했다. 설정은 RSI/거래량/Pivot/근사 매물대/반등 OFF, MA5, 최소 지지 1개다.
- Release 빌드 경고 0 / 오류 0. 대상 WPF 184개, 전체 오프라인 회귀 891개 통과. 전체 검사는 증권사 네트워크 호출·계좌 거래 0건.
- 단일 게시본과 루트 `Stock.exe`: 80,670,957 bytes / SHA256 `8F97D6F75474DBBD02EAEB8C7EB6C75233BDC6C83C4091F827016263D3C2FC5B`.
- 루트 smoke `artifacts/diagnostic-strategy-root-smoke-final-20261011/result.json`: Success/NoBrokerCalls/RawNativeRendered/ScreeningSnapshotVerified=true. 고정 TEST_FIXTURE만 사용했다.

## 2026-10-11 전략 설정 추가·이름 변경·복구·JSON 불러오기 검증

- `추가`는 현재 58개 입력을 검증해 새 이름으로 저장하고, `이름 변경`은 새 레코드 저장 후 이전 이름 state를 제거한다. 이름 충돌·빈 이름·80자 초과를 차단한다.
- `복구`가 미저장 입력을 선택 전략의 마지막 저장값으로 되돌리고, 전략 선택과 재시작이 마지막 적용값을 유지함을 SQLite/WPF 검사로 확인했다.
- `불러오기`는 다운로드 JSON의 schemaVersion=1, strategyId=`krx-oversold-rebound`, 전체 Config Validation을 통과한 경우에만 고유 이름으로 등록한다. 계좌키·거래기록·과거 시점 자료는 포함하거나 변경하지 않는다.
- Release 빌드 경고 0 / 오류 0. 대상 WPF 182개, 전체 자체 콘솔 회귀 889개 통과. 증권사 네트워크 및 실제 계좌 거래 0건.
- 게시본 `artifacts/strategy-library-final-20261011/Stock.exe`, EXE-only 및 루트 smoke 모두 종료0. 두 result.json의 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true.
- 최종 루트 Stock.exe: 80,670,041 bytes / SHA256 `5ACA27C1CE116C7D4D11A40912D578D1A6E889CF86DE98F40E0E93970E9FDFF0`. 이전 EXE: `artifacts/Stock-before-strategy-library-4D6816F6.exe`.

## 2026-10-11 토스증권 참고 UI 디자인 검증

- 중앙 Styles/TossInspired.xaml로 흰 Surface, 옅은 회색 Canvas, 파란 주요 동작, 조용한 보조 문구와 둥근 입력 컨트롤을 적용했다. 화면별 비즈니스 바인딩·명령·주문 서비스는 변경하지 않았다.
- Release 빌드 경고 0 / 오류 0. 낙주·전략 설정·스크리닝 WPF 171개, 다량 전략 WPF 15개, 전체 오프라인 874개 통과. 전체 로그: artifacts/toss-ui-full-verified-20261011.log.
- 1540×1040과 최소 1160×760 렌더에서 중앙 차트 높이 각각 514px/234px. 기존 오른쪽 최소14, 숫자 소수 입력, 58개 파라미터, 25행, 마지막 봉 hover, 스크리닝 이동과 1초 표시 검사 통과. 화면 자료: artifacts/toss-ui-preview-20261011-e, artifacts/toss-multi-ui-20261011.
- EXE-only 검사 artifacts/toss-ui-clean-result-20261011/result.json: Success, NoBrokerCalls, RawNativeRendered, FixedEditorVerified, IntegerInsertionVerified, ScreeningSnapshotVerified true. 고정 TEST_FIXTURE 전용이며 실제 증권사 통신·주문 없음.
- 최종 게시본과 루트 Stock.exe: 80,657,800 bytes / SHA256 F28481D698A9C75DB5148B7BE947ECDC9BA90691B5D4AA000018086EC4B1132B. 이전 실행파일은 artifacts/Stock-before-toss-ui-78954CC5.exe.
- 실제 모니터 물리 입력, 화면 배율/DPI 이동, 실제 키움 장중 체결은 검증하지 않았다. 사용자 키·운영 DB·보유 포지션을 변경하지 않았다.

## 2026-10-11 선택 계좌 낙주 실시간 경로 검증

- `dotnet build Stock.sln -c Release --no-restore`: 경고 0, 오류 0. `Stock.Tests` 자체 콘솔 전체 **874개 통과**, 확장 검사 **117개**, 다량 팝업 WPF **15개** 통과. FakeBroker/FakeFeed만 사용했으며 증권사 네트워크·실제 주문 0건. 선택 계좌 한 종목 주문, 체결 전 무보유, 체결번호 대사, 중지 시 계좌 점유 해제, 누락 틱 발생 시 대기 신호 무효화를 확인했다.
- 단일 EXE `artifacts/oversold-live-final-20261011/Stock.exe` SHA256 `78954CC5F7286C10E8160D934212CF687F5B76228C33DE121043BF0B8CF5967B`. EXE만 복사한 `artifacts/oversold-live-final-smoke-20261011/Stock.exe`를 빈 `artifacts/oversold-live-final-result-20261011` 결과 폴더에서 종료까지 실행했다. `result.json`: Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified 모두 true.
- 처음 게시 시 공유 컴파일러의 중간 DLL 잠금으로 CS2012가 한 차례 발생했다. `UseSharedCompilation=false`로 다시 게시하여 경고/오류 없이 완료했다. 별도 smoke의 첫 시도는 동일 결과 DB에 두 GUI 실행이 겹쳐 실패했으며, 이후 새 빈 결과 폴더에서 단독 실행해 성공했다. 실패 산출물은 최종 검증본이 아니다.
- 루트 `Stock.exe`는 사용자 실행 프로세스 PID 20632가 잠근 이전 버전이다. 별도 검증 EXE만 갱신됐고 사용자 앱 종료 전 루트 교체는 하지 않았다. 실제 계좌 장중/API 체결 검증은 수행하지 않았다.

## 이전 2026-10-11 소스 검증 기록

- Release 솔루션 빌드: 경고 0, 오류 0. 전체 862개, 대상 WPF 171개, UI 전체 274개, Core 확장 115개, 종목별 다량 모의 5개, 다량 팝업 WPF 10개 통과. 전체 검사는 증권사 네트워크 호출·실계좌 거래 0건.
- 테스트 호스트가 WPF App.OnStartup으로 일반 창을 열던 문제를 ManualWindowStartup으로 분리했다. UI 전체 검사에서 현재 전략의 해외 제외 완료 메시지를 검증하고, 관심그룹 표시가 버튼을 아래로 밀지 않는지 확인한다.
- 별도 단일 EXE만 있는 폴더 실행 검사 종료 0: artifacts/screening-clean-result-20261011/result.json의 Success, NoBrokerCalls, RawNativeRendered, FixedEditorVerified, ScreeningSnapshotVerified 모두 true. 검증용 TEST_FIXTURE 자료이며 실제 계좌를 호출하지 않았다.
- 로그: artifacts/screening-work-full.log, artifacts/screening-work-ui-all.log. 루트 Stock.exe는 사용자 실행 프로세스 PID 20632가 잠그고 있어 아직 교체하지 않았다. 기존 루트 SHA256: 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4. 검증된 EXE: artifacts/screening-clean-20261011/Stock.exe.

## 이전 검증 기록 — 빌드와 테스트

- 최신 Release 솔루션 빌드 성공: 경고 0, 오류 0.
- 전체 오프라인 테스트 **256개 통과**: `artifacts/workspace-tests.txt`.
- 검증 범위: 기존 틱 전략·주문 보호·SQLite 회귀, 일봉 이동평균·일목 26봉 이동 및 준비 구간, 미래 가격 독립성, 다음 시가 체결, 수수료·현금·유동성 제한, 취소, 공개 응답 파싱, 실제 호스트 시장가 요청 형식(가짜 HTTP), 실제 주문 한도 오입력 차단, 두 모드 분리, 검색 결과/스크리닝에서 차트 이동, 5개 WPF 화면.
- 마지막 테스트 이후 변경은 ToolTip과 XML 설명 주석 추가뿐이며, 최종 publish 및 배포 UI 검사를 다시 통과했습니다.

## 공개 시세 통합 검사 (2026-09-27)

`artifacts/workspace-public/result.json`: 실제 네이버·Yahoo 공개 데이터 읽기만 수행. 삼성전자·애플을 한글로 검색해 일봉·업종·차트·백테스트 완료. 당시 설정으로 국내 55건, 미국 31건 가상 체결.

국내 20종목 검사: 조건 일치 10개, 조회 실패 2개. 미국 30종목 검사: 조건 일치 8개, 조회 실패 1개. 실패 종목은 결과에서 제외하며 합성 가격으로 대체하지 않습니다. 수치는 검사일과 제공 데이터에 따라 달라집니다. 일부 데이터 실패가 있으므로 전체 시장 완전 수집으로 해석하면 안 됩니다.

## 단독 EXE 검사

최종 `STOCK/Stock.exe` 및 EXE만 복사한 `artifacts/workspace-clean/Stock.exe`에서 고정된 검증 자료로 검색·다운로드 서비스 연동·SQLite·백테스트·모의 재생·스크리닝·5개 탭 렌더링·정상 종료(0)를 확인했습니다.

- `artifacts/workspace-root-final-verified/result.json`
- `artifacts/workspace-clean-verified/result.json`
- 각 폴더의 `domestic-chart.png`, `overseas-chart.png`, `domestic-screening.png`, `workspace-0.png` ~ `workspace-4.png`.

검증 전용 고정 자료는 TEST_FIXTURE로 표시하며 일반 실행에서는 사용하지 않습니다. 두 배포 검사는 증권사 호출을 하지 않습니다.

최종 EXE SHA-256: `D7B7B4E02C7BA77CD1F52A1EE19230967AF66CA77BB6B8419BCF88808D65BCD7`.

## 미검증 범위

실제 키움 인증키를 사용한 연결·실계좌 접수·취소·체결은 수행하지 않았습니다. 실계좌 경로는 가짜 HTTP 핸들러로 형식과 보호 동작을 검증했으며 실제 주문을 전송한 적이 없습니다. 다른 PC의 운영체제·화면 배율·백신, 장시간 운용 및 고빈도 운용도 별도 확인 대상입니다. 일봉 기반 가상 체결 결과는 실제 시장 수익성 검증이 아닙니다.

## 2026-09-28 기능 확장 최종 검증
- Release 빌드: 경고 0 / 오류 0. Win64 단일 EXE 게시 성공.
- 280개 회귀 검사 통과: 키 취소/인증 거부/서버 장애/검증 성공, 오프라인 키 설정 유지, 주·월 OHLCV, 틱 합성 방지, 이전 기록 복원, 시장 카드, 정수 표시와 수수료 정밀도, 악재 제외·박스 거래량·독립 지지 터치.
- 공개 실제 데이터 EXE 검사 성공: 국내/미국 검색·일봉·백테스트·스크리닝, 주/월 차트, 시장 현황. 국내 20개 중 3개, 미국 30개 중 1개 조회 실패는 결과에서 제외하고 화면에 표시했다. 합성 데이터로 대체하지 않았다.
- EXE 하나만 있는 별도 폴더 검사: 종료 코드 0, SQLite·6개 WPF 탭·백테스트·모의매매 정상.
- 증거: artifacts/workspace-feature-tests.txt, artifacts/feature-exe-public/result.json, artifacts/feature-clean-result/result.json 및 해당 폴더 PNG.
- 최종 Stock.exe SHA-256: DF3EEE317CC4C3C1459536AC11E76997647DBD4A87F3C92FE9E552D5067FCA2C
- 실제 계좌 키 인증·실전 웹소켓·실제 주문 체결은 실행하지 않았다. 인증 분기는 가짜 서버로 검증했다. 실제 키가 없으면 새로운 틱을 받을 수 없고, 저장된 실제 틱이나 공개 일봉을 사용한다.

## 2026-09-28 지수 카드·관심종목 최종 검증
- 빌드: 경고/오류 0, 295개 검사 통과.
- 새 검사: 기본 그룹명, 그룹 중복 등록 방지, 그룹별 독립 등록/해제, 이름 중복 방지·수정, 재실행 저장 복원, 관심 차트 열기, 실제 화면 그룹명 표시, 국내/해외 지수 전환·관측 추이·전일 대비 계산, 버튼 바로 오른쪽 결과 배치.
- 공개 데이터 EXE 검사 성공: 코스피·코스닥·다우·나스닥·S&P 500 미니 차트, 삼성전자·애플 관심 시세, 백테스트 결과. 스크리닝의 일부 제공처 실패는 기존처럼 제외·건수 표시.
- 단일 EXE만 있는 별도 폴더: 7개 화면 및 가상 테스트 정상, 종료 코드 0.
- 증거: artifacts/watch-index-tests.txt, artifacts/watch-index-public/result.json, artifacts/watch-index-clean-result/result.json 및 화면 PNG.
- Stock.exe SHA-256: CB71693B3B1FF7543F93D0538B5780FE0484B19E3C726B9FD2F9865331B42CE0
- 실제 계좌 키·주문은 사용하지 않았다.

## 2026-09-28 검색·계좌 선택·관심그룹 스크리닝
- Release 빌드 오류/경고 0, 356개 검사 통과. 신규: Enter/클릭 실제 WPF 이벤트, 주기 탭, 그룹 복수 선택·중복 제거·국가/분야 필터, DPAPI 암호화·변조 거부·증권사별 키·기본 계좌 복원, 연결 실패 시 미저장, 선택 계좌로만 주문·계좌 변경 시 이전 세션 폐기.
- KIS 실전 고정 호스트·TR·시장가·취소·누락 숫자 차단, 메리츠 form 인증·MAC·시장가·경고 미접수·연속 키 공백·응답 유실/미확인 코드 검사. 가짜 HTTP 전용, 실제 계좌 미사용.
- 새 EXE의 공개 데이터 검사 성공. 관심그룹 2개, 중복 1개 제외, 국내·미국 두 종목을 각각 1회 조회. 기존 상위 목록에서는 국내 3/20, 해외 1/30 제공처 조회 실패를 명시적으로 제외했다.
- EXE 하나만 있는 별도 폴더에서 내장 증권사 로고 6개, 7개 화면, SQLite, 백테스트·가상 매매, 관심그룹 스크리닝 실행 성공(종료 코드 0).
- 증거: artifacts/broker-workflow-tests.txt, artifacts/broker-public/result.json 및 PNG, artifacts/broker-clean-result/result.json.
- Stock.exe SHA256: 349E2FEA948A8D7955C2A56A6C4B8E4FA440E21472D759A40B791AA9F21C89A0 (66,328,496 bytes).
- 지원 범위: 키움 국내·미국 / KIS·메리츠 국내. 미래에셋·삼성·신한 실제 연결은 미완료이며 UI에서 안내한다. 실제 키 인증·주문 체결을 검증했다는 의미가 아니다.

## 2026-09-29 최종 UI·차트·전략 설정 검증
- Release 빌드 성공: 경고 0 / 오류 0. 전체 회귀 447개 통과: artifacts/full-revision-tests.txt.
- WPF AutomationPeer 버튼 클릭 172회 및 비활성 23회 확인: artifacts/button-click-audit.txt. 도움말 팝업, 콤보 선택, 첫 검색 자동조회, 같은 종목 재클릭, 동기 Busy 전환 방지, 우클릭 날짜, 그룹 가로 배치, 설정/체결 영속성을 검사했다. 실제 사용자 디스플레이의 모든 프레임 깜빡임을 측정한 것은 아니다.
- 세 전략 Standard/Advanced 동등 결과, 기존 Advanced=null 동작 보존, 새 모의 위험·세금·청산 강도, 토스 계좌 한정·응답 유실 재전송 방지·분봉 페이지 검사를 통과했다.
- 루트 EXE 공개 읽기 검사 성공(종료0): artifacts/revision-public/result.json. 2026-09-29 08:25 한국시간 조회에서 삼성전자 마지막 봉 2026-09-28 15:30, 애플 마지막 봉 2026-09-28 22:30 한국시간. 오늘 장 개장 전이라 9/28이 마지막이다. 국내/미국 실제 차트와 백테스트, 7탭, 지수 카드, 관심그룹 중복 제거를 확인했다.
- 공개 스크리닝 조회 실패: 국내 20개 중 3개 / 미국 30개 중 1개. 실패 종목을 제외하고 개수를 알리며 합성 자료로 대체하지 않았다. 관심종목 2개 조회는 실패0, 두 그룹 중복1 제외.
- EXE만 있는 폴더 실행 성공(종료0): artifacts/revision-clean-bin/Stock.exe 및 artifacts/revision-clean-result/result.json. 고정 검증 자료로 WPF/SQLite/백테스트/모의매매/탭을 확인. 일반 실행은 검증용 합성 자료를 사용하지 않는다.
- 두 배포 검사 NoBrokerCalls=true. 실제 인증키·계좌 주문을 사용하지 않았다. 토스는 국내/분봉 구현이며 해외·WebSocket 및 실제 계좌 인증/체결은 미검증 또는 미구현이다.
- 일반 Publish 중 기존 refint/Stock.dll 파일 잠금 CS2012 발생. 기존 프로세스 종료나 파일 삭제 없이 별도 --artifacts-path artifacts/revision-build 로 Publish 성공 후 루트 EXE를 갱신했다.
최종 Stock.exe: 66444742 bytes / SHA-256: FC3001E938540EE9E6AF66DB018EDD62830C39845CD948A076C4796FEA48DBC2.


## 2026-09-30 Pipeline / 정기매수 검증
- Release 전체 빌드: 경고 0 / 오류 0.
- 회귀 492개 통과: artifacts/pipeline-recurring-tests.txt.
- 수동/CSV/Mock 순위, 시점/미래 자료 차단, 4단계 필터, 틱·농부 진입/청산, Portfolio 배정·예산, Metadata·모드 변환, SQLite 복원 검사.
- 정기매수: 거래일 주/월 버킷, 시가/종가, 수수료, 정수 수량/현금 이월, 외부 납입을 제거한 MDD, 매수 마커·결과 카드·저장 기록.
- WPF 렌더: recurring-layout.png, pipeline-standard.png, pipeline-advanced.png.
- 첫 공개 smoke는 시장 필터 변경으로 선택 종목이 해제된 뒤 정기매수를 실행하는 검사 순서 문제로 실패. PublishedSmoke에서 종목 재선택을 추가했다. 일반 앱 계산 코드는 변경하지 않았다.
- 수정 후 최종 공개/단독 EXE 결과를 아래에 추가한다. 실제 계좌 인증/주문 없음.

최종 공개 데이터 검사: artifacts/pipeline-public-final/result.json, Success=true, 종료0. 국내 9/29 확정봉 / 미국 9/29 장중봉 포함·백테스트 제외. 정기매수 확정 435거래일. 전체 목록 일부 실패 국내3/20·미국1/30 제외 표시, 관심종목2/2 실패0.
최종 제목 여백/실제 가격의 작은 매수 점 조정 뒤 publish 및 EXE-only 검사: artifacts/pipeline-clean-final/result.json, Success=true, 종료0, 정기매수234거래일·필터4단계·실계좌호출없음. 마지막 변경은 계산/네트워크 경로를 변경하지 않은 렌더 배치이다.
루트 Stock.exe 66,502,570 bytes / SHA256 2DF034BE3ED93392E5F68701D59B24DB728A8710571AB27A6B93B5E455A6E380.
마지막 공개 자료 화면은 pipeline-public-final, 최종 렌더 배치 화면은 pipeline-clean-final 참고. 모든 검사 프로세스 종료.

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
외부 한계: 새 증권사 과거 개별체결 REST 백필 어댑터는 미구현. 기존 실제 Kiwoom WebSocket 수집 및 IMarketDataProvider Import 구조를 사용하고 저장 자료 없는 경우 임의 생성하지 않음. 실제 가시 창 물리 입력 지연은 미검증. 기존 브로커 규격/사용자 계좌 검증 [!]는 유지하며 임의 진행하지 않는다.## 2026-10-03 다중매매·차트 도구 완료
- 최종 전체 회귀 547개 통과: artifacts/multi-regression-final.log. Release 경고0 오류0: multi-build-final.log.
- 추가 검증: 선택 단일계좌/독립 스냅샷/실제 부분체결/중복대사/소유권/취소접수와확정/unknown무재시도/응답 전후 stop경합/외부보유/예약현금/계좌변경/일손실복원. 공식 개별체결 HTTP fixture. 실제 돈 주문 없음.
- WPF 1540x1040,1160x760 각3탭, chart stretch, 팝업 수명주기와 offline stop, 원래0.015 수수료 보존 검사. artifacts/multi-ui-final.
- publish 로그 multi-publish.log 경고/오류 없음. 별도 EXE만 있는 multi-clean-exe에서 smoke 성공, multi-clean-result/result.json Success=true/RawNativeRendered=true/NoBrokerCalls=true, native libSkiaSharp 추출 확인.
- Stock.sln 옆 Stock.exe 갱신: 80,507,209 bytes, SHA256 8C59340055E00DA9ACE839613956B8EBAC5DFEF9BD547C89408C73975EBBA93B.
- 문서: docs/ui/MultiTrade.md, docs/ui/ChartTools.md, docs/api/LiveBatchKiwoom.md, architecture. 실제 계좌/물리 마우스/다른 모니터 DPI 이동 검증은 수행하지 않음.
- 선행 다중매매/UI 작업 완료. 새 과매도 일봉 전략은 별도 진행하며 이 EXE에는 아직 포함하지 않음.

## 2026-10-03 국내 과매도·낙주 반등 일봉
- Release 최종: 0 warning / 0 error (artifacts/oversold-build-final.log).
- 621 checks PASS (기존547 + OversoldTests67 + OversoldUiTests7): artifacts/oversold-regression-final.log. 미래자료/시가체결/정지/부분익절/고정stop/재진입/비용/지표/저장과 UI 검증, 브로커 네트워크/실제 주문 없음.
- WPF 두 크기 1540×1040,1160×760 렌더, 새 설정/성과/거래 상세: artifacts/oversold-ui-final. 실제 물리 입력·모니터별 DPI 전환 미검증.
- 단일 publish: artifacts/oversold-publish.log, 경고/오류 없음. 실행파일만 artifacts/oversold-clean-exe에 복사하여 --smoke-test, 종료0, Success=true, OversoldTrades=2, RawNativeRendered=true, RawTicks=2000, NoBrokerCalls=true. artifacts/oversold-clean-result/result.json. 합성 검증 전용 자료 사용.
- native Skia DLL 추출 확인: artifacts/oversold-native/Stock. 단독검사와 publish/root EXE 해시 일치.
- 최종 Stock.exe 80,560,114 bytes / SHA256 190C9EE9A1364AFA1C3C820BDACD1A245DDAB42A6CBE91CC7391EFB18C70FDE3. AGENTS.md는 같은 폴더.
- 공개 과거 facts API/실계좌에서 새전략 검증 미수행. 새전략은 일봉백테스트 전용이며 실제다중매매 지원목록에 추가하지 않음. 필수 사실 자료 미지는 진입보류. 자세한 제한·성과식·교체모듈·33항목은 docs/oversold-implementation-report.md.

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

## 2026-10-03 전략 파라미터 사용범위 분류 — 최종 완료
- 58개 기존 항목을 중복/누락 없이 백테스트 전용7 / 공통 전략 조건51 / 온라인 투자 전용0으로 분류. 백테스트7: InitialCapital, CommissionPercent, TaxPercent, SlippagePercent, TouchPolicy, AnnualRiskFreePercent, AnnualTradingDays. 기존 필터/지지/진입/청산/기간 분야는 하위 그룹 유지.
- 중앙 OversoldParameterMetadata의 소속/설명 → ParameterCatalog → 같은 ParameterField를 사용범위 화면과 기존 저장 경로가 공유. Tooltip/상세 Popup에 사용범위와 실제 적용 경로 표시. Config 속성/기본값/전략 계산/validation/SQLite 키/프리셋 형식 무변경.
- 공통은 현재 실계좌 적용을 의미하지 않음. 새 전략은 일봉 백테스트와 마지막 하루 신규 진입 스크리닝만 연결. 온라인 그룹에는 전용 파라미터 없음·자동매매 미지원 및 기존 수동 주문 설정 위치를 표시. 다중매매 차단·오른쪽 최소14 유지.
- 스크리닝은 전체 설정 validation을 유지(초기현금≥예정투자금 포함). 한 거래일 빈 포지션 상태에서 Buy 신호만 검사하므로 체결/청산/성과 가정으로 후보를 선별하지 않음을 도움말과 실제 변화 비교 검사로 확인.
- 검증: 전체657검사 통과 artifacts/parameter-scope-regression.log. 새 사용범위20검사 포함. 최종 대상 UI63검사 통과 parameter-scope-ui-final.log(전체와 중복되어 합산하지 않음). 저장/재시작/프리셋/기본값/실패시 저장보존/다른 그룹 사이 validation/0.015표시0.02와 원값보존/도움말/58카드 가로맞춤/두 크기 스크롤 검사.
- Release 경고0 오류0: parameter-scope-build-final.log. publish: parameter-scope-publish.log. EXE만 있는 새 폴더 smoke 종료0, parameter-scope-clean-result/result.json Success=true, ParameterUsageScopes=7/51/0, OnlyOversold=true, LegacyCommandsBlocked=true, OversoldTrades=2, RecurringSessions=234, RawNativeRendered=true, RawTicks=2000, NoBrokerCalls=true. parameter-scope-native/Stock 하위 libSkiaSharp.dll 추출 확인.
- Stock.sln 옆 Stock.exe 교체, publish와 SHA256 일치: 80,571,237 bytes / EB2854DCEB2B11A4FBE644E23E59A2436315D61C54D99BA828357A37BE549FD6. AGENTS.md 옆 유지. artifacts/parameter-scope-release.json.
- 추가/수정 파일: Core/Research/Oversold/OversoldParameterMetadata.cs 신규, Core/Trading/ParameterMetadata.cs, App/OversoldWorkspace.cs·OversoldParameterPanel.xaml·ParameterHelpDialog.cs·PublishedSmoke.cs, Tests/ParameterScopeUiTests.cs 신규·Program.cs·UiTests.cs. docs/ui/ParameterUsageScopes.md 신규 및 StrategyPage/OversoldBacktest/architecture/VALIDATION/TASKS 갱신.
- 검사 자료는 TEST_FIXTURE. 실제 키/실자금 주문/중요 DB 삭제 없음. 물리 입력·모니터 DPI 이동 미검증. 기존 시점 자료 API 미지원 등 기능 한계는 그대로. 승인 범위 완료, 안전하게 이어서 진행할 다른 미완료 구현 없음. 위 중간 체크포인트는 이 최종 기록으로 대체.

## 2026-10-04 파라미터 라벨·명시 행·고정 소수 입력 — 최종 완료
- 사용자 지정51라벨을 OversoldConfig의 중앙 ParameterAttribute에 반영. 카드/Tooltip/상세 도움말 공통. Config 키/기본값/엔진 계산/저장 키는 유지.
- 기존 사용범위7/51/0 유지. OversoldParameterLayout에25개의 명시3칸 행(58항목+빈칸17) 정의. RSI·거래량·TP 등 연관항목 동행, 다른 분야가 빈칸을 채우지 않음. 입력칸180×32/Padding8,4 통일, 1160×760/1540×1040 가로맞춤·세로스크롤·오른쪽최소14 검사.
- FixedDecimalEdit/Input 도입. 숫자 자리 덮어쓰기/소수점·천단위쉼표 건너뛰기/선택치환/소수2자리끝차단/붙여넣기 검증/삭제/음수·음수0/빈값/IME/Home·End·방향키 검사. 정수 입력은 정수 및천단위표시. 비율목록은 원소별2자리, MA기간목록은 scalar 포맷 제외.
- ParameterField.Value 원값 / InputText 표시값 분리. .015는 .02 표시 및 정확값 Tooltip 안내, 무편집focus/blur/save에서 .015 유지. 실제 사용자 편집만2자리 저장. 소수3자리 이상 paste는 거부하고 기존값 유지. 목록가변횟수·합100·범위 및교차validation 유지.
- 전체730검사 통과: artifacts/fixed-editor-regression-final.log(기존657+새73). 최종대상UI136도 통과: fixed-editor-ui-final.log. 두 숫자는 중복 검사로 합산하지 않음. Release 경고0 오류0: fixed-editor-build-final.log. 실제 WPF routed 입력/붙여넣기/키 이벤트 및 렌더 사용; 물리 입력지연/DPI이동은 미검증.
- 첫 단독smoke의 탭 layout 대기 순서 오류는 PublishedSmoke에서 수정. 새 fixed-editor-clean-result-final/result.json: Success=true, FixedEditorVerified=true, ParameterUsageScopes=7/51/0, OnlyOversold=true, LegacyCommandsBlocked=true, OversoldTrades=2, RecurringSessions=234, RawNativeRendered=true, RawTicks=2000, NoBrokerCalls=true; 종료0. fixed-editor-native-final/Stock 하위 libSkiaSharp.dll 추출 확인.
- publish 로그 fixed-editor-publish.log. Stock.sln 옆 Stock.exe 갱신 및 publish 해시 일치: 80,581,152 bytes / SHA256 A7710CB6700AD3977419362A8E96DC265B7B51B19086240F3FDCC31AAAC286E2. AGENTS.md 옆 유지. artifacts/fixed-editor-release.json.
- 신규 파일: Core/Research/Oversold/OversoldParameterLayout.cs, App/FixedDecimalEdit.cs·FixedDecimalInput.cs, Tests/FixedEditorTests.cs·RequestedLabelTests.cs, docs/ui/FixedDecimalParameters.md. 관련 Metadata/Config라벨/Workspace/ParameterField/Panel/NumericDisplay/PublishedSmoke/기존UI검사/문서 수정.
- docs/ui/StrategyPage·ParameterUsageScopes·OversoldBacktest·ChartTools, architecture, TASKS/VALIDATION 갱신. 단독검사는 TEST_FIXTURE이며 실제계좌·실주문·실시세보증이 아님. 사용자DB삭제·키변경 없음. 기존 실계좌자동매매 미지원 및 과거시점자료 요구 유지.
- 승인 범위 완료. 안전하게 이어서 진행할 다른 미완료 구현 없음. 위 진행중/배포대기 체크포인트는 이 최종 기록으로 대체한다.

## 2026-10-05 숫자/계좌 키/낙주 스크리닝 확장 — 안전 범위
- 전체810검사 통과 artifacts/expansion-regression-verified.log. 신규 집계·동시시각순서·누락·미완성·임의단위·overflow/인과성, immutable snapshot/funnel/disabled, 판단 snapshot, key reset dummy, 접수≠체결/부분체결/중복·독립상태 fixture 포함.
- 최종 UI 열 너비·관측가격 표기 수정 후 대상156검사 통과 artifacts/expansion-ui-final.log. 전체와 중복되어 합산하지 않음. WPF 1,160/1,540 폭, 숫자/목록/원값보존/7·51·0/25행/최소14 회귀.
- 최신 Release artifacts/expansion-build-final.log: 경고0 오류0.
- 첫 단독EXE smoke 성공 artifacts/expansion-clean-result/result.json: IntegerInsertionVerified/ScreeningSnapshotVerified/FixedEditorVerified=true, OnlyOversold/LegacyCommandsBlocked=true, OversoldTrades=2, RecurringSessions=234, RawTicks=2000, RawNativeRendered=true, NoBrokerCalls=true. 최종 UI 변경 재publish/smoke는 아래 배포기록 참조.
- 자동 승인 검토가 낙주 전략을 실계좌 LiveStrategyBatch 주문경로에 연결하는 변경을 거부했다. 해당 명령은 실행되지 않음. 기존 자동매매 차단 유지. OversoldLiveStrategy는 미연결 순수계산/FakeBroker 단독 검증이며 실거래 통합/3stop/공유현금 검증 완료 아님.
- 실제 키/실제돈 주문/사용자 키 초기화/중요 DB 삭제 없음. 합성 TEST_FIXTURE와 임시 저장소 사용. 물리 입력지연·다중DPI 미검증. 실제 API 분봉의 정확 거래대금/확정구간 계약이 없는 경우 자료부족으로 명시 차단.

### 최종 단독 EXE 및 루트 배포
- artifacts/expansion-clean-result-final/result.json: 종료0, Success=true, IntegerInsertionVerified=true, ScreeningSnapshotVerified=true, FixedEditorVerified=true, OnlyOversold=true, LegacyCommandsBlocked=true, RawNativeRendered=true, NoBrokerCalls=true; 낙주 모형2거래/정기매수234세션/원본2,000틱 검사.
- 별도 새 추출 폴더 expansion-native-final/Stock/Pcb-rmJ6bUQK/libSkiaSharp.dll 11,439,160bytes. EXE만 복사한 폴더에서 실행.
- Stock.sln 옆 Stock.exe: 80,624,331 bytes, SHA256 406FEBF652280F0230CB75EAE42315AD96EA8C305A7E51F6E97E16BA81C7EB95. publish 일치, AGENTS.md 존재. expansion-release.json에 기록. 새 낙주 실계좌 자동주문은 활성화하지 않았다.
## 2026-10-10 현재 일봉 스크리닝 / 장중 표시 완료
- [x] 과거 JSON 없이 현재 일봉의 기술적 조건/반등 평가. 자료 미확인 후보와 자동주문 신호 구분, 과거백테스트 엄격검증 유지.
- [x] 1초 UI 반영·제공처 간격 준수·당일 OHLCV 및 관측시간·진행봉 분리·팬줌/종목변경 처리.
- [x] CurrentDailyUiTests 39행 컴파일 오류 및 null 경고 수정. 새 WPF 검사를 Program/UiTests에 연결; 바인딩을 제거하던 검사 코드는 SetCurrentValue로 보정. OnlyOversoldUiTests의 이전 JSON 강제탈락 기대값을 현재 후보/미확인/주문불가 검사로 변경.
- [x] Release 경고0 오류0: artifacts/resume-build.log. 대상95, WPF164, 전체846 검사 통과(서로 중복이므로 합산 금지). resume-target.log/resume-ui.log/resume-regression.log.
- [x] artifacts/current-daily-publish 배포. current-daily-clean-a4d0bb1754134b6688c385634487bb28/result/result.json Success/RawNativeRendered/NoBrokerCalls=true, 종료0. 루트 Stock.exe 교체 및 publish 해시 일치: 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4 . 이전EXE artifacts/Stock-before-current-daily-406FEBF6.exe 보관.
- 문서 docs/ui/CurrentDailyScreening.md 및 architecture/README/TASKS/HANDOFF/VALIDATION 갱신.
- 실제 장중 증권사 수신·물리마우스/DPI 검증은 미실행. 공개 시세는 응답 갱신간격(기본7초), 화면만1초. 실주문/키변경/사용자DB삭제 없음. 자동매매 [!] 및 C 후속은 여전히 남음.

## 2026-10-11 관심등록·스크리닝 상세 차트·다량 전략 편집
- 관심등록 후 현재 그룹 포함 여부와 파란 채움 별 렌더를 검사했다. 그룹 변경 시 빈 별로 돌아가고 별도 그룹 등록 후 다시 채워진다. 비버튼 ListBoxItem 커서는 Arrow로 검사했다.
- 종목 골라보기의 직접 입력 ComboBox/TextBox/DatePicker/RadioButton과 스크리닝 시작 버튼 ActualHeight가 모두 40px인지 WPF layout에서 검사했다.
- 상세 차트 자료가 메인 ActiveTab을 변경하지 않는지, 일봉 snapshot과 독립 Window가 생성되는지 검사했다. 검증 원본 3틱 fixture로 1틱봉 3개가 정확히 주기 선택에 추가되는 것도 검사했다. `screening-workspace.png`, `screening-chart-dialog.png`는 `artifacts/latest-ui-check-2`에 있다.
- 다량 전략 창에서 수동 추가 종목이 첫 전략을 자동 선택하는지, 종목별 Budget 및 TotalBudget/DailyLoss TextBox와 모의 CheckBox가 실행 전 활성인지 검사했다. 렌더는 `artifacts/latest-multi-check-2/multi-trade-dialog.png`다.
- Release 빌드 경고0/오류0. `--oversold-ui` 175개, `--multi-ui` 18개, 전체 881개 통과. 전체 회귀는 가짜 HTTP/FakeBroker/FakeFeed와 임시 DB만 사용했으며 출력상 `no broker network calls or account transactions`를 확인했다.
- 단일 EXE 게시 `artifacts/screening-popup-final-20261011/Stock.exe`. EXE-only 및 루트 EXE smoke 모두 종료0. `result.json`의 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 루트와 게시본 80,668,018 bytes, SHA256 `C8F17B3DF79B745913EA7F24CE0025F4BF848172A7EC6D5ED81111C1424B8223`. 이전 EXE는 `artifacts/Stock-before-screening-popup-F28481D6.exe`에 보관했다.
- 실제 계좌 키, 증권사 네트워크, 실자금 주문, 사용자 DB 삭제는 수행하지 않았다. 실제 분봉·틱봉은 검증된 원본이나 읽기 가능한 계좌 분봉이 없으면 생성하지 않는 경계를 유지했다.
# 2026-10-11 툴팁·컨트롤 정렬·스크리닝 상태 설명

- Release build: 경고 0, 오류 0.
- `--oversold-ui`: 178 checks 통과.
- `--ui-smoke`: 290 checks 통과.
- 전체 회귀: 885 checks 통과, broker network call/account transaction 없음.
- 최종 설명 문구 반영 뒤 `--oversold-ui` 178 checks 재통과. 최종 게시본과 루트 smoke 모두 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true.
- 흰색 ToolTip 배경·짙은 글자·테두리를 실제 WPF popup에서 검사했다.
- 화면 배치 Button/TextBox의 세로 중앙 정렬과 ScreeningResultDialog의 자료부족/미평가 설명을 1160/1540px 렌더링으로 확인했다.

# 2026-10-13 낙주 백테스트·스크리닝 팝업

- `dotnet build Stock.sln -c Release --no-restore`: 경고 0, 오류 0.
- `Stock.Tests.exe --oversold-checks`: 70 통과.
- `Stock.Tests.exe --oversold-ui artifacts/backtest-popup-ui-cancel-final`: 186 통과.
- `Stock.Tests.exe --public-oversold-verification artifacts/samsung-oversold-verification-final`: 삼성전자 공개 일봉 1,500개, 검증 전략 3개 기대 청산 6/6 통과.
- 전체 회귀: 896 통과, broker network call/account transaction 없음. 첫 sandbox 실행의 가짜 주문저널 임시파일 접근 거부는 일반 Windows 권한 재실행에서 통과했다.
- 독립 단일 EXE smoke: `artifacts/backtest-popup-smoke-visual-c1823dc17d254049a4852ce91fde6bc1/result.json`, 종료 0, Success/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified/RawNativeRendered/NoBrokerCalls=true.
- 게시본: `artifacts/backtest-popup-final-20261013/Stock.exe`, 80,683,012 bytes, SHA256 `802EE3EAA7EF81C11937E8C57BFC0761C70BFA1A239B96604E5FC72E6829A31F`.
- 루트 파일 잠금 때문에 `Stock.exe`는 이전 `8F97...` 상태이며, 동일 폴더 `Stock-검증완료-20261013.exe`가 새 게시본과 동일하다. 실제 계좌 주문·키·사용자 DB 변경 없음.

# 2026-10-14 전략 선택·스크리닝 성능·차트 조작·통합 EXE

- `dotnet build Stock.sln -c Release --no-restore`: 경고 0, 오류 0.
- `Stock.Tests.exe --current-daily-checks`: 99 통과. 공개 현재가가 종목별 cadence로 동작해 전역 7초 직렬 대기를 만들지 않는 검사 포함.
- `Stock.Tests.exe --oversold-ui artifacts/strategy-selection-ui-20261014`: 193 통과. 세 화면 전략 선택기, 백테스트 이름, 중복 래퍼 제거, 표 중앙 정렬, 날짜 정렬, 표식 봉 단일 클릭, 병렬 스크리닝 검사 포함.
- `Stock.Tests.exe --ui-smoke artifacts/ui-smoke-20261014`: 305 통과. 관심그룹 전환 시 스크리닝 행 위치 364.1→364.1 유지.
- 전체 회귀: 904 통과, broker network call/account transaction 없음.
- 합성 8종목 병렬 스크리닝: 0.468초, 최대 동시성 8 관측. 앱의 실제 배치 제한은 4종목이고 각 종목 내부 상태/현재가 요청이 병렬이다.
- 공개 읽기 전용 최근 1주 스크리닝: KOSPI/KOSDAQ 각10개, 합계20개를 9.52초와 9.55초에 완료. 결과 JSON은 `artifacts/public-screening-performance-20261014`와 `artifacts/public-screening-performance-repeat-20261014`.
- 게시본 smoke와 루트 smoke: 종료0, Success/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified/RawNativeRendered/NoBrokerCalls=true.
- 게시본/루트: 80,755,858 bytes, SHA256 `49FD7DDA43D5518F53B700ED7B02C9F64F62344EAF1F708818532241B59C7BDE`.
- `Stock-검증완료-20261013.exe` 제거 확인. 이전 루트는 `artifacts/Stock-before-strategy-screening-8F97D6F7.exe`에 보관. 실제 키·실계좌 주문·사용자 운영 DB 삭제 없음.

# 2026-10-14 장중 일봉·모의 전략 실제 실행 확인

- 장중 루트 `Stock.exe`에서 삼성전자 공개 일봉을 직접 조회했다. 표시 가격/관측 시각이 `235,500원 / 10:42:42`에서 `234,750원 / 10:43:38`로 바뀌었다.
- WPF 화면 병합 타이머는 1초, 네이버 KRX 공개 시세 조회는 서버 지정 7초다. 같은 관측값을 매초 인위적으로 바꾸지 않으며 새 스냅샷은 다음 1초 화면 주기에 반영한다. 1초 경로가 정상이라 3초 변경은 하지 않았다.
- 앱의 `모의 · 실제 주문 없음`에서 삼성전자를 추가하고 종목별 모의 백테스트를 완료했다. 결과는 현재 설정/기간 기준 매매 0건, 최종 평가 10,000,000원이며 실제 주문은 없었다.
- 현재 체크된 모의 UI는 과거 백테스트다. 실시간 전략 실행은 키움 국내 실계좌 경로이며, 실제 장중 계좌 검증은 하지 않았다.
- `dotnet build Stock.sln -c Release`: 경고 0, 오류 0.
- `--current-daily-checks`: 99개 통과. `--multi-mock`: 5개 통과. 전체 회귀: 904개 통과, broker network call/account transaction 없음.

