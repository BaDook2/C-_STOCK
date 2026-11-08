# 완료 — 진행 중 스크리닝 결과·애플 차트·DB 복구

## 구현

일반 스크리닝에서4개 병렬 작업을 모두 기다리던 Task.WhenAll을 Task.WhenEach로 바꿔 완료 종목부터 즉시 반영한다. 현재 검사 snapshot이 있으면 결과 창을 열고, 완료된 후보의 이름/더블클릭/백테스트 보기로 독립 차트를 연다. 완료되지 않은 종목을 임의로 평가하지 않는다.

- CanReviewScreeningResults는 일반 검사 중 결과 열람만 허용한다. 결과열기 명령은 공통 Run의 executing 게이트를 통과시키지 않고 기존 OpenScreeningStock 경로를 직접 호출한다. 시장·전략·봉·기간 및 중복 시작 잠금은 유지한다.
- 동일 당시 설정/원본으로 IOversoldBacktestService를 호출한다. 주 스크리닝의 취소 토큰·메인 차트·종목 선택을 바꾸지 않는다. 다음/이전은 동일 실행에서 새로 완료한 종목도 반영한다.
- 결과 창250ms 갱신은 선택 종목을 유지한다. 저장 오류는 자료부족/조회실패로 위장하지 않고 병렬 작업을 취소하고 오류를 노출한다. 차트 캐시 저장 실패도 네트워크 실패로 오인하지 않는다.

수정: OnlyOversoldWorkspace, OversoldScreeningWorkspace, ScreeningNavigation, ScreeningResultDialog, MainWindow.xaml, WorkspaceViewModel. 검사: ProgressiveScreeningTests/ScreeningUiTests. 주말에도 장중 시나리오를 검사하도록 공개 시세 fixture의 날짜/시각을 주입했다. 제품의 실제 장외 제한을 완화하지 않았다.

## 검증

- 관련 실제 WPF352개 성공.1개 완료/3개 요청 대기 중 결과·차트 버튼 활성, 설정 잠금, 결과 팝업/공통 차트 열기, 스크리닝 유지, 추가 결과 반영/선택 유지/요청 중복 없음/최종 기록을 확인했다.
- 최종 빌드0경고·0오류, 전체1,342개 종료0. 실제 공개 검색/애플 일봉 검사2개도 성공했다.
- 로그 artifacts/progressive-screen-{build-final,full,related,publish}.log. 배포/루트 EXE 실행 결과와 해시는 아래 최종 확인에 기록한다.

## 애플 원인과 실제 DB 정정

검색 API는 애플/AAPL/Apple에 AAPL.O를 반환한다. 실제 사용자 창에도 애플 검색 결과가 있었다. 애플을 클릭하면 차트 자료의 SQLite State 쓰기/읽기에서11/11을 재현했다. 정상인 공개 일봉 대신 AAPL 캐시가 위치한 DB 페이지 손상이 즉시 실패 원인이다. 손상을 처음 발생시킨 사건은 아직 확정하지 못했다.

이전 도구 점검은 AppData가 Codex LocalCache로 리다이렉트된 다른144,805,888bytes 파일을 검사했었다. 사용자 실행 앱의 실제 DB392,929,280bytes와 다르다. 앞선 정상 판단은 실제 DB의 증거가 아니다. StorageInspection.PhysicalDatabasePath, StoragePaths, 저장소 점검 화면 및 단위 검사를 추가해 열린 파일의 최종 경로도 보고한다.

앱을 작업 없는 상태에서 정상 종료했다. Explorer 문맥으로 실제 원본을 artifacts/storage-user-20261107-original/workspace-v2.db에 보존했다. SHA256 5F987540CED3935BE404E5E73345D643EF0C6F6EB22DA45B1A88801F78E4E318. 실제 원본 손상은 Python과 앱3.53.4에서 확인했다. 이후 아래 사용자 승인을 받아 실제 활성 DB를 교체했다. 원본 바이트와 API키는 보존했고 실제 주문은 실행하지 않았다.

