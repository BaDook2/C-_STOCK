# 현재 — v0.1.17 Windows·Apple Silicon Mac 게시 완료

[공개 릴리즈](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.17). 사용자 요청 범위의 구현·검사·게시·공개 재다운로드 검증이 끝났다. 새 사용자 지시가 없으면 완료 상태를 유지하며 추가 태그/릴리즈를 만들지 않는다.

## 소스·실행 파일

- Windows `v0.1.17` / 앱 소스 `d067be25553d502a3a1c16316164115bbd8e7cbc`.
- Mac `macos-v0.1.17-r1` / 패키징 소스 `17a297b67ac5cfca0de2d07a19237fb8221e01bf`. 두 커밋의 Stock.Core/Stock.Avalonia 내용은 같다. 차이는 실행기 선택과 문서다.
- [Windows CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42842992293)와 [Mac CI](https://github.com/BaDook2/C-_STOCK/actions/runs/42848351841) 모두 성공. [파일 크기·SHA256·실행 근거](docs/verification/Release-v0.1.17.md).
- Windows EXE107543358bytes, SHA25643a5673c21f78c1ba82381a0d424daa3bea133cd15c3c211ee403bf2c053a1b2.
- Mac DMG53145790bytes, SHA25618d34f3cc2da104e2928e19e298a747bce03219c552a243a449bad120f45984d.
- Mac ZIP46839840bytes, SHA256087f8e70d152ed503d8d05e71aba8629c3afc5294b403687615a2d4e87eff32c.
- 원본 WPF `Stock.App`/`Stock.sln`은 호환 소스로 보존한다. 개발은 `STOCK.Avalonia.sln`/`Stock.Avalonia`, 공통 Core88개 manifest를 사용한다.

## 구현·검증

전략 설정은 백테스트 전용→급락 판단 전략→공통 전략 조건이다. 제목 옆 사용 체크, OFF여도 그룹 펼침, 독립 봉 단위, 실제 일봉 MA240/사용된 급락 저점선, JSON 보존·엄격 검증을 포함했다. D OFF는 기존 엔진, ON은 전체 D 진입/청산이다. 자금/비용은 부모 전략을 공유한다. [전략 설명·전체 JSON2개](docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md)는 코드 기본값 기준이다.

Windows WPF1655/D13/선행11/Core192/Avalonia631/디자인18, Mac Core192/Avalonia633/디자인18 및 더미 Keychain 성공. 양OS 공개 배포본 시작601봉/2quote/NoBrokerCalls, Mac 공개 차트 삼성전자1500/SK하이닉스1500/애플2513봉 성공. 공개6개 자산 해시·크기를 API digest/체크섬/CI와 대조했다. 기존 자산 ID/해시 불변. 로컬 증거 `artifacts/releases/v0.1.17-verification/final-verification.json` 및 양CI artifact.

첫 Windows CI42838779384에서 UTC/KST 자정의 기록 기본 날짜 불일치를 발견해 WPF/Avalonia에 KoreaClock.Today를 적용했다. 수정 후 전체 재검사 성공. 첫 Mac42846537829는 실행기 미할당으로 취소했고 macos-26 ARM6442848351841이 성공했다. 검사 생략이나 기존 태그 덮어쓰기 없음.

## 개발·지원 경계

Rider의 `Mac · .app 빌드 후 실행`은 `/Applications/Stock Development.app`을 생성한다. [안내](docs/maintenance/Rider-Mac-App.md). 현재 설치본은 이전 UI 배치 교체 버전이며 이번 릴리즈로 자동 교체하지 않았다. 개인 DB/키/설정은 그대로다.

Mac Developer ID/공증은 없으며 [설치 안내](docs/avalonia/MACOS_APPLE_SILICON.md)를 따른다. 물리 입력·사용자 Keychain 권한/잠금 및 실제 증권사 API는 별도다. 실제 돈 주문0. D 실계좌 자동 주문은 미지원이며, 기존 지원 실전 자동은 키움 국내+검증된 시점별 자료를 요구한다. WPF 자료는 자동 이관하지 않는다. 일반 가격/틱 캐시는 메모리에만 둔다.

이전 상세 진행·개발 기록은 [이력](docs/history/HANDOFF-before-v0.1.17-complete-20261111.md), 기능 지도는 [START_HERE](docs/START_HERE.md)에 있다. 문서 기록만 바꾼 후속 커밋으로 새 릴리즈를 반복하지 않는다.
