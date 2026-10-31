## 현재 작업 2026-10-31 — GitHub v0.1.4 Release 게시 [x]
- [x] WPF 레이아웃 수정과 검증 기록만 `main`, `v0.1.1`~`v0.1.4`로 push했다. API 키·DB·로컬 EXE는 포함되지 않았다. v0.1.1~v0.1.2는 실제 행 높이가 44px로 고정됐지만 GitHub 가상 화면에서 버튼 템플릿 좌표가 달라진 테스트 문제였고, v0.1.3은 CI 화면이 760px로 제한되어 1040px 창도 같은 높이로 잘리는 해상도 의존 테스트 문제였다. v0.1.4에서는 44px 행/스크롤 영역과 차트의 `*` 가변 행·Stretch 배치를 구조로 검증한다.
- [x] 로컬 빌드 경고0/오류0, UI 스모크 401개, 전체 회귀 1,077개를 통과했다. GitHub Actions run `41174068757`도 전체 테스트, self-contained 게시, EXE smoke, SHA256, Release 생성을 모두 통과했다. Release 자산은 `Stock.exe` 80,792,975 bytes와 `Stock.exe.sha256` 77 bytes이며, 다시 내려받아 계산한 SHA256 `3E8C82F178AD3537CFEB069F4E9697DEB1BCBC8D3A1E2B912B1766C9CF1C303D`가 게시 체크섬과 일치한다. 실제 브로커 호출·계좌 거래는 수행하지 않았다.

## 현재 작업 2026-10-31 — 루트 Stock.exe 최신 게시본 교체 [x]
- [x] 루트 `Stock.exe`가 2026-10-24 게시본(SHA256 `B29DA7C…`)이고 `release/Stock.exe`가 2026-10-31 최신 게시본(SHA256 `DA99029…`)임을 확인했다. 기존 파일은 `artifacts/Stock-before-actions-layout-B29DA7CD.exe`에 보관하고 루트를 최신 게시본으로 교체했다.
- [x] 교체된 루트와 `release/Stock.exe`는 모두 80,759,921 bytes, SHA256 `DA990294DE55DD9C51639DD1B8E8D64AF616CA11E42252E3C0289CC02C693D1D`로 일치한다. 루트 단독 smoke는 `ExitCode=0`, `Success/NoBrokerCalls=true`로 통과했다.

## 현재 작업 2026-10-31 — GitHub Actions Release WPF 레이아웃 회귀 수정 [x]
- [x] GitHub Actions의 WPF 환경에서 관심종목 그룹 가로 스크롤바가 소스 선택 행을 3px 키워 스크리닝 컨트롤이 아래로 이동하는 문제를, 스크롤바 공간을 포함한 44px 고정 높이 행으로 수정했다. 검사를 느슨하게 만들지 않고 실제 화면 좌표가 `362.1 → 362.1`로 유지되게 했다.
- [x] Release 빌드 경고0/오류0, UI smoke 401개, 전체 1,077개를 통과했다. 게시본 `Stock.exe`는 80,759,921 bytes, SHA256 `DA990294DE55DD9C51639DD1B8E8D64AF616CA11E42252E3C0289CC02C693D1D`이며 단독 smoke가 `Success/NoBrokerCalls=true`로 통과했다. 실제 GitHub push·Release 재실행·브로커 호출·계좌 거래는 수행하지 않았다. 수정 커밋 후 새 `v0.1.1` 태그를 push하면 Release 워크플로를 새 코드로 다시 검증한다.

## 현재 작업 2026-10-31 — GitHub Release 단일 EXE 배포 [x]
- [x] 개인 API 키·계좌 DB와 80MB 게시 EXE는 Git 추적에서 제외한 채, `v*` 버전 태그를 올리면 GitHub Actions가 Windows x64 독립 실행형 `Stock.exe`를 빌드·검증해 Release에 첨부하도록 구성했다.
- [x] `.github/workflows/release.yml`, `docs/GitHubRelease.md`와 README의 최신 Release 안내를 추가했다. Release에는 단일 EXE와 SHA256 파일만 올리고 키·DB·로그·빌드 산출물은 계속 제외한다.
- [x] Release 빌드 경고0/오류0, 대상 WPF UI 221개, 최종 전체 1,077개를 통과했다. 첫 전체 실행의 `End` 키 탐색 검사는 일시적으로 1회 실패했으나 대상 UI 재실행과 최종 전체 회귀에서는 모두 통과했다. 워크플로와 같은 게시본 `Stock.exe`는 80,759,684 bytes, SHA256 `B29DA7CDD67E132182A8904268E5FE022058D0ADEFF35363791ABCA1E1B8EA81`이며 단독 smoke가 `Success/NoBrokerCalls=true`로 통과했다. 실제 GitHub tag push·Release 생성·브로커 호출·계좌 거래는 수행하지 않았다.

## 현재 작업 2026-10-24 — 전체 지정봉 분·시간 선택과 단위별 범위 [x]
- [x] 스크리닝·전략 백테스트의 지정봉 단위를 공통 `분봉/시간봉` 콤보박스로 만들고 값 목록을 분봉 1~59, 시간봉 1~12로 전환했다. 단위를 바꾸면 현재 값은 새 범위 안으로 보정되며 이전 저장 틱/잘못된 단위는 분봉으로 안전하게 마이그레이션한다.
- [x] 시간봉을 공통 백테스트와 실시간 다중전략의 KRX 세션 경계 집계에 연결했다. 분봉·시간봉 모두 완성봉 신호 다음 실제 이벤트부터 주문하며 결과 주기와 차트 주기를 보존한다.
- [x] Release 빌드 경고0/오류0, 확장136개, WPF UI401개, 전체1077개를 통과했다. 1분봉 20거래일 7,800개는 1,639ms, 총 할당338.1MB, 결과 보유 증가22.6MB였다. 게시본·루트·EXE 단독 폴더 `Stock.exe`는 80,759,684 bytes, SHA256 `B29DA7CDD67E132182A8904268E5FE022058D0ADEFF35363791ABCA1E1B8EA81`로 일치하며 루트와 단독 smoke가 Success/NoBrokerCalls=true로 통과했다. 이전 루트는 `artifacts/Stock-before-timebar-52A330B3.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-24 — 전략 백테스트 지정 분봉·부하 검증 [x]
- [x] 종목/차트의 전략 백테스트에 일봉과 별개인 지정 분봉 입력을 추가하고 온라인 연결의 공식 분봉 범위 조회를 사용한다.
- [x] 분봉 완성 시점 판단 후 다음 분봉 시가 체결, look-ahead 방지, 기간·표식·저장 결과를 기존 백테스트 흐름과 통합한다.
- [x] 장기간 분봉 성능·취소·UI·전체 회귀를 확인했다. Release 경고0/오류0, 낙주75, UI396, 전체1,067 통과. 1분봉20거래일 7,800개는 1,646ms, 총 할당339.8MB, 결과 보유 증가24.4MB로 제한 20초/256MB를 통과했다. 게시본·루트·EXE 단독 폴더 smoke가 Success/NoBrokerCalls=true로 통과했고 세 `Stock.exe`는 80,758,816 bytes, SHA256 `52A330B3EB0E8CD5565097D8CDE4FA679A8D0B14525927681EC4212872789FDF`로 일치한다. 이전 루트는 `artifacts/Stock-before-minute-backtest-82D8B600.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-23 — 스크리닝 기간 경계·국내 시장 선택·1시간봉 조회 최적화 [x]
- [x] 스크리닝 상세 전체보기의 봉과 매매 표식을 실행 snapshot의 시작일 00:00 이상·종료일 다음날 00:00 미만으로 고정했다. 기간 밖 체결이 가장자리 봉에 붙지 않는다.
- [x] 스크리닝 시장을 코스피·코스닥·코스피+코스닥으로 분리하고 해외 선택을 제거했다. 단일 시장은 해당 시장 상위 N개, 통합은 두 시장에서 각각 상위 N개를 요청한다.
- [x] 자료조회와 전략계산 시간을 분리 계측한다. 키움 공식 60분봉 직접 조회, 종목·봉·기간·브로커별 2분/최대 128개 범위 캐시, 불필요한 종목 상세 조회 제거를 적용했다. 주 병목은 직렬 REST 요청과 650ms 간격이므로 Rust/C++ DLL은 추가하지 않았다.
- [x] Release 빌드 경고0/오류0, 대상 UI 219개, UI smoke 394개, 전체 회귀 1060개를 통과했다. 게시본과 루트 smoke는 Success/NoBrokerCalls/RawNativeRendered/ScreeningSnapshotVerified=true다. 게시본·루트 `Stock.exe`는 80,651,838 bytes, SHA256 `82D8B600B6A49E7D8E6986A5587816481AFDBEAF6A2AD135D4B7F9F4F5D57393`로 일치한다. 이전 루트는 `artifacts/Stock-before-screening-market-performance-EAA06D41.exe`에 보관했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-23 — 스크리닝 지정봉 전체보기·우클릭 탐색 전환 [x]
- [x] 스크리닝 당시 지정한 틱·분·시간·일봉을 상세 창의 기본 주기로 명시적으로 선택한다. 시간봉도 장중 주기로 판별해 시간 축과 체결 봉 매핑을 정확히 적용한다.
- [x] 상세 차트 첫 화면은 전체 기간 잠금 상태로 두고, 우클릭 시 포인터 봉 중심의 안전 범위(최대 500봉)로 확대해 휠·드래그·체결 선택을 활성화한다. 다시 우클릭하면 전체 기간으로 복귀해 입력을 잠근다.
- [x] 우클릭 확대 이후의 최대 축소 범위를 최초 확대 범위로 제한하고 매수·매도 번호 표식과 클릭 상세를 공통 `CandleChart` 방식으로 유지한다.
- [x] 스크리닝 목록과 결과 창의 `종목별 조건·체결` 진입 경로가 동일한 immutable snapshot과 차트 상태 전환을 사용하는지 검증했다. Release 빌드 경고0/오류0, 전체 1044개, 게시본·루트 smoke를 통과했다. 화면은 `artifacts/screening-review-ui-final-20261023/screening-chart-focused-hour.png`이며 게시본·루트 `Stock.exe`는 80,649,488 bytes, SHA256 `EAA06D410CB8ABFE168FF69F07F84FB8292D31BB3B978BC3DBAAF5788D930B65`로 일치한다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-22 — 시장별 상위 선택값·장기간 지정봉 이동 성능 [x]
- [x] 스크리닝의 시장별 상위 선택값을 10·20·50·100으로 변경했다. 선택 수는 KOSPI와 KOSDAQ에 각각 적용된다.
- [x] 긴 분봉·시간봉 차트의 이동 거리를 실제 소수 픽셀 봉 폭에 맞추고, 드래그 중 렌더를 프레임당 한 번으로 합쳤다. 이동평균·일목 계산 결과와 펜/브러시를 재사용한다.
- [x] 여러 봉이 같은 픽셀에 겹치는 축소 구간은 화면 표시용 OHLCV 열로만 묶는다. 원본 봉은 그대로 보존되어 확대·정보·매매 표식·백테스트에 사용된다.
- [x] Release 빌드 경고0/오류0, WPF 211개, 전체 1036개를 통과했다. 1만 개 1분봉 중 2천 개 표시·이동평균/일목 활성 조건의 12회 이동 렌더는 최적화 전 1,096ms에서 최종 787ms였고 지표 캐시는 1회만 생성됐다. 게시본·루트 `Stock.exe`는 80,648,597 bytes, SHA256 `DEE1F31D41637F742A65CBCAE52F546E2A0D133209DE785AFFDF6738CDD4D350`으로 일치하며 smoke도 통과했다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-22 — 스크리닝 온라인 지정봉·전체 기간 상세 차트 [x]
- [x] 오프라인 스크리닝에서는 일봉만 보이고 지정봉 입력은 숨기며, 저장된 지정봉 설정도 오프라인 실행에 사용되지 않게 했다.
- [x] 온라인 스크리닝에서 분봉을 명확히 선택할 수 있게 하고 틱봉·분봉·시간봉 단위를 구분해 표시했다.
- [x] 지정봉 상세 차트가 스크리닝 시작일~종료일의 전체 분봉 원본을 확보해 모든 집계봉을 표시하고 전체 범위에서 확대·축소할 수 있게 했다. 키움 요청은 종료일을 기준일로 하고 시작일을 포함한 페이지까지 연속 조회한다.
- [x] 2026-10-01~2026-10-20 1시간 fixture의 완성봉 84개가 첫 화면에 모두 들어오며 확대 후에도 원본 84개가 유지됨을 확인했다. 화면은 `artifacts/screening-range-ui-final-20261022/screening-hour-full-range.png`다.
- [x] Release 빌드 경고0/오류0, 전체 1033개, WPF 379개, 게시본·루트 smoke를 통과했다. 게시본과 루트 `Stock.exe`는 80,645,331 bytes, SHA256 `04FB712C5372CA2414CD0F983C6D914639C6DA7C9A450270B7C4C31DB73C6874`로 일치한다. 실제 브로커 네트워크 호출·계좌 거래·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-22 — 백테스트 거래번호·마우스 기준 확대·스크리닝 지정봉 상세 [x]
- [x] 백테스트 차트의 모든 매수·매도 표식 앞에 거래상세목록과 같은 `#거래번호`를 붙였다. 한 거래의 분할매수·부분매도는 같은 번호를 유지하고, 축소 집계 배지도 포함 번호를 표시한다.
- [x] 봉 차트 휠 확대·축소가 오른쪽 끝이 아니라 마우스가 가리키는 봉과 화면상 위치를 보존하도록 수정했다.
- [x] 스크리닝 상세 차트의 첫 화면을 실행 snapshot의 정확한 틱/분/시/일 지정봉과 시작일~종료일로 구성했다. 키움 현재 진행 중 분봉 한 행의 0/불일치 OHLC 때문에 정상 행 전체가 폐기되던 원인을 고쳐, 잘못된 행만 제외하고 완성된 봉은 유지한다.
- [x] Release 빌드 경고0/오류0, WPF 207개, 전체 1014개, 게시본 및 루트 `Stock.exe` smoke를 통과했다. 화면은 `artifacts/numbered-marker-ui-final-20261022/zoomed-out-trade-counts.png`이며 게시본과 루트는 80,712,184 bytes, SHA256 `76602D9E0F20AE1A52E7DC01A5C7045387CC86636D47CED0E47085469F477C6B`로 일치한다. 실제 계좌 주문·브로커 네트워크 호출·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-22 — 축소 매매 표식 집계·모바일식 차트 이동 [x]
- [x] 실제·모의·백테스트·정기매수 체결을 공유하는 봉 차트와 원본 틱 차트에서 화면상 약 92px 구간의 매수·매도를 각각 묶어 `매수 N건`/`매도 N건`으로 표시한다. 원본 체결은 합치거나 삭제하지 않는다.
- [x] 봉 차트 어느 위치에서든 좌우 드래그가 동작하고, 오른쪽으로 밀면 과거·왼쪽으로 밀면 이후 시점을 보여주는 모바일식 직접 조작으로 수정했다. 클릭 판정용 4px 여유는 유지한다.
- [x] 축소 WPF 렌더 `artifacts/marker-cluster-ui-final-20261022/zoomed-out-trade-counts.png`, WPF 205개, 원본 틱 30개, 전체 1004개, Release 경고0/오류0, 게시본·루트 smoke를 통과했다. 게시본과 루트 `Stock.exe`는 80,710,099 bytes, SHA256 `6B2E25C99061FF33D5ECDF7352D4A7C761F2C842E16206161F695CA463EA13D3`로 일치한다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-22 — 백테스트 매매 표식 실제 화면 검증 [x]
- [x] 삼성전자 `백테스트 검증 1 · 1일 보유`, 2025-10-22~2025-11-30을 실제 루트 WPF에서 실행했다. 완결 5회, 승률 60.00%, 최종 평가자산 10,050,300원, Profit Factor 2.05가 나타났다.
- [x] 백테스트 종료일 이후 일봉이 1초 시세 갱신으로 다시 붙어 표식이 압축되던 문제와, 표시 체크를 OFF→ON했을 때 일반 최신 차트에서 백테스트 기간으로 돌아오지 않던 문제를 수정했다.
- [x] 체크 OFF에서 표식이 사라지고 ON에서 테스트 기간·`매수`/`매도` 표식이 복원되는 실제 화면을 확인했다. 스크린샷은 `artifacts/backtest-marker-visible-20261022/backtest-trade-markers-verified.png`다.
- [x] Release 경고0/오류0, 표식 WPF 회귀 202개, 전체 1001개, 루트 단일 EXE smoke를 통과했다. 게시본과 루트 `Stock.exe`는 190,775,540 bytes, SHA256 `962609E9B504ECEE917DB98CC528152FD0D9365609C79CE039CA9BFDF3BDB619`로 일치한다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-21 — 스크리닝 명칭·온라인 1시간봉·백테스트 표식·전면 검증 [x]
- [x] `종목 골라보기` 탭 이름과 관련 사용자 문구를 `스크리닝`으로 정리했다.
- [x] 온라인 연결의 공식 1분봉을 선택한 분/시간봉으로 집계하여 지정봉 스크리닝에 사용한다. 거래대금은 1분봉에 정확한 체결대금이 없어 대표가격×거래량 근사임을 결과에 표시한다.
- [x] 과거 시점의 종목 분류·거래정지/관리·Fundamental Risk가 없으면 해당 사실은 `자료부족`으로 보존하되, Crash/RSI/거래량/Support 기술 조건은 관측 자료로 계속 평가한다. 주문 허가로 해석되지 않는다.
- [x] 종목/차트 백테스트 완료 시 `백테스트 매매 내역 표시`를 자동 선택하고 테스트 기간으로 차트 범위를 바꿔 체결 표식이 즉시 보이게 했다. 표식 또는 같은 봉을 한 번 클릭하면 별도 상세 팝업이 열린다.
- [x] 실제 WPF와 저장 기본 키움 계정의 읽기 전용 경로에서 삼성전자 1시간봉(2026-10-14~2026-10-21) 1종목을 확인했다. 1.45초에 완료됐고 `조회 실패/봉 없음 0개`였으며 기술 조건이 평가됐다. 실시간 모의 시작 조건 실패도 하단 문구가 아닌 별도 안내 팝업으로 직접 확인했다.
- [x] Release 빌드 경고0/오류0, 전체 회귀 998개, 게시본·루트 smoke를 통과했다. 게시본과 루트 `Stock.exe`는 190,775,540 bytes, SHA256 `C79DC0A55AC4C0BF7A32B2E6D26D53F7FA8A3FFE8E7B8B0293C0A89A1F0A0FF4`로 일치한다. 실제 계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-21 — 실시간 모의매매 직접 검증·팝업 안내 가시성 [x]
- [x] 실제 WPF 다량 실행 창의 클릭 흐름을 확인했다. 장 종료 뒤 실제 피드에서는 새 체결을 만들 수 없었고, 지원 틱/분 FakeFeed 회귀에서 시작→PaperBroker 가상 체결→차트 기록→전체 중지 전 과정을 확인했다. 브로커 주문 호출은 0건이다.
- [x] 다량 실행 팝업의 명령 결과와 시작 조건 실패를 하단 문구와 별도 `다량 전략 실행 안내` 팝업에 함께 표시한다.
- [x] 지원되지 않는 봉/연결에서도 실행 버튼을 누르면 무반응 대신 정확한 이유를 별도 안내 팝업으로 표시한다.
- [x] Release 빌드·전체 998개 회귀·문서·단일 EXE 교체를 완료했다. 실제 키를 새로 읽거나 표시하지 않았고 실계좌 주문·사용자 운영 DB 삭제도 수행하지 않았다.

