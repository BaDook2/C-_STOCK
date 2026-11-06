# 기능/버튼 점검표 — 2026-11-06

## 범위와 증거 구분

일반 모드 전체 회귀 **1,257개**가 통과했다. 명령 이름만 확인하지 않고 `ButtonAuditTests`에서 실제 WPF `ButtonAutomationPeer.Invoke`로 **33회 버튼 호출**과 연결 팝업을 실행했다. 키 입력/초기화·주문·다량 실행은 격리 DB, 더미 키, 가짜 증권사를 사용한다. 실제 계좌의 주문/취소/초기화 버튼을 눌렀다는 뜻이 아니다. 모든 가능한 입력/실제 브로커 상황을 전수 검증했다는 주장도 하지 않는다.

`MainWindow.xaml`의 Button 요소는 기존39개 + 저장소 점검1개다. 데이터 템플릿과 팝업/생성 컨트롤은 별도 검사한다. 아래 표는 클릭 목록과 기존 기능별 회귀를 합친 결과다.

| 영역/컨트롤 | 오프라인 검사 | 온라인/가짜 증권사 검사 | 근거 |
|---|---|---|---|
| 이름 검색/Enter/결과 선택 | 실제 Invoke·네이버 실제 가격·차트 | 계좌 시세 라우팅/연결 변경 | ButtonAudit / UiTests / CurrentDailyUiTests |
| 기간7종·일/주/월·분/시간·확대/이동/교차선 | 선택색/빈구간/전체범위·실제 차트 | native 분봉/취소/형식/시간봉 | PipelineRecurringUiTests / RealtimeScreeningUiRevisionTests |
| 관심등록·별·표식 체크3종 | 저장·표시·이전 결과 초기화 | 확인 체결만/종목·출처 분리 | ButtonAudit / UiTests / TradeHistory |
| 가상 전략매매 설정·추가·전략/예산/시드 | 공개시세 로컬 모의·체결·닫기 유지 | fake 피드·중복 방지·시드·예산·부분체결 | MultiTradeUiTests / PublicPaperTests / RealtimePaperBatchTests |
| 실제 전략매매 설정/시작/중지·전체 중지 | 노출 안 함 | fake 주문/감시·3종 정지·보호 관리·오류 격리 | MultiTradeUiTests / LiveBatchTests / LiveExecutionApiTests |
| 실제 매수/매도 | 노출 안 함 | 실제 WPF Invoke 확인 거절 시0요청 / fake 승인 시정확1요청 | ButtonAudit / ConnectionTests / BrokerTests |
| 대기/선택 주문 취소·미확인 외부 확인 | 가상 취소 | fake 계좌 미체결/취소·불확실 응답·재전송 금지 | ButtonAudit / BrokerTests / LiveBatchTests |
| 키 설정·선택 연결·기본 계좌·키 표시 | 더미 UI 입력/가리기/표시·닫기 삭제 | 계좌 라우팅·검증 실패시미저장 | ConnectionUiTests / ConnectionTests |
| 선택/모든 API키 초기화 | 더미키/임시 vault에서 확인·취소 | 활성 세션 정리 후 키 삭제/다른 계좌 보존 | ConnectionUiTests / CredentialReset 검사 |
| 백테스트·상세/판단 기록 | 실제 Invoke·공통 엔진 결과·매수매도 | 실제 Invoke·동일 엔진·지정 분/시·취소 | ButtonAudit / OversoldUiTests / OversoldTests |
| 정기매수 실행·도움말 | 실제 Invoke·카드/자산/매수표식 | 계좌 모드와 독립 계산 | ButtonAudit / PipelineRecurringUiTests / RecurringTests |
| 목록/분야 불러오기·스크리닝 시작/결과 | 실제 Invoke·기간·시장·중복·funnel | native 봉 범위·캐시·자료부족·오류 격리 | ButtonAudit / ScreeningUiTests / CurrentDailyUiTests |
| 백테스트 보기/행 클릭/더블클릭 | 팝업·당시 전략/기간/봉·거래/Snapshot | 온라인/오프라인 동일 경로·DB오류격리 | ButtonAudit / RealtimeScreeningUiRevisionTests |
| 팝업 이전/다음/다시실행/닫기 | 공통 엔진·generation·취소·캐시 | 원본 피드와 UI 수명 분리 | ScreeningUiTests / UiRevisionTests |
| 실시간 스크리닝 시작/중지·전략 변경 | 공개가격/완성봉·탭전환유지 | fake/실제 피드·조건편입이탈·선택유지·중복 | ScreeningUiTests / RealtimeScreeningTests |
| 실시간 자동매매 체크 | 공개 스크리닝은 신호 표시만 | fake 체크 이전 신호 소급 안 함/체크 해제 후 보호관리 | RealtimeScreeningTests / RealtimePaperBatchTests |
| 전체 대상/미충족 사유 보기 | 일치만/전체 전환·미확인통과금지 | 과거 신호 잔존만으로 편입 안 함 | RealtimeRepairTests / RealtimeScreeningUiRevisionTests |
| 전략 추가/이름변경/설정저장/복구/기본값 | 실제 Invoke·선택독립저장·재시작 | 같은 저장 전략을 거래/스크리닝이 공유 | ButtonAudit / OnlyOversoldUiTests |
| 전략 JSON 불러오기/다운로드 | 실제 Invoke → DI 파일선택 → 읽기/쓰기·roundtrip | 계좌·키 포함 안 함 | ButtonAudit / OversoldPresetExport |
| 각 파라미터 도움말·입력/ON-OFF | 모든 도움말 팝업·정수/소수/커서/비율 | 동일 config validation | OnlyOversoldUiTests / FixedEditorUiTests |
| 계좌 새로고침/기록 선택/상세 기록 | 가상 상태/기록 선택 현재 차트 보호 | fake 잔고/실제 계좌 읽기·연결 변경 | ButtonAudit / ConnectionTests |
| 시장 현황·국내/해외 지수 | fixture 지수/가격/관심 갱신 | 모드별 시세 라우팅 | UiTests / CurrentDailyTests |
| 그룹 추가/편집/저장/취소/시세새로고침/차트/해제 | 실제 Invoke·중복종목·영속화 | 바뀐 그룹에 늦은응답 오염 방지 | ButtonAudit / Watchlist 검사 |
| 작업 취소/긴급중지 | 로딩 취소·대기 해제·다량 모의 취소 | 신규주문최종차단·불확실주문수동확인 | UiTests / LiveBatchTests / PublicPaperTests |
| 저장소 점검 | 실제 Invoke·원본 전체검사·보고서 | 계좌와 독립, no broker calls | ButtonAudit / StorageDiagnosticsTests / 실제 루트 EXE |
| 시작/중복실행/정상종료/재시작 | mutex/단일 EXE smoke·설정 재열기 | 피드·저장 종료/가상 잔존 상태 | PublishedSmoke / StoreLifecycleTests |

