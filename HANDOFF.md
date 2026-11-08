# 현재 — Windows v0.1.16 및 Apple Silicon macOS 앱 게시 완료

## Apple Silicon 앱 — 최신 완료

2026-11-08 사용자 요청을 이행했다. 기존 v0.1.16 앱/Windows 자산은 유지하고 macos-v0.1.16-r3(a2ba9d8c8991a0d60ab6bce06ddf8b4bacd87478)에서 Stock.app/DMG/ZIP을 생성했다. Mac ARM64 CI42487371788 전체 성공, Core192/UI275/디자인18/실제 더미 Keychain4 및 앱/ZIP/DMG 시작·공개 차트3종목 통과. 공개 재다운로드·CI 산출물·GitHub digest·SHA256 일치, Mac 이미지 확인. 현재 릴리즈에 Stock-AppleSilicon-v0.1.16-r3.dmg(53,161,455bytes, SHA256 3d06c06bd8166dd0ef8e2c7b47299215eab08a0df8fe8ddbd320d05a90e0cbbe)과 ZIP/개별 체크섬을 추가했다. docs/avalonia/MACOS_APPLE_SILICON.md가 검증/설치 정본이다. Developer ID/공증은 없으며 최초 실행 안내를 포함했다. 실제 돈 주문0. 이 Mac 게시 요청도 완료했으므로 새 요청 없이 추가 태그/Release하지 않는다.

2026-11-08. “미완료 기능부터 완성” 답변에 따라 E1~E4를 구현했고, 앞선 조건부 Windows 교체·릴리즈 요청을 검증 후 이행했다. 현재 진행 작업 없음. 이전 “답변 대기/교체 전/주간40% 중단” 기록은 이력이다. 추가 릴리즈는 새 명시 요청이 필요하다.

## 현재 소스·실행 파일

- 공개 Git 소스: db1ee44e3083118b05982f4574446a8ba8f7a787 / tag v0.1.16.
- https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.16
- https://github.com/BaDook2/C-_STOCK/actions/runs/42480054799 전체 성공.
- Stock.exe 107,497,278 bytes / FileVersion0.1.16.0 / SHA256 993dcfeee8fad08be12fbe5ad2bf81e30f2a40259d824155f103ad75c1cff266.
- STOCK/Stock.exe와 release/Stock.exe 및 sibling stock_avalonia/Stock.Avalonia.exe가 모두 위 공개 바이너리다.
- 이전 WPF E433834B...1934 및 별도 Avalonia258F8CB6...C095E는 artifacts/before-avalonia-v0.1.16-20261108-215113에 해시 확인해 보존했다.
- 원본 WPF Stock.App/Stock.sln은 유지한다. 새 UI 개발/시작 프로젝트는 STOCK.Avalonia.sln / Stock.Avalonia. 공통 Core85개를 공유하며 Git용 manifest는 UTF8/LF 정규화 SHA다.
- sibling stock_avalonia는 별도 개발 사본이다. Core 변경은 양쪽/manifest/검사를 함께 갱신한다. 개인 DB/키/설정/.git/빌드 출력을 복사하지 않는다.

## 검증 근거

Git tree211849d7만 새 경로에 추출해 양 Release 빌드0경고0오류, WPF1652/D13/Core192/Avalonia273/디자인18 성공. 실제공개28 및 Windows 일반 EXE 입력·검색·재실행/백테스트·차트·달력 검수도 성공.
공개 파일 재다운로드→GitHub digest/.sha256/파일SHA/버전 일치→격리 시작0/601봉·2quote/NoBrokerCalls→실제 검색4회/백테스트4거래/+66,000/양방향팬·단일클릭·거래 행 선택/익절빨강·손절파랑→백업·교체 후 루트 검색4회와 별도 루트 시작까지 성공했다.
증거: artifacts/releases/v0.1.16-verified. 검증 중 만든 모든 일반 앱은 정상 닫았다. 실제 돈 주문0, 원본 DB/키 읽기·복제·삭제0.

## 동작 경계

키/기본 연결은 OS 보안 저장, 검증 후 저장한다. 실제 주문은 기본 잠김/활성화·건별 확인과 기존 최종 전송 guard를 거친다. 실전 자동은 기존 키움 국내+검증 facts/실제 거래대금 입력이 필요하며 전략 D 실전은 미연결이다. 다른 브로커 수동 주문과 구분한다.
일봉 가상은 다음 거래일의 신선한 관측 이후 주문·후속 관측 체결 모형이다. 공식 시가 보장 아님. checkpointv2 원자 저장/대사, paused 복원, 보호 재개도 신규/추가매수OFF다. raw tick/일반 시세 캐시는 메모리에만 둔다.
원본 WPF와 새 앱 저장소는 별도다. 기존 자료를 자동 이관하지 않으므로 계좌를 새 앱에서 다시 연결한다. Mac ARM64 CI의 시작·공개 차트·더미 Keychain은 검증했지만 사용자 Mac의 DPI/트랙패드/키체인 권한·잠금, Developer ID·공증과 실제 증권사 API는 미검증이다. 이전 양RID 교차 빌드와 이번 Mac 네이티브 CI를 구분한다.

## 다음 시작

새 요청 없으면 완료 상태를 유지한다. [구현](docs/avalonia/COMPLETION_E1_E4_20261108.md), [최종 검증·첫 CI 실패 원인](docs/verification/Avalonia-Release-2026-11-08.md), README-Avalonia.md를 우선한다.
추가된 검증 도구 avalonia-scripts/verify-release-download.ps1은 성공한 RunId/SourceCommit/새 Output을 요구하며 실제 계좌 대신 격리 시작만 검사한다. 기존 v0.1.15는 ignore가 Stock.App까지 제외해 CI 실패/미공개한 이력이며 태그를 덮어쓰지 않았다. 문서·검증 도구만 정리한 후속 커밋으로 새 릴리즈를 반복하지 않는다.
