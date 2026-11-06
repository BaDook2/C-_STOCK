# 현재 상태 — 2026-11-06 스크리닝 차트 개선

## 이어갈 작업

요청 완료. v0.1.13 코드 c51f668544672436e5c7d47e099f416cedf47f55, Actions42175965891 전체1,280개/빌드0경고·0오류/publish·smoke·Release 성공. 공개 다운로드/checksum/digest/실행과 루트/release 동기화도 완료했다. 새 요청 없이 같은 검사/태그/릴리즈를 반복 실행하지 않는다. docs/verification/Release-v0.1.13.md 참고.

- 최신 요구: 스크리닝 차트 주기 콤보 제거/분석 기준 읽기 전용, 별도 지정봉(분1~59/시간1~12)/일·주·월, 실시간 가격·전일 대비1초 표시, 우클릭 후 휠 확대/최대 축소 한도, 렌더 최적화.
- 구현: ScreeningChartViewModel의 비동기 조회/취소/파생 시리즈 캐시, ScreeningChartData의 기존 공개/증권사 제공자, ScreeningChartDialog의 UI, CandleChart의 이진 탐색·표식/지지구간 캐시·프레임 병합. 분석/체결 snapshot은 변경하지 않는다.
- 원인: 84시간봉을 우클릭25봉으로 만든 뒤 최소25봉 때문에 휠이 막혔다. 스크리닝 확대 상태만 최소5봉, 최대는 최초 확대 봉 수. 포커스/PreviewMouseWheel도 보완했다.
- 추가 회귀: 같은 봉에서 원본/주기 변경 후 툴팁 캐시가 갱신되지 않는 문제까지 수정했다.

## 검증·현재 EXE

- 최종 전체1,280개/전용21개/Release 빌드 경고0·오류0/publish·EXE smoke 모두 성공. 실제 주문/키 변경은 하지 않았다.
- 실제 Windows 마우스로 일봉↔지정봉,84→25→5봉, 드래그 offset37→46 및 실시간 검증 시세를 확인했다. 온라인/오프라인 가격 경로는 fake broker/fixture, 실제 공개 조회는 삼성전자5분봉325개(11-04 09:00~11-06 14:55)로 구분한다.
- 2만 원본 봉/160표식/500봉 뷰포트의 동일 재렌더 비교 P95 7,107.11→29.16ms. 실제 렌더·비트맵 복사 포함, 원본/체결은 유지. 상세 docs/verification/ScreeningChart-2026-11-06.md.
- 공개/루트/release EXE 80,866,955 bytes / SHA256 6CB0CEF0018E6BB205981259040FF0012248B793CCB7960FFBA3692264B8894A. 공개 checksum/자산 digest/계산 해시 일치, 다운로드 EXE smoke 종료0/Success/NoBrokerCalls=true. 사전 로컬 FCCB 빌드와 이전 공개본은 artifacts에 보존했다.
- v0.1.12/a213cf4의 Actions42174269437은 툴팁 회귀를 고치려고 게시 전에 취소했다. 공개 EXE/Release 없음, 태그 보존. 이 버전의 재개 스크립트는 실행하지 않는다.
- 최신 공개는 https://github.com/BaDook2/C-_STOCK/releases/tag/v0.1.13 이다. CI 전체 회귀13분6초/전체job15분12초. 완료 기록만 갱신하는 문서 커밋으로 새 릴리즈를 만들지 않는다.

## 증거·명령

- artifacts/screening-chart-{build-final.log,checks-final.log,final-full.log,publish-final.log,publish-final-smoke/}, chart-review-benchmark/{comparison.json,public-data.json}, screening-chart-native-final/, release-v0.1.13-{ci.log,proof.json}.
- AGENTS → TASKS → docs/START_HERE.md. 빌드/배포 명령은 docs/maintenance/Verification.md. 사용 도구 artifacts/tools/Invoke-GitHub.ps1은 기존 Git 인증을 메모리에서만 사용하며 토큰을 출력/저장하지 않는다.
- 게시 전용 컴파일이 오래 걸렸지만 검사/분석기를 생략하지 않고 성공했다. CI job 제한45분. 앱 실행 중에는 무단 종료/실주문 중단을 하지 말고 EXE 교체 충돌을 별도로 처리한다.

## 유지할 이전 DB 사실

- 실제 DB C:/Users/wkdru/AppData/Local/StockPaperTrading/KiwoomQuant/workspace-v2.db. 09:18/09:20 SQLite11 최초 유발 사건은 재현되지 않았다. 원본98MB/실패 사본4개 무결성·JSON 정상; 원본 백업은 같은 폴더 diagnostics-20261106-102905. 이후 실제 감시 점검 DB145MB도 정상.
- 장기/풀링 연결을 작업별 비풀링으로 바꾸고 StorageDiagnostics/테스트 기록의 저장소 점검/--storage-check를 추가했다. 데이터 초기화·재구축·VACUUM·실제 키 변경은 없었다. 새 failure 증거 없이 근본 원인이 확정됐다고 말하거나 정상 DB를 재구축하지 않는다.
- 상세 docs/maintenance/Storage.md, docs/verification/{StorageAudit,FeatureAudit}-2026-11-06.md. 과거 누적 지침은 docs/history에 보존했다.
