# 2026-10-05 변경 보고 — 검증된 안전 범위

전체 32개 요구의 상태는 [OversoldExpansion-20261005.md](OversoldExpansion-20261005.md)에 항목별로 표시했다. 이 보고는 A/B 및 C의 구현 범위이며 D 실계좌 다중매매 완료 보고가 아니다.

## 추가·수정 파일

| 영역 | 파일 | 내용 |
|---|---|---|
| 숫자 입력 | App/FixedDecimalEdit.cs, FixedDecimalInput.cs | 정수 삽입, 소수만 덮어쓰기, 상태 특례 제거 |
| 키 UI | App/ConnectionDialog.cs, SecretKeyInput.cs, MainWindow.xaml.cs | 4개 브로커, 감춤/표시, 두 초기화 버튼 |
| 키 정리 | App/CredentialResetService.cs, BrokerCredentialStore.cs, WorkspaceFeatures.cs | 확인·범위·세션 정리·기존 DPAPI 재사용 |
| 연결 수명 | Core/Trading/RemoteTradingSession.cs, KiwoomBroker/KisBroker/MeritzBroker/TossBroker.cs | 종료 세션 차단, 진행 작업 대기, token 참조 해제 |
| 봉 모델 | Core/Research/Oversold/BarSpecification.cs, TickCoverageTracker.cs | 임의 틱/분/시, 세션/완전성/인과성/정확 거래대금 |
| 공통 계산 | OversoldEntryDetector.cs, OversoldPositionRules.cs, OversoldBacktest.cs, SupportProviders.cs | 기존 조건을 공통화, 순차 반등 상태, 일봉 회귀 보존 |
| 기록 | OversoldResults.cs, OversoldScreening.cs | 판단 당시 cluster/추가진입/stop snapshot, immutable run |
| 스크리닝 UI | App/OversoldScreeningWorkspace.cs, ScreeningResultDialog.cs, OnlyOversoldWorkspace.cs, ConfiguredScreening.cs, WorkspaceViewModel.cs, MainWindow.xaml | 데이터 기준, 0건 결과, 조건 상세, 상태·지표 열 |
| 기존 결과 상세 | App/OversoldResultDialog.cs | 모형 신호/체결별 당시 snapshot 표시 |
| 도움말 | OversoldConfig.cs, OversoldParameterMetadata.cs | 봉 단위 설명 및 현재 지원 범위 |
| 미연결 초안 | OversoldLiveStrategy.cs | FakeBroker 검증용 공통 detector/position rules. 앱 주문 서비스 연결 없음 |
| 검증 | Tests/FixedEditorTests.cs, ConnectionTests.cs, StrategyBarTests.cs, OversoldScreeningTests.cs, OversoldRuntimeFixtureTests.cs, ScreeningUiTests.cs, Program.cs, UiTests.cs, App/PublishedSmoke.cs | 입력/키/집계/인과성/저장/상세/WPF/fake 체결/단독 실행 |

## 사용자에게 보이는 동작

숫자 `1|.00`에 `2`를 입력하면 `12|.00`이 된다. 소수점 키로 소수부로 이동하고 그 뒤 두 자리만 덮어쓴다. 원래 저장된 세 자리 이상 정밀값은 보기만 해서는 바뀌지 않는다.

계좌 설정에서 키 표시 전환과 선택/전체 초기화를 제공한다. 사용자 확인을 거친 경우에만 정해진 계좌키 파일을 지우며 거래 자료는 유지한다. 테스트에는 임시 저장소와 더미키만 사용했다.

스크리닝은 일봉을 유지하면서 지정 봉(임의 정수·틱/분/시)을 추가했다. 상태·관측시각·RSI·급락률·거래량비율·지지 수를 보여주고, 실행 당시 조건/계산값을 별도 결과창에서 확인한다. 이전 run은 SQLite에 남지만 현재 결과창의 선택 대상은 최신 run이다. 행을 클릭하면 기존 백테스트 신호·체결을 시간순으로 조회한다.

## 데이터와 계산 정책

OHLCV는 원본 체결이나 검증된 원본 분봉을 재집계한다. 틱 거래대금은 `price × individual size` 합이다. 누적량은 누락 검사용이며 더하지 않는다. 미완성·누락·세션이 없는 자료로 합성 봉을 만들지 않는다. 시간봉은 시장 장 시작에 맞추며 종료 잔여 구간을 완성봉으로 오인하지 않는다.

시세/분류/위험/거래일이 확인되지 않은 종목은 자료 부족으로 제외한다. 현재 일반 분봉 API adapter는 확정 구간과 정확 대금 계약이 없어 전략 자료로 사용하지 않는다. 공식 키움 안내에는 ka10079/ka10080이 목록에 있으나 이 사실만으로 해당 API의 누락·구간·대금 의미를 검증했다고 취급하지 않았다. [키움 공식 API 가이드](https://openapi.kiwoom.com/m/guide/apiguide?dummyVal=0).

기존 `*Days` 설정 키와 저장소를 유지하면서 지표 lookback은 봉 수로 계산한다. 후보 유지/보유/재진입은 시장 거래일을 유지한다. 일봉 Backtest, Screening은 EntryDetector와 PositionRules를 공유한다. 스크리닝은 실제 체결을 만들지 않는다.

## 미완료·차단 범위

- 실계좌 다중매매 연결 변경은 자동 승인 검토에서 직접적인 사용자 승인을 확인하지 못했다는 이유로 거부되어 실행되지 않았다. 반복하거나 우회하지 않았다. 기존 자동매매 차단을 유지한다.
- 새 전략의 다중매매 팝업/앱 단일 service/계좌·후보 고정/session 수명/3종 stop/전송 경합/공유 현금/새 live 체결 상세 통합은 후속 작업이다.
- FakeBroker 단독 테스트는 접수와 체결의 구분, 부분 체결 평균가, stage 횟수/중복 체결, 상태 독립, 잘못된 종목 체결 차단만 검증한다. 새 전략의 전체 실거래 통합 검증을 대신하지 않는다.
- 모든 실패한 추가진입 판단의 매 봉 감사, 과거 run 선택 UI, 실제 과거 원본 API adapter 확장도 남아 있다.
- 실제 키/실제 주문/사용자 DB 삭제/사용자 키 초기화는 수행하지 않았다. WPF routed 이벤트·오프스크린 렌더 검사이며 물리 입력 지연이나 다중 모니터 DPI 검증은 아니다.

## 검증

Release 빌드 경고 0, 오류 0: `artifacts/expansion-build-final.log`.

전체 **810개 검사 통과**: `artifacts/expansion-regression-verified.log`. 기존 일봉 Core 회귀 67개와 새 UI/집계/snapshot/FakeBroker 검사가 포함된다. 숫자는 중복된 개별 실행 결과와 합산하지 않는다.

단독 EXE 및 배포 해시는 TASKS.md / VALIDATION.md 마지막 기록을 따른다.

최종 배포: `Stock.sln` 옆 `Stock.exe` 80,624,331bytes, SHA256 `406FEBF652280F0230CB75EAE42315AD96EA8C305A7E51F6E97E16BA81C7EB95`. 최종 UI156검사도 통과(810과 중복). EXE 단독 실행·native 추출·새 결과창/숫자/4계좌 확인 성공. `artifacts/expansion-clean-result-final/result.json`, `artifacts/expansion-release.json` 참조. 미완료 실거래 연결은 배포하지 않았다.
