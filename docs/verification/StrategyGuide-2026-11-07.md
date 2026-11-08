# 전략 상세 설명서 검증 (2026-11-07)

사용자가 ChatGPT와 전략 설계를 논의할 수 있도록 현재 소스 기준의 독립 문서 작성. 앱/엔진 코드는 변경하지 않았다.

- 정본: docs/strategy/과매도_낙주반등_전략_상세설명서.md
- 사용자 파일: 루트 전략_상세설명서.txt, Documents/Codex/STOCK전략문서의 MD/TXT/selected-strategy.json.
- 재생성/근거: artifacts/strategy-discussion-20261107/create_guide.py, source-manifest.json, parameter-reference.json, verify_guide.py.
- 원본 앱의 다운로드 버튼으로 계좌/키 제외 전략 JSON 내보냄. 현재 선택 `백테스트 검증 1 · 1일 보유`,61개 설정. 저장 완료된 설정이라고 단정하지 않고 내보낸 편집 snapshot으로 표시.
- 36,516문자/958행/22장.61개 기본/선택값 비교, 전체 지표·Support·군집·반등·진입·분할·청산·재진입·스크리닝·접근도·실시간·성과 계산, 수량/비용 예제, 논의 질문, JSON 포함.
- 문서 구조/61개 유일행/단위·수식 예제/JSON 경계/소스 hash 불변17개 확인. artifacts/strategy-discussion-20261107/documentation-checks.json.
- 기존 `--oversold-checks`78개 통과(로그 core-checks.log). 문서만 변경하므로 EXE 재빌드/교체는 하지 않았다. root EXE SHA256 기존88052CCE842989043934A50825DA01B8C39079EE128CE3D616B0FC345B37AF1A 유지.
- 주의 동작은 개선 제안과 구분: 처음 접근 Close 저점, 연결식 군집, 대기 후에도 낮은 지지 요구, 근접 후보에서 Support 부재 가능, TP/기간/실시간 차이, UI도움말과 연결 코드 불일치, 준비범위 차이. 이 작업에서 전략 변경/수익성 주장/새 실주문 테스트하지 않음.
- 키/계좌번호/잔고/개인 거래기록은 문서에 없음. 앱은 기존 선택 그대로 열어 둠. GitHub 게시 없음.
