# 최종 화면 검수·익절/손절 색상·배포 판단

2026-11-08. 사용자는 익절 빨강/손절 파랑 수정과 전체 검수 후 WPF Stock.exe를 Avalonia Stock.exe로 교체·GitHub Release할 것을 요청했다. 이는 기능 검증 완료를 조건으로 한 요청이다. 아래 검사 통과와 기존 기능 동등성 충족을 구분한다.

## 구현

공통 StockChart의 글자·배지 테두리·가격 표식을 같은 TradeMarkerPalette로 통일했다. 이 차트를 사용하는 종목/백테스트/일반·실시간 스크리닝/거래 기록/전략D/정기매수 화면에 공통 적용된다.

- 실제 실현손익 양수:빨강 F04452, 음수:파랑3182F6.
- 매수는 기존 빨강, 본전·손익/청산 의도 미확인은 중립색.
- 실제 손익이 있으면 Stop/TP 이름보다 우선. 수익이 난 추적 청산을 손절색으로 바꾸지 않는다.
- 병합 표식은 모든 매도의 실현손익이 확인된 경우 합산 순손익으로 표시한다. 미확인 금액을0으로 만들지 않으며, 확대 시 원래 체결을 유지한다.
- 실현손익이 없지만 공통 TradeOutcome에 명시된 익절/손절 의도가 있으면 그 색을 사용한다. 추적 청산이라는 이름만으로 손실을 추정하지 않는다.

수정: Views/ChartMarkerLayout.cs, StockChart.axaml.cs. 검사: Stock.Desktop.Tests/TradeOutcomeColorChecks.cs, Program.cs, scripts/verify-native-backtest.ps1의 CheckOutcomeColors.

## 검수 결과

| 화면/범위 | 확인 내용 | 근거/구분 |
|---|---|---|
| 종목·차트 |검색/Enter/반복·재실행, 실제 국내 시세, 일봉/집계, 줌·팬·복귀·포인터, 실제 백테스트/표식 단일 클릭/달력 |루트 후보 실제 키보드·화면 + UI fixture |
| 일반 스크리닝 |실제 KOSPI10 전 대상 평가, 진행 중 열람·취소·집계/상세·공통 백테스트 팝업, 당시 설정·봉 기준 보존 |실제 공개28 + UI fixture |
| 실시간 스크리닝 |선택/전략 변경·앱 수명·중복 차단·가상 실행·중지·오류/지연/시장 마감 |열린 시장/온라인 시세는 fixture. 실제 주말 시작은 장외 팝업·주문0 확인 |
| 전략 설정 |61개·전략D, Save/Restore/Defaults/Add/Rename/JSON export/import·정밀도/유효성 |공통 코어/화면 fixture·파일 picker adapter; 실제 모든 OS 파일 선택기를 검증한 것은 아님 |
| 계좌·주문 |4개 계좌 타일/가림·보기/취소/메모리 연결·계좌조회·모드 재사용·초기화·오류 |더미 키/가짜 broker. 실제 돈 주문 없음 |
| 테스트 기록 |단일 종목/스크리닝/정기매수/D snapshot 기록·기간 조회·재열기·차트/거래 선택 |fixture + 실제 공개 백테스트/스크리닝 |
| 시장 현황 |5개 실제 지수 가격/추이, 국내/해외 선택 |실제 공개28 + native 화면 |
| 관심종목/가상 팝업 |등록/그룹/중복 체크·예산/전략·실행 동결·닫기/재열기·중복/긴급중지 |fixture + 실제 공개 관심등록·native 화면 |
| 7개 화면 공통 컨트롤 |두 일반 앱 각7탭, 버튼 호버99개(WPF49/Avalonia50), 콤보 실제 열기/닫기14개 |release-final-native-pages/result.json Success:true. 실제 키 초기화/삭제/주문 버튼 일괄 실행은 하지 않음 |
| 손익 색상 |양수 Stop, 음수 TP, 병합 순손익, 본전/미확인 및 실제 글꼴/테두리·개요 |색상8개 fixture + 실제 후보 화면 픽셀 |