## 현재 작업 2026-10-21 — 전일 종가 등락·전략 목록 동기화·다량 실행 안내 팝업 [x]
- [x] 종목/차트 현재가의 금액·등락률을 선택 봉과 무관하게 전일 일봉 종가 기준으로 고정했다. 실시간은 제공 시세의 전일 종가, 과거 화면은 마지막 표시 일봉 직전 거래일 종가를 사용한다.
- [x] 다량 전략 실행의 등록 전략 목록을 전략 설정 탭의 저장 전략 목록과 같은 저장소에서 불러온다. 과거 모의·실시간 모의·실계좌 모두 각 종목에서 선택한 저장 전략 Config를 사용한다.
- [x] 다량 실행의 시작 실패·자료 조건 안내를 하단 문구와 별도 `다량 전략 실행 안내` 팝업에 함께 표시했다. 틱/분은 전략식의 제한이 아니라 실시간 확정봉·검증 원본 경계라는 점을 문서화했다.
- [x] Release 빌드 경고0/오류0, 낙주/현재가 UI 198개, 다량 UI 25개, 전체 오프라인 회귀 992개를 통과했다. 게시본·루트 smoke 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다.
- [x] 게시본 `artifacts/header-strategy-popup-final-20261021/Stock.exe`와 루트 `Stock.exe`는 80,702,235 bytes, SHA256 `3231B86179C4BD5C79FD9DC1BFDA6E942EC402EC016F88305BFCCCFED8F4B69F`로 일치한다. 이전 루트는 `artifacts/Stock-before-header-strategy-popup-33F0AC33.exe`에 보관했다. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-21 — 1초 차트·기본계좌 자동연결·온라인 백테스트·가상계좌 주문 [x]
- [x] 틱·분·일·주봉의 화면 가격/마지막 봉 반영을 공통 1초 주기로 통일했다. 실시간 이벤트는 1초 단위로 합쳐 렌더하고, 분봉 이력과 같은 구간의 실시간 체결을 병합해 현재가 변화가 마지막 봉에 반영된다.
- [x] 저장·검증된 기본계좌가 있으면 온라인 투자 선택 시 키 설정 창 없이 연결한다. 온라인 투자 옆에 증권사와 마스킹 계좌번호를 표시하며, 기본계좌가 없을 때만 기존 키 설정 창을 연다.
- [x] 온라인 모드에서도 동일한 저장 전략과 공통 백테스트 엔진으로 전략/정기매수 백테스트를 실행할 수 있다.
- [x] 종목/차트 우측에 `가상 계좌 주문` 탭을 실제 계좌 다음에 추가했다. 실시간 전략 모의매매는 모의+실시간으로 잠기고, 실제 탭의 다중전략은 실제 모드로 잠긴다. 실제 탭에서 가상 주문 UI를 제거했으며 오프라인에서는 실제 탭을 숨기고 가상 탭을 기본 선택한다.
- [x] Release 빌드 경고0/오류0, 일봉·주봉/UI 194개, 다중전략 UI 24개, 전체 오프라인 회귀 987개를 통과했다. 게시본·루트 smoke 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다.
- [x] 게시본 `artifacts/live-account-final-20261021/Stock.exe`와 루트 `Stock.exe`는 80,700,174 bytes, SHA256 `33F0AC33EA0DA515C57B46AE56CA4C8D37564DBB63AD231AE1A1636655B35947`로 일치한다. 이전 루트는 `artifacts/Stock-before-live-account-3AAA0573.exe`에 보관했다. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-21 — 백테스트 체결 표식·분봉 응답·긴급중지 취소 [x]
- [x] 종목/차트에 실제·모의와 독립된 `백테스트 매매 내역 표시`를 추가했다. 선택한 단일 종목 백테스트 체결을 실제·모의·정기매수 표식과 합쳐 표시하며, 체크 상태는 재시작 뒤에도 복원된다.
- [x] 키움 `ka10080` 분봉 조회가 연속 페이지를 최대 100회 순회해 약 1분 뒤 실패하던 문제를 수정했다. 차트 이력만 최근 3페이지를 유효한 부분 결과로 사용하고 화면 조회에는 15초 제한·취소·실패 원인 안내를 적용했다. 잔고/주문 조회의 기존 완전 페이지 정책은 유지한다.
- [x] 차트/API 로딩 중에도 우측 상단 `긴급중지`를 즉시 실행할 수 있게 했다. 현재 화면 작업과 시세·관심종목·시장현황·틱/분/일봉 백그라운드 API 요청을 공통 토큰으로 취소하고 Busy/차트 로딩 표시를 즉시 해제한다. 중지는 신규 전략 실행을 막되 기존 보유분을 임의 청산하지 않는다.
- [x] Release 빌드 경고0/오류0, WPF UI 349개, 전체 오프라인 회귀 980개를 통과했다. 루트 `Stock.exe` 독립 smoke도 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/IntegerInsertionVerified/ScreeningSnapshotVerified=true다.
- [x] 최종 게시본과 루트 `Stock.exe`는 80,698,922 bytes, SHA256 `3AAA05739D536323EF791407A20219F7A0A90C971E433771B2CF37ECF3CD90EA`로 일치한다. 이전 루트는 `artifacts/Stock-before-backtest-minute-emergency-D2990AD4.exe`에 보관했다. 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았으며 분봉 연속 페이지는 공식 스키마 가짜 HTTP로 검증했다.

## 현재 작업 2026-10-14 — 다중 전략 실시간 모의매매·전 구간 체결 표식 [x]
- [x] 다중 전략 창에 별도 `실시간` 선택을 추가했다. `모의 + 비실시간`은 기존 과거 백테스트, `모의 + 실시간`은 실제 키움 시세와 종목별 가상 계좌, `모의 해제`는 기존 선택 키움 국내 실계좌 경로를 사용한다.
- [x] 실시간 모의는 `OversoldLiveStrategy`와 검증 원본·시점별 상태를 재사용하되 `PaperBroker`만 호출한다. 실계좌 주문 API는 호출하지 않으며 신호 다음 실제 이벤트부터 가상 체결한다.
- [x] 실계좌 경로의 계좌 점유·주문 직전 모드/중지/현금/노출 재검증과 실제 체결 대사를 보존했다. 실시간 모의 중지는 새 가상 주문을 막고 미체결만 취소하며 보유분을 자동 청산하지 않는다.
- [x] 가상 체결을 source `실시간 모의매매`인 공통 `TradeMarker`로 저장한다. 동일 체결이 틱봉·분봉·일봉·주봉에 같은 시각·가격으로 매핑되고, 온라인 차트 재로딩과 기존 표식 클릭 상세에 나타나는 것을 검증했다.
- [x] Release 빌드 경고0/오류0. `--multi-mock` 5개, 확장 126개, 장중 일봉 99개, 다중 UI 23개, 낙주 UI 193개, 전체 회귀 915개를 통과했다. 모두 가짜 브로커/피드 또는 임시 DB이며 실제 계좌 주문·실키·사용자 운영 DB 삭제는 수행하지 않았다.
- [x] 단일 게시본과 루트 `Stock.exe`의 SHA256이 `76B2AB5464FF5457A7D909A0A3A159F6233F8B6528490565498B4A8903AF4B37`로 일치한다(80,693,800 bytes). 게시본·루트 독립 smoke 모두 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true다. 이전 루트는 `artifacts/Stock-before-realtime-paper-49FD7DDA.exe`에 보관했다.

