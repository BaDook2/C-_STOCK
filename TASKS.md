# 현재 완료 — Windows v0.1.16 / Apple Silicon macOS r3

## v0.1.17 Windows·Mac 릴리즈 진행 — 2026-11-10 사용자 명시 요청

- [x] 현재 변경·원격 main·기존 태그/릴리즈 확인, 앱 버전0.1.17·릴리즈 노트 준비. 계좌/키/DB 제외 및 전체 변경 소스 개인정보 검사.
- [x] 새 버전 로컬 빌드0/0, Core192/UI629/디자인18 (JSON303 포함) 통과. 확정 커밋만 Git archive로 새 폴더에 추출해 같은 전체 검사 재통과, Windows WPF 호환 교차 빌드도0/0. 실제 Windows 실행은 미확인.
- [x] Windows 단일 EXE 교차 publish 및 Mac 앱/DMG/ZIP 로컬 생성·서명·원본/ZIP/DMG 시작·공개 차트 검증 성공. Mac Developer ID/공증은 없음.
- [~] HTTPS/SSH Git 인증 없음, GitHub 연결의 쓰기403 확인. GitHub CLI 설치·공식 기기 인증의 권한 안내 후 사용자의 계속 진행 지시를 받아 승인을 진행했으며 GitHub가 추가 본인 재인증(패스키/인증 앱)을 요청했다. 사용자 본인 확인을 기다리는 중이며 게시/태그 생성 없음. 수동 배포도 전체 검사 뒤에만 새 태그/자산을 만들며 기존 자산을 덮어쓰지 않는다.
- [ ] Git-only Windows CI WPF·D·Core·Avalonia·배포 EXE, 공개 EXE 재다운로드·시작 확인.
- [ ] Mac CI ARM64·Keychain·앱/ZIP/DMG·공개 차트, 공개 Mac 재다운로드·시작 확인.
- [ ] 최종 소스·CI·자산 해시·다운로드 기록 확정. 실패/미완료는 완료로 보고하지 않는다.

## 전략 JSON 점검·논의용 설명서 완료 — 2026-11-10 (로컬 소스)

- [x] 실제 불러오기·내보내기·복구·편집기 재생성으로 root61개·D 수치/정책·기간·ON/OFF 보존 확인. 가져온 지표 봉 크기 초기화 문제 수정.
- [x] 오타·중첩 중복 키·잘못된 기간 키·OFF 상태의 잘못된 D를 저장 전에 거부. numeric enum 내보내기 유지, 영문 enum 가져오기 지원. 기존 파일·단독 D·동명·취소·공유 자금/비용 검사.
- [x] 관련303/Core192/UI629/디자인18, Release 빌드0/0 성공. 사용자 후속 요청에 따라 상세 설명서 하나에 코드 기본값 전체 JSON 예제2개(기존 전략/D ON)를 모두 수록해 `../outputs`에 전달. 각 JSON 블록을 생성 fixture와 대조했다. 정본 docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md; 근거 docs/verification/Strategy-JSON-2026-11-10.md.
- 실행 중 설치 앱은 이번 작업에서 교체하지 않았다. 새 importer는 Rider 재빌드로 반영한다. 개인 저장값·실계좌·API 사용·게시 없음.

## 전략 설정 배치·펼침 수정 완료 — 2026-11-10 (로컬)

- [x] 백테스트 전용과 공통 전략 조건 사이에 급락 판단 전략 배치. 시점별 검증 자료 상위 영역·수동 JSON 입력 제거. 접힌 제목 옆 사용 체크박스.
- [x] 사용 OFF여도 하위 10개 그룹을 펼치고 접을 수 있도록 입력 영역만 비활성화. 체크박스 클릭과 펼침 클릭을 분리.
- [x] 빌드0/0, 관련49/UI326/디자인18 검사 성공. 사용자 교체 요청에 따라 `/Applications/Stock Development.app` 갱신 완료. 검증본 DLL SHA256 일치, strict 서명·설치본 네이티브 시작601봉/2quote 성공. 검증 기록: docs/verification/Strategy-Layout-2026-11-10.md.

