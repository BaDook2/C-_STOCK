# 전략 JSON 매칭·상세 설명서 검증 — 2026-11-10

대상은 현재 로컬 Avalonia 개발 소스다. 개인 저장소·계좌·API 키를 읽지 않고 격리 파일/SQLite와 fixture 파일 대화상자로 검사했다. 기존 실행 중 설치 앱과 소스 검증을 구분한다.

## 발견 및 수정

- `StrategyDEditorModel.Apply`가 지표 단위를 적용할 때 항상 Size=1을 써서 가져온 3시간봉 등이 1시간봉으로 바뀌었다. 기존 단위가 같으면 크기를 보존하고 새 단위를 선택할 때만1로 시작한다.
- `LegacyParameter.Timeframe`도 같은 단위의 바인딩 재설정 시 원래 크기를 보존한다. root 5분·D 3시간을 실제 불러오기→Current→내보내기→복구→편집기 재생성으로 검사했다.
- JSON의 오타 항목이 기본값으로 조용히 넘어가지 않게 `UnmappedMemberHandling.Disallow`를 사용한다. 중복 키는 최상위뿐 아니라 중첩 객체/사전에서도 대소문자 무시 기준으로 거부한다. 오류에는 JSON 경로를 포함한다.
- period 사전 키는 지원하는9개 속성만 허용하고 대소문자를 정규화한다. null·지원하지 않는 단위·MA 분/시간·일봉Size2도 거부한다.
- OFF 상태의 D도 검증한다. 잘못된 대기 정책이 먼저 DB에 저장된 뒤 편집기가 열리지 않는 상황을 방지한다. 중첩 D 재귀도 거부한다.
- 내보내기 숫자 enum 형식은 유지한다. 가져오기는 기존 숫자와 영문 enum 이름을 모두 받는다.
- 전략 진입·청산 수식과 기본값을 이번 작업에서 바꾸지 않았다. D의 자금·비용5개가 바깥 설정을 따르는 기존 동작은 명시적으로 검증했다.

## 검사 범위 및 결과

신규 `Stock.Desktop.Tests/StrategyJsonChecks.cs`를 전체 UI 검사에 포함하고 `--strategy-json`으로 독립 실행할 수 있게 했다.

| 확인 | 결과 |
|---|---|
| 기본값·변경값 전체, root61개 필드, D 설정·기간·JSON 전용 사전 | 실제 명령 기반 왕복·등록값·현재값 일치 |
| D ON/OFF·대기값 보존, root5분/D3시간, 같은 단위 재설정 | 보존 성공 |
| 다른 단위 선택 | 의도대로Size1부터 시작 |
| 부모 자금·비용5개와 D 중복값 | 편집기·실행 정의 모두 부모가 원본 |
| 문법/형식/정수/범위/목록/ID·필수값/오타/중복/기간/잘못된 OFF D | 오류, 기존 편집값·등록 목록·DB 불변 |
| 취소·기존 schema1 누락값·단독 schema2 D·동명 전략 | 취소 보존·기본값 호환·D 활성 통합·새 이름 등록 |
| 관련 검사 | **303 성공** |
| 최종 Release 빌드 | **0경고·0오류** |
| Core 회귀 | **192 성공** |
| Avalonia 전체 회귀 (관련303 포함) | **629 성공** |
| 디자인 | **18 성공** |

최종 실행 명령:

```bash
bash avalonia-scripts/verify.sh
dotnet run --project Stock.Desktop.Tests -c Release --no-build -- artifacts/strategy-json-audit --strategy-json
```

최종 로그는 workspace의 `../work/json-final-verification.log`, `../work/json-audit.log`다. 첫 sandbox 빌드의 Avalonia telemetry 경로 권한 문제와 일시 NuGet 경고는 권한이 있는 실행에서 restore 후 해소했고 최종 빌드는0/0이다. core-source-manifest의3개 변경 파일 SHA256을 UTF8/LF로 갱신했다.

## 설명서·예제

정본 문서는 `docs/strategy/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md`다. 이전2026-11-07 문서는 당시 상태의 이력이며 이번 문서는 현재 통합 엔진·단위·지원 경계를 설명한다. `docs/START_HERE.md`의 안내를 갱신했다.

사용자 후속 요청에 따라 전달본은 workspace `../outputs/현재_전략_상세설명_및_JSON_가이드_2026-11-10.md` 하나다. 문서8.1/8.2에 기존 전략과 D ON의 전체 JSON을 각각 수록했다. 신규 검사가 공식 serializer로 기본값 예제와 catalog를 생성하며, 문서의 두 JSON 블록을 생성 fixture와 JSON 객체로 대조해 동일함을 확인했다. 별도 배포용 JSON2개는 이 문서로 통합했다. 예제는 개인 현재값이 아닌 코드 기본값이다. D 내부 hidden19개 중 실제 사용·검증·통계·미사용을 구분하고, 공유 자금·비용5개·JSON 전용 사전 우선순위를 명시했다. 후속 변경은 문서만으로 추가 빌드·전략 검사는 반복하지 않았다.

관련 소스: `StrategyDefinition.cs`, `StrategyDEditorModel.cs`, `OversoldConfig.cs`, `StrategyEditor.cs`, 신규 검사/Program.

## 적용 경계

이번 작업은 로컬 소스·문서 수정이다. `/Applications/Stock Development.app`을 교체하거나 실행 앱을 종료하지 않았다. 앱 반영은 정상 종료 후 Rider `Mac · .app 빌드 후 실행`을 사용한다. 기존 설치본은 이전 UI 배치 변경까지 반영된 상태다. 개인 등록 전략 자동 변경·실제 API·실제 주문·커밋/push/Release는 수행하지 않았다.
