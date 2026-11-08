# 오프라인·온라인 기능 점검 — 2026-11-08

**최신 정정:** [일반 실행 창 직접 입력/화면 멈춤 수정](NATIVE_SEARCH_FIX_20261108.md). 일반 EXE의 실제 입력에서 조회 완료 후 로딩 프레임 잔류를 재현했다. 아래 초기 검사의 명령 직접 호출/RenderTargetBitmap은 실제 창의 표시 실패를 놓쳤다. Win32 화면 갱신 경로를 수정하고 새 EXE의 실제 키보드·마우스/Enter·반복 검색·재실행4회 및 화면 픽셀 검사를 통과했다. 아래의 미재현 결론은 초기 검사 당시의 기록이며 최신 결론이 아니다.

## 결론과 구분

사용자는 오류 팝업이 아니라 멈춤/무한 로딩처럼 보였다고 설명했다. 같은 증상으로 이어질 수 있는 무응답 제공처 경로를 결함 주입으로 재현해 수정했다. 다만 사용자가 실행한 환경(Windows/Mac/IDE)과 종목은 아직 확인되지 않았으며, 정상 공개 조회는 현재 Windows에서 성공했다. 사용자 환경의 정확한 원인을 확정 또는 해결 완료로 표시하지 않는다.

이번에는 합성 fixture 외에 **실제 공개 HTTP 응답 → 실제 검색 TextBox/버튼·Enter → 동일 ViewModel/서비스 → ScottPlot 렌더링**, 그리고 **배포 단일 EXE → 실제 공개 자료 → native 창의 차트 렌더링**을 검사했다. 별도로 발견한 시간 초과 오인과 감시 종료 교착을 수정했다.

원본 WPF 소스/루트 Stock.exe/사용자 자료는 유지했다. 공통 Core80개는 원본과 동일하다. 실제 계좌키·사용자 DB를 가져오지 않았고 실계좌 주문은 실행하지 않았다. GitHub 게시 없음.

## 실제로 재현해 수정한 문제

### 1. 제공처 시간 초과를 사용자 취소로 오인

HttpClient의 시간 초과도 OperationCanceledException/TaskCanceledException을 발생시킨다. 기존 ViewModel이 이 예외를 무조건 사용자 취소로 처리해, 오류 팝업 없이 빈 차트가 남을 수 있었다. `workflow-timeout-before.log`에서 오류 알림 검사가 실패함을 확인했다.

이제 호출자 토큰이 실제로 취소됐을 때만 취소로 처리한다. 나머지 시간 초과는 `OperationError.ResponseTimeout`의 한국어 오류로 표시한다. 검색/차트, 백테스트, 정기매수, 결과 팝업, 계좌 인증, 실시간 준비에 적용했다. 일반 스크리닝과 실시간 준비의 종목별 시간 초과는 다른 종목을 일괄 취소하지 않고 해당 종목의 자료 오류로 기록한다.

실시간 시세 수신의 종목별 시간 초과도 일반 조회 실패처럼 격리하며, 일부 누락과 전체 미수신을 구분한다. 모든 수신이 반복 실패하면 기존 진입 차단 정책을 따른다. 주문 재시도를 추가하지 않았다.

### 2. 실시간 감시 중 UI 스레드 종료 교착

활성 감시를 가진 ViewModel을 UI 스레드에서 Dispose할 때, 내부 비동기 종료의 continuation이 같은 UI 컨텍스트로 돌아오기를 기다려 프로세스가 종료되지 않는 문제를 재현했다. 검사37개까지 출력한 뒤 프로세스가 남았으며, 해당 격리 검사 프로세스만 확인 후 종료했다.

RealtimeScreeningWorkspace/RealtimeScreeningHost의 종료 대기에 ConfigureAwait(false)를 적용했다. UI 타이머 중지·이벤트 해제는 await 전에 수행하고, 이후 continuation은 UI 객체를 수정하지 않는다. 활성 감시가 있는 상태에서 Dispose가5초 이내에 끝나는 검사를 추가했고 현재 정상 종료한다. 기존 보유분을 시장가 청산하는 동작은 추가하지 않았다.

### 3. 제공처가 응답·취소에 반응하지 않을 때 검색 명령이 계속 잠김

완료되지 않는 Task를 반환하는 제공처를 주입하면 기존 조회는 로딩 및 검색 명령을 무기한 유지할 수 있었다. 이제 검색·차트 조회를 포함한 메인 조회에 기본45초의 전체 대기 제한을 적용한다. `AppServices.QueryTimeout`에서 중앙 관리하고 테스트에서는 짧은 제한을 주입한다. 제한이 지나면 요청 토큰을 취소하고 오류 안내와 함께 로딩·검색 버튼을 해제한다. 재시도는 사용자가 결정한다.

