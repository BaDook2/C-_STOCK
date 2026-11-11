# 현재 인수인계 — 두 전략 UI 및 로컬 앱 반영 완료

2026-11-11 사용자 정정대로 Avalonia 전략설정을 Inje’s Strategy/Bin’s Strategy 두 상위 그룹으로 교체했다. 이름 옆 사용 체크는 접혀도 보이고 한 번에 하나만 켜진다. 둘 다 끄면 실행 거절. Inje 네 알고리즘 초안, Bin 전체 파라미터·초/분/시/일 기간·봉 단위·비용표·국가별 예산을 편집한다. 전체 프로필 JSON은 두 체계와 사용 선택을 보존하고 단독 Inje/Bin JSON도 받는다. 기존 Inje 저장값을 복사하며 계좌/주문/실제 기록 삭제는 하지 않는다. 마지막 선택 프로필은 `strategy-profile-selected-v1`에 저장한다.

`Stock.Core/Strategies/StrategyProfile.cs`, `Strategies/Bins/EditorModel.cs`/`EditorLabels.cs`, `Stock.Avalonia/ViewModels/StrategyEditor.cs`, `Views/StrategyPanel.axaml`/`SettingInput.cs`가 편집 경로다. `BinsResearchBridge`와 MarketEvent의 선택적 Bin 자료 필드가 백테스트/일반 스크리닝에 연결된다. 스크리닝 evaluationOnly는 주문 제출을 하지 않는다. 미래 첨부 봉은 최종 원본 이벤트 시점 이후면 제외한다. 혼합 실행 envelope를 거절한다. Inje의 가상 계좌 보호/복원은 유지한다.

**Bin 실시간 가상 주문/재시작 원장 복원은 아직 연결하지 않아 시작 차단한다.** 실계좌 자동 전략 주문도 계속 잠겨 있다. 새 전략 체크는 다음 실행에 적용하며 진행 세션의 고정 설정을 바꾸지 않는다. 선택 변경은 신규 진입을 중지하고 기존 Inje 보유 보호를 유지한다. 일반 공개 일봉만 있으면 두 체계 모두 필요한 원본 자료가 없다는 사실을 표시한다.

## 검증과 설치

- Avalonia492, Inje363, Bin187, 디자인17 검사 PASS. Core manifest103파일 일치. 설명서 Inje5개+전체 프로필1개 strict import PASS.
- `STOCK.Avalonia.sln` Release와 `Stock.sln -p:EnableWindowsTargeting=true` 빌드 경고0/오류0. WPF는 교차 컴파일이며 실제 Windows 실행 아님.
- macOS ARM64 self-contained 앱 실제 합성 시작·전략 헤더·Bin 파라미터 캡처 PASS. broker/network/개인키 사용 없음.
- 11:50 KST `/Applications/Stock Development.app`에 검증본 설치 완료. bundleID `com.stock.desktop.avalonia`, 표시버전0.1.17이지만 공개 릴리즈와 다른 미게시 소스다. Core hash `2d2734bbc394145b1d5c08ff40eaf29cb9f9c32802a6bab02cabdf0cea2d29f1`, UI hash `e6e888826449c1829e6d1a90165bc9552a1f5e6fbb2a838ab574dec2081682b1`. 코드서명 strict 통과, 이전 앱이 실행 중이 아님 확인 후 교체했다.
- 이전0.1.16 앱은 `~/Library/Application Support/Stock App Backups.noindex/2026-11-11-strategy-ui/Stock-v0.1.16-before-strategy-ui.saved-app`에 보관. 임시 앱 경로 등록 해제, 설치 경로 등록. 최종 mdfind/LaunchServices 결과는 해당 bundleID의 설치 앱1개다. 사용자 DB/키/실제 기록 수정 및 일반 앱 자동 실행 없음.

정본 설명서 한 파일: `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`. 두 체계 설명·Bin22개 결정/파라미터와 전체 프로필 JSON을 포함한다. 검증 문서: `docs/verification/Strategy-UI-2026-11-11.md`, UI 동작: `docs/ui/StrategyPage.md`. 실행 로그/설치 manifest는 저장소 형제 `../work/strategy-ui-*.log`/`strategy-ui-install-result.json`; 화면/격리 데이터는 `artifacts/strategy-ui-*`, Bin 결과 `artifacts/bins-ui`.

이 작업과 선행 Inje/Bin의 변경은 모두 미커밋 상태로 보존했다. 사용자 지시대로 commit/push/tag/Actions/Release 없음. 검증 완료를 GitHub 게시 승인으로 해석하지 않는다. 앱 중복 정리의 이전13개 복구본과 다른 저장소 StockViewer는 보존한다. 이전 상태는 `docs/history/*before-strategy-ui-20261111.md`와 `*before-app-cleanup-20261111.md`에 있다.
