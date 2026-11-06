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

## 검증 후 릴리즈 루틴

사용자는 2026-11-06부터 제품 변경의 필수 검사가 모두 통과하면 GitHub 릴리즈까지 게시하도록 요청했다. 이 범위의 커밋/푸시/릴리즈는 추가 확인 없이 수행한다.

1. 관련 검사, 전체 회귀, Release 빌드, 단일 EXE smoke의 종료코드와 결과를 확인한다. 실패/차단 검사가 있으면 원인을 고치거나 TASKS에 기록하고 배포를 보류한다. 실제 주문은 실행하지 않는다.
2. 원격 main/기존 태그와 변경 파일을 확인한다. 키·DB·실제 진단 자료·개인 설정이 Git에 포함되지 않도록 검사한다. 변경을 커밋하고 충돌 없는 새 `vX.Y.Z` 태그를 push한다. 기존 태그는 덮어쓰지 않는다.
3. `.github/workflows/release.yml`은 태그 push 시 Windows에서 복원 → 빌드 → 전체 검사 → 단일 파일 publish → 배포 EXE smoke → SHA256 → GitHub Release 순서로 실행한다. 모든 필수 단계가 성공해야 Release 단계에 도달한다.
4. Actions 성공과 릴리즈의 소스 커밋을 확인하고 `Stock.exe`, `Stock.exe.sha256`을 다시 내려받는다. 계산한 해시/체크섬/공개 자산 digest를 대조하고 내려받은 EXE로 격리 smoke를 실행해 종료0, Success/NoBrokerCalls=true를 확인한다.
5. 실행 중인 앱이 없는지 확인한 뒤 검증한 공개 EXE를 루트 `Stock.exe`와 `release/Stock.exe`에 반영한다. 버전/소스 커밋/검사 수/빌드/Actions/해시/링크/남은 실제 제한을 TASKS/HANDOFF에 기록한다. 릴리즈 기록만 갱신하는 문서 커밋은 추가 릴리즈를 만들지 않는다.

실제 API 검증과 fixture 검증은 구분한다. 재현되지 않은 과거 장애는 미확정으로 공개하며, 현재 검사 성공을 과거 원인 확정이나 모든 시장 상황의 보증으로 표현하지 않는다.