## 현재 작업 2026-10-14 — 장중 일봉·모의 전략 실제 실행 확인 [x]
- [x] 루트 `Stock.exe`를 장중에 직접 실행하고 삼성전자를 조회했다. 화면 반영은 1초 Dispatcher 주기이며 공개 제공처 조회는 서버가 지정한 7초 주기다. 실제 표시가 235,500원/10:42:42에서 234,750원/10:43:38로 갱신되어 UI 반영 경로가 정상임을 확인했다. 1초 타이머가 정상이라 3초로 변경하지 않았다.
- [x] 실제 앱의 `모의 · 실제 주문 없음` 상태에서 삼성전자를 추가해 `종목별 모의 백테스트`를 실행했다. 완료 상태와 실제 주문 없음이 표시됐고, 현재 설정/기간에서는 매매 0건·최종 평가 10,000,000원이었다.
- [x] `--current-daily-checks` 99개, `--multi-mock` 5개, 전체 회귀 904개를 통과했다. 전체 회귀의 FakeBroker/FakeFeed 실시간 전략 검사는 다종목 독립 주문, 부분체결, 중복 방지, 손실 한도, 피드 단절, 전체/개별 중지, 낙주 확정봉 이후 주문을 검증했다.
- 현재 UI의 체크된 `모의`는 **실시간 모의매매가 아니라 과거 종목별 백테스트**다. 실시간 전략 루프는 현재 검증된 키움 국내 실계좌 경로에만 연결되어 있다. 이번 요청에서는 실제 키·실계좌 인증·실주문을 수행하지 않았다.
- Release 빌드 경고0/오류0. 소스·루트 `Stock.exe`는 변경하지 않았고 검증 후 앱을 종료했다.

## 현재 작업 2026-10-14 — 전략 선택·스크리닝 성능·차트 조작·최종 EXE 통합 [x]
- [x] 종목/차트와 종목 골라보기에 저장 전략 선택기를 추가하고 버튼을 `백테스트`로 변경했다. 설정 화면의 중복 일봉 백테스트 래퍼와 중복 저장 버튼을 제거했다.
- [x] 스크리닝 기본 기간을 최근 1주로 변경했다. 목록 설명과 종목 평가는 최대 4개 병렬, 일봉 캐시 10분 재사용, 현재 상태·현재가 동시 조회, 결과 DB 저장 8종목 단위, 현재가 cadence를 symbol별로 분리했다. 합성 8종목은 0.468초, 공개 KOSPI/KOSDAQ 각10개 합계20종목은 9.52초/9.55초였다.
- [x] 가격 차트의 좌우 드래그를 양방향 범위로 보정했다. 거래 표식 글자를 10→14 DIP로 키우고 한 번 클릭 및 같은 봉 아무 위치 클릭으로 가장 가까운 체결 상세를 선택한다.
- [x] 전역 DataGrid 셀/헤더의 가로·세로 중앙 정렬과 스크리닝 DatePicker/관심그룹 행 정렬을 통일했다.
- [x] Release 빌드 경고0/오류0, 현재가 대상99, WPF 대상193, 전체 WPF305, 전체 회귀904 통과. 공개 성능 보고서는 artifacts/public-screening-performance-20261014 및 repeat 폴더에 저장했다. 단일 게시본과 루트 Stock.exe는 80,755,858 bytes, SHA256 `49FD7DDA43D5518F53B700ED7B02C9F64F62344EAF1F708818532241B59C7BDE`로 일치한다. 게시/루트 smoke 모두 Success/NoBrokerCalls/RawNativeRendered/ScreeningSnapshotVerified=true. `Stock-검증완료-20261013.exe`는 제거했고 이전 루트는 artifacts/Stock-before-strategy-screening-8F97D6F7.exe에 보관했다.
- 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않는다.

## 현재 작업 2026-10-11 — 전략 진단 프리셋 추가 [x]
- [x] 전략 설정 목록에 `전략 진단`을 기존 저장 전략과 같은 일반 프리셋으로 자동 추가했다. 기존 동명 전략이 있으면 덮어쓰지 않는다.
- [x] RSI·거래량·Pivot·근사 매물대·반등 확인을 OFF, MA5·최소 지지 1개를 사용해 `사용안함`을 확인하기 쉽게 했다. `자료부족`과 `미평가`는 실제 자료 결손 및 앞 단계 제외 규칙을 그대로 사용한다.
- [x] 별도 진단 버튼과 주문/백테스트 차단은 두지 않았다. 선택만으로 주문하지 않지만, 사용자가 실행하면 일반 전략과 같은 경로를 사용한다.
- [x] Release 빌드 경고0/오류0, 대상 WPF 184개, 전체 회귀 891개 통과. 루트 EXE smoke 종료0, Success/NoBrokerCalls/ScreeningSnapshotVerified=true. 실제 계좌 주문과 운영 DB 변경은 수행하지 않았다.

## 현재 작업 2026-10-11 — 전략 설정 추가·이름 변경·복구·JSON 불러오기 [x]
- [x] 1 `불러오기`를 버전·전략 ID·파라미터 검증이 있는 JSON 가져오기로 변경했다. 같은 이름은 덮어쓰지 않고 고유 이름의 새 전략으로 등록한다.
- [x] 2 `복구`는 선택 전략의 마지막 저장값으로 58개 입력을 다시 구성해 저장 이후의 미저장 수정을 폐기한다.
- [x] 3 `추가`와 `이름 변경`을 구현했다. 여러 낙주 전략 설정을 SQLite에 독립 저장하고 선택 즉시 전환하며 마지막 선택 이름을 재시작 후 복원한다. 이름 변경은 이전 키를 제거한다.
- [x] 4 UI ToolTip과 문서·회귀 검사를 갱신했다. Release 빌드 경고0/오류0, 대상 WPF 182개, 전체 회귀 889개 통과. EXE-only/루트 smoke 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 루트 Stock.exe 80,670,041 bytes, SHA256 5ACA27C1CE116C7D4D11A40912D578D1A6E889CF86DE98F40E0E93970E9FDFF0. 이전 루트는 artifacts/Stock-before-strategy-library-4D6816F6.exe. 실제 계좌 주문·키·사용자 DB 삭제는 수행하지 않았다.

## 현재 작업 2026-10-11 — 툴팁·컨트롤 세로 정렬·스크리닝 상태 설명 [x]
- [x] 1 모든 툴팁을 흰 배경·짙은 글자·옅은 테두리/그림자로 통일했다.
- [x] 2 모든 화면 배치 버튼과 텍스트박스/날짜 입력의 내용 세로 정렬을 중앙으로 통일하고 사용자 지정 템플릿도 같은 값을 사용하게 했다.
- [x] 3 스크리닝 결과 상단에 미평가/자료부족의 원인과 함께 집계될 수 있는 이유를 표시했다.
- [x] 4 Release 빌드 경고0/오류0, 대상 UI 178개, 전체 UI 290개, 전체 회귀 885개 통과. 최종 문구 반영 뒤 대상 UI 178개도 재통과. 게시본과 루트 EXE smoke 모두 종료0이며 Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 루트 Stock.exe 80,738,541 bytes, SHA256 4D6816F6020297443E75B2CCD6F0049E093C7DECFBD4A70177E263B84E184EA5. 실제 계좌 주문은 실행하지 않았다.

## 현재 작업 2026-10-11 — 관심등록·스크리닝 상세 차트·다량 전략 편집 [x]
- [x] 1 관심등록 성공 상태를 파란색 채움 별로 표시하고 ListBox/ComboBox 항목/탭 같은 비버튼 영역의 손 모양 커서를 제거했다.
- [x] 2 종목 골라보기 상단 입력 컨트롤 높이를 스크리닝 시작 버튼과 같은 40px로 통일했다.
- [x] 3 후보의 파란 종목 이름/결과창 행 더블클릭 시 메인 탭 이동 없이 일봉·가용 1분봉·틱 기준 실행의 N틱봉과 매수·매도 marker/표를 보여주는 독립 팝업을 구현했다. 분·틱 원본이 없으면 가짜 봉을 만들지 않고 안내한다.
- [x] 4 다량 전략 실행의 새 행은 첫 등록 전략을 기본 선택하며 종목별 예산, 전체 투자 한도, 일 손실 한도, 모의 선택을 실행 전 편집할 수 있다. 미연결 실제 모드는 살펴볼 수 있으나 실행은 계속 차단한다.
- [x] 5 Release 빌드 경고0/오류0, 대상 WPF 175개, 다량 WPF 18개, 전체 881개 통과. 단일 EXE와 루트 EXE smoke 종료0, Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 루트 Stock.exe 80,668,018 bytes, SHA256 C8F17B3DF79B745913EA7F24CE0025F4BF848172A7EC6D5ED81111C1424B8223. 실제 계좌 주문은 실행하지 않았다.

## 현재 작업 2026-10-11 — 토스증권 참고 UI 디자인 정리 [x]
- [x] 1 토스증권의 현재 웹 UI를 참고해 색상·타이포그래피·간격·라운드·상태 표현을 공통 디자인 토큰과 전역 스타일로 정리했다.
- [x] 2 Button/TextBox/ComboBox/CheckBox/RadioButton/DatePicker/DataGrid/Tab/Card를 같은 시각 체계로 통일하고 기존 숫자 입력·바인딩 동작을 보존했다.
- [x] 3 종목·차트, 오른쪽 주문/백테스트, 종목 골라보기, 전략 설정과 다량 전략 팝업에 새 공통 스타일을 적용했다. 1540×1040 차트 514px, 최소 1160×760 차트 234px로 렌더링 확인.
- [x] 4 Release 빌드 경고0/오류0, 대상 WPF 171개, 다량 전략 WPF 15개, 전체 874개 통과. EXE-only smoke Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified=true. 게시본과 루트 Stock.exe SHA256 F28481D698A9C75DB5148B7BE947ECDC9BA90691B5D4AA000018086EC4B1132B, 80,657,800 bytes로 일치.
- 디자인 원칙: 토스증권을 복제하지 않고 흰 Surface, 옅은 회색 Canvas, 선명한 파란 주요 동작, 조용한 보조 텍스트, 10~18px 라운드와 충분한 여백을 참고한다. 기존 MVVM/DI/명령/주문 안전장치는 변경하지 않는다.
- 시작 기준: 루트 Stock.exe는 검증 게시본과 같은 SHA256 `78954CC5F7286C10E8160D934212CF687F5B76228C33DE121043BF0B8CF5967B`로 교체 완료. 실제 키·주문·사용자 DB는 건드리지 않는다.

## 현재 작업 2026-10-10 — 기간별 스크리닝·차트 이동·다량 전략 실행 [x]
- [x] 1 기간 DatePicker/검증, 기간별 평가·warmup·불변 run/진행률 구현. Core/WPF/전체 검사 통과.
- [x] 2 표 단일/결과창 더블클릭 차트 이동, 범위 내 낙주 백테스트 모형 체결 marker·상세 구현. 대상 WPF와 전체 검사 통과.
- [x] 3 마지막 봉 hover 구현. 마지막 봉·팬·줌 검사 통과.
- [x] 4 백테스트 0 원인 진단 및 모의매매 시작 UI 제거. 대상 WPF와 전체 검사 통과.
- [x] 5 전략 설정 JSON 다운로드. 직렬화/파일·WPF 검사 통과.
- [x] 6 기존 MultiTrade 팝업을 재사용해 관심그룹·멤버 중복 동기화, 최신 스크리닝 후보, 종목별 낙주 모의 백테스트와 SQLite 기록/편집 잠금 구현. 온라인은 선택 계좌 표시만 하고 자동 실행 비활성. 모의 5개·팝업 WPF 10개 통과, 실제 주문 0건.
- [x] 7 최신 Release 빌드 경고0/오류0, 전체 874개·증분 집계 117개·다량 팝업 WPF15개 통과. 별도 단일 EXE smoke Success/NoBrokerCalls/RawNativeRendered/FixedEditorVerified/ScreeningSnapshotVerified 성공. 최종 게시본 `artifacts/oversold-live-final-20261011/Stock.exe` SHA256 `78954CC5F7286C10E8160D934212CF687F5B76228C33DE121043BF0B8CF5967B`. 2026-10-11 사용자 종료 후 루트 Stock.exe도 같은 해시로 교체·확인. 실제 키·주문·사용자 DB는 건드리지 않음.
- [x] 8 명시 요청에 따라 다량 전략 창의 모의 체크(기본 체크/오프라인 고정), 온라인 모의 과거 백테스트, 모의 해제 시 선택 키움 국내 실계좌만 대상으로 낙주 전략 틱·분 실시간 실행 연결. 기존 LiveStrategyBatch/RemoteTradingSession/OrderGuard의 계좌 점유·예산·손실·체결 대사·응답 불명 차단 재사용. 확정봉 뒤 체결 신호, 연속 원본 증분 집계와 결손 시 신호 무효화, 오늘 세션/종목 상태/위험/준비 봉 검증, 시작 확인창 구현. FakeBroker/FakeFeed만 검증, 실제 장중 키·체결·주문은 미검증. 기존 자동 승인 검토 거부는 당시 기록이며 이번 연결은 이후 명시 요청 범위로 처리.
- 시작 기준: 10/10 검증 Stock.exe SHA256 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4, 846검사. 먼저 백업한다.
- 다음 재개 위치: 이 목록과 HANDOFF를 확인하고 관련 코드 분석. 실계좌 키 초기화·실주문·사용자 DB 삭제 금지.
# 현재 작업 목록 — 2026-10-10 인수인계 기준

