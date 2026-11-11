# 현재 인수인계 — 전략별 샘플 검증 완료

사용자의 전략별 샘플 요청을 Inje4개/Bin5매수패턴 총9개로 수행했다. 합성 원본 이벤트로 기능을 검증했으며 실제 시장/수익성 검증과 구분했다. 미커밋 로컬 변경이며 GitHub 게시/설치 앱 교체/개인 DB·키·실제 기록 변경/실계좌 주문 없음. 직전 v0.1.18 릴리즈는 완료된 상태로 그대로 보존한다.

## 구현/결과

Stock.Desktop.Tests/StrategySampleFixtures.cs가 독립 프로필9개·세션/가격 규칙/당시 가용 준비 자료·합성 체결/호가를 만든다. StrategySampleChecks.cs가 실제 OversoldBacktestService와 MainViewModel의 연구/일반 스크리닝 명령까지 실행한다. 샘플마다 매수1/매도1/완결1, 보유/미체결0, 후속 이벤트 체결/수량 정합, 중복 거래·현금 증가 없음, 호가/자료 누락 차단, 날짜 입력/관측 소수 가격, 스크리닝 후보1/자료 없는 비교 종목 제외를 확인했다. Inje 스크리닝 주문 허용OFF, Bin ScreenMatched로 주문 없음.

테스트 중 Inje ResearchBridge가 합성 출처를 SyntheticFixture에 전달하지 않던 표시 누락을 수정했다. Stock.Avalonia/ViewModels/ScreeningWorkspace.cs의 USD 가격 정수 내림을 재현/수정했다. MainWindow의 옛 공개 일봉 평가 안내와 ScreeningPanel의 급락률 열 제목을 실제 원본 판단/기준 대비 비율로 정정했다. 전략 수치/계산·실계좌 잠금은 변경하지 않았다. 초기 호가 동일가 print 미체결과 눌림목 준비 부족은 올바른 차단이었으며 엔진을 느슨하게 하지 않고 샘플 자료를 맞췄다.

샘플135/Avalonia전체996/Inje363/Bin187 PASS. Avalonia/WPF교차 빌드0경고/0오류, manifest104개 일치, 설명서 JSON15개(Inje예시5+기본프로필1+샘플9) strict import PASS. Windows 실제 실행이나 실제 과거 원본 시세 수익성 검증은 이번에 하지 않았다. 화면 PNG를 육안 확인했다.

## 파일/재현

사용자용 JSON9개는 기존 한 파일 docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md 마지막에 details/JSON 블록으로 수록했다. Stock.Injes.Tests/Catalog.cs의 가이드 검증도10개 전체 프로필(기본1+샘플9)에 맞췄다. JSON만 불러온다고 합성 이벤트가 사용자 앱에 생성되지 않는다. 공개 일봉으로 전략 원본 틱/호가를 만들지 않는다.

검증 정본 docs/verification/Strategy-Samples-2026-11-11.md. 결과/프로필/화면은 Git 제외 artifacts/strategy-samples-regression, sample-summary.json이며 로그는 ../work/strategy-samples-*.log. 단독 재현: dotnet run --project Stock.Desktop.Tests -c Release -- artifacts/strategy-samples --strategy-samples. 기본 Desktop suite에도135개 검사를 포함했다. 현재 TASKS가 완료됐으므로 새 사용자 요청 없이 예전 릴리즈나 미완료 작업을 재실행하지 않는다.

## 사용량 조건/보존

사용자 최신 지시: 주간 잔여15%를 보존하고 다음 작업이15% 아래로 소모할 우려가 있으면 미리 멈춘다. get_usage_limits의 secondary.usedPercent로 확인한다. 이번 시작35%, 중간34%/33%를 확인했다. 작업단계/큰 작업 전 다시 확인하며 다른 채팅의 소비도 고려한다. 이전 '사용량 조건 무시' 기록보다 우선한다.

Bin 실시간 가상 주문/복원과 실계좌 자동 전략 주문은 기존처럼 시작 차단/잠금 유지. 공개 v0.1.18(Windows42962568324/Mac42963164613, source2c1891c)과 현재 미커밋 샘플/표시 수정은 구분한다. 설치 개발 앱/이전 앱 복구본/StockViewer 저장소/기존 공개ZIP·DMG는 건드리지 않았다. 이전 상태는 docs/history/*before-strategy-samples-20261111.md에 있다.

최종 사용량 확인: 주간 잔여32%에서 작업을 완료했다. 다음 단계 시작 전에도15% 보존 조건을 확인한다.
