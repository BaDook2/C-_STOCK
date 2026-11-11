# 현재 인수인계 — STOCK 앱 검색 중복 정리 완료

2026-11-11 사용자 요청으로 앱 사본을 정리했다. 소스/설정/계좌키/DB/실제 거래기록·설치 앱 버전은 변경하지 않았다. GitHub commit/push/태그/Actions/Release 없음.

## 앱 상태

유일하게 등록·검색되는 이 프로젝트 앱은 `/Applications/Stock Development.app`이다. bundleID `com.stock.desktop.avalonia`, 표시명 STOCK Avalonia, 설치 버전0.1.16. 정리 전후 Core SHA256 `a16364a9aa912ccdb40645cfc8703298e3514757ba17513cd6b19f60d26328cd` 일치, `codesign --verify --deep --strict` 종료0. 이번 요청은 정리이며 새 로컬 소스로 교체하지 않았다.

프로젝트 artifacts와/private/tmp에 남은 생성·검사 사본13개를 `/Users/jang-gyeongbin/Library/Application Support/Stock App Backups.noindex/2026-11-11-1002`에 01~13 `.saved-app` 폴더로 이동했다. 삭제하지 않았다. 같은 폴더의 restore-manifest.json에 원래 경로/보관 경로/버전/Core hash가 있고13개 모두 moved-and-verified다. Spotlight 미색인 폴더와 .app이 아닌 확장자로 보관했다. 설치 앱 외 중복 등록과 존재하지 않는 이전 DMG 마운트 등록은 해당 경로만 lsregister -u로 해제했다. 다른 앱 전체의 LaunchServices DB를 초기화하지 않았다.

정리 후 LaunchServices와 `mdfind 'kMDItemCFBundleIdentifier == "com.stock.desktop.avalonia"'` 모두 설치 앱1개만 반환했다. 일반 stock 앱 검색에는 설치 STOCK 외에 별도 저장소 C-_STOCK_Viewer의 StockViewer-macOS.app 및 이름이 .App으로 끝나 macOS가 앱으로 분류하는 WPF 소스 Stock.App/StockViewer.App도 나온다. 두 소스 폴더에는 Contents/Info.plist와 실행 번들이 없으며 경로를 바꾸면 개발 프로젝트에 영향이 있어 보존했다. 별도 StockViewer·Apple 주식도 정리하지 않았다. 따라서 STOCK bundleID 검색1개와 일반 문자열 검색 결과를 구분한다. 기존 ZIP/DMG/소스/검증 결과도 보존했다. 이전 검증 문서의 추출된 임시 .app 경로는 이제 보관 폴더로 옮겨져 직접 실행할 수 없고 원본 ZIP은 남아 있다.

실행 스크립트 `avalonia-scripts/build-and-run-macos.sh`는 명시 실행할 때 같은 설치 경로를 갱신한다. 사용자가 업데이트를 요청하면 별도 검증 후 실행하며, 정리 완료를 자동 교체 승인으로 보지 않는다.

## 근거·직전 개발 상태

저장소 밖 `../work/stock-app-registrations-before-20261111.json`, `stock-app-cleanup-candidates-20261111.json`, `stock-app-cleanup-result-20261111.json`, `stock-app-registrations-after-20261111.json`에 목록/전후 검증이 있다. `cleanup-stock-apps-20261111.py`는 정확한 ID/허용 경로/hash/실행 중 여부를 확인한 후 이동한 일회성 스크립트이며 같은 보관 폴더가 있으면 재실행을 거절한다.

직전 Bin Core 완료 상태는 `docs/history/HANDOFF-before-app-cleanup-20261111.md`와 `docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md` 참조. 로컬 소스에는 Inje UI와 Bin 독립 Core가 있으며 Bin UI/실피드/운영 복원은 아직 연결하지 않았다. 설치 앱0.1.16과 이 소스/이전 Inje ZIP을 혼동하지 않는다. 실계좌/실제 기록 삭제/새 GitHub 게시에는 별도 사용자 요청이 필요하다.
