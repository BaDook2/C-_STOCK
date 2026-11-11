# Inje / Bin 전략설정 UI 연결 검증

2026-11-11 로컬 미게시 개발본. 사용자 답변에 따라 두 체계 사용 체크는 단일 선택이다. 실제 주문·실제 거래기록 삭제·GitHub 게시 없음. 테스트는 격리 DB·합성 이벤트·더미 계좌만 사용했다.

## 구현 결과

전략설정 상위 그룹을 Inje’s Strategy/Bin’s Strategy로 교체했다. 접어도 이름 옆 사용 체크가 보인다. 서로 배타적이며 모두 해제하면 새 실행을 거절한다. Inje 네 알고리즘의 초안을 각각 보존하고 Bin의 모든 파라미터·초/분/시/일 기간·봉 단위·우선순위·시장/국가 프로필·비용표를 편집한다. 불필요한 소수점은 강제하지 않고 실제 소수 파라미터는 정밀도를 보존한다.

`StrategyProfile`의 strict JSON은 두 체계와 사용 선택을 모두 보관한다. 단독 Inje/Bin JSON도 불러온다. 알 수 없는 키·중복·누락·범위/단위 오류는 반영 전에 거절한다. 비활성 값과 마지막 선택 프로필도 재시작 때 복원한다. 기존 Inje 설정은 복사하며 계좌/주문/실제 기록을 삭제하지 않는다.

선택한 실행은 한 체계만 포함한다. Bin 백테스트와 일반 스크리닝을 연결했고, 스크리닝은 주문을 생성하지 않는다. 자료가 없는 시세를 일봉으로 합성하지 않는다. **Bin 실시간 가상 주문/재시작 복원은 미연결이며 host가 시작을 거절한다.** Inje 가상 원장 보호/복원은 유지한다. 실계좌 자동 전략 주문 잠금도 유지한다.

## 검사 결과

| 검사 | 결과 | 로컬 근거 |
|---|---|---|
| Avalonia 솔루션 Release 빌드 | 경고0/오류0 | `../work/strategy-ui-build.log` |
| WPF 호환 소스 교차 빌드 | 경고0/오류0, Windows 실행 아님 | `../work/strategy-ui-wpf-build.log` |
| Avalonia headless 전체 회귀 | 492 PASS | `../work/strategy-ui-tests.log`, `artifacts/strategy-ui-full` |
| 디자인·실제 입력 컨트롤 | 17 PASS 및 PNG | `../work/strategy-ui-design.log`, `artifacts/strategy-ui-design` |
| Inje Core | 363 PASS | `../work/inje-ui.log` |
| Bin Core + 신규 bridge | 187 PASS | `../work/bins-ui.log`, `artifacts/bins-ui` |
| Core 원본 검증 목록 | 103파일 일치 | `docs/core-source-manifest.json` |
| 통합 설명서 | Inje5개 + 전체 프로필1개 strict import PASS | `현재_전략_상세설명_및_JSON_가이드_2026-11-11.md` |
| macOS ARM64 실제 시작 | Success/StrategySettingsVerified true, 601봉·3시세, broker 호출 없음 | `artifacts/strategy-ui-native/result.json`, 네이티브 PNG3개 |
| 앱 서명 | codesign --verify --deep --strict 통과 | 패키지 및 설치 경로 |

신규 검사는 체크박스 상호 배제·접힌 헤더 표시·전체 필드 JSON 대응·비활성 값 보존·기간/단위/비용표·재시작 프로필·잘못된 입력의 저장 차단을 확인한다. Bin bridge에서는 원본 이벤트의 동일 엔진 사용, 스크리닝 주문0, 미래 첨부 봉의 영향0, 자료 누락 및 두 체계 혼합 실행 거절을 검사한다. 합성 결과는 실제 시장 수익성이나 공급자 계약 검증이 아니다.

## 설치

2026-11-11 11:50:43 KST `/Applications/Stock Development.app`을 검증본으로 교체했다. 당시 앱이 실행 중이 아님을 lsof로 확인했다. 사용자 DB·키·실제 기록을 열거나 수정하지 않았다. 새 앱은 원본과 같은 bundleID `com.stock.desktop.avalonia`, 표시 버전0.1.17, 소스 revision 표기 `local-inje-bin-ui-20261111`이다. GitHub의 동명 공개 버전과 내용이 다르다.

- 설치 Core SHA256: `2d2734bbc394145b1d5c08ff40eaf29cb9f9c32802a6bab02cabdf0cea2d29f1`
- 설치 UI SHA256: `e6e888826449c1829e6d1a90165bc9552a1f5e6fbb2a838ab574dec2081682b1`
- 이전0.1.16 앱: `/Users/jang-gyeongbin/Library/Application Support/Stock App Backups.noindex/2026-11-11-strategy-ui/Stock-v0.1.16-before-strategy-ui.saved-app`
- 설치 결과/이전·이후 hash: `../work/strategy-ui-install-result.json`, 백업 폴더의 `install-manifest.json`.

기존 앱은 `.saved-app`으로 미색인 폴더에 보관했다. 임시 패키지 앱은 설치 경로로 이동했고 해당 임시 경로의 LaunchServices 등록을 해제했다. 기존 source/ZIP/DMG나 다른 저장소의 StockViewer는 건드리지 않았다. 일반 사용자 데이터로 새 앱을 자동 실행하지 않았으며, 실제 계좌/공급자 시세 검증이나 Windows 실행 검사를 했다고 주장하지 않는다.

설치 후 전체 권한 범위에서 mdls의 bundleID와 mdfind 검색을 확인했다. 해당 bundleID의 검색 결과는 `/Applications/Stock Development.app` 한 개이며, LaunchServices도 같은 설치 경로 한 개만 등록되어 있다.
