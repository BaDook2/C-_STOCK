# v0.1.13 릴리즈 검증 — 2026-11-06

- 스크리닝 차트 개선 후, 실시간 봉/주기 변경에 따른 같은 봉의 툴팁 캐시 무효화를 추가했다.
- v0.1.12/a213cf4의 CI42174269437은 게시 전에 중단했고 태그를 보존했다. 공개 EXE/Release는 생성하지 않았다.
- 수정본 전용21항목/최종 전체1,280항목/빌드 경고0·오류0/단일 EXE smoke 성공. 새 v0.1.13 태그로 게시했다. [구현·성능·실제 마우스 검증](ScreeningChart-2026-11-06.md).
- 수정본 로컬 publish: SHA256 FCCB3B5A2712B50324A6849FBF8328F402474549F68AA3AAF6A2656BB5E2D425.
- 소스 c51f668544672436e5c7d47e099f416cedf47f55, [Actions42175965891](https://github.com/BaDook2/C-_STOCK/actions/runs/42175965891) 전체 단계 성공. 전체 회귀13분6초/전체job15분12초. 검증 스크립트: artifacts/Verify-Release-v0.1.13.ps1.
- 공개2026-11-06 15:55 KST. [Release](https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.13), [Stock.exe](https://github.com/BaDook2/C-_STOCK/releases/download/v0.1.13/Stock.exe).
- 공개 EXE 80,866,955 bytes / SHA256 `6cb0cef0018e6bb205981259040ff0012248b793ccb7960ffba3692264b8894a`. checksum/GitHub 자산 digest/실제 다운로드 해시 일치, 다운로드 EXE smoke 종료0/Success/NoBrokerCalls=true.
- 루트/release Stock.exe도 공개본과 일치한다. 근거 artifacts/release-v0.1.13-{ci.log,proof.json}, download-v0.1.13-smoke-20261106-155928/result.json. 코드 추가 변경 없이 완료 문서를 후속 커밋했다.