## Rider 로컬 .app 생성 흐름 완료 (미게시)

- [x] Rider 공유 실행 설정 2개 추가: `.app 만들기` / `.app 빌드 후 실행`. 기존 Mac 패키징 스크립트를 재사용하고 실행 중 앱 교체 방지·실패 시 기존 앱 보존·IDE의 dotnet PATH 탐색 적용.
- [x] `/Applications/Stock Development.app` 실제 생성 및 strict 서명 검증 성공. macOS 앱 번들 실행 검사 Success/NoBrokerCalls/SyntheticOnly true, 601봉/2quote.
- 작업 폴더 File Provider가 FinderInfo를 재부착하는 서명 실패를 확인해 로컬 임시 경로에서 패키징 후 Applications로 이동한다. 동작·검증 정본: docs/maintenance/Rider-Mac-App.md. 원본 자료·기존 Windows/Mac 릴리즈·태그 변경 없음.

## 로컬 개발 변경 완료 — 전략 설정 통합 (2026-11-09, 미게시)

- [x] 기존 전략 안에 급락 판단 전략(D) 설정·사용 스위치·저장/복구/JSON 통합. 별도 D 버튼·설정 탭 제거, 자금/비용 중복 입력 제거.
- [x] 봉 기간 콤보·온라인 독립 판단 자료·오프라인 일봉 안내, 불필요한 소수 자리 제거. 기존 소수 수수료·비율 정밀도 유지.
- [x] 검은 일봉 MA240 및 실제 D 매수에 사용된 저점의 보라색 선/이름, 짧은 결과의 준비 자료·표시 범위 반영.
- [x] 빌드0/0, Core192/UI288/디자인18, 관련11 재검사 및 Mac 네이티브 합성 시작 성공(601봉/2quote).
- 동작 경계: D 가상은 기존 원본 체결 세션을 사용한다. D 실계좌 주문 미지원, 혼합 배치는 각각 시작. 실제 계좌/API 호출 없음. 커밋·push·Release 없음. 상세: docs/ui/Strategy-2026-11-09.md / docs/verification/Strategy-2026-11-09.md.

## Mac 개발 환경 준비 (2026-11-09)

- [x] 사용자 Mac에 Rider 2026.2.4.1 ARM64, AvaloniaRider, .NET SDK 10.0.403 및 Avalonia.Templates 12.1.5 설치.
- [x] 로컬 소스 d172d5a의 STOCK.Avalonia.sln Release 빌드 0경고/0오류, Core192/UI275/디자인18 통과. macOS15.6 osx-arm64 네이티브 합성 시작 성공(601봉/2quote). 근거: artifacts/mac-setup-smoke/result.json. 실제 계좌/API 사용 없음.
- Rider 첫 실행의 라이선스 선택/계정 로그인은 사용자가 완료한다. 앱 소스 변경·배포 없음.

## Apple Silicon macOS 배포 완료 (2026-11-08)

- [x] M1 기존 v0.1.16 앱 코드 유지, macOS15.8.2 ARM64 CI에서 self-contained Stock.app/DMG/ZIP 생성·서명 무결성/실행 권한 검사.
- [x] M2 Mac Core192/UI275/디자인18/실제 더미 Keychain4, 실제 앱 합성 시작·공개자료3종목 차트 및 ZIP/DMG 재열기 성공.
- [x] M3 Windows 자산 유지, v0.1.16에 Stock-AppleSilicon-v0.1.16-r3.dmg/zip 및 각각의 .sha256 추가. 공개 재다운로드가 CI 파일·GitHub digest·체크섬과 일치. 설치 안내 추가.
- [!] Apple Developer ID/공증 자격증명은 제공받지 않았다. ad-hoc 서명과 Apple 공증을 구분하고 첫 실행 안내를 포함한다. 실제 돈 주문은 실행하지 않는다.