아래 현재 목록이 뒤의 과거 체크포인트에 남아 있는 [ ]/[~]보다 우선한다. 상세 재개 방법은 HANDOFF.md. 과거 기록은 이력으로 보존한다.
- [x] 다른 계정 재개 문서/명령/소스와 EXE 차이/인증 및 데이터 이관 범위 정리.
- [x] 10/06 현재 일봉 스크리닝·1초 장중 표시: 10/10 검증/배포 완료. 하단 최신 기록 참조.
- [x] 테스트 컴파일 오류 해결 및 Release/전체846/WPF164/단독EXE 검증 완료.
- [ ] 위 완료 후 C 실제 원본 adapter·감사·과거 결과 조회 후속 검토.
- [!] 실계좌 자동주문/다중매매 신규 연결: 기존 승인 검토 거부 미해결. 계정 변경으로 우회하지 않는다.

---
# 작업 상태 — 2026-09-28

## 완료한 요청

- [x] 사용자 첨부 지침을 Stock.exe 옆 `STOCK/AGENTS.md`에 그대로 추가
- [x] 오프라인 테스트 / 온라인 투자 라디오, 국내/해외와 한글 이름 검색
- [x] 종목 코드·참조가격·CSV·모의 계좌 연결 UI 제거
- [x] 공개 실제 일봉 다운로드·캐시·한국시간·이동평균·일목 차트
- [x] 일봉 전략 공통 평가, 업종/국가 필터 스크리닝, 백테스트·가상 수동 매매
- [x] 실제 계좌 키 설정 및 사용자 확인을 거치는 실계좌 직접 시장가 주문 경로
- [x] 주문 한도 잘못된 입력 차단 및 기존 값으로 묵시적 주문 방지
- [x] 화면별 `docs/ui/`, `docs/architecture/ARCHITECTURE.md`, `docs/api/` 문서와 ToolTip 추가
- [x] Release 빌드 성공, 오프라인 280개 테스트 통과
- [x] 최신 단독 Stock.exe를 프로젝트 루트에 배치
- [x] 루트 EXE와 EXE만 있는 별도 폴더에서 실행·WPF·SQLite·백테스트·정상 종료 확인
- [x] README.md, VALIDATION.md 및 상태 문서 갱신

## 다음 실행 시 확인할 사항

이전 미완성 문장 “그리고 code”에서 별도 작업을 추측하지 않았으며, 이후의 구체적인 1~7 요청을 처리했다.
- [!] 실제 키움 계좌 연결·주문·취소 체결 검증은 사용자 직접 확인이 필요하다. 개발 중 실제 돈 주문을 보내지 않는다.

승인된 구현 작업에 남은 미완료 항목은 없다. 새 요청이 없으면 위 확인 사항을 임의 수행하지 않는다.

## 이어받을 구조

- 현재 진입: `Stock.App/MainWindow.xaml` → `WorkspaceViewModel.cs`.
- 데이터: `Stock.Core/Research/PublicMarketData.cs`. 한글 이름·업종은 네이버, 국내 일봉 네이버, 미국 일봉 Yahoo. 공개 인터페이스 변경·제한 가능.
- 전략·백테스트: `ChartStrategies.cs` / `BarPaperAccount`. 종가 판단 후 다음 시가 가상 체결. 현재 UI에 실계좌 자동 전략은 없음.
- 화면: `CandleChart.cs` / `ConnectionDialog.cs`. 모든 사용자 시간은 한국시간.
- 주문: `KiwoomBroker(live:true)` → `RemoteTradingSession` → `OrderGuard`. 모의/실전 저널 분리, 응답 불명 자동 재전송 금지.
- 저장: `%LOCALAPPDATA%\StockPaperTrading\KiwoomQuant\workspace-v2.db`. 기존 stock.db와 구형 MainViewModel은 보존한다. 틱 엔진은 새 실제 틱 관찰 화면에서도 사용한다.
- 공개 검사에서 일부 종목 조회 실패가 있었음(국내 3/20, 미국 1/30). 결과 제외·실패 건수/이유 표시. 합성 가격으로 대체하지 않음.
- WPF 배포 캡처는 Dispatcher idle 후 레이아웃을 렌더링해야 표 열 너비가 정확함.

## 검증 위치

`artifacts/workspace-feature-tests.txt`, `artifacts/feature-exe-public/result.json`, `artifacts/feature-clean-result/result.json`.
최종 EXE SHA-256: `349E2FEA948A8D7955C2A56A6C4B8E4FA440E21472D759A40B791AA9F21C89A0`.

```powershell
dotnet build Stock.sln -c Release
.\Stock.Tests\bin\Release\net10.0-windows\Stock.Tests.exe artifacts
dotnet publish Stock.App\Stock.App.csproj -p:PublishProfile=Win64
```

`--smoke-test <출력폴더>`는 고정 검증 자료, `--public-smoke <출력폴더>`는 공개 시세 읽기 검사다. 어느 쪽도 증권사 주문을 보내지 않는다.

## 2026-09-28 추가 요청 — 완료

- [x] 모드와 무관한 키 설정, 온라인 진입 시 실전 인증·실패 팝업·오프라인 복귀
- [x] 가상 재생 정지 버튼을 명확한 텍스트로 변경
- [x] 시장 현황 탭: 코스피·코스닥, 일평균 거래량, 당일 등락 5초 갱신
- [x] 결과 카드 및 이전 백테스트 기록 조회
- [x] 틱·일봉·주봉·월봉 표시 (실제 틱만 사용)
- [x] 첨부 이미지 3가지 매매 조건 설정과 데이터 제한 표시
- [x] 화면 소수점 제거(내부 정밀도 유지), 테스트·문서·EXE 갱신

이미지는 전략의 참고자료다. 종목 추천이나 실제 주문 실행 지시로 해석하지 않는다. 급락 원인·희석 위험은 자동 판별하지 않으며 사용자 확인 항목으로 모델링한다.

### 추가 요청 검증 진행 기록
2026-09-28: 1~7 요청의 코드·UI·문서를 반영했다. 솔루션 빌드 경고/오류 0, 280개 회귀 검사 통과(실제 계좌 주문 없음). WorkspaceFeatures.cs에서 인증 전환, 시장 탭, 틱 구독·캐시, 기록 복원을 분리했다. 루트 EXE 갱신 완료. 공개 시세 시장 탭·주/월 차트·국내/해외 백테스트 실행 성공. EXE만 있는 별도 폴더 실행도 종료 코드 0으로 성공했다. 승인된 구현에 남은 작업은 없다. 실제 키를 쓰는 인증·틱·체결은 미검증이며 키를 임의로 사용하지 않는다.

## 2026-09-28 지수 디자인·관심종목 요청 — 완료
- [x] 국내/해외 지수 카드, 실제 추이 미니 차트, 등락 색상
- [x] 테스트 결과를 백테스트·모의매매 버튼 바로 오른쪽으로 이동
- [x] 관심종목 탭, 종목 등록·해제, 그룹명1·그룹명2 및 그룹 편집·저장
- [x] 빌드·검증·문서·루트 EXE 갱신

최종 검증: Release 빌드 경고/오류 0, 295개 검사 통과. 루트 Stock.exe의 공개 데이터 검사 및 EXE만 있는 폴더의 실행 검사(종료 코드 0) 성공. 국내 2개/해외 3개 실제 지수 추이, 버튼 바로 옆 백테스트 결과, 관심종목 국내/미국 가격·그룹명·편집 표시 확인. 남은 승인된 구현 작업 없음.
증거: artifacts/watch-index-tests.txt, artifacts/watch-index-public/result.json 및 PNG, artifacts/watch-index-clean-result/result.json.
주요 수정: IndexCards.cs, WatchlistViewModel.cs, MainWindow.xaml, WorkspaceFeatures.cs, PublicMarketData.cs, WorkspaceModels.cs, UI/API/구조 문서. 실전 키·주문에 관한 기존 [!]는 그대로이며 이번 작업에서도 실계좌를 사용하지 않음.

## 2026-09-28 검색 조작 · 증권사별 연결
- [x] Enter 검색, 종목 클릭 조회, 차트 주기 탭
- [x] 증권사 선택 카드와 연결 성공 후 증권사별 암호화 저장
- [x] 키움 국내·미국, 한국투자·메리츠 국내 실전 어댑터와 회귀 검사
- [!] 미래에셋·삼성 개인 주식거래 API 계약 규격, 신한i indi 이용 환경 확인 필요
- [x] 문서·실행 파일 갱신

- [x] 관심그룹 복수 선택 스크리닝 및 시장·종목 키 기준 중복 제거
- [x] 온라인 기본 계좌 지정·변경과 선택 계좌 단독 주문 검증

지원 범위: 한국투자·메리츠는 국내주식 연결만 구현했다. 해외주식·틱 어댑터는 추가 구현 범위이며 현재 연결창에 국내 한정을 표시하고 해외 주문을 차단한다.
- [!] 실제 계좌 인증·체결은 개발 과정에서 실행하지 않음. 새 어댑터를 포함한 사용자 계좌 검증 필요.

현재 상태: UI와 실행 가능한 공개 API 범위 구현, 356개 회귀 검사 통과, 루트 EXE 갱신. 공개 실제 시세 EXE 검사 성공(관심그룹 2개/중복 1개 제외/2종목 조회 성공). 실제 키나 주문을 사용하지 않음. EXE만 있는 별도 폴더 실행도 성공(종료 코드 0).
수정: MainWindow.xaml(.cs), ConnectionDialog.cs, BrokerCredentialStore.cs, WorkspaceViewModel.cs, WorkspaceFeatures.cs, WatchlistViewModel.cs, BrokerConnection.cs, KisBroker.cs, MeritzBroker.cs, KiwoomBroker.cs, RemoteTradingSession.cs, ConnectionTests.cs, PublishedSmoke.cs, 로고 리소스 및 문서.
남은 작업: 미래에셋·삼성은 사용자 서비스/API 규격, 신한은 indi 환경 확인 뒤 별도 연동. 한국투자·메리츠 해외·틱은 다음 구현 범위로 명시. [!]를 연결 완료로 표시하지 않음.

검증 완료: artifacts/broker-workflow-tests.txt (356개), artifacts/broker-public/result.json, artifacts/broker-clean-result/result.json. 두 EXE 검사 모두 성공, BrokerTilesRendered=true, NoBrokerCalls=true. 루트 Stock.exe 66,328,496 bytes / SHA256 349E2FEA948A8D7955C2A56A6C4B8E4FA440E21472D759A40B791AA9F21C89A0. AGENTS.md는 EXE 옆 기존 사용자 원문을 유지했다. 현재 실행 중인 검사 프로세스 없음.

## 2026-09-29 UI·차트·전략 설정 요청 — 완료
- [x] 버튼 전역 Busy 깜빡임·동일 종목 중복 조회 개선, 검색 첫 결과 자동 조회·로딩
- [x] 데이터 없음/권한 문구, 오프라인 틱·분 제한, 1/3/5/10/15/30/60틱·분
- [x] 공통 콤보·버튼 스타일, 관심그룹 선택 가로 배치
- [x] 최신 당일 차트·확정봉 백테스트 분리, 우클릭 기간 지정·보정
- [x] 가상/선택 실계좌 체결 SQLite 영구 기록과 표시 체크
- [x] 토스 공식 API 국내 어댑터·분봉·연결 카드·키 저장
- [x] Standard/Advanced 모델·Mapper·Metadata·공통 도움말·Validation·SQLite 저장
- [x] 기존 엔진 보존, 새 설정의 실제 적용 및 두 모드 동일 결과 검증
- [x] 회귀 447개, WPF 버튼 클릭 172회·비활성 23회, 문서 갱신
- [x] 루트 EXE 갱신·공개 데이터/EXE 단독 실행 검사

