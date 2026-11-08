# 완료 — 표식·분봉 속도·전략/표 UI (2026-11-07)

요청8개 완료. 남은 실행 가능한 작업 없음. 세부 근거/파일/제한: docs/verification/CompactUi-2026-11-07.md.

- CandleChart 실제 FormattedText Rect 충돌 병합, 확대 시 원본 체결 분리. 횟수/거래번호/손익/익절·손절 개수 유지.
- 공통 SupportProvider dense 매물대(배분·누적 순서 유지), 정확도 보장 범위 MA prefix/고정밀 fallback. 7,800봉 default CPU2397→1596ms/할당256→160MB/결과 byte 동일. 실제 broker 통신 시간은 미측정.
- 스크리닝 동시4개 작업 slot 즉시 재사용, 과거 범위 메모리 cache(세션 달력 key)/실시간TTL·128제한 유지. 키움 시작일 일부 페이지 중단 방지. 사용자 데이터/전략/봉 생략 없음.
- 카드145px고정 제거(약78px),32px 입력 정상 baseline/단위 옆 배치/boolean 설명 생략/온라인0개 범위 숨김. 공통 표 content center 및 표 안 숫자 중앙 정렬.
- 계좌 실행 창명 실제/가상 계좌 전략 실행, 실제 버튼 실시간 실제 전략매매 설정. 관심그룹 편집 파랑. 재연결 AccountInfo 문자열 누적 제거.

## 검증/EXE

Release0경고·0오류. 전체1429개 이후 broker210/UI670/실시간UI42/최종 라벨55 모두 통과. 채워진 표21캡처/439셀 좌표 검사. 실제 키/실주문 없이 격리 broker/DB. 로그 artifacts/compact-*.log, 이미지와 table-audit-summary.json은 compact-ui-final/compact-realtime.

루트/release/publish Stock.exe80,861,669bytes/SHA256 `88052CCE842989043934A50825DA01B8C39079EE128CE3D616B0FC345B37AF1A`, release 체크섬 동일. publish/루트 smoke 종료0/Success/NoBrokerCalls=true. 이전 root artifacts/before-compact-ui-20261107-190205/Stock.exe 보존. Native 새 root EXE 정상 시작/오프라인 화면 확인, 앱을 열어 두었다. 다음 GUI 작업 시 HWND는 새로 조회한다. 격리 데모 창은 종료했다. GitHub 게시 없음; 사용자 명시 요청 때만 게시.

## 유지할 주의

재조회 시세/종목 캐시·raw ticks는 메모리, 사용자 설정·주문 안전/테스트 재현 snapshot은 기존 보존 정책. 키는 기존 DPAPI 파일, 변경 없음. 이전 DB 최초 손상 사건 미확정/원본 보존·승인 복구 사실은 docs/maintenance/Storage.md 및 ProgressiveScreening-2026-11-07.md. 도구 AppData는 실제 사용자 DB와 다른 Codex LocalCache로 리다이렉트될 수 있다. 이번 작업에서는 DB 삭제/수리·키 변경·실계좌 주문 없음.
