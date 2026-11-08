# 검증 기록 — 2026-11-08

최신 차트 수정: [CHART_INTERACTION_PARITY_20261108.md](CHART_INTERACTION_PARITY_20261108.md). 빌드0/0·Core192/UI172(차트 전용30 포함)/디자인16, 최종 일반 앱 실제 십자선 화면 픽셀 가로274/세로176, 배포EXE 검색/Enter/반복/재실행4회 성공. 루트 EXE SHA256 1D7783EAF7183AA750145524A827298A2BC30EF6C7B74500D3ABC93C137CEB0D. 실제 원본 비교는 현재 오프라인에서 노출된 일봉, 나머지 공통 봉은 격리 검사다. 실제Mac/실제증권사 검증으로 해석하지 않는다.

최신 디자인/상태 검증: [INTERACTIVE_UI_PARITY_20261108.md](INTERACTIVE_UI_PARITY_20261108.md). Release0경고0오류, Core192/기능142/디자인16 성공. 두 일반 앱 7탭의 버튼 호버108개·콤보 열기/닫기14개·펼침 상태144개, 채운 표의 실제 픽셀3상태 일치, 새 루트EXE 입력/Enter/반복/재실행4회 성공. 해시 C475123C8BF6575AE0212F4473F2AA83B1B3BB02A6770732FB29A27F8D69BF7F. 위험한 실제 버튼 명령은 일괄 실행하지 않았다. 아래는 이전 이력이다.

최신 후속: [일반 실행 창 직접 입력/화면 멈춤 수정](NATIVE_SEARCH_FIX_20261108.md). 실제 Windows 키보드·검색 버튼/Enter·반복/재실행4회와 **화면 픽셀**을 확인했다. 이전 EXE는 내부 조회 완료여도 화면에 로딩이 남아 실패했고, 수정 후4회 모두 봉 표시 성공. 배포EXE F86BCC3F...F09A, 전체 회귀192/116/디자인16·빌드0/0. 이전 아래 기록의 미재현 결론과 RTB 기반 실제표시 판정은 이 후속 결과로 정정한다.

최신 모드별 점검은 [MODE_AUDIT_20261108.md](MODE_AUDIT_20261108.md). Core192·Avalonia기능116·디자인16·실제공개28·동일코어 원본계약965, 최종 EXE 실제공개/합성 각각Exit0. 무응답 제공처에서45초 제한·실제 취소 버튼·재시도를 확인했다. 실제Mac/증권사 인증은 미검증이며 사용자 환경의 정확한 실패 원인도 미확정이다.

후속 디자인 변경의 최신 검증은 [UI_PARITY.md](UI_PARITY.md)를 먼저 본다. 최신 빌드0/0·Core192·기존UI74·추가디자인16·루트EXE native Exit0를 확인했다. 아래 표의 EFCAF 배포/기존74개 기록은 1차 전환판의 이력이다.

## 원본 WPF를 먼저 마무리

- 기존 전체 회귀1651 성공: `../STOCK/artifacts/strategy-d-regression.log`.
- 신규 D 코어192 + WPF D 화면13 성공: 원본 `strategy-d-final-core.log`, `strategy-d-final-ui.log`.
- 원본 Release0경고0오류와 단일EXE 격리 시작 검사 성공. 원본 `Stock.exe`80,983,144bytes, SHA256 `E433834BC4DAFB369723F5E9D60161E14B17B27E09700E318F1A8DB4354F1934`.
- 이전 exe는 원본 `artifacts/before-strategy-d-20261108-000540/`에 보존. GitHub 게시 없음.

## Avalonia

| 검증 | 결과 | 근거 |
|---|---|---|
| .NET10 Release 전체 solution | 경고0 / 오류0 | `artifacts/avalonia-final-build.log` |
| WPF 없는 공통코어 검사 |192 성공|`artifacts/avalonia-core-checks.log`|
| 실제 AXAML/ScottPlot Headless |74 성공|`artifacts/avalonia-ui-checks.log`|
| Windows self-contained 단일EXE |성공|`artifacts/avalonia-win-x64.log`|
| 배포EXE 두 번 native 실행·종료 |두 번 Exit0/Success, 각각93봉·2회quote, broker호출0|`artifacts/native-smoke-summary.json`|
| osx-arm64 publish |성공·Mach-O CPU16777228|`artifacts/avalonia-osx-arm64.log`, `macos-audit.json`|
| osx-x64 publish |성공·Mach-O CPU16777223|`artifacts/avalonia-osx-x64.log`, `macos-audit.json`|
| 공통 소스80개 원본/manifest 대조 |차이0|`artifacts/source-and-exe-verification.json`|
| 사용자 DB/키/.git 복제 |없음·소스 비밀파일 확장자0|`artifacts/source-privacy-audit.json`|

