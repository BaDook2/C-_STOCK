# 전환 설계

기존 WPF는 STOCK에 유지한다. 이 폴더는 독립 .NET10 Avalonia 앱이며 WPF XAML을 이름만 바꾸지 않는다. 계산·broker·Repository·백테스트·D런타임은 검증된 Core 소스 snapshot으로 재사용한다. UI·Dispatcher·file picker·dialog·OS 보안 저장을 플랫폼 adapter로 분리한다. 기능별 전환/검증은 TASKS에 표시한다.

복사는 Core의.cs/.csproj80개뿐. 원본 DB/API키/개인설정/.git/빌드·배포파일은 포함하지 않는다. 새 앱 저장 경로도 분리한다. 공통Core변경은 manifest와 양쪽 검사 갱신.

공식 확인(2026-11-08): [Avalonia 설치](https://docs.avaloniaui.net/docs/get-started/install-avalonia), [Desktop12.1.5](https://www.nuget.org/packages/Avalonia.Desktop/12.1.5), [ScottPlot.Avalonia5.1.59](https://www.nuget.org/packages/ScottPlot.Avalonia/5.1.59), [차트 시작](https://scottplot.net/quickstart/avalonia/), [Headless 검사](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform). ScottPlot 패키지는Avalonia>=12 요구. 설치된 API로 컴파일/검증한다.

Windows 실행/headless/cross-publish와 Mac 실제 실행을 구분한다. 현재 Mac 호스트 미제공. Mac 파일 접근/키 저장/차트 native 라이브러리/앱 서명·Gatekeeper는 실제 환경에서 별도 확인해야 한다. 미구현 기능을 작동하는 버튼/성공 검사로 위장하지 않는다.