빌드0경고0오류, Core192/UI207/디자인18/실제 공개28 성공. 로그: artifacts/release-final-regression.log, release-final-public.log. Core80개 원본/manifest 변경0개.

## 실제 Stock.exe 후보

- 파일: artifacts/release-candidate/Stock.exe,107330359bytes.
- SHA256:258F8CB67F7E67C90FAE57D5923DC70CCD983D25F9D69E2B120318AF267C095E.
- --startup-smoke:Exit0/Success/NoBrokerCalls,601봉·2회 관측, 격리 합성 저장소.
- 실제 일반 후보 EXE 검색/Enter/반복·종료/재실행4회 성공:release-candidate-native-search/result.json.
- 삼성전자 검증1·2026-08-01~10-30:4거래/+66,000. 실제 화면 익절+98,000 배지 빨강322픽셀·파랑0; 손절-32,000 배지 파랑333픽셀·빨강0. 두 색 모두 확인.
- 팬09/23→09/04와 복귀, 단일 클릭 거래#1 상세, 상세 차트→같은 거래 행/텍스트/선택색, 달력202×201 모두 성공:release-candidate-native-backtest/result.json.
- 스크린샷:release-candidate-native-backtest/backtest-trades.png. 사용자의 개인 API키/DB를 배포 디렉터리에 포함하지 않았다. 검수 상태는 readiness.json에 별도 기록한다.
- 현재 Avalonia 루트 Stock.Avalonia.exe도 이 색상 수정판으로 갱신, 이전78CB판은 artifacts/before-outcome-colors-20261108에 보존했다.
- Mac arm64/x64도 색상 수정 소스로 교차 빌드했다. 실제 Mac 실행 검증이나 GitHub 배포를 뜻하지 않는다.

시작 검사에서 처음 WPF 옵션 --smoke-test를 사용한 실행은 일반 실행으로 분류되어 결과 파일을 만들지 않았다. 직접 시작한 그 후보 프로세스만 정상 종료한 뒤 올바른 Avalonia --startup-smoke로 다시 검증했다. 이 초기 검사기 오류를 앱 실패나 성공 결과로 혼동하지 않는다.

## 교체/릴리즈 조건을 아직 충족하지 못한 항목

1. 실제 계좌 전략 실행·주문 취소 UI 및 실제 체결 표시가 잠겨 있다. OrderToolsPanel.axaml/AccountPanel.axaml/MainWindow.axaml에 명시적 IsEnabled=False. 원본 WPF의 기능을 대체하지 못한다.
2. API키·기본 계좌의 영구 저장/복구와 Mac Keychain 미구현. AccountWorkspace는 메모리 연결만 유지하며 종료 시 지운다.
3. 일봉의 다음 거래일 자동 가상주문·보유/미체결 복원이 미구현. 현재 일봉 자동 실행을 명시적으로 차단한다. 실제 소스 RealtimeScreeningHost/RealtimeScreeningWorkspace에 차단 코드가 있다.
4. 실제 증권사 read-only API 및 실제 Mac 실행/트랙패드/폰트/서명·공증 미검증. 현재 실제 공개 시장은 주말이라 장중 실시간 검증도 fixture와 구분한다.

따라서 "전체 기능이 완벽하므로 기존 Stock.exe를 삭제하고 교체"로 판정하지 않았다. 기존 WPF Stock.exe(E433834B...1934)는 그대로이며 GitHub 최신 v0.1.14와 원격 main fbeed251...도 변경하지 않았다. 기존 Release Actions는 WPF를 빌드하므로, 향후 교체를 선택할 때 Avalonia 소스·검증·게시 대상으로 함께 전환해야 한다. exe 자산만 소스/CI와 무관하게 업로드하지 않는다.

사용자에게 알려진 누락을 먼저 완성할지, WPF를 유지한 테스트용 사전 공개로 할지, 누락을 수용하고 교체할지 질문했다. 답변 전에는 원본 EXE 교체와 커밋/push/tag/Release를 진행하지 않는다. 조건이 충족됐다는 뜻으로 검사 수만 제시하지 않는다.
