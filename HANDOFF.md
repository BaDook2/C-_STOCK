# 현재 인수인계 — v0.1.19 릴리즈 완료

사용자의 현재 전략 포함 GitHub 릴리즈 요청을 이행했다. [v0.1.19](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.19)에 Windows EXE, Mac DMG/ZIP, 샘플 JSON/설명서 ZIP과 체크섬 총8개 자산을 공개하고 재다운로드 검증했다. 두 태그 v0.1.19/macos-v0.1.19-r1은 소스 c2b52811cb7b45ffc5978d64d2143cbea7fc94cb다. 이후 문서 커밋은 앱 소스/태그를 바꾸지 않는다. 새 요청 없이 릴리즈를 반복하지 않는다.

## 결과와 증거

Windows43039584970 성공: Windows338/Inje363/Bin187/UI1007, manifest105/설명서JSON15, 디자인/배포 EXE/공개 재다운로드 실행. Mac43041052949 성공: Inje363/Bin187/UI1009, 같은 manifest/JSON, 디자인/Keychain/앱·ZIP·DMG/공개 파일 실행·차트. macos-15 대기43040473470만 취소하고 이전 성공 환경 macos-26으로 전환했다. 로컬 Avalonia/WPF 빌드0경고/0오류, UI1009 및 네이티브 시작의 샘플9개/601봉/3quote도 확인했다.

Stock.Core/Strategies/Samples의 임베디드 JSON과 StrategySampleCatalog가 샘플9개를 첫 실행에1회 추가한다. 이름 충돌 시 사용자 값 우선, 현재 선택 보존, marker로 이름 변경 후 재생성 방지. 새 설치는 기본1+샘플9개다. 합성 시세/거래 기록은 넣지 않는다. 기존 기능 검증 fixture와 모든 파라미터 일치·설치 보존·재시작 검사를 추가했다. 스크리닝 USD 소수 가격과 합성 출처 표시 수정도 포함한다.

공개8개 파일 크기/SHA256이 체크섬/API digest와 일치한다. 샘플 ZIP의9개 JSON 및 단일 설명서도 소스와 같다. 이전v0.1.18 자산 ID/hash/크기 보존. 정본은 docs/verification/Release-v0.1.19.md, Git 제외 증거는 artifacts/releases/v0.1.19-verification/final-verification.json과 양 CI/태그/공개 파일, 로그는 ../work/release-0.1.19-*.log다.

## 보존과 한도

설치 개발 앱(/Applications/Stock Development.app)은 이전0.1.17 표시본이며 이번에 교체하지 않았다. 사용자 DB/키/실제 기록에도 접근·변경하지 않았다. 이전 요청으로 사용자 프로필9개를 저장한 상태는 그대로다. 개발 폴더에 새.app 사본을 만들지 않았다. Bin 실시간 가상 주문/복원 미연결 차단, 실계좌 자동 전략 주문 잠금, 원본 틱/호가 필수 및 MissingData 정책을 유지한다.

주간 잔여19%를 확인했다. 사용자 지시인 잔여15% 보존 조건을 작업 전/단계별로 확인하고, 다음 단계로15% 미만이 될 우려가 있으면 미리 중단한다. 현재 요청의 남은 작업 없음. 이전 기록은 docs/history/*before-release-v0.1.19-20261111.md.
