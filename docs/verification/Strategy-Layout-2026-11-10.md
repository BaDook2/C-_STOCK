# 전략 설정 배치·펼침 수정 검증 — 2026-11-10

요청 범위는 Avalonia 전략 설정 화면이다. 섹션 순서는 `백테스트 전용 → 급락 판단 전략 → 공통 전략 조건`, 기존 수동 검증 자료 상위 영역과 JSON 입력은 제거했다. 사용 여부는 접힌 제목 옆에서 선택한다.

원인: `CrashGroups` 목록 전체에 `IsEnabled=UseCrashStrategy`가 적용돼 사용 OFF 상태에서 Expander 헤더까지 비활성화됐다. 수정 후 헤더는 항상 활성화하고 각 그룹의 입력 영역만 잠근다. 공통 Expander 헤더 템플릿은 ContentPresenter의 hit testing을 끄므로, 제목 안 체크박스가 있는 해당 Expander에만 예외 스타일을 적용했다.

관련 코드: `StrategyPanel.axaml`, `ViewModels/StrategyEditor.cs`. 기존 사용범위 메타데이터의 Scope로 백테스트 그룹과 나머지 그룹을 나눠 같은 DataTemplate을 재사용한다. 저장 설정·알고리즘·수수료·시점별 자료 검증 로직은 변경하지 않았다.

검증 결과:

- Release 빌드 경고 0 / 오류 0.
- `--strategy-settings`: 49개 검사 성공. 실제 마우스 입력으로 접힌 제목의 체크박스 ON/OFF, 펼침 상태 독립, OFF 상태에서 하위 10개 그룹 각각 펼침/접기, 입력만 잠김, ON 이후 편집 재활성화를 확인했다. 화면 위치·수동 JSON 입력 제거·저장/복구와 이전 설정 정밀도도 검사했다.
- 전체 Avalonia headless 회귀 326개 성공. 디자인 18개 성공.
- 별도 경로에 self-contained ARM64 개발 앱 생성 및 strict 서명 검증 성공. 네이티브 시작 검사도 Success/NoBrokerCalls/SyntheticOnly true, 601봉/시세 2회로 통과했다. 근거는 `artifacts/strategy-layout-native/result.json`이다.
- 스크린샷: `artifacts/strategy-layout/strategy-sections-collapsed.png`, `integrated-strategy-settings.png`.

격리 DB·합성 시세·가짜 계좌를 사용했다. 실제 계좌/API 주문은 실행하지 않았다. 공통 엔진 변경이 없으므로 기존 Core 전체 회귀는 이번 UI 변경에서 반복하지 않았다. GitHub 게시·태그 생성 없음.

후속 사용자 교체 요청에 따라 실행 종료를 확인하고 `/Applications/Stock Development.app`을 갱신했다. 별도 검증본과 설치본의 Stock.Avalonia.dll SHA256은 모두 `e27437314c3364985d1e6d72df48d1a086c297d6ce956edfcd79ecf05c04a134`다. 설치 후 strict 서명 및 네이티브 시작 검사 성공(601봉/2quote, NoBrokerCalls/SyntheticOnly true). 근거 `artifacts/strategy-layout-installed/result.json`. 실행 중 앱 강제 종료는 하지 않았다.