현재: Release 빌드 경고0 오류0, 447개 통과(artifacts/full-revision-tests.txt). 도움말·팝업·콤보·기간·저장·토스 분봉 페이지 및 계좌 한정 검증. 청산 강도와 박스 붕괴 여유의 역방향 매핑을 수정하고 반등 되돌림 청산을 추가했으며 기존 Advanced=null 경로는 그대로다. 실제 계좌 인증·주문은 실행하지 않았다.
일반 Publish는 기존 refint/Stock.dll 잠금 CS2012로 실패하여 별도 artifacts/revision-build 출력으로 성공했다. publish/revision-win-x64/Stock.exe를 루트 Stock.exe에 복사 완료. artifacts/revision-public 및 artifacts/revision-clean-result의 검사가 모두 성공·종료0. 공개 데이터에서 국내·미국 9/28 최신 봉 확인. 관심그룹2·중복1제거·조회실패0. 공개 시장 목록 일부 실패(국내3/20, 미국1/30)는 제외·표시. VALIDATION.md에 최종 기록을 남겼다.

주요 파일·세부 구현·지원 한계는 docs/implementation-2026-09-29.md 참조. 실제 틱/뉴스/체결 방향 데이터가 없는 경우 임의 생성하지 않는다. 토스 국내·분봉만 지원, 해외·WebSocket 미구현. 기존 미래에셋·삼성 규격/신한 환경 및 실제 키 검증 [!]는 유지한다. 해결되지 않은 승인 대기 없음.
최종 실행 파일: Stock.exe 66444742 bytes / SHA256 FC3001E938540EE9E6AF66DB018EDD62830C39845CD948A076C4796FEA48DBC2. AGENTS.md는 EXE 옆에 유지. 검사 프로세스 종료 완료. 승인된 이번 구현 작업에 남은 항목 없음. 위 외부 연동/실제 계좌 검증 [!]는 임의 실행하지 않는다.

## 2026-09-29 Universe~Portfolio 파이프라인 및 정기매수 요청 — 완료
- [x] 모든 숫자 그룹 구분 표시 및 입력 파싱 유지
- [x] 단계별 Config/필터/틱떼기·농부매매/Portfolio 엔진
- [x] Manual/CSV/Mock 순위 Provider와 시점 검증
- [x] Standard/Advanced 공용 Config, Metadata 도움말/Validation/SQLite
- [x] 스크리닝·백테스트·실시간 신호·모의 실행 연결
- [x] 회귀/UI 검증·문서·단독 EXE 갱신
기존 C#/WPF/MVVM, 기존 세 전략과 일봉 기능은 유지. 새 파이프라인을 선택할 수 있게 추가하며 기존 저장 설정을 덮어쓰지 않는다. 실제 계좌 자동 주문/실제 자금 이체는 수행하지 않는다. Core 이동은 모의 포트폴리오 내부 배정으로 모델링한다.

추가 요청: 이미지 기반 종목/차트 UI 및 정기매수 백테스트(매일/매주/매월, 시가/종가, 금액/초기금/수수료, 결과카드·평가곡선·매수표시·기록). Core RecurringPurchaseBacktest와 RecurringWorkspace/RecurringPurchasePanel 작성 중. 배당 지급 자료 없음은 기능 비활성·안내로 처리. 단위가치 MDD로 외부 납입 영향 제거. 최근 파이프라인 첫 빌드 경고0 오류0 확인. EXE는 아직 이전 447개 검증 버전이다.

### 2026-09-30 재개 체크포인트
구현 완료: Pipeline Config/Mapper/Metadata/Provider/필터·두 전략·Portfolio, SQLite, 공용 백테스트·모의 재생·실시간 신호, 관심그룹 스크리닝. 정기매수 계산/결과/마커/평가곡선/기록과 이미지 기반 UI. 숫자 그룹 구분.
검증: 이전 실행 492개 통과·경고0 오류0. 최종 화면 압축/금액 알림/추가 숫자 서식 이후 마지막 전체 검사가 진행 중.
수정 파일·한계는 docs/pipeline-recurring-2026-09-30.md. 남은 작업: 최종 스크린샷 확인, publish 별도 artifacts 출력, 루트 EXE 복사, 공개 데이터/단독 실행 smoke, VALIDATION/TASKS 완료 표기.
현재 EXE는 아직 이전 버전이다. 다음 작업은 실행 중 회귀 결과 확인부터 시작.

최종 빌드/회귀 492개 완료·경고0 오류0. publish/pipeline-win-x64/Stock.exe 생성 및 루트 Stock.exe 갱신 완료. 현재 공개 데이터 EXE smoke 검사 중(artifacts/pipeline-public); 종료 후 단독 폴더 검사와 완료 기록 필요.

### 최종 완료 — 2026-09-30
- [x] 이미지 기반 종목·차트 UI, 정기매수 일/주/월·시가/종가·금액/수수료, 좌우 결과 카드, 실제 매수가 점·평가 곡선, SQLite 기록
- [x] 회귀 492개·Release 경고0 오류0·별도 artifacts publish
- [x] 공개 시세 EXE 검사: artifacts/pipeline-public-final/result.json 성공·종료0. 국내 9/29 확정봉, 미국 9/29 장중봉 표시/백테스트 제외, 정기매수 435거래일 계산. 관심그룹 2·중복1 제외·조회 실패0.
- [x] 최종 UI 제목 여백/마커 크기 변경 후 EXE 단독 검사: artifacts/pipeline-clean-final/result.json 성공·종료0. EXE 외 파일 없는 폴더에서 실행.
최종 EXE: Stock.exe 66,502,570 bytes / SHA256 2DF034BE3ED93392E5F68701D59B24DB728A8710571AB27A6B93B5E455A6E380.
AGENTS.md는 EXE 옆에 유지. 검사 프로세스 종료 완료. 새 화면/수치/설정/구조/한계는 docs/pipeline-recurring-2026-09-30.md, UI/architecture/API 문서에 기록.
공개 전체 목록 일부 조회 실패(국내3/20, 미국1/30)는 기존 제공처의 실패 항목을 제외·표시한 결과이며 전체 성공으로 숨기지 않는다.
남은 외부 자료 한계: Toss/Kiwoom 검색순위 미연동 → Manual/CSV/명시적 Mock, 배당 지급 자료 없음 → 재투자 비활성. 실제 틱/호가/보조 자료 누락은 보류한다. 실제 계좌 자동 주문과 Core 송금은 제공하지 않는다. 기존 브로커 [!] 항목은 유지.
위 중간 체크포인트의 진행 중 문구는 이 최종 기록으로 대체된다.
## 2026-09-30 ScottPlot 5 Raw Tick 차트 — 완료
- [x] 현재 WPF/MVVM/SQLite/체결/백테스트 구조 분석 및 ScottPlot.WPF 5.x 설치/API 확인
- [x] 실제 WPF 성능 POC 100K/250K/500K/750K/1M/1.5M/2M: 생성/render/pan/zoom/CPU/memory/GC/dispatcher 지연 실측 → 표시 상한 결정
- [x] 원본 보존 SQLite 복합 index/안정 keyset 범위 조회, 동일 timestamp row ID 탐색
- [x] chart-only cap, 24/12/6h 축소, 양방향 lazy/preload/빈구간 넘김, bounded cache/eviction, 취소/generation/viewport 유지
- [x] ScottPlot SignalXY 상대 시간축·밀리초 crosshair, 정밀 체결 marker/선택/상세/거래 확대
- [x] 실제 feed→SQLite→chart 연결, 과거 탐색 중 강제 최신 이동 방지
- [x] 백테스트 전체 raw 2M 소비 및 저장/순서/cap독립/race/marker 회귀, 문서/최종 단일 EXE 검증
제약: 차트 상한은 DB/백테스트로 전파하지 않음. 원본 삭제/다운샘플링 없음. 합성 tick은 별도 성능 DB 전용. 실자금 주문 금지.
분석: 현재 CandleChart는 직접 DrawingContext 렌더. live tick 메모리는 2,000개로 잘리지만 events SQLite 원본은 별도 저장한다. TradingStore events(id,market_key,source,at,kind,digest,json), ix_events(market_key,source,at), at=UTC ticks, id 안정 순서. ReadEvents는 현재 1M 제한이므로 전체 백테스트 경로와 새 차트 조회 API를 분리해 개선해야 한다. .NET10 WPF MVVM partial WorkspaceViewModel, DI는 생성자 주입, 체결/정기매수 결과 기존 SQLite state 재사용.

### ScottPlot 재개 체크포인트
설치: ScottPlot.WPF 5.1.59, SkiaSharp 3.119.0. 의존성 NU1701을 숨기지 않고 App/Tests의 Windows TFM을 net10.0-windows10.0.19041.0으로 맞춰 빌드 경고0 오류0.
구현: RawTickRepository(index at/id keyset), TickChartWindow(cache/cap/time 축소), RawTickChartModel(async 취소 generation/좌우 탐색), RawTickChartView(ScottPlot SignalXY/정밀 crosshair/marker/detail/zoom), RawTickWorkspace(새 8번째 탭과 원본1틱 버튼), TickTradeDetails(FIFO partial), RawTickIngestion(provider→DB).
기존 실제 feed는 품질 필터 전에 원본을 수집하고 1초/128건 단위 background SQLite 저장. StreamingBacktest는 UI 밖에서 전체 선택기간을 원래 at/id순으로 읽고 chart cap을 사용하지 않는다. Fill/BarFill에 원래 주문번호/이유를 추가해 새 결과에 보존한다.
검사: 별도 DB artifacts/raw-tick-checks/raw-fixture-fda085ae88a149cfa8c629ee839f3ac2.db에 2,000,000개(동일 timestamp 250,000개) 저장·전체paging·전체backtest. 입력 192,432ms, paging 약6,600ms, backtest 약21,800ms. chart 1,000 cap에서도 engine events/equity 모두2M. 29개 raw 검사 통과, WPF 실제 axis pan/marker pixel hit/요청경합/메모리합계 포함.
측정: artifacts/tick-performance/measurements.json 7개규모1차완료. 1200x700 WPF 실제 PaintSurface 이벤트/자동axis pan·zoom24회씩. 약100ms WPF표시주기 지연 관찰; 실제 사용자의 물리 마우스/체감은 미측정. RenderExecution/Process.Refresh 메모리 측정을 추가했으므로 재측정 필요.
진행: 전체 기존회귀 실행중. 탭 수7→8 검사 수정(기존오류는 새탭 수 기대값). 최근 좌표 X/Y 변환을 Task.Run으로 이동했고 아직 다음 빌드에 포함해야 함.
남은 작업: 진행중 검사 결과 확인 → 최근 좌표변환 최종빌드/Raw 검증 → 새 성능측정 → 보고서/필요한 성능보정 → 최종 전체회귀/단일 EXE publish/native라이브러리/깨끗한폴더실행.
현재 루트 EXE는 이전 파이프라인·정기매수 완료 버전이며 Raw Tick 배포본은 아직 아님.