화면의 `작업 취소` 버튼도 제공처가 실제로 종료되기를 기다리지 않는다. 실제 버튼 입력 검사에서 무응답 Task가 남아 있어도 로딩과 검색 잠금이 풀림을 확인했다. 뒤늦은 응답은 기존 토큰/세대 검사로 새 종목을 덮어쓰지 못한다. 이 제한은 실계좌 주문 재시도나 전략 실행 제한에 적용하지 않는다.

세 수정 모두 코드에서 재현한 결함이다. 사용자의 최초 실패가 정확히 어느 경로인지 단정하지 않는다.

## 기능별 확인표

| 영역 | 이번 확인 | 구분 |
|---|---|---|
| 국내·해외 종목 검색 | 삼성전자/SK하이닉스/애플, 실제 입력·마우스 버튼·Enter | 실제 공개 자료 |
| 일봉 차트 | 국내1500봉/애플2514봉 수신, 선택 기간61/65봉 실제 렌더 | 실제 공개 자료+배포 EXE |
| 기간/차트 조작 | 1개월~10년, 휠 중심 확대·드래그·표식 병합·우클릭 전체/상세 | 실제 자료+fixture 입력 이벤트 |
| 현재가 | 공개 polling 응답과 제목 가격 바인딩,1초 pump | 실제 응답/주기 동작은 fixture도 병행; 장중 실제 변동은 휴장일이라 미검증 |
| 전략 백테스트 | 삼성전자 실제 일봉의 판단·체결·표식·저장 기록 | 실제 공개 자료 |
| 전략 D | 공통 런타임·계산·재생·가상 실행·설정·당시 지지 | Core192+Avalonia fixture; 실제 증권사 원본 체결 미검증 |
| 정기매수 | 실제 삼성전자 일봉으로 계산·결과·기록 재열기 | 실제 공개 자료+fixture |
| 일반 스크리닝 | 실제 코스피 상위10 전체 평가·관측가격·결과 팝업 | 실제 공개 자료 |
| 대상 시장 | 코스닥 상위 대상, S&P500501/나스닥100101/중복 제거516 | 실제 공개 목록 응답 |
| 실시간 스크리닝 | 후보·선택 행 유지·표시·전략 변경·탭 이탈·오류 격리 | 장중 fixture |
| 장외 실시간 시작 | 주말 실제 실행에서 장외 팝업·미시작 | 실제 현재 날짜와 공개 자료 |
| 온라인 계좌 연결 | 키움/한투/메리츠/토스 선택·잔고/주문 조회·모드 재사용·부분/전체 초기화 | 가짜 broker; 실제 인증 아님 |
| 온라인 분·시간 차트/백테스트 | 연결 계좌 분 자료로5분/1시간 집계와 공통 엔진 실행 | 가짜 broker; 실제 증권사 조회 아님 |
| 다량 가상 실행 | 종목별 설정·예산 고정·중복 방지·팝업 닫기/재열기·신규 진입 해제·긴급중지 | 저장/모의 스트림 fixture |
| 실제 주문 | 전환판에서 잠김을 유지 | 실제 주문 미실행, 전송 UI 미구현 |
| 전략 설정 | 저장/복구/기본값/추가/이름 변경/JSON 입출력/정밀도·문화권 | 격리 저장소+UI fixture |
| 관심종목 | 등록·그룹 추가/이름 변경·선택 차트·그룹에서 제거 | 실제 검색 종목+격리 저장소/fixture |
| 시장 지수 | 코스피/코스닥/S&P500/나스닥/다우의 값과 추이 | 실제 공개 자료5개 |
| 테스트 기록 | 백테스트·스크리닝·정기매수·D 기록, 당시 설정/가격/체결 재열기 | 실제 자료+격리 저장소/fixture |
| 오류·중단 | HTTP 실패/시간 초과/사용자 취소/늦은 응답/종목별 누락/감시 종료 | 결함 주입 fixture, 실패 전후 비교 |

지원 옵션의 모든 숫자 조합·모든 종목·모든 시장 상황을 검사했다는 뜻은 아니다. 예를 들어10년 선택에서도 공개 제공처가 반환한 실제1500일봉만 사용하며 없는 봉을 만들지 않는다.

## 검사 결과와 근거

