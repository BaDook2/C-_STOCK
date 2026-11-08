# 현재 상태 — 2026-11-07 요청 완료

## 완료 범위

일반 스크리닝 진행 중 완료된 종목부터 결과·차트를 열도록 구현했다. 애플 검색은 정상이었으며 실제 사용자 DB의 AAPL 캐시 손상 때문에 차트 조회가 실패했다. 사본 복구·비교 후 사용자의 명시 승인에 따라 실제 DB를 교체하고 원본을 보존했다. 로컬 Stock.exe 갱신·실제 사용자 창 확인까지 완료했다.

## 구현 진입점

- OnlyOversoldWorkspace: 4개 병렬 조회를 Task.WhenEach로 완료 즉시 반영. StorageException은 자료부족으로 위장하지 않고 동료 요청을 취소한다.
- OversoldScreeningWorkspace/ScreeningNavigation: CanReviewScreeningResults로 일반 검사 중 결과·완료 종목 차트만 독립 명령으로 허용. 설정/중복 시작 잠금·당시 snapshot/공통 백테스트 엔진 유지. 이전/다음 목록은 동일 실행의 추가 완료 종목도 반영한다.
- MainWindow.xaml/ScreeningResultDialog: 결과 버튼까지 끄던 조상 잠금 제거, 개별 설정 잠금 유지. 250ms 결과 갱신 시 선택 종목 유지.
- WorkspaceViewModel: 공개 자료 조회 실패와 DB 캐시 쓰기 실패를 구분한다.
- StoragePaths/StorageDiagnostics/StorageWorkspace: GetFinalPathNameByHandleW로 실제 열린 파일 경로를 CLI/앱 진단에 표시한다.
- ProgressiveScreeningTests/ScreeningUiTests/StorageDiagnosticsTests: 진행 중 실제 WPF 버튼/결과/차트·선택 유지·실제경로 검사. 공개 장중 fixture는 고정 평일 시각을 주입하며 제품 장외 제한은 유지한다.

## 검증·실행 파일

- Release 빌드0경고·0오류, 관련 WPF352개, 전체1342개 통과. 공개 Apple/지수 조회2개 성공. 전체 검사는 fake broker/격리 DB이며 실주문이 아니다.
- publish 및 루트 EXE smoke 종료0/Success=true/NoBrokerCalls=true.
- publish/win-x64/Stock.exe, 루트Stock.exe, release/Stock.exe:80,850,366bytes / SHA256 `AA641B1964C1C0DB83970E5EBA8C9542B5C6EB300BF26194F04DE77786135B49`. release 체크섬 갱신. 이전 EXE는 artifacts/before-progressive-screen-20261107-110322 보존.
- 로그 artifacts/progressive-screen-{build-final,related,full,publish}.log 및 progressive-screen-{publish-smoke,root-smoke}/result.json.
- 실제 사용자 Explorer에서 루트 EXE 실행 → 오프라인 애플 검색/342.65 USD/일봉 차트 확인. 앱 저장소 점검 integrity ok/JSON0/state1071/events11933/runs30. 보고서 inspection-20261107-020937-095.json. 실제 키 변경/주문 없음. 창은 애플 차트로 남겨 두었다. HWND/프로세스는 다음 작업 시 다시 조회한다.

## 실제 DB 복구와 중요한 경로 정정

- 논리 경로 C:/Users/wkdru/AppData/Local/StockPaperTrading/KiwoomQuant/workspace-v2.db를 도구에서 열면 Codex LocalCache의144,805,888bytes/state42/events110238/runs9 파일로 리다이렉트된다. 예전 정상 점검과5백테스트/2스크리닝 보존 주장은 실제 사용자 DB의 근거가 아니었다. 실제 앱과 열린 핸들 최종 경로·크기·행 수를 반드시 대조한다.
- 실제 사용자 원본392,929,280bytes/state1083/events11939/runs30에서 B-tree/overflow 손상·AAPL 캐시 오류를 재현했다. 앱 정상 종료 후 사용자 Explorer 문맥으로 보존했다. 최초 손상 유발 사건은 미확정이다.
- 승인 “복구 DB로 교체하고 원본 보존” 이후 실제 프로필에서 원본을 `workspace-v2.before-repair-20261107.db`로 보존하고 검증된 복구 파일을 활성 workspace-v2.db로 적용했다. 도구 LocalCache DB나 키 파일을 수정하지 않았다.
- artifacts/storage-user-20261107-original/workspace-v2.db 원본 SHA256 `5F987540CED3935BE404E5E73345D643EF0C6F6EB22DA45B1A88801F78E4E318`.
- 사본 복구 application-review.db SHA256 `387F2CED4046A3F1973AC8732F2CF8D8447AA993F9842CB98706F871060E50CB`. 공식 SQLite3.54.1 .recover --ignore-freelist, 앱3.53.4 구조/JSON 검사 성공. 정상1070state/11933events/30runs 모든 컬럼 동일. state13개 격리·원본틱6건 정상복구 미확인·lost_and_found3개 미배정. 사용자 승인에 이 제한을 포함했다.
- 개인 원본/복구 SQL/격리/검사 JSON 모두 Git 제외 artifacts에 보존. exact-record-comparison.json, native-review.json, application-review.json, native-validation.json을 참조한다. DB/실제 진단/키를 Git에 올리지 않는다.

## 남은 작업·게시 정책

요청 작업 완료. 새 요구 없이 검사·DB 교체·태그·릴리즈를 반복하지 않는다. 현재 사용자는 명시 요청 시에만 GitHub Release를 원한다. 이번 commit/push/tag/Release 없음. 공개 v0.1.14에는 이번 로컬 변경이 포함되지 않는다.

기능/검증 상세: docs/verification/ProgressiveScreening-2026-11-07.md. 이전 기능(근접 후보·장외 안내·미국 일봉/지수·통합 기록): docs/verification/ResearchExpansion-2026-11-06.md. 저장소: docs/maintenance/Storage.md. 빌드 명령: docs/maintenance/Verification.md. 이전 인계 원문: docs/history/HANDOFF-before-ProgressiveScreening-20261107.md.