## 테스트 경계/미확인

이번 실제 화면에서 추가로 발견한 결함: 지원 키움 계좌에 연결해도 `CanAutoTradeRealtimeScreening` 변경 알림이 없어 체크박스가 오프라인 비활성 표시를 유지했다. Refresh에서 갱신하도록 수정하고 연결 전 비활성→연결 후 활성(체크는 OFF 유지)→해제 후 비활성 WPF 검사3개를 추가했다. 최종 관련 WPF40개 및 전체1,257개를 통과했다. 실제 자동매매를 켜거나 실주문으로 확인하지 않았다.

루트 EXE 실제 화면에서도 오프라인에서 비활성인 체크박스가 기본 키움 계좌 연결 뒤 활성 표시로 즉시 바뀌고 체크는 해제 상태를 유지하는 것을 재확인했다. 감시를 시작하거나 주문하지 않고 정상 종료했다.

게시 검사 도구도 보완했다. 같은 출력 폴더의 이전 fixture DB 설정을 재사용해 기본 소수 입력 검사가 실패할 수 있었으므로 매 실행마다 독립 fixture DB를 사용한다. smoke 창 종료 시 정상 앱 watchdog의 강제 종료0이 실패 코드를 가리지 않도록 수동 수명 모드를 사용한다. 동일 폴더에서 게시 EXE smoke를 연속2회 실행해 모두 종료0/Success=true/NoBrokerCalls=true였다.

실제 새 저장소 수명 코드로 사용자 DB 점검, 네이버 삼성전자 검색/1초 시세·차트, 키움 기본 계좌 연결/계좌 새로고침, 20종목 실제 피드 감시·11:00 확정봉 분석, HLB 더블클릭→15회 모형거래/매수매도, 닫기/선택 유지/중지를 확인했다. 정상 종료 후 DB는 state42/events110,238/runs8이며 전체 무결성/JSON은 정상이다. 최초 백업 이후 실제 수신 이벤트가 늘어난 것이며 모의 결과를 실제 체결로 저장하지 않았다. 검사 중 새 Stock .NET 종료 이벤트는0개다.

- 실계좌 주문과 실제 API키 초기화는 실행하지 않았다. 해당 기능은 임시 vault/가짜 broker에서 검증했다.
- 해외 증권사 계좌별 권한·실제 체결 품질·장애 상황 전체는 fixture 검사로 실제 성공을 보장하지 않는다. 키움 해외 잔고 권한 제한은 별도 계좌 문제다.
- 오프라인 공개 원본 틱은 없고 표시 틱 UI는 사용자 요청으로 숨김이다. 누락된 데이터/시점별 상태/위험은 일치·자동주문 허가로 바꾸지 않는다.
- 국내 정규장은390분이므로 7~12시간짜리 완성 봉을 한 세션에서 만들 수 없다. 다른 봉으로 임의 변경하지 않고 자료부족을 안내한다.
- 과거 SQLite 최초 오류 원인은 미확정이다. 정상 DB 전체검사와 새 실패 증거를 유지한다. [DB 조사](StorageAudit-2026-11-06.md).

실행 로그: `artifacts/storage-audit-full.log`, `artifacts/storage-audit-full-ui/button-audit-clicks.txt`, `storage-audit-published-smoke/result.json`. 재현 명령: [검증 명령](../maintenance/Verification.md).