최신 사용자 답변인 “미완료 기능부터 완성”과 앞선 조건부 교체·게시 요청을 이행했다. 현재 진행 중인 구현 작업은 없다. 새 요구 없이 완료 작업을 다시 실행하거나 릴리즈하지 않는다. 사용량40% 중단 지시는 해제됐다.

- [x] 기존 범용 당일매매 엔진/전략 D P0~P6 및 기존61개 설정을 보존하고 공통 Core를 재사용했다.
- [x] Avalonia UI/검색/차트/마우스/날짜/표/스크리닝·기록·전략 설정·가상 실행을 이식·검수했다. 익절 빨강/손절 파랑 및 병합/단일 클릭·거래 행 선택을 적용했다.
- [x] E1 키·기본 계좌 영속화, Windows DPAPI / Mac Keychain adapter, 성공 검증 후 저장·초기화·늦은 응답 방지.
- [x] E2 수동 실제 주문/취소·확인 체결, 한도·최종 계좌/중지·WAL/미확인 취소 검사를 유지했다. 가짜 계좌만 검사했다.
- [x] E3 기존 키움 국내 실전 batch/피드/대사 adapter와 앱 수명의 실행 상태를 연결했다.
- [x] E4 일봉 다음 거래일 관측 가상 실행, checkpoint v2/현금·수량 대사/원자 저장·정지 복원·신규매수OFF 보호 재개.
- [x] E5 Git-only 경로 빌드0/0 / WPF1652+D13 / Core192 / Avalonia273 / 디자인18, 실제공개28, 일반 EXE 직접 검색/백테스트·팬·클릭/달력/손익색 검수.
- [x] v0.1.16 CI 전체 성공→공개 EXE SHA/버전/시작/실제 마우스 검색·차트 검증→기존 EXE 백업·교체→루트 경로에서 재실행 검증.

## 지원 경계·외부 검증

- [!] Mac ARM64 CI의 앱/공개 차트·더미 Keychain은 검증했다. 사용자 Mac의 DPI·트랙패드·권한 승인/잠금과 Apple Developer ID 서명·공증은 별도다. Intel 앱은 이번 게시 범위가 아니다.
- [!] 실제 증권사 read-only API·실제 돈 주문은 이번 검사에서 실행하지 않았다. 계좌 주문은 사용자의 명시 활성화 전까지 잠근다.
- 실전 자동은 기존 지원인 키움 국내만 연결한다. 검증된 시점별 종목/위험/시장 세션/실제 거래대금 자료가 필요하다. 전략 D 실전 adapter는 이전과 같이 미연결이다.
- 새 앱 저장소는 원본 WPF와 분리된다. 기존 자료는 보존하고 자동 이관하지 않으므로 새 앱에서 계좌를 다시 연결한다.
- 원본 DB 과거 최초 손상 원인은 이번 작업의 조사 대상이 아니며 원본을 삭제·복제·복구하지 않았다.

## 최종 작업 상태 기록 — 2026-11-08

공개 소스 db1ee44e3083118b05982f4574446a8ba8f7a787 / v0.1.16 / Actions42480054799 성공.
현재 STOCK/Stock.exe, STOCK/release/Stock.exe, sibling stock_avalonia/Stock.Avalonia.exe 모두 공개 파일과 동일한 SHA256 993dcfeee8fad08be12fbe5ad2bf81e30f2a40259d824155f103ad75c1cff266 (107,497,278 bytes, 버전0.1.16.0).
이전 파일은 artifacts/before-avalonia-v0.1.16-20261108-215113에 보존했다. 수정 중인 앱 코드·빌드 실패·배포 미완료 없음.

다음 작업은 AGENTS→HANDOFF와 [검증 보고](docs/verification/Avalonia-Release-2026-11-08.md), [구현 설명](docs/avalonia/COMPLETION_E1_E4_20261108.md)을 읽고 새 사용자 지시가 있는 경우에만 진행한다. 이전 진행/중단 기록은 docs/history에 보존했다.
