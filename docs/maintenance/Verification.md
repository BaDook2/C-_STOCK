# 도움말·StockViewer 가격·JSON 검증 — 2026-11-11 후속

[최신 검증 기록](../verification/Viewer-Price-JSON-2026-11-11.md): Avalonia861/Inje363/Bin187 PASS, 현 전략 JSON의 변경값 Inje166개(Advanced+Standard)/Bin172개, 실제 물음표 팝업, Mac 합성 시작/실제 공개 차트, StockViewer 동시 가격 비교. Release 빌드0경고/0오류, Core manifest104파일. 13:36 KST 로컬 설치 앱 교체. GitHub 게시·개인 DB 수정·실계좌 주문 없음.

# 두 전략 UI 연결 검증 — 2026-11-11

현재 결과: [두 전략 UI 검증 기록](../verification/Strategy-UI-2026-11-11.md). Avalonia492, Inje363, Bin187개 합성 검사, 디자인17개, Core manifest103파일, 설명서 JSON6개, Avalonia/WPF 빌드0경고·0오류, macOS 네이티브 합성 시작·전략 화면 검사·서명 검증 통과. WPF는 Mac에서 교차 컴파일한 결과이며 Windows 실행 검사가 아니다. Bin 실시간 가상 주문/재시작 복원은 미연결로 시작 차단한다. GitHub 업로드/릴리즈 없음.

# 현재 로컬 Bin·Inje 검증 — 2026-11-11

Bin 독립 Core의 최신 결과는 [Bin 검증 기록](../verification/Bins-2026-11-11.md)이다. `dotnet run --project Stock.Bins.Tests -c Release -- artifacts/bins-tests`를 기본 로컬 검사에 추가했다. 전체 설정 JSON 검사는 `--validate-guide docs/strategy/Bins_Strategy_구현_검증_가이드_2026-11-11.md`다. UI/실계좌/실제 거래기록/공개 배포를 변경하는 작업은 이번 Bin 범위가 아니다.

GitHub 업로드 금지. 이 작업에서는 아래 로컬 검사만 수행하며 릴리즈/Actions/실계좌를 실행하지 않는다. 최신 근거는 [Inje 검증 기록](../verification/Injes-2026-11-11.md)이다.

```sh
dotnet build STOCK.Avalonia.sln -c Release
dotnet run --project Stock.Portable.Tests -c Release --no-build
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/injes-ui
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/injes-design --design
dotnet run --project Stock.Portable.Tests -c Release --no-build -- --validate-guide docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-11.md
# macOS에서 WPF 컴파일만 검증 (Windows 실행 검사와 다름)
dotnet build Stock.sln -c Release -p:EnableWindowsTargeting=true
# 실제 Windows에서 수행할 현재 suite
dotnet run --project Stock.Tests -c Release -- --current-checks artifacts/injes-wpf
```

새 기능은 위 suite로 확인한다. 아래 구전략/D 전용 명령·과거 검사 수·릴리즈 명령은 과거 버전에 대한 기록이며 현재 변경의 통과 근거로 사용하지 않는다.

---

# 검증 명령

작업 위치는 저장소 루트다. 아래 명령은 실계좌 주문 없이 실행한다. 일반 실행 중인 앱이 빌드 출력 DLL을 잡고 있으면 먼저 정상 종료한다.

```powershell
dotnet build STOCK.sln -c Release -m:1 -nr:false -p:UseSharedCompilation=false
& ./Stock.Tests/bin/Release/net10.0-windows10.0.19041.0/Stock.Tests.exe --store-lifecycle-checks
& ./Stock.Tests/bin/Release/net10.0-windows10.0.19041.0/Stock.Tests.exe --ui-smoke artifacts/audit-ui
& ./Stock.Tests/bin/Release/net10.0-windows10.0.19041.0/Stock.Tests.exe artifacts/audit-full-ui
dotnet publish Stock.App/Stock.App.csproj -c Release -p:PublishProfile=Win64 -m:1 -nr:false -p:UseSharedCompilation=false
```

- `--store-lifecycle-checks`: 실제 손상 페이지·오류 보고서·JSON 분리·재연결/파일 핸들·늦은 콜백 검사.
- `--ui-smoke`: 실제 WPF 컨트롤, fixture 시세/가짜 증권사, 팝업·차트·전략·계좌/초기화·다량 매매·긴급중지 검사. `button-audit-clicks.txt`는 명령 연결을 포함한 실제 Invoke/팝업 실행 목록이다.
- 전체 인자 실행: Core/전략/API 계약/저장/모의/위험/기존 화면 검사까지 실행한다. 로그 마지막 PASS 개수와 ExitCode를 둘 다 확인한다.
- 단일 EXE: `--smoke-test <출력폴더>`를 숨김 프로세스로 실행한다. `result.json`의 Success/NoBrokerCalls를 확인한다.
- 사용자 실제 DB: `--storage-check`는 [저장소 운영](Storage.md) 참고. DB 절대 경로를 명시한다.
- 실제 시세/계좌 검사는 주문과 분리한다. 키/토큰/잔고 원문을 로그에 쓰지 않는다. API 성공을 가짜 응답 검사로 대체했다고 보고하지 않는다.