- 전체 solution Release: 경고0 / 오류0.
- Portable Core192개: 동일 전략D/일중 런타임의 계산·보호·동시성/설정.
- Avalonia 기능 회귀116개: 기존74개+확장42개. 설정/계좌/오류/종목별 누락/활성 감시 종료/무응답 조회 및 실제 취소 버튼을 포함한다.
- 디자인·실제 입력 렌더16개.
- 실제 공개 자료28개: `artifacts/live-public-loading-final.log`. 기본 실행 때 외부 네트워크를 강제하지 않도록 별도 모드다.
- 공유 코어/증권사 API 계약 원본965개: `artifacts/reference-contracts.log`. 같은80개 소스의 기존 fixture 검사 메서드를 원본 수정 없이 호출했다. HTTP handler는 가짜이며 주문도 모형 요청이다. WPF UI를 호출하지 않았고 실제 인증/거래 성공의 증거가 아니다.
- 최종 루트 EXE의 실제 공개 자료 모드와 합성 시작 모드 각각 Exit0/Success/NoBrokerCalls. 실제 모드의 삼성전자·SK하이닉스61봉/애플65봉은 RenderedCandles와 일치했다.
- 실제 공개 자료 EXE 측정은 각각 약970/633/637ms(조회와500ms 렌더 대기 포함). 특정 환경1회 측정이며 성능 보장값이 아니다.

최종 로그: `artifacts/loading-final-verification.log`, `live-public-loading-final.log`, `reference-contracts.log`, `loading-native-public/result.json`, `loading-native-synthetic/result.json`. 실제 native PNG3개는 `loading-native-public/005930.png`, `000660.png`, `AAPL.png`. 최초 실패 재현 로그는 `workflow-timeout-before.log`, 이전 회귀 이력은 `full-mode-final-verification.log`에 보존한다.

## 수정 파일

- `Stock.Avalonia/AppServices.cs`, `ViewModels/MarketWorkspace.cs`: 메인 조회45초 제한·즉시 취소·검색 잠금 해제.
- `Stock.Avalonia/ViewModels/OperationError.cs`: 시간 초과 표시 정책.
- MarketWorkspace, ResearchWorkspace, RecurringWorkspace, ScreeningWorkspace, RealtimeScreeningWorkspace, ResearchBrowserViewModel, DayTradingViewModel, AccountWorkspace: 호출자 취소 구분 및 오류 알림.
- `Services/RealtimeScreeningHost.cs`: 종목별 시간 초과 격리·수신 상태·UI 종료 교착 방지.
- `App.axaml.cs`, `NativePublicProbe.cs`: `--verify-public` 명시 검사 모드. 임시 DB/실제 공개 자료만 사용하며 broker factory를 차단한다.
- `Stock.Desktop.Tests/WorkflowChecks.cs`, `LivePublicChecks.cs`, `OnlineDesktopChecks.cs`, `Program.cs`: 실제 입력·공개 자료와 확장 회귀.
- `scripts/verify-public.ps1/.sh`, AGENTS/TASKS/HANDOFF 및 이 문서.

## 배포 파일

최신 루트 `Stock.Avalonia.exe`:107,277,111bytes, SHA256 **F86BCC3FF384669B3C85A8A5B698DEAC9C9BA03303B5309A8D463DF9FDD6F09A**. 직접 입력 화면 수정 전C302판은 `artifacts/before-native-presentation-fix-20261108/Stock.Avalonia.exe`, 무응답 대기 제한 전40290판은 `artifacts/before-unbounded-query-fix-20261108/Stock.Avalonia.exe`, 그 이전 디자인판은 `artifacts/before-mode-validation-20261108/Stock.Avalonia.exe`에 보존했다. osx-arm64/x64 배포 폴더와 개발용 `.app`도 같은 소스로 갱신했다. 원본 WPF 루트 Stock.exe는 E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934를 유지한다.

## 사용자 환경 재현에 필요한 것

남은 정보는 실패한 실행 환경(Windows EXE / Mac .app / IDE 실행)과 선택한 종목이다. 사용자는 오류 문구 없이 로딩이 끝나지 않는 모습이라고 답했다. 키나 계좌번호는 필요하지 않다. Mac 호스트·사용자의 실제 인증 세션이 없어 해당 환경의 오류는 재현하지 못했다.

Mac에서 실제 공개 자료 진단을 실행하려면 이 프로젝트에서:

```sh
bash scripts/verify-public.sh
# 또는 publish한 앱의 공개 조회/차트 표시만 검사:
./Stock.Avalonia.app/Contents/MacOS/Stock.Avalonia --verify-public /tmp/stock-public-check
```

Windows 루트 EXE에서도 `Stock.Avalonia.exe --verify-public <빈 출력 폴더>`를 사용한다. 원래 앱 DB 대신 격리 DB를 만들고 result.json/차트 PNG를 기록한 뒤 종료한다. 실제 Mac의 파일 선택창·폰트·트랙패드·native 차트·키 보관, 실제 증권사 인증/시세/실제 주문, 장중 실제 가격 변화는 완료로 표시하지 않는다.
