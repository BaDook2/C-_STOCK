# Avalonia Windows 릴리즈 검증 — 2026-11-08

## 구현 범위

키·기본 계좌 영속화(Windows DPAPI / Mac Keychain adapter), 수동 실제 주문·취소·확인 체결, 기존 키움 국내 전략 실행 adapter, 일봉 가상 실행·checkpoint v2/보호 재개를 추가했다. 상세 파일/설계/제약은 `../avalonia/COMPLETION_E1_E4_20261108.md`에 기록했다. 실제 돈 주문은 0건이다.

## 소스 및 검사

- 소스 commit: `db1ee44e3083118b05982f4574446a8ba8f7a787`, tag `v0.1.16`.
- 로컬 파일 의존을 배제하기 위해 Git tree `211849d79933e6ae0dca71fa0affaabd02bb7ee5`만 빈 경로에 추출했다.
- 이 경로에서 WPF/Avalonia Release 빌드 각각 경고 0 / 오류 0.
- WPF 전체 1,652 / 전략 D 화면 13 / portable Core 192 / Avalonia 기능 273 / 디자인 검사 성공. 공통 source manifest 85개 일치.
- 실제 공개자료 검사 28개, 일반 앱 검색·Enter·반복/재실행 4회, 백테스트·양방향 팬·단일 클릭/행 선택·달력 및 익절 빨강/손절 파랑 검수 완료. 이는 로컬 최종 후보의 검수이며 공개 EXE 검수와 구분한다.

## 첫 CI 실패 원인과 수정

`v0.1.15`는 빌드 실패로 공개되지 않았다. 새 `*.app/` Git 제외 규칙이 Windows 대소문자 무시로 `Stock.App`에도 적용돼 기존 로컬에만 있던 소스 9개가 커밋에서 빠졌다. 규칙을 `Stock.Avalonia.app/`로 좁히고 9개를 포함했다. 기존 태그/자산은 덮어쓰지 않았다. 위 Git-only 경로 빌드/전체 검사로 누락 문제가 해결됐음을 확인했다.

## 게시 상태

[v0.1.16 Actions](https://github.com/BaDook2/C-_STOCK/actions/runs/42480054799)는 전체 성공했다. [Windows 릴리즈](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.16)는 2026-11-08 12:46:30 UTC에 공개됐다. 빌드·전체 검사·단일 EXE 시작 검사 성공 후에만 게시했다. 직전 v0.1.14의 원격 전체 검사 소요는 1,291초였으며 이번에도 검사 중이라는 이유로 생략하지 않았다.

공개 Stock.exe 107,497,278 bytes, FileVersion 0.1.16.0, ProductVersion 0.1.16+db1ee44e3083118b05982f4574446a8ba8f7a787. SHA256:

~~~text
993dcfeee8fad08be12fbe5ad2bf81e30f2a40259d824155f103ad75c1cff266
~~~

- GitHub asset digest / 공개 .sha256 / 내려받은 파일의 SHA256 일치.
- 공개 EXE 격리 시작 Exit0 / Success / NoBrokerCalls / 601봉·2quote.
- 공개 EXE 일반 실행·실제 키보드/마우스 삼성전자 → SK하이닉스 Enter → 삼성전자 반복 → 종료/재실행 4회 성공. SCREEN 봉 픽셀도 확인했다.
- 공개 EXE 실제 공개자료 2026-08-01~10-30 백테스트 4거래 / 승률75.00% / +66,000 / MDD0.40%. 익절 +98,000 배지는 빨강322·파랑0픽셀, 손절 -32,000은 파랑333·빨강0픽셀. 양방향 드래그/복귀·단일 클릭 상세·해당 거래 행 선택·202×201 달력 성공.
- 원본/백업 해시 확인 후 STOCK/Stock.exe, STOCK/release/Stock.exe, stock_avalonia/Stock.Avalonia.exe를 같은 공개 바이너리로 교체했다. 교체한 실제 루트 Stock.exe에서 일반 검색·재실행4회, 별도 Avalonia 루트 EXE 시작도 다시 성공했다.
- 기존 바이너리: STOCK/artifacts/before-avalonia-v0.1.16-20261108-215113. WPF 이전 SHA256 E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934. 별도 Avalonia 이전258F8CB6...C095E도 함께 보존했다. 원본 WPF 소스/DB/계좌키는 삭제·복제·수정하지 않았다.
- 교체의 첫 호출은 PowerShell null 문자열 인자로 파일 교체 전에 실패했다. 기존/백업 불변을 확인하고, 작은 사본의 원자 교체 시험 후 구체적 백업 경로를 사용해 교체·전후 해시 검증을 완료했다.

로컬 증거는 STOCK/artifacts/releases/v0.1.16-verified의 verification.json, startup-smoke/result.json, native-search/result.json, native-backtest/result.json/PNG, root-native-search/result.json, separate-root-smoke/result.json, promotion.json이다. 실행 파일·개인 자료·검수 로그는 Git에 포함하지 않는다. 재검증 도구는 avalonia-scripts/verify-release-download.ps1이며 RunId/SourceCommit/새 Output 경로를 명시한다.

## 검증 경계

실제 증권사 로그인/원격 주문·실제 돈 주문·Mac 실기/Keychain 권한·서명/공증은 검사하지 않았다. Mac 양 RID 교차 빌드는 실기 검증이 아니다. 새 앱 저장소는 원본 WPF와 분리되며 기존 키/DB를 자동 이관하지 않는다. 실제 전략 실행은 키움 국내의 검증된 종목·위험·시장 세션·실제 거래대금 입력을 요구한다. 가상 일봉 체결은 다음 거래일 관측 모형이며 공식 시가 체결을 보장하지 않는다.