Windows Avalonia `Stock.Avalonia.exe`:107,158,327bytes, SHA256 `EFCAFD0B9DCF7B0853664A64136B69BB3B5447B53EE2DC7F0B3453687F719C3B`. 루트와 publish 파일은 같다. Windows 실제 native 창/스키아 렌더 이미지는 `artifacts/native-smoke-2/native-window.png`. 합성 자료임을 차트 종목명과 제공처에 표시한다.

Mac CPU별 `artifacts/macos/<RID>/Stock.Avalonia.app`를 생성했다. apphost와 AvaloniaNative/SkiaSharp/HarfBuzzSharp/SQLite 파일 존재를 확인했다. Windows에서 만든 파일은 Mac 실행 권한을 확인해야 한다. **이 결과는 실제 Mac 실행 성공을 뜻하지 않는다.**

## Headless74개가 확인한 범위

검색·삼성/애플·일주월 집계·1초 pump·61개 정확도0.015·실계산 BUY/SELL·기록 popup·관심그룹·정기매수·5개 지수·진행 중 스크리닝 열람·17단계 집계·행 선택 유지·독립 D AXAML/같은1신호4체결·계좌 가림/보기/검증 후 연결·메모리 재사용/초기화·공식분봉 경로·off→daily·실시간앱수명/별도차트·전략교체초기화·신규가상gate·장외차단·일봉미지원차단·정수삽입/소수덮어쓰기·표식병합/분리·봉한번클릭·실제휠/드래그/커서중심확대·취소된이전응답차단·기록당시snapshot·긴universe조회전설정동결·관심그룹중복체크·가상팝업수명/예산잠금/중복방지·우클릭전체/상세/축소한도·미지원시장분봉초기화·예상밖Command오류0.

10,000봉 전체/1,200봉 상세 입력·렌더 검사 기록은 `artifacts/avalonia-ui/candle-navigation-performance.json`. Windows Headless 합성자료에서 마지막 전체 약133ms, 상세 약49ms(여러 render flush 포함)였다. 초기190/60ms 수준의 실행도 있어 Mac 성능이나 일정FPS를 보장하지 않는다. 상세1,200봉은 이 검사 범위에서 보수적으로 사용한 화면 수이며 원본/백테스트 자료를 줄이지 않는다.

이 검사는 합성 시세/더미 broker/격리 SQLite만 사용했다. 실계좌 잔고·주문·개인 DB·진짜 키·실제 네트워크 시세·Mac UI는 검사하지 않았다. Windows native 시작 검사는 화면을 잠깐 만드는 자동 검사로, 두 번 모두 네트워크와 계좌 연결을 금지한 공급자를 주입했다. 실제 API 제한/시장 품질/수익성을 검증한 것으로 해석하지 않는다.

## 알려진 제한

- 실제 Mac에서의 native 실행·파일 대화상자·트랙패드·폰트·SQLite·키 저장 확인 필요.
- macOS Keychain/실계좌 주문 UI와 대사/서명·공증 미구현. 실제 주문 잠금.
- 일봉 실시간 자동주문은 미지원이라 차단. 신호 감시만 제공. 온라인 분/시간 관측모형 가상 실행을 검사했다.
- 국내 감시100/기존 matcher20 제한, 해외 실시간/보유 복원·일부 WPF 고급 chart 설정은 후속.
- 원본 P0~P6의 strict 자료 품질 차단·원본 구독20종목·실계좌 adapter 미연결 제한도 유지.

주간잔여 마지막 조회70%.40% 하한을 넘겨 작업하지 않았다. 이후 재개 때 반드시 새 수치를 조회한다.
