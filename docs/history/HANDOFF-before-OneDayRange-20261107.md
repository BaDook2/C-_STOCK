# 완료 — ChatGPT 논의용 전략·계산 상세 설명서 (2026-11-07)

- 정본 docs/strategy/과매도_낙주반등_전략_상세설명서.md. 사용자 첨부용 루트 전략_상세설명서.txt 및 Documents/Codex/STOCK전략문서/MD·TXT·selected-strategy.json.
- 22장/61개 설정의 코드 기본값 vs 앱 내보낸 선택값 비교. 현재 앱 선택은 백테스트 검증1·1일보유라 원래 기본 전략과 구분. 내보내기는 편집값 snapshot이며 별도 설정 저장 사실을 단정하지 않음.
- 급락·Wilder RSI·거래량·MA·확정 Pivot·근사bin·연결식 군집·반등·다음봉·분할·청산·재진입·접근도·실시간·성과 수식, 예제, 현재 구현의 논의 항목/제약 포함. CodeMetadata의 오래된 실계좌 미지원 문구 vs 실제 연결 코드 차이도 명시.
- 문서17개 대조/기존 핵심78개 통과. 보고 docs/verification/StrategyGuide-2026-11-07.md, 재생성·원본JSON·sourcehash는 artifacts/strategy-discussion-20261107.
- 문서/JSON 외 앱/전략/DB/EXE 변경 없음. 실계좌 주문·키 접근·GitHub 게시 없음. 새 실행 가능한 미완료 없음.

## 현재 제품/EXE 기준

직전 표식·분봉·UI8개 요청 완료 상태 유지. 루트/release/publish Stock.exe80,861,669bytes/SHA256 88052CCE842989043934A50825DA01B8C39079EE128CE3D616B0FC345B37AF1A. 전체1429개 및 후속 broker210/UI670/RT42/표식55 성공, smoke 안전 성공. 상세 CompactUi-2026-11-07.md. Native root 앱은 전략 설정에 열려 있음(현재 HWND는 다음 GUI 작업 시 새로 조회). 이전 인계 docs/history/HANDOFF-before-StrategyGuide-20261107.md.

## 유지할 주의

기존 사용자 DB 최초 손상 사건은 미확정. 승인 복구 원본 보존·AppData 리다이렉션 주의는 docs/maintenance/Storage.md 참고. 원본 tick/재조회시세는 메모리, 테스트 재현·주문 안전·설정 기록은 기존 보존 정책. 사용자가 명시적으로 요청할 때만 GitHub 게시.
