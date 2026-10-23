# Git 업로드 전 개인정보 보호

루트 `.gitignore`는 빌드/게시 산출물, `Stock.exe`, 로그, 테스트 결과, SQLite DB와 WAL/SHM, `.env`, 로컬 설정, DPAPI 보호 키 파일 및 `broker-keys` 폴더를 제외한다. 키움 공식 명세인 `docs/kiwoom-api-spec.json`은 문서이므로 제외하지 않는다.

계좌 키는 프로젝트가 아니라 `%LOCALAPPDATA%\StockPaperTrading\KiwoomQuant\broker-keys` 아래에 Windows DPAPI CurrentUser로 암호화해 저장한다. 소스·문서·로그에는 API Key, Secret, Access Token, 계좌번호 원문을 기록하지 않는다.

첫 커밋 전 `git status --short --ignored`와 비밀 문자열 검색을 확인한다. `.gitignore`는 이미 Git이 추적 중인 파일을 자동으로 제거하지 않으므로, 기존 저장소에 민감 파일을 이미 커밋했다면 해당 파일을 인덱스와 과거 이력에서 별도로 제거하고 키를 폐기·재발급해야 한다.
