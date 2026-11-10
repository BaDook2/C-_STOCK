# Rider에서 수정 후 .app 만들기

`STOCK.Avalonia.sln`을 연 상태에서 상단 실행 설정을 선택한다.

- `Mac · .app 빌드 후 실행`: 저장한 소스로 앱을 만들고 macOS 앱으로 연다.
- `Mac · .app 만들기`: 앱만 새로 만든다.

설정이 보이지 않으면 솔루션을 다시 연다. `.run/*.run.xml`은 Rider의 기본 Shell Script 플러그인을 사용하는 프로젝트 실행 설정이다. 별도 플러그인 설치는 필요하지 않다. 공식 참고: [Rider Shell Script 실행 설정](https://www.jetbrains.com/help/rider/Run_Debug_Configuration_Shell_Script.html).

결과 경로는 `/Applications/Stock Development.app`이다. 응용 프로그램 폴더에서 더블클릭해서도 실행할 수 있다. .NET 런타임을 포함하는 self-contained Apple Silicon 앱이므로 별도 `dotnet run` 명령은 필요 없다. 다른 CPU의 Mac에서는 기존 publish 스크립트가 해당 CPU를 선택한다.

실행 중인 개발 앱은 먼저 앱 메뉴 또는 ⌘Q로 정상 종료한다. 빌드 스크립트는 실행 중인 앱이나 거래 작업을 강제 종료하지 않는다. 새 앱의 생성·서명이 성공한 다음 기존 개발 앱을 교체하며, 생성 실패 시 기존 앱을 유지한다. 실행 후 소스를 수정한 것만으로 이미 생성된 앱이 바뀌지는 않으므로 다시 ▶를 눌러 새 앱을 만든다. 코드 디버깅·중단점 작업에는 기존 `Stock.Avalonia` 실행 구성을 사용한다.

터미널에서도 같은 작업을 실행할 수 있다.

```sh
bash avalonia-scripts/build-and-run-macos.sh --open
bash avalonia-scripts/build-and-run-macos.sh --build-only
```

`STOCK_DEV_OUTPUT_DIR` 환경변수로 다른 로컬 출력 폴더를 지정할 수 있다. 동기화/File Provider 작업 폴더는 `.app`에 FinderInfo를 다시 붙여 엄격한 서명 검증을 실패시킬 수 있으므로 로컬 폴더를 사용한다. 이 환경의 Codex 작업 폴더에서 해당 현상을 재현했고, 로컬 임시 폴더에서 패키징한 뒤 `/Applications`로 옮겨 검증했다. 생성 앱의 FinderInfo/ResourceFork 속성만 정리하며 원본 소스·설정·개인 DB는 건드리지 않는다.

구현: `.run/Mac App Build and Run.run.xml`, `.run/Mac App Build.run.xml`, `avalonia-scripts/build-and-run-macos.sh`. 앱 구성·서명은 기존 `publish-macos.sh`를 재사용한다. GitHub Release·태그 생성은 수행하지 않는다. Apple Developer ID 공증은 포함하지 않는 로컬 개발용 ad-hoc 서명이다.

검증: 셸 문법/XML 파싱 성공, 실제 ARM64 self-contained publish 성공, 설치 후 `codesign --verify --deep --strict` 성공. `open -W -n`으로 앱 번들을 실행한 격리 시작 검사 결과 Success/NoBrokerCalls/SyntheticOnly 모두 true, 601봉·시세 2회. 근거: `artifacts/rider-app-smoke/result.json` (검사 시 macOS27.2/osx-arm64). 제품 소스 변경은 없으므로 전략 회귀 검사는 중복 실행하지 않았다. Rider 화면에서 실행 설정을 클릭하는 UI 조작은 별도 수행하지 않았다.