공식 SQLite3.54.1 CLI를 공식 다운로드 manifest의 SHA3(E171F3993A992EDF076746926DD52B5E2EBFABAEE38818E74987E1D8112AB987에서 공백 제외)와 대조하고 사본에만 복구했다. `.recover --ignore-freelist` 방법은 https://sqlite.org/recovery.html 를 따른다. 앱 검토용 application-review.db는 구조·활성 JSON 검사에 통과했다.

- 정상1070개 state/11933개 events/30개 runs는 원본의 모든 컬럼과 정확히 일치한다.
- state13개는7개 읽히지만 잘못된 JSON/6개 읽기 불가로 활성 기록에서 분리하고 격리 목록과 가능한 원본 바이트를 보존했다. 캐시·스크리닝·백테스트 관련 기록이다.
- 원본 틱6건은3개 읽기 오류/3개 원래 행 부재로 정상 복구를 확인하지 못했다. 확인되지 않은 값을 생성하지 않는다. 검증된 틱 coverage 주장은0개다.
- lost_and_found3개는 어느 활성 테이블에도 임의 편입하지 않았다. 원본/SQL 복구 자료/격리 내용을 모두 보존하고 Git에서 제외한다.

일부 기록의 정상 복구를 확인하지 못한 범위를 안내한 뒤 사용자가 **“복구 DB로 교체하고 원본 보존”**을 선택했다. 승인에 따라 실제 사용자 프로필에서 원본을 `workspace-v2.before-repair-20261107.db`로 보존하고, 검증된 application-review.db를 활성 `workspace-v2.db`로 적용했다. 도구의 AppData 리다이렉션을 피하기 위해 실제 사용자 Explorer 문맥에서 파일을 옮겼다. 기존 키 파일은 변경하지 않았다. 상세 절차는 docs/maintenance/Storage.md.

복구 파일 적용 전 SHA256은 `387F2CED4046A3F1973AC8732F2CF8D8447AA993F9842CB98706F871060E50CB`, 원본 파일은 위5F9875 해시다. 실제 앱을 다시 실행한 후 새 Apple 캐시가 저장되므로 활성 DB의 해시는 이후 바뀔 수 있다. 개인 근거: artifacts/storage-user-20261107-original/{application-review,exact-record-comparison,native-review,native-validation}.json. 원본·복구 자료는 Git 제외다.

## 최종 확인

- 최종 Release 빌드0경고·0오류, 관련 WPF352개/전체1342개 통과. publish/루트 EXE smoke 모두 종료0/Success=true/NoBrokerCalls=true. 합성 검사와 실제 API/사용자 앱 확인을 구분한다.
- publish/win-x64/Stock.exe, 루트Stock.exe, release/Stock.exe는80,850,366bytes, SHA256 `AA641B1964C1C0DB83970E5EBA8C9542B5C6EB300BF26194F04DE77786135B49`로 일치한다. release/Stock.exe.sha256 갱신. 이전 EXE는 artifacts/before-progressive-screen-20261107-110322에 보존했다.
- 실제 사용자 Explorer에서 루트 Stock.exe를 실행해 해외 → 애플 검색 → 가격342.65 USD/일봉/거래량 표시를 확인했다. 실계좌 연결이나 주문은 실행하지 않았다.
- 실제 앱의 테스트 기록 → 저장소 점검: 논리·실제 파일 모두 사용자 정상 AppData 경로, SQLite3.53.4, integrity ok, JSON 오류0, state1071/events11933/runs30. state는 정상1070개 복구 후 새 Apple 캐시1개가 추가된 값이다. 보고서 inspection-20261107-020937-095.json. Codex LocalCache를 검사한 결과가 아니다.
- 앱은 애플 차트를 표시한 채로 남겨 두었다. TASKS/HANDOFF 완료. GitHub commit/push/tag/Release 없음.
- 최초 물리적 손상 유발 사건은 여전히 미확정이다. 이번 복구가 원인 확정이나 모든 데이터의 완전 복구를 뜻하지 않는다.