최종 재검증: 좌표 준비 Task.Run·구간 이동 90% overlap·저장 소스 자동 선택 반영. Raw Tick 30개 및 전체 회귀 492개 통과, Release 경고0 오류0. docs/ui/RawTickPage.md, docs/api/RawTickData.md, architecture 갱신. 성능 재측정 실행 중이며 이후 publish/EXE 단독 검사를 남겼다.

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
## 2026-09-30 실계좌 전략 다중매매 — 완료 (2026-10-03)
사용자 구현 승인: 온라인 선택 계좌에서 체크 종목별 등록 전략을 실시간 주문하는 기능. 개발 중 실키/실제 주문 금지. 새 과매도/낙주 전략은 미승인으로 제외.
- [x] 기존 RemoteTradingSession/OrderGuard/등록 프리셋/실제 feed 지원 범위 분석
- [x] 종목별 독립 신호·실제 주문/부분체결 상태·계좌 직렬화/예산 예약/소유권/중지·취소/reconciliation 서비스
- [x] 검색·추가·체크·등록전략·한도·상태 팝업, 메인 상태/중지, 실행 스냅샷·목록 저장·재시작 중지
- [x] FakeBroker/FakeFeed 및 WPF 검사, 전체 회귀/문서/단일 EXE
분석: 실제 틱 feed는 키움이며 해외 시간 해석/실제 보조 데이터가 불완전하므로 첫 지원 범위는 키움 국내 원본 틱 기반 등록 전략으로 제한 검토. 일봉/Pipeline/미제공 뉴스·체결방향 필요 전략은 이유와 함께 실행 차단. 기존 보유 매도 금지, 원본 주문 접수는 체결로 간주하지 않음. 기존 루트 EXE(ScottPlot 522 검사 버전)는 새 검증 완료까지 유지.추가 UI 요청 (원래 다중매매 작업과 함께 진행): 참고 이미지 2개 view_image 확인 완료. 종목/차트 오른쪽 3개 내부 탭(실제 계좌 주문/전략 백테스트/정기매수 백테스트), 가운데 차트 Auto/* stretch로 바닥까지 채움, 헤더 재배치/5·20·60·120일선 범례, 모든 읽기 % 표시 둘째자리. 수수료 등 편집 원값은 반올림하지 않음. 이미지 수치/낙주전략은 복제하지 않음. 작은/큰 창 WPF 렌더 검증 필요.
### 2026-10-02 재개 체크포인트
진행: LiveStrategyBatch/LiveBatchModels, RemoteTradingSession/OrderGuard, KiwoomBroker 실제 개별 체결 및 현금 주문가능금액, MultiTradeWorkspace/ViewModel/Dialog 작성. 계좌 변경/오프라인/종료 시 stop 연결. 취소 로그 number/Number 충돌은 OriginalNumber/ReceiptNumber로 수정.
UI: MainWindow 오른쪽 주문·전략·정기매수 3탭, 중앙 차트 stretch, 5/20/60/120 범례, RecurringPurchasePanel 세로 배치. 읽기 % 두자리와 원래 편집값 분리 NumericDisplay template 추가.
검사: 517개(기존492+다중매매25) 통과한 후 추가 수정됨. 마지막 빌드는 Stock.Tests/MultiTradeUiTests.cs의 BatchFeed.Description 누락으로 오류1. 최신 전체회귀는 아직 미실행. 최신 추가: 일 손실의 명시적 재시작 간 누적 저장, 가짜 feed/clock DI, WPF 다중매매 수명주기·작은/큰창 3탭·수수료 정밀값 테스트.
남은 일: 누락 멤버 수정 → Fake Kiwoom execution API/중지·외부보유/예산 추가 검사 → 최신 전체회귀 및 WPF 크기별 스크린샷 확인 → 문서/빌드/publish → 단독 EXE smoke → 루트 Stock.exe 갱신.
현재 루트 EXE는 앞서 검증한 ScottPlot 버전이며 미검증 코드는 배포하지 않았음. 실제 키 변경/인증/실자금 주문 없음. 미승인 낙주·과매도 전략 구현 없음.
수정 파일은 위 Core/App 영역과 Stock.Tests/LiveBatchTests.cs, MultiTradeUiTests.cs, UiTests.cs, Program.cs 및 표시 문자열 관련 WorkspaceModels/TickTradeDetails/IndexCards/WorkspaceViewModel/RecurringWorkspace/MainViewModel.Data. 다음 시작점은 BatchFeed.Description 추가.

### 2026-10-03 재개 및 승인 변경
- 직전 실제 검증: artifacts/multi-regression.log 534개 통과, multi-build.log 경고0 오류0. 10/02의 517/Description 누락 기록은 이 결과로 대체. 이미지 tools-1160-2/tools-1540-1/multi-trade-popup 검토 완료.
- 이후 미검증 변경: LiveExecutionApiTests 공식 개별체결/현금 매핑 검사, LiveBatchTests 외부보유·현금예약·피드단절·계좌변경·당일 손실누적 5검사, LiveStrategyBatch 동일상태 중복 SQLite 쓰기 제거, StopMulti 피드종료 오류에도 정리 보장, popup 배경 명시.
- 직전 최종 빌드 명령은 자동 승인 검토 사용 한도로 실행되지 않음(위험 판정 아님). 10/03 동일 승인 경로 재시도 성공하여 multi-build-final/multi-regression-final 실행 중. 미검증 EXE 배포 금지.
- 새 사용자 명시 승인: 국내 과매도·낙주 반등 **일봉 백테스트 전용** 구현. 이전 '미승인 제외'는 아래 작업에 한해 대체. 실계좌 자동 실행 승인은 아님.
## 2026-10-03 국내 과매도·낙주 반등 일봉 백테스트 — 완료
선행 다중매매/UI 배포 완료 후 시작. 기존 WPF/MVVM/SQLite/백테스트 기록 재사용.
- [x] 시점별 universe/risk 입력·저장, Config/validation/정책 문서
- [x] Wilder RSI·MA·확정 pivot·근사 거래량 구간 provider 및 연결식 cluster
- [x] 종목별 상태기계·next-open 체결·분할매수·청산·재진입·계좌평가
- [x] 중앙 PerformanceCalculator·상세 snapshot 거래기록·설정/기록 SQLite
- [x] 전략 설정/일봉 백테스트 UI 및 누락자료 안내
- [x] 20개 이상 인과성/체결/지지선/비용/기간/저장 검사, 기존 회귀, 문서/EXE
요구사항 및 모호성 결정은 docs/oversold-rebound-spec.md에서 관리. 실제 거래대금/과거분류/당시 위험 자료 없으면 미지로 유지하고 진입 보류. 현재 목록을 과거 전체시장으로 가장하지 않는다. 현재 일봉 1,500 제한/MA999 warmup 부족 명시. OHLC에서 실제 장중 체결을 추정하지 않는다. 새 전략은 기존 실계좌 다중매매 지원 목록에 추가하지 않는다.
## 2026-10-03 다중매매·차트 도구 완료
- 최종 전체 회귀 547개 통과: artifacts/multi-regression-final.log. Release 경고0 오류0: multi-build-final.log.
- 추가 검증: 선택 단일계좌/독립 스냅샷/실제 부분체결/중복대사/소유권/취소접수와확정/unknown무재시도/응답 전후 stop경합/외부보유/예약현금/계좌변경/일손실복원. 공식 개별체결 HTTP fixture. 실제 돈 주문 없음.
- WPF 1540x1040,1160x760 각3탭, chart stretch, 팝업 수명주기와 offline stop, 원래0.015 수수료 보존 검사. artifacts/multi-ui-final.
- publish 로그 multi-publish.log 경고/오류 없음. 별도 EXE만 있는 multi-clean-exe에서 smoke 성공, multi-clean-result/result.json Success=true/RawNativeRendered=true/NoBrokerCalls=true, native libSkiaSharp 추출 확인.
- Stock.sln 옆 Stock.exe 갱신: 80,507,209 bytes, SHA256 8C59340055E00DA9ACE839613956B8EBAC5DFEF9BD547C89408C73975EBBA93B.
- 문서: docs/ui/MultiTrade.md, docs/ui/ChartTools.md, docs/api/LiveBatchKiwoom.md, architecture. 실제 계좌/물리 마우스/다른 모니터 DPI 이동 검증은 수행하지 않음.
- 선행 다중매매/UI 작업 완료. 새 과매도 일봉 전략은 별도 진행하며 이 EXE에는 아직 포함하지 않음.
### 2026-10-03 과매도 계산·화면 연결 중간 체크포인트
추가: Stock.Core/Research/Oversold의 Config/HistoricalFacts/SupportProviders/Results/Backtest, App/OversoldWorkspace·ParameterPanel·ResultDialog, Tests/OversoldTests. 이전 빌드 0경고/0오류 후 새 UI 연결 중. 현재 테스트 40여개 작성, 아직 전체 통과 전(소수점 RSI 비교 보정, WPF StackPanel FontSize 속성 보정 후 재실행).
완료한 코드: next-open 시점 분리, 고정 최초 cluster, 실제 시점 facts 필수, 순차지표/확정pivot/근사volume, 상세 성과·거래·판단/SQLite 참조, 파라미터 도움말·설정저장·실행·기록 popup. 미지 사실은 진입차단. 실제 주문 호출 없음.
남음: 단위검사 실패 확인·원인 수정, cooldown/지지AND/누락 캘린더·위험 입력 추가검증, UI 실행/재시작/크기검증, API/UI/architecture 문서, 전체 회귀, publish 및 단독 EXE 검사 후 루트 EXE 갱신. 루트 Stock.exe는 다중매매 547검사 검증 버전 유지.
### 2026-10-03 최신 검증 체크포인트 — 앞선 46개/미검증 기록 대체
- 새 Core 67검사 통과: artifacts/oversold-checks.log. support 추가매수 AND, 위험 AND, 고정 최초 lower, cooldown5마감, 예외옵션, 정지 중 TP보다 stop 우선, 분할정수0주, 미래facts/바시각, prefix invariance 포함.
- 전체 최신 회귀 621개 통과: artifacts/oversold-regression-final.log. Release 경고0 오류0: oversold-build-final.log. UI 새7검사(실행/상세/정밀값/재시작/JSON오류보존/두크기) 포함.
- WPF 실제 렌더 artifacts/oversold-ui-final. 1540×1040,1160×760 차트/오른쪽 도구, 설정, 성과/거래 popup 검토. 창 내용 캡처라 외곽 margin 투명도는 실제 OS 창 배경과 다름. 물리마우스/DPI이동 검증 아님.
- 당시 자료와 일봉 hash snapshot을 기존 state에 따로 저장. 설명은 중앙 ParameterAttribute; 도움말 높임/낮춤 효과 보완. JSON risk 생략/자료null 오류검증. 새전략 실주문 연결 없음.
- docs/ui/OversoldBacktest.md, docs/api/OversoldHistoricalFacts.md, architecture, 33항목 docs/oversold-implementation-report.md 작성.
- 현재 publish/oversold-win-x64 단일 EXE 생성 확인 단계. 다음: EXE만 복사한 새폴더에서 --smoke-test 실행, 새 OversoldTrades>0 및 RawNativeRendered/NoBrokerCalls 확인, native library 추출 확인, 루트 Stock.exe 복사/해시 기록, TASKS/VALIDATION/보고서 최종상태 갱신. 아직 루트는 선행 다중매매621이 아닌547검사 버전 유지.

## 2026-10-03 과매도·낙주 일봉 전략 최종 완료 — 위 중간 체크포인트 대체
- 새 Core67 + UI7 + 기존547 = 전체621검사 통과. 최종 Release warning0/error0. artifacts/oversold-regression-final.log / oversold-build-final.log.
- 단일 publish 완료: artifacts/oversold-publish.log. EXE만 있는 artifacts/oversold-clean-exe에서 종료0. oversold-clean-result/result.json Success=true, OversoldTrades=2, RawNativeRendered=true, RawTicks=2000, NoBrokerCalls=true. 근거는 TEST_FIXTURE이며 실제 시점 facts API나 실계좌 검증이 아님.
- libSkiaSharp.dll이 artifacts/oversold-native/Stock 하위에 추출되는 것 확인. WPF 두 크기 화면, 설정, 상세 팝업 검토 완료.
- 루트 Stock.sln 옆 Stock.exe 교체 및 publish 해시 일치 확인. 80,560,114 bytes / SHA256 190C9EE9A1364AFA1C3C820BDACD1A245DDAB42A6CBE91CC7391EFB18C70FDE3. AGENTS.md 옆 유지.
- 33항목 보고: docs/oversold-implementation-report.md. UI/API 입력형식/architecture/spec/VALIDATION 갱신. 코드와 UI, 설정/결과/판단·상태전이·시점자료/일봉 snapshot의 SQLite 저장 및 재조회 연결 완료.
- 실제 사용 한계: 과거 일반주식·관리/정지 상태/실제 거래대금/위험/검증된 시장일의 자동 API 어댑터 없음. 현재는 시점 JSON 입력과 누락시 진입보류. 시장 캘린더 자체를 외부 휴장일과 자동 대조하지 않음. 공개 1,500봉 한계, 지표 준비 부족 후보 제외. 일봉 근사 매물대/next-open 가정, 실제 장중 유동성이나 배당·기업행동 통합 재현 아님.
- 선행 다중매매/UI와 이번 일봉 전용 전략 모두 해당 승인 범위 완료. 실제 키 변경·인증·실자금 주문 없음. 기존 실계좌/API 미지원 관련 [!] 항목을 이번 완료로 해제하지 않는다. 자동으로 진행할 추가 승인 작업 없음.

## 2026-10-03 사용자 변경: 오늘의 낙주 전략만 유지 / 오른쪽 글자 최소14 — 완료 (최종 기록은 하단)
- [x] 구형 전략 설정·선택·등록·자동실행/프리셋 복원/스크리닝 경로 제거·차단, 기존 기록/DB 읽기 보존. 새 일봉 전략으로 통일.
- [x] 수동 주문/수동 모의매매/정기매수/차트 보존. 다중매매는 지원전략 없음 안내 및 시작 차단.
- [x] 오른쪽 3탭 내부 모든 글자/입력/버튼/결과/콤보/툴팁 최소14 DIP, 작은창 스크롤 검증.
- [x] 새 정책 회귀/도움말/레거시 복원 차단/WPF 렌더/문서/Release/단독 EXE 검증 및 갱신.
현재 배포는 앞선 낙주 포함621검사 버전. 새 요청 검증 전에는 이 EXE 유지. 실제 주문/DB 삭제 없음.

### 최신 진행 체크포인트
- OnlyOversoldWorkspace로 단일 낙주 전략의 설정/프리셋/스크리닝/백테스트 경로 통합. 구형 프리셋·틱/Pipeline·자동 재생 실행 차단. DB와 과거 기록 보존, 수동 거래와 정기매수 유지.
- MinimumTextSize를 오른쪽 RightToolsPanel에만 적용. 실제 템플릿/달력/툴팁 최소14 검사 및 1540/1160 세 탭 렌더 완료. 전략 모든 도움말 팝업 검사 포함 목표 UI41검사 통과.
- 전체 회귀에서 기존 해외 전략 스크리닝 기대값을 새 국내 전용 정책에 맞게 수정, 다시 실행 중. 최신 Release 0경고/0오류. 안내 문구/줄바꿈 보완 뒤 최종 빌드 및 단독 EXE 검증 예정.
- 작업 파일: OnlyOversoldWorkspace/MinimumTextSize/MainWindow/RecurringPurchasePanel/WorkspaceFeatures/WorkspaceViewModel/StrategyEditorViewModel/MultiTradeWorkspace/ViewModel/OversoldWorkspace/관련 UI검사/PublishedSmoke/docs. 루트 EXE 아직 이전621검사 버전.

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

## 2026-10-03 전략 파라미터 사용범위 분류 — 완료 (최종 검증은 하단)
- [x] 실제 코드 참조 추적, 중앙 Metadata 사용범위 분류. 백테스트 전용 / 공통 전략 조건 / 온라인 투자 전용.
- [x] 기존 세부 분야를 하위 그룹으로 유지, 온라인 미지원과 스크리닝 실제 사용 설명. 값/기본값/저장키/계산은 유지.
- [x] 전체 파라미터 누락·중복, 편집·저장·복원·기본값·validation·도움말·퍼센트 정밀도 검사. 크기별 WPF/UI 최소14 유지.
- [x] Release/문서/TASKS/VALIDATION/단독 EXE 검사 후 Stock.exe 갱신.
현재 배포: EAABCEBFD97DC25AFB42DBD3537B413CF1179B19423BD996917426936B8D9BCC. 새 분류 검증 전 유지. 실제 키/주문/DB 삭제 금지. 다음: OversoldConfig와 OversoldBacktest/ScreenOversold 참조에 근거해 메타데이터 및 화면 모델 추가.

### 파라미터 사용범위 중간 체크포인트
- 구현 완료: ParameterMetadata의 optional usage 정보, 중앙 OversoldParameterMetadata(58항목 명시분류/설명), OversoldWorkspace의 동일 필드 공유 투영, 세 영역 및 기존 분야 하위 Expander, 도움말 사용범위 표시.
- 실제 참조 확인: 백테스트7/공통51/온라인0. ScreenOversold의 하루·빈 포지션 평가에서 백테스트 가정은 신규 신호를 바꾸지 않지만 전체 validation(초기현금≥투자금 포함)은 유지됨. 감지정책은 비상손절/익절/추적 감지이며 지지 이탈 종가 규칙과 구분.
- Release 0경고/0오류, 대상 UI63검사 통과(기존43+새20). 전체 회귀 실행 중. 두 창 크기 가로맞춤·세로스크롤 확인. 캡처를 첫 화면과 온라인 안내로 나누는 테스트만 보완.
- 수정 파일: Stock.Core/Trading/ParameterMetadata.cs, 새 Research/Oversold/OversoldParameterMetadata.cs, App/OversoldWorkspace.cs·OversoldParameterPanel.xaml·ParameterHelpDialog.cs·PublishedSmoke.cs, Tests/ParameterScopeUiTests.cs·Program.cs·UiTests.cs, docs/ui 사용법. Config/엔진/기본값/저장키는 무변경.
- 다음: 전체 결과 확인→캡처 보완 포함 최종 빌드/UI→publish/단독 smoke(7/51/0 포함)/native확인→루트 EXE·해시·문서 완료. 현재 루트 EXE는 앞선 단일전략/최소14 버전 유지. 실제 계좌 주문 없음.

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

## 2026-10-04 파라미터 라벨·명시 행·고정 소수 입력 — 완료 (최종 검증은 하단)
- [x] 요청한 51개 라벨 중앙 변경, 7/51/0 사용범위 유지, 분야별 명시적 3열 행과 빈칸 모델.
- [x] 비율 목록 원소별2자리/MA 기간 목록 scalar 포맷 금지. compact 동일 입력 크기.
- [x] 고정2자리 덮어쓰기 공통 editor: caret/소수점/선택/붙여넣기/삭제/부호/콤마/빈값/IME. 고정밀 기존값 무편집 보존 및 실제 편집만2자리 저장.
- [x] 실제 WPF 이벤트·목록·정밀도·행·라벨·값/결과 보존 테스트와 작은/큰 렌더. Release/전체 회귀/문서/단독EXE/native/루트갱신.
이전 완료 배포: 80,571,237 bytes / EB2854DCEB2B11A4FBE644E23E59A2436315D61C54D99BA828357A37BE549FD6. Config 키/기본값/계산 의미/저장키/실제주문 경계 유지. 이번 고정2자리 편집은 전략 파라미터에 적용하며 과거 focus 원정밀도 표시정책을 대체하되 무편집 원값을 보존.

### 10/04 최신 재개 체크포인트 — 코드 연결 중, 아직 빌드 전
AGENTS/TASKS 재확인. 앞선 사용범위7/51/0 완료본 유지. 새 작업의 기존 수정부터 이어서 진행 중이며 중복 구현 없음. 이번 실행에서 사용한도 차단 오류는 관찰하지 못함.
- 중앙51라벨 변경(속성 키/기본값 무변경), OversoldParameterLayout 명시25행×3칸(빈칸 null), 기존 사용범위 Metadata와 연결.
- FixedDecimalEdit 순수 입력규칙과 FixedDecimalInput WPF 어댑터 신규 구현. 아직 테스트 전: 실제 source 무편집보존/선택·삭제·caret 변형을 검사해야 함.
- ParameterField.InputText(표시) / Value(원값) 분리, InputTooltip 정확값 안내, 목록 InputKind 명시. NumericDisplay의 목록/고정소수 scalar 포맷 차단.
- 수정파일: OversoldConfig(라벨만), OversoldParameterMetadata/Layout, ParameterMetadata, App/FixedDecimalEdit/Input, StrategyEditorViewModel, OversoldWorkspace, NumericDisplay. 남음: XAML 명시행·compact입력 연결→빌드→입력/행/UI/전체검사→문서/publish/native/EXE.
- 현재 루트 EXE는 EB2854... 이전 완료본. 실제 주문/DB삭제 없음. 재개 위치는 OversoldParameterPanel의 ItemsControl Rows/Cells와 InputText/FixedDecimalInput binding 연결.

### 10/04 입력·행 검증 체크포인트
- 51라벨/25명시행/58필드+17빈칸/compact180×32 연결 완료. fixed decimal 입력135 UI검사 통과(기존63+새72). 전체729검사 통과 artifacts/fixed-editor-regression.log. 실제 WPF 이벤트의 입력/선택/붙여넣기/삭제/Home/End/방향키/음수/IME/빈값/목록 count·sum/원정밀도 무편집보존 검증.
- XAML 태그/테스트 Thickness 인수 컴파일 오류 수정 완료. Release0경고/0오류. 화면 1160/1540 확인. Config/엔진 계산/기본값은 변경하지 않음.
- 추가 보완: 정수 InputText의 천단위 그룹 표시 유지 검사와 단독EXE의 FixedEditorVerified/25행 검사. 최신 대상 UI 재실행 중. 남음: 결과 확인, publish, 단독smoke와native확인, 루트EXE교체/해시 및 문서완료.
- 새/수정 파일: Core/OversoldParameterLayout·Metadata·Config 라벨 / App/FixedDecimalEdit·FixedDecimalInput·StrategyEditorViewModel·OversoldWorkspace·OversoldParameterPanel·NumericDisplay·PublishedSmoke / Tests/FixedEditorTests·RequestedLabelTests·ParameterScopeUiTests·OversoldUiTests·Program·UiTests.
- 현재 배포는 이전 사용범위 분류 버전 유지. 문서 docs/ui/FixedDecimalParameters.md 작성. 실제 주문/DB삭제 없음.

### 최종 배포 검사 보완
최신 전체730검사/대상UI136검사 통과, Release0경고/0오류. 첫 단독EXE 검사에서 탭 전환 후 layout 전에 파라미터 editor를 찾는 smoke 순서 오류(Sequence contains no matching element) 발견. 앱 계산/편집 검사들은 통과했으며 PublishedSmoke에서 Dispatcher layout 대기를 먼저 하도록 보정. 재publish 후 새 fixed-editor-clean-result-final 폴더로 재검사 예정. 루트 EXE 아직 EB2854... 이전 버전 유지. 실패 검사DB는 fixture 폴더에만 있음, 사용자DB 변화 없음.

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

## 2026-10-04 정수 삽입 / 소수부만 덮어쓰기 — 진행 중
- [~] FixedDecimalEdit/Input의 정수부 숫자를 삽입으로 복원, 소수2자리만 덮어쓰기. 직접 소수점 위치 선택과 연속 정수 입력의 경계를 구분해 자릿수 증가 지원.
- [ ] 부호/천단위/비율목록/선택/삭제/커서/붙여넣기·무편집정밀도보존 실제WPF 검증. 51라벨/25행/compact/7·51·0/최소14 유지.
- [ ] Release/회귀/문서/TASKS/단독EXE/native/루트EXE 갱신. 실제주문/DB삭제 없음.
현재 배포: A7710CB6700AD3977419362A8E96DC265B7B51B19086240F3FDCC31AAAC286E2(730검사). 새 요청 검증 전 유지.

## 2026-10-05 낙주 전략 온라인·스크리닝 확장 — 진행 중
사용자 최신 요청의 32개 구현/검증 항목은 docs/architecture/OversoldExpansion-20261005.md에 기록했다. 이전 일봉전용 제한은 검증된 새 경로부터 확장하며 실제 키/실제돈 주문/사용자자료 삭제는 개발 중 금지한다.
- [~] A 중단 숫자입력 수정: 소수점 바로 앞도 무조건 정수 삽입. 이전 ContinueInteger 특례 제거.
- [ ] B 계좌 선택 정리/키 표시/선택·전체 초기화(확인·활성 세션 정리/더미 검증).
- [ ] C 임의 Tick/Minute/Hour 집계·봉 연결·공통 순차 screening·funnel/snapshot/이벤트 상세.
- [ ] D 앱 서비스 다중매매/고정 세션/실제 fill 대사/3단계 중지/경합·예약·오류 격리.
- [ ] E 전체 회귀/WPF/문서/단독EXE/native/루트 배포.
현재 루트 EXE A7710... 유지. 다음: FixedDecimalEdit/Input의 caret 경계 단순화 및 실제 WPF 회귀, 이후 ConnectionDialog/기존 credential store 확장.

### 10/05 체크포인트 — A/B 및 C 기반
- A 최신 규칙 구현, WPF 149검사 통과(기존136+13). 정수/소수점앞 삽입, 소수점뒤 overwrite. ContinueInteger 임시상태 제거. 원정밀도/목록/25행 보존.
- B ConnectionDialog 4개선택/SecretKeyInput/초기화 UI, CredentialResetService 확인후 stop→기존DPAPI 삭제. BrokerCredentialStore 선택/전체 초기화는 지정된 .protected/default 파일만. 숨긴 broker 파일은 자동삭제 안함. Workspace PrepareCredentialReset / RemoteTradingSession.Revoke 신규주문 차단. 실제 초기화 미실행, dummy 검증.
- A/B 기준 전체756검사 통과 expansion-regression.log, 빌드0경고0오류. 현재 후속 C 변경 전 검증 결과이며 최신 C 전체결과 아님.
- C 진행: BarSpecification/StrategyBarAggregator(개별체결량·정확대금·세션·미완성·누락), StrategyBarTests 신규. OversoldEntryDetector에 기존 flat-entry 조건/반등 상태 추출, OversoldBacktest에서 재사용하도록 수정. OversoldScreeningEngine 순차warmup 및 immutable run/funnel 기반 추가. 아직 UI/provider 연결과 C 검증 미완료.
- 다음: 최신 빌드 및 Oversold 기존회귀 확인→C 스크리닝 UI/provider/결과팝업 연결, 이벤트·holding 공유→D. 현재 루트EXE 이전A7710... 보존. 새 집계/엔진 아직 미배포. 실제키/실주문/사용자DB삭제 없음.

### 10/05 재개 및 자동 승인 검토 제한
- [!] D의 실계좌 자동주문 연결 변경은 자동 승인 검토가 거부했다. 정확한 이유: "이 변경은 기존 실계좌 자동매매 경로에 낙주 전략을 연결해 실제 주문이 발생할 수 있는 범위를 확장하며, 해당 구체적 자동주문 활성화에 대한 신뢰할 수 있는 명시 승인이 없습니다."
- 거부된 작업: LiveStrategyBatch 기존 실계좌 실행 경로에 OversoldLiveStrategy와 분할 진입/청산 수량·실제 체결 적용을 연결하는 변경. 해당 명령은 실행되지 않았다. 우회/재시도 금지. 필요한 승인 범위: 낙주 전략을 선택 단일 실계좌에서 자동 주문할 수 있도록 연결하는 코드 변경(개발 중 실제돈 주문은 별도 금지).
- 사용자가 재개시 영향 없는 A/B/C 및 FakeBroker 검증을 먼저 완료하도록 전달했다. LiveStrategyBatch와 다중매매 UI의 기존 사용차단 유지. LiveBatchModels의 중간 확장도 제거. OversoldLiveStrategy.cs는 미연결 순수 계산 초안이며 실거래 지원 완료를 뜻하지 않는다.
- C 안전 범위: 임의봉 집계/원본검증, 순차 진입 detector+position rules 공통화, immutable screening run/0건 결과팝업/행 상세, 판단시점cluster·추가매수·지지/추적 snapshot→backtest 체결 연결 구현. 104개 확장/Core검사 통과. 이전 전체777검사 이후 UI/추가검사 변경은 최종회귀 대기.
- 다음: UI156예상 검사 및전체회귀, fake-only 격리 검사, docs32항목 실제 완료범위 정리, 단독EXE smoke/native 검사 후 검증된 안전범위 배포. 실제 API 과거분봉은 확정시각·정확거래대금 계약 부족으로 명시 자료부족 처리; 종가×거래량 대체 금지.

### 10/05 안전 범위 최종 검증 중
- 최신 전체808검사 통과(expansion-regression-final.log): A/B/C UI와 FakeBroker 단독 런타임 6검사, 새 지표/집계/immutable snapshot 검사 포함. 이후 천단위 봉 입력2검사, 결과창 흰 배경/설정 표시, 단독EXE 검증항목을 보완하고 expansion-regression-verified.log 재실행 중. 최신빌드 경고0/오류0.
- 새 실계좌 자동매매 경로 미연결. 거부된 명령의 LiveStrategyBatch 변경은 실행되지 않았고 기존 차단 유지. 독립 OversoldLiveStrategy 파일은 계산 초안/FakeBroker 단독검사용.
- UI: 일봉/지정봉(임의 양의 정수·틱분시), 최신/진행/0건 결과, 단계별 통과·실패·미평가·자료부족, 독립 MA/Pivot/zone 진단, 조건별 상세와 기존 신호·체결 조회. 설정과 run 결과 SQLite 보존, 행클릭 상세. 수신체결 coverage 증명 없는 자료는 자료부족.
- 문서: docs/ui/ScreeningResults.md, CredentialReset.md 신규. FixedDecimalParameters/아키텍처/32항목명세 갱신. 아직 새 EXE 배포 전: 루트A7710... 유지.
- 다음: 검증810 예상 결과 확인→publish expansion-win-x64→신규 clean 폴더 smoke(숫자경계/4broker/ScreeningSnapshot 포함)→native추출/루트EXE hash→VALIDATION/TASKS 실제완료보고.

## 2026-10-05 A/B 및 C 안전 구현 범위 — 검증·배포 완료
- [x] A 정수부/소수점 바로 앞 일반 삽입, 소수점 뒤 두 자리 overwrite. 10/04 중단 항목은 이 최신 규칙과 완료 기록으로 대체. 원정밀값 무편집보존/비율목록/천단위/25행/7·51·0 유지.
- [x] B 4브로커 선택·키 표시·선택/전체 초기화 확인·취소·세션 정리·기존 DPAPI 삭제 범위·더미키 회귀. 거래 DB 보존. 제거 브로커 자료 자동삭제 없음.
- [x] C 검증된 구현: 임의 BarSpecification/틱·분·시 집계·봉기준 계산/순차 반등 detector/공통 position rules/funnel 독립가지/immutable 저장/0건·진행 결과창/관측가격·RSI·낙폭·거래량·지지수/기존 전략 신호·모형체결 상세와 판단 당시 cluster·추가진입·stop snapshot.
- [~] C 후속: 실제 과거 원본 API 확정구간·정확대금 adapter, 모든 실패한 추가진입의 매봉 감사, 과거 run 선택 UI, 실거래 이벤트 연결. 현재 검증된 저장체결/기존 일봉은 사용하고 부족하면 명시 자료부족. 과거 상태·위험·세션 JSON 필요.
- [!] D 새 낙주 실계좌 자동주문 연결은 위 자동 승인 검토 거부로 미연결. 기존 CanOpenMulti=false 유지. 거부 명령을 우회·반복하지 않음. 새 popup/session/다종목 통합·3stop·live 대사는 후속. OversoldLiveStrategy는 독립 계산/FakeBroker 검사만 된 미연결 초안.
- [x] 안전 범위 최종 Release 경고0 오류0(expansion-build-final.log). 전체810검사(expansion-regression-verified.log) 통과. 최종 UI만 변경한 뒤 대상156검사(expansion-ui-final.log) 통과; 중복이므로 합산 금지. 숫자/더미키/시점·집계/immutable/FakeBroker·기존일봉67/WPF두크기 검증.
- [x] 최종 publish expansion-publish-final.log. 새 EXE 전용폴더 smoke 종료0: expansion-clean-result-final/result.json Success/IntegerInsertionVerified/ScreeningSnapshotVerified/FixedEditorVerified/RawNativeRendered/NoBrokerCalls=true, OnlyOversold/LegacyCommandsBlocked=true, OversoldTrades=2, RecurringSessions=234, RawTicks=2000. expansion-native-final/Stock/Pcb-rmJ6bUQK/libSkiaSharp.dll 추출(11,439,160bytes) 확인.
- [x] Stock.sln 옆 Stock.exe 갱신, AGENTS.md 옆 유지. 80,624,331 bytes / SHA256 406FEBF652280F0230CB75EAE42315AD96EA8C305A7E51F6E97E16BA81C7EB95. publish와 일치. artifacts/expansion-release.json.
- 문서: docs/architecture/ExpansionDelivery-20261005.md 파일별 보고와 한계, OversoldExpansion-20261005.md 32항목 실제상태, docs/ui/ScreeningResults.md·CredentialReset.md·FixedDecimalParameters.md, 기존 ScreenerPage/StrategyPage/architecture/VALIDATION 갱신.
- 실제 사용자 키/실제돈 주문/사용자 키 초기화/중요DB삭제 없음. 물리 입력·DPI 이동 미검증. 원래 전체 확장 요청은 D 및 C후속이 남아 있으므로 전체완료로 처리하지 않는다. 다음 시작은 이 체크포인트와 [!] 승인범위를 확인하고, 미연결 초안을 실계좌에 임의로 연결하지 않는다.

## 2026-10-06 현재 스크리닝 / 장중 일봉 갱신 — 진행 중
- [~] 현재 후보 평가를 과거 시점 필수자료 정책과 분리. OHLCV 조건·반등 warmup 계산, 미확인 상태/거래대금/위험 명시, 자동주문 신호와 구분. 과거백테스트 엄격검증 유지.
- [~] 선택 종목 시세 단일 루프 및 1초 UI 갱신, 제공처 pollingInterval/실제 체결 재사용, 당일봉 미완성 분리, stale 응답/취소/backoff/팬줌 보존.
- [ ] FakeClock/provider·WPF·전체 회귀, 문서, 단독 EXE/native 검사 및 루트 Stock.exe 배포.
- 확인: 네이버 공개 polling 응답에 pollingInterval=7000ms가 있음. 공개 API를 1초마다 호출하지 않고 공급자 간격을 준수하며 UI는 1초 갱신. 실제키/주문/초기화 및 거부된 자동주문 경로 변경 없음.


## 2026-10-10 계정 전환 인수인계
- HANDOFF.md 추가, AGENTS/README 진입 링크와 현재 목록 보완. 문서만 수정, 제품 소스/키/DB/루트EXE 변경 없음.
- 현재 소스 Release 검증 시 테스트39행 CS1525 2건, 경고0. artifacts/handoff-20261010-build.log. 테스트 실행/새 배포 미완료; 재개할 파일·조건·명령은 HANDOFF에 기록.
- 루트EXE 해시는 HANDOFF와 동일한 10/05 배포본. 10/06 작업은 사용한도 중단. 신규 계정이 소스 전체를 열고 컴파일 오류부터 재개할 것.
## 2026-10-10 현재 일봉 스크리닝 / 장중 표시 완료
- [x] 과거 JSON 없이 현재 일봉의 기술적 조건/반등 평가. 자료 미확인 후보와 자동주문 신호 구분, 과거백테스트 엄격검증 유지.
- [x] 1초 UI 반영·제공처 간격 준수·당일 OHLCV 및 관측시간·진행봉 분리·팬줌/종목변경 처리.
- [x] CurrentDailyUiTests 39행 컴파일 오류 및 null 경고 수정. 새 WPF 검사를 Program/UiTests에 연결; 바인딩을 제거하던 검사 코드는 SetCurrentValue로 보정. OnlyOversoldUiTests의 이전 JSON 강제탈락 기대값을 현재 후보/미확인/주문불가 검사로 변경.
- [x] Release 경고0 오류0: artifacts/resume-build.log. 대상95, WPF164, 전체846 검사 통과(서로 중복이므로 합산 금지). resume-target.log/resume-ui.log/resume-regression.log.
- [x] artifacts/current-daily-publish 배포. current-daily-clean-a4d0bb1754134b6688c385634487bb28/result/result.json Success/RawNativeRendered/NoBrokerCalls=true, 종료0. 루트 Stock.exe 교체 및 publish 해시 일치: 470A1A6C214212E80A51523273FB488FD6D2D68B6B0F608ECCA88147A703ADA4 . 이전EXE artifacts/Stock-before-current-daily-406FEBF6.exe 보관.
- 문서 docs/ui/CurrentDailyScreening.md 및 architecture/README/TASKS/HANDOFF/VALIDATION 갱신.
- 실제 장중 증권사 수신·물리마우스/DPI 검증은 미실행. 공개 시세는 응답 갱신간격(기본7초), 화면만1초. 실주문/키변경/사용자DB삭제 없음. 자동매매 [!] 및 C 후속은 여전히 남음.



## 현재 작업 2026-10-13 — 낙주 백테스트 0건 진단·검증 전략 3개·스크리닝 백테스트 팝업 [x]
- [x] 세션 배열이 없으면 일봉이 있어도 즉시 종료하던 원인을 수정했다. 실제 완성 일봉 날짜에서만 KRX 세션을 파생해 날짜별 판단과 상태전이를 항상 기록한다.
- [x] 삼성전자 공개 일봉 1,500개로 기대 결과를 검증한 `백테스트 검증 1 · 1일 보유`, `2 · 초단기 익절`, `3 · 지지이탈`을 일반 전략 프리셋으로 자동 저장한다. 각각 MaxHoldingDays, TP1, SupportBreak가 실제 발생했다.
- [x] 종목/차트와 스크리닝 팝업이 공통 `IOversoldBacktestService`/`OversoldBacktest`를 사용한다. 후보 더블클릭과 `백테스트 보기`도 같은 `OpenScreeningStock` 경로다.
- [x] 팝업에 실행 당시 Bar/Config/Facts/기간, 성과 요약, 거래표, marker, 판단 당시 Support Cluster zone·상세, marker/행 연동, 이전/다음, 요청 취소 및 해시 cache를 연결했다. 지정 틱/분/시는 기존 일봉 체결 엔진의 범위 밖이라 검증 봉과 저장 체결만 표시하며 가짜 백테스트 체결을 만들지 않는다.
- [x] Release 빌드 경고0/오류0, 낙주 Core70, WPF186, 삼성 공개일봉6, 전체896 검사 통과. 전체 첫 샌드박스 실행의 가짜 주문 저널 쓰기 접근 거부는 일반 Windows 권한 재실행에서 통과했다. 실제 계좌 주문·키·사용자 DB 변경 없음.
- [x] 최종 publish와 EXE 단독 smoke 종료0. Success/FixedEditorVerified/ScreeningSnapshotVerified/RawNativeRendered/NoBrokerCalls=true. 새 게시본 80,683,012 bytes, SHA256 `802EE3EAA7EF81C11937E8C57BFC0761C70BFA1A239B96604E5FC72E6829A31F`.
- [!] 기존 루트 `Stock.exe`가 다른 프로세스에 의해 잠겨 교체할 수 없었다. 같은 위치에 검증된 `Stock-검증완료-20261013.exe`를 두었으며 게시본과 해시가 같다. 기존 루트는 변경되지 않았고 백업 `artifacts/Stock-before-backtest-popup-8F97D6F7.exe`를 보존했다. 앱을 닫은 다음 새 파일로 루트 교체 가능하다.

## 현재 작업 2026-10-20 — 차트 범위·가용성·키움 틱/분봉·Git 보호 [x]
- [x] 1/3/6개월·1/3/5/10년을 일·주·월봉과 같은 선택색 RadioButton으로 변경했다. 일목의 미래 슬롯 예약을 제거해 마지막 실제 봉이 오른쪽 끝까지 차며 3봉×7기간=21개 조합의 확대·이동 렌더를 검증했다.
- [x] API/원본이 없어 사용할 수 없는 틱·분 선택기를 비활성화하고 연결 계좌의 지원 능력에 따라 갱신한다.
- [x] 키움 국내 `ka10079` 원본 1틱과 `ka10080` 1분봉 REST 이력을 구현했다. N틱/N분 집계 후 종목·차트에 직접 표시하며 틱 선택 시 `원본 틱` 탭으로 강제 이동하지 않는다.
- [x] 실제 체결과 모의 체결 체크를 분리했다. 모의매매와 실시간 모의 전략 체결은 공통 marker로 틱·분·일·주·월봉에 표시되고 기존 한 번 클릭 상세를 사용한다.
- [x] 계좌키·토큰·로컬 DB·빌드/게시 산출물을 제외하는 `.gitignore`와 `docs/security/GitPrivacy.md`를 추가했다. 임시 Git 저장소에서 민감 경로 6개 제외와 공식 명세 추적 가능을 검증했다.
- [x] Release 빌드 경고0/오류0, 전체 회귀 962개, 확장 WPF UI 345개, 게시본/루트 smoke를 통과했다. 단일 게시본과 루트 `Stock.exe`는 190,689,012 bytes, SHA256 `D2990AD476CC5CE2DE5B2B67287A0E8BD26C1BA780915A9CD84096A1EC0932A9`로 일치한다.
- 실제 키·실계좌 주문·사용자 운영 DB 삭제는 수행하지 않았다. `ka10079`/`ka10080`은 공식 형식의 가짜 HTTP 응답으로 검증했으며 실제 키움 계정·장중 권한 검증은 사용자가 연결한 환경에서 확인해야 한다.

