# 현재 인수인계 — Inje’s Strategy 로컬 교체

2026-11-11 03:56 KST 최종 검증. 기반 HEAD `ca8ea4a`에서 미커밋 로컬 변경. **GitHub 업로드 금지**이며 commit/push/태그/Actions/Release를 하지 않았다. 사용자 시간 상한은 2026-11-11 06:00 KST, 이후 자동 재개하지 않는다. 과거 릴리즈 완료 이력이나 Bin 미착수를 근거로 자동 작업하지 않는다.

## 현재 구현

정본 `docs/requirements/Injes-Strategy-20261111.txt`. 사용자 최신 정정이 첨부의 구전략/D 보존 조항보다 우선한다. D 전용 Core/runtime/editor/window를 삭제했다. 다른 구전략 실행 로직도 제거하고 호환 DTO/함수 호출은 명시 거절한다. 계좌/키/관심종목/정기매수/실제 주문 안전 기록은 보존한다.

`Stock.Core/Strategies/Injes`에 공통14개·전용10/12/11/13개인 네 전략과 Standard6개 매핑, registry/family, strict JSON, causal rolling feature, support cluster, risk/execution/replay/stream/journal을 구현했다. `IStrategyExecutionAdapter`와 기존 `PaperBroker`/`TradingStore`를 공유한다. UI마다 알파 엔진을 만들지 않았다. Bin은 미등록이며 실제 자동 전략 주문은 잠겼다.

Avalonia/WPF는 기존 selector·저장/복구/불러오기/다운로드·설정 카드·연구·스크리닝·가상 실행 팝업을 사용한다. 네 전략 중 하나의 고정된 설정으로 미국 USD 종목1~20개를 처리한다. USD 가격 소수 정밀도와 검은240일선은 공통 차트에 유지한다. 자료 누락은 MissingData로 표시한다. 보유/주문대기 행은 후보 필터로 숨기지 않는다. 보관 원장 복원은 새 매수 OFF이며 보호 재개가 별도다.

신호와 체결, 위험승인과 주문허용, 취소 요청과 확인을 구분한다. 원장 복원 때 현금/수량/평균가/비용/최초 체결시각·owner·정의를 대사한다. 저장 실패는 진입 잠금·매수 취소 요청·메모리/마지막 정상 저장본 보존 상태이며 자동 복구를 가정하지 않는다. 긴급중지/종료는 임의 청산하지 않는다. 응답 없는 피드의 종료 대기는 제한된다. 확인된 원장 체결로 누락 차트 표식을 재구성한다.

`TradingStore.RetireLegacyStrategies()`는 앱 시작 시 한 번 구전략 설정·전용 연구/가상 기록만 정리하는 트랜잭션이다. 격리 DB로 삭제/보존/반복 경계를 검사했다. 사용자 개인 DB/자격증명/설치 앱은 개발 도구로 읽거나 초기화·교체하지 않았다.

## 검증과 산출물

- Avalonia Release, Windows WPF 교차 빌드: 경고0/오류0.
- Portable/Core363개, Avalonia263개, 디자인17개 검사 통과. 합성/격리/가짜 broker 검사이며 실제 주문0.
- Core manifest89파일과 단일 설명서의5개 전체 JSON import/Standard 매핑 검사 통과.
- Mac ARM64 self-contained 앱 생성 → ZIP → 새 임시 폴더 압축 해제 → `codesign --verify --deep --strict` 종료0 → 실제 실행 파일의 합성 시작 검사 종료0/Success/NoBrokerCalls/SyntheticOnly=true. 차트601개·현재가2회·창표시 확인.
- `artifacts/Injes-Strategy-local-macos-arm64-20261111.zip` (43,291,908bytes), SHA256 `8970020f182324987a297bcfc025cfba83b73a24bb3dd5ab3aa950b8e258413f`.
- 앱은 로컬 ad-hoc 서명만 있고 Developer ID/공증은 없다. 버전 속성은0.1.17을 유지했으나 새 미게시 Inje 코드다. 기존 설치 앱과 혼동하지 않는다.
- Documents FileProvider가 `.app` 서명을 방해할 수 있어 `/private/tmp/injes-delivery.mF0SoC/package/Stock.app`에서 만들고 ZIP을 전달본으로 보관했다. workspace의 이전 실패 `.app` 폴더는 전달본이 아니다. 임시 경로의 장기 보존은 기대하지 않는다.

명령과 검사 범위/실제 제한의 정본: `docs/verification/Injes-2026-11-11.md`. 전체 로그는 저장소 밖 `../work/injes-final-*.log` 및 `injes-delivery-package.log`에 있다. 그림은 `artifacts/injes-final-desktop`, `injes-final-design`, `injes-final-zip-startup`에 있다. artifact/개인 자료는 Git 제외다.

## 다음 시작 위치와 제한

1. 사용자에게 전달할 파일은 ZIP과 `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md`다. 네 전략 전체 JSON과 Standard 예시를 이 설명서 한 파일에 넣었다. 별도 JSON 설명 파일을 여러 개 만들지 않는다.
2. 기존 미국 피드/parser는 존재하지만 유효 TickSize·정지·뉴스·동일 경과시간 과거 RVOL·확인된 세션/허용 주문·필요한L2를 실자료로 확인/보완해야 한다. 누락 시 진입 보류가 정상이다. 공개 일봉/폴링을 틱으로 만들지 않는다. 실제 시장 수익성/체결 품질은 미검증이다.
3. Windows UI 실행은 이 Mac에서 불가했다. `dotnet run --project Stock.Tests -c Release -- --current-checks artifacts/injes-wpf`를 실제 Windows에서 확인해야 한다. 교차 빌드와 실제 실행을 구분한다. 원격 CI/새 Windows EXE/릴리즈는 수행하지 않았다.
4. Bin 요구사항은 아직 없다. 별도 family/registry로 추가하며 Inje/D를 재활용한 대체 실행을 하지 않는다.

이전 공개 배포 인수인계는 `docs/history/HANDOFF-before-Injes-20261111.md`, 현재 구조/UI 흐름은 `docs/architecture/Injes-Strategy.md`와 `docs/ui/Injes-Strategy.md`다.
