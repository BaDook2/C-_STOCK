# 현재 — STOCK 앱 중복 정리 완료 (2026-11-11)

사용자 요청: stock 검색에 여러 .app이 나오는 문제 정리. 이 프로젝트 ID `com.stock.desktop.avalonia`인 생성/검사용 번들만 대상으로 했다. 앱 버전 교체/새 빌드/소스·사용자 DB·키·거래기록 수정은 하지 않았다. GitHub 게시 없음.

- [x] LaunchServices/Spotlight 및 실제 번들의 경로·ID·버전·Core hash 확인.
- [x] 설치 앱 `/Applications/Stock Development.app` 보존. 현재 설치본 v0.1.16이며 이번 정리에서 교체하지 않았다.
- [x] 프로젝트 artifacts 및 임시 폴더의13개 생성/검사 앱을 복구 가능한 보관 폴더로 이동. 다른 StockViewer 앱·Apple 주식 앱·WPF 소스 Stock.App은 정리 대상에서 제외.
- [x] 이 프로젝트의 중복·존재하지 않는 옛 마운트 앱 등록 해제. LaunchServices와 STOCK bundleID Spotlight 검색 모두 설치 앱1개만 남음 확인. 일반 stock 문자열 검색의 StockViewer는 별도 프로그램, Stock.App/StockViewer.App은 소스 폴더로 확인해 보존했다.
- [x] 보관13개 Core hash 대조 및 원래 경로 없음 확인. 설치 앱 Core hash 변경 없음·strict codesign 검증 성공.

보관: `/Users/jang-gyeongbin/Library/Application Support/Stock App Backups.noindex/2026-11-11-1002` 안의 `.saved-app` 폴더들. restore-manifest.json에 원래/보관 경로·버전·hash가 있다. 복원 시 필요한 폴더만 원래 .app 이름/경로로 되돌리고 등록한다. 원본 ZIP/DMG와 개발 소스/검증 로그는 유지했다.

기존 Bin Core 구현 완료/검증180개·Inje363개·Avalonia263개와 실제 지원 한계는 `docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md` 및 `docs/history/TASKS-before-app-cleanup-20261111.md` 참조. 설치 앱은 이 로컬 Core/소스와 다른 과거 버전이며 업데이트는 별도 요청 범위다. 완료 후 자동으로 앱 교체/실계좌 활성화/기록 삭제/게시를 진행하지 않는다.
