# 구현 구조 — Workspace v2

기존 .NET 10 WPF 솔루션을 유지하고, 진입 화면을 WorkspaceViewModel로 전환했습니다. 기존 MainViewModel과 틱/호가 엔진은 이전 기록·회귀 검증을 위해 보존합니다.

| 사용자 요청 | 구현 |
|---|---|
| 오프라인 테스트 / 온라인 투자 라디오 | MainWindow.xaml, WorkspaceViewModel.IsOnline / IsOffline. 기본은 오프라인 |
| 한글 종목 검색 / 국내·해외 | PublicMarketData.Search, StockItem. 실제 거래소는 내부 매핑 |
| 키 없는 가격 다운로드 | 네이버 국내 일봉, Yahoo 미국 일봉. 파싱 검증·확정 봉·캐시·오류 표시 |
| 차트 / 이평 / 일목 | CandleChart: 캔들·거래량·한국시간·휠 확대·드래그·커서 상세. ChartIndicators 공통 계산 |
| 전략 스크리닝 / 업종 필터 | 공개 상위 종목 Universe + Describe, WorkspaceViewModel.Screen. 현재 목록 한도 표시 |
| 과거 모의매매 / 백테스트 | ChartStrategies + BarPaperAccount. 다음 시가, 수수료·슬리피지·유동성·현금 한도 |
| 실제 계좌 키 설정 / 시장가 주문 | KiwoomBroker(live:true), CurrentPrice, RemoteTradingSession, OrderGuard. 실전/모의 저널 분리 |
| 한국시간 | 조회 기간·차트·가상 체결·활동 기록은 +09:00. 저장 시간은 오프셋 보존 |
| 가격 입력 / CSV 제거 | 새 화면에 종목 코드·참조가격·CSV·모의 연결 버튼 없음 |
| 비활성 버튼 가독성 | 명시적인 어두운 회색 텍스트와 회색 배경, 활성 버튼은 흰색 텍스트 |
| 저장 | 별도 workspace-v2.db: 캐시, 프리셋, 설정·데이터 해시·백테스트 결과 |

공개 데이터는 개인용 연구 인터페이스이며 서비스 변경·조회 제한이 가능합니다. 스크리닝은 현재 상장된 시가총액 상위 목록이며 과거 생존편향을 제거하지 않습니다. 호가·틱 없는 일봉으로 스캘핑 성능을 주장하지 않습니다. 실계좌 자동 전략은 노출하지 않으며 실제 돈 주문은 반드시 앱 사용자의 종목·수량 확인을 거칩니다. 네트워크 검증은 공개 데이터 읽기에 한정하고 증권사 경로는 가짜 HttpMessageHandler로 검증합니다.

키움 공식 명세 사본: kiwoom-api-spec.json. 실전 현재가: 국내 ka10001 / 미국 usa20100. 주문: 국내 kt10000·kt10001, 미국 ust20000·ust20001. 실제 서비스 사용 전 키/IP/계좌별 허용 범위 확인이 필요합니다.

## 증권사·검색·관심그룹 확장
IAccountBroker로 주문 경로를 통일했다. 키움은 국내/미국, KIS·메리츠는 국내 실전 어댑터를 사용한다. 다른 세 회사는 로고/안내만 제공한다. 키는 DPAPI CurrentUser로 검증 성공 후 저장하며 기본 계좌는 온라인 전환 시 사전 선택에만 쓰고 활성 주문 세션은 하나다. Enter 검색, 종목 클릭 조회, 주기 라디오 탭, 관심그룹 다중 선택 및 중복 제거를 추가했다. 자세한 범위는 docs/ui/ConnectionDialog.md, docs/api/KIS.md, docs/api/Meritz.md 참고.