각 변경에 맞는 검사를 먼저 실행한 뒤 전체 회귀는 한 번 수행한다. 실패나 코드 추가 수정이 있을 때만 관련 검사를 반복한다. 공유 Roslyn compiler가 멈추면 이번 빌드의 프로세스만 확인·정리하고 위의 비공유 단일 노드 명령을 사용한다.

## 명시적인 릴리즈 요청을 받은 경우의 절차

최신 사용자 요청(2026-11-06)에 따라 GitHub Release는 사용자가 명시적으로 요청할 때만 게시한다. 이전의 검증 성공 후 자동 릴리즈 지시는 철회됐다. 작업 완료·검사 성공·작업 재개·소스 push 요청은 릴리즈 요청으로 간주하지 않는다. 요청이 없으면 아래 배포 절차를 실행하지 않고, 배포 태그 생성/push나 릴리즈 Actions 실행도 하지 않는다. 구현·로컬 빌드/테스트·문서 갱신은 기존 완료 조건대로 수행한다.

1. 관련 검사, 전체 회귀, Release 빌드, 단일 EXE smoke의 종료코드와 결과를 확인한다. 실패/차단 검사가 있으면 원인을 고치거나 TASKS에 기록하고 배포를 보류한다. 실제 주문은 실행하지 않는다.
2. 원격 main/기존 태그와 변경 파일을 확인한다. 키·DB·실제 진단 자료·개인 설정이 Git에 포함되지 않도록 검사한다. 변경을 커밋하고 충돌 없는 새 `vX.Y.Z` 태그를 push한다. 기존 태그는 덮어쓰지 않는다.
3. `.github/workflows/release.yml`은 태그 push 시 Windows에서 복원 → 빌드 → 전체 검사 → 단일 파일 publish → 배포 EXE smoke → SHA256 → GitHub Release 순서로 실행한다. 모든 필수 단계가 성공해야 Release 단계에 도달한다.
4. Actions 성공과 릴리즈의 소스 커밋을 확인하고 `Stock.exe`, `Stock.exe.sha256`을 다시 내려받는다. 계산한 해시/체크섬/공개 자산 digest를 대조하고 내려받은 EXE로 격리 smoke를 실행해 종료0, Success/NoBrokerCalls=true를 확인한다.
5. 실행 중인 앱이 없는지 확인한 뒤 검증한 공개 EXE를 루트 `Stock.exe`와 `release/Stock.exe`에 반영한다. 버전/소스 커밋/검사 수/빌드/Actions/해시/링크/남은 실제 제한을 TASKS/HANDOFF에 기록한다. 릴리즈 기록만 갱신하는 문서 커밋은 추가 릴리즈를 만들지 않는다.

실제 API 검증과 fixture 검증은 구분한다. 재현되지 않은 과거 장애는 미확정으로 공개하며, 현재 검사 성공을 과거 원인 확정이나 모든 시장 상황의 보증으로 표현하지 않는다.

최근 릴리즈 검증 근거: [v0.1.11](../verification/Release-v0.1.11.md). GitHub Windows의 전체 회귀는 이 버전에서23분4초가 걸렸다. 로컬 실행 시간만으로 CI 중단 여부를 판단하지 말고 단계 상태/이전 성공 로그를 확인한다.

v0.1.12의 로컬 단일 파일 게시에서는 게시 전용 컴파일이 일반 빌드보다 오래 걸렸으나 모든 분석기와 smoke를 통과했다. 전체 회귀 후 게시까지 끝낼 수 있도록 Release job 제한은45분이다. 검사 단계/성공 조건은 동일하다.

## 메모리 시세·모드 전환 검사

`Stock.Tests.exe --memory-mode-checks <출력폴더>`는 격리 DB와 가짜 증권사로 시세/틱 비영속성, 기존 행 보존, 테스트 snapshot 유지, 재실행 시 캐시 없음, 가격 내림/원본 정밀도, 일봉 완성 시점, 반복 모드 전환의 추가 인증·계좌 요청0, 오프라인 긴급중지의 재연결 가드, 우측 탭 정렬/양방향 스크롤바를 검사한다. mode-switch-timing.json은 fixture에서 측정한 모드 왕복 시간이며 실제 네트워크 지연 측정값이 아니다.
