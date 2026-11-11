# 현재 인수인계 — 샘플9개 실제 저장 완료

사용자의 명시 요청으로 격리 검증 파일에만 있던 Inje4개/Bin5패턴 샘플을 실제 앱 프로필에 각각 저장했다. 기존2개/현재 선택/다른 데이터는 보존했다. 실제 기록 삭제/실계좌 주문/제품 코드 추가 변경/GitHub 게시/앱 교체 없음.

## 저장 결과

실제 DB는 /Users/jang-gyeongbin/Library/Application Support/Stock.Avalonia/workspace.db다. 앱 종료 상태에서 실제 경로/크기102400bytes/inode/무결성/행 수 확인 후 SQLite backup API로 같은 데이터 폴더 backups/20261111-strategy-samples/workspace-before-samples.db에 보존했다. 단기 연결 구조로 당시 열린 DB 핸들은 없었다. DB/백업/save-verification.json은 Git에 넣지 않는다.

독립 사본 저장 재현 후 설치 /Applications/Stock Development.app의 Stock.Core.dll에 있는 strict Import/StrategyProfileStore.Save로 artifacts/strategy-samples-regression의 profile.json9개를 저장했다. 최상위 이름만 한글로 바꿨으며 수치는 그대로다. Inje: 급등주 눌림목/호가 스프레드 틱떼기/폭락주 초단기 반등/저유동성 지지구간. Bin: 하락 반등/상승 고점 돌파/상승 눌림목 반등/박스 하단 반등/박스 상단 돌파. 접두사는 샘플 · Inje 또는 샘플 · Bin.

원래 기본 설정/새 전략 설정을 포함해 총11개, 현재 선택은 새 전략 설정 그대로다. 9개 JSON 재조회 파라미터 일치, state9개 추가+목록 키1개 수정만 확인했다. 나머지 기존 state와 다른 테이블 전체 행 동일. 실제 앱 실행 → 전략 설정 → 상단 목록에서 스크롤해9개 전부 확인했다. 선택은 바꾸지 않았다. 시작 후 integrity ok/11개/기존 상태·다른 테이블 동일 재검사. 현재 앱은 전략 설정 화면이다.

정본 docs/verification/Strategy-Samples-2026-11-11.md 마지막 절. 일회성 도구/사본은 저장소 외부 ../work/save-strategy-samples다. 합성 이벤트·거래 기록은 개인 DB에 넣지 않았으므로 실제 백테스트에는 원본 체결/호가·당시 자료가 필요하다.

## 이전 상태와 한도

이전 샘플135/Avalonia전체996/Inje363/Bin187 및 양솔루션 빌드/manifest 검사는 완료된 상태로 보존한다. 이번 데이터 추가에 전체 회귀/릴리즈를 반복하지 않았다. 기존 미커밋 샘플·표시 수정은 그대로다. 공개v0.1.18/설치 개발 앱0.1.17/미커밋 소스는 구분한다. 새 게시에는 새 요청이 필요하다.

주간 잔여31% 확인. 사용자 지시인 잔여15% 보존을 작업 전/단계별로 확인하고 다음 단계로15% 미만 우려가 있으면 미리 중단한다. 현재 요청은 완료, 남은 작업 없음. 이전 기록은 docs/history/*before-saving-samples-20261111.md.
