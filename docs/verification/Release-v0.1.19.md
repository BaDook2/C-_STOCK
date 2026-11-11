# v0.1.19 릴리즈 검증 진행

사용자의 현재 전략 포함 새 릴리즈 요청에 따라 Windows x64/Apple Silicon 및 샘플9개를 준비했다. 현재 문서는 게시 완료 선언이 아니며 CI/공개 파일 확인 후 확정 결과를 기록한다.

샘플은 공개 합성 fixture에서 만든 전체 JSON이며 개인 DB를 배포하지 않는다. StrategySampleCatalog가 앱 시작에1회 추가하고 동일 이름 사용자 값·현재 선택·다른 기록을 보존한다. 이름 변경 후 재시작 중복을 방지한다. 원본 시세 요구와 실계좌 자동주문 잠금/Bin 실시간 미연결 차단을 유지한다.

로컬 Avalonia 빌드0경고/0오류, 샘플 기본 탑재·수치 일치·충돌 보존·재시작·원본 이벤트 미삽입 검사를 포함한 전체UI1009, Inje363/Bin187/디자인17/manifest105/설명서JSON15개 검사 통과. GitHub CI의 Windows·Mac 실제 수는 OS별로 구분해 기록한다. 로그는 ../work/release-0.1.19-*.log, 결과는 Git 제외 artifacts/release-0.1.19-*다.

호환 WPF 교차 빌드도0경고/0오류. Mac 격리 네이티브 시작은 Success/StrategySettingsVerified/StrategySamplesVerified/NoBrokerCalls true, 샘플9개/601봉/3quote였다. staged41파일에 개인 DB/키/바이너리가 없음을 확인했다. 다음은 양 플랫폼 CI·공개 파일 재다운로드 검증이다. 시작 주간 잔여23%, 중간22%/21%, 잔여15% 보존 조건 유지.
