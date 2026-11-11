using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public sealed record BinsDecision(DateTimeOffset Time, string Symbol, string Kind, string Reason, string ConfigurationHash,
    string? SignalId = null, string? OrderId = null, EntrySignal? Entry = null, ExitIntent? Exit = null, string? StateSnapshot = null);
public sealed record BinsSymbolSnapshot(string Symbol, RegimeSnapshot Regime, StageOneSnapshot Stage1, StrategyFamily? SelectedFamily,
    EntryCandidateSnapshot[] Candidates, string? PositionId, int Quantity, bool UnresolvedOrders, bool AwaitReconciliation,
    DateTimeOffset? CooldownUntil, string Status, string ConfigurationHash)
{
    public BinsHeldPositionSnapshot? Position { get; init; }
    public BinsOrderSnapshot[] Orders { get; init; } = [];
    public string? LastEntryEpisode { get; init; }
    public ExitIntent? PendingExit { get; init; }
    public BinsAccountRiskSnapshot? AccountRisk { get; init; }
}
public sealed record BinsHeldPositionSnapshot(string PositionId, StrategyFamily EntryFamily, EntryPattern EntryPattern, DateTimeOffset FirstFillAt,
    int InitialConfirmedQuantity, int RemainingQuantity, decimal AverageFillPrice, decimal TechnicalStop, decimal Peak, decimal TrailingStop,
    bool TrailingActive, int TP1Filled, int TP2Filled, string ConfigurationHash);
public sealed record BinsOrderSnapshot(string Id, bool Buy, int Quantity, int Filled, decimal LimitPrice, OrderState State, DateTimeOffset CreatedAt, DateTimeOffset? CancellationRequestedAt);
public sealed record BinsClosedTrade(string PositionId, string Symbol, string Currency, StrategyFamily Family, EntryPattern Pattern,
    DateTimeOffset EnteredAt, DateTimeOffset ExitedAt, int InitialQuantity, decimal Profit, ExitReason? ExitReason, int TP1Filled, int TP2Filled, string ConfigurationHash)
{ public DateTimeOffset CooldownUntil { get; init; } }
public sealed record CurrencyEquityPoint(DateTimeOffset Time, string Currency, decimal Equity, decimal Exposure);
public sealed record BinsAccountRiskSnapshot(string Currency, DateOnly TradingDate, int ConfirmedEntries, int ReservedEntries, int ConsecutiveStops,
    bool DailyLossLatched, decimal OpeningEquity, decimal CurrentEquity, IReadOnlyDictionary<string, int> SymbolEntries, IReadOnlyDictionary<StrategyFamily, int> FamilyEntries);

/// <summary>One account lock serializes all symbol events, reservations and confirmed fills. No broker/network/live submission path.</summary>
public sealed class BinsStrategyEngine
{
    private sealed class Row
    {
        public required Instrument Instrument;
        public required BinsStrategyConfig Config;
        public required BinsHistory History;
        public required BinsCausalInput Input;
        public required MarketRegimeClassifier Classifier;
        public required SupportActivationGate Stage;
        public required EntryPatternBook Patterns;
        public readonly StrategyFamilySelector Selector = new();
        public BinsBarBuilder[] Builders = [];
        public MarketEvent? Quote;
        public MarketSession? Session;
        public BinsMarketInput? LastInput;
        public BinsPositionState? Position;
        public EntrySignal? PendingEntry;
        public ExitIntent? PendingExit;
        public BinsStrategyConfig? PendingConfig;
        public decimal? PreviousPrice;
        public DateTimeOffset LastTrade;
        public DateTimeOffset? Cooldown;
        public string SessionId = "", Status = "InsufficientHistory";
        public bool AwaitReconciliation;
        public string? LastEpisode;
        public int ExitsFilledAtIntent;
        public int ConfirmedQuantity;
        public DateTimeOffset LastSupportBar;
        public string? LastRuleHash;
        public SupportSnapshot? Support;
        public string? AuditState;
        public string? BoxMonitorId;
        public DateTimeOffset? BoxBreakStarted;
        public int BoxBreakTicks;
    }
    private sealed class DailyAccount(decimal opening)
    {
        public decimal OpeningEquity = opening;
        public int Entries, ConsecutiveStops;
        public bool LossLatched;
        public readonly Dictionary<string, int> Symbols = [];
        public readonly Dictionary<StrategyFamily, int> Families = [];
    }
    private readonly object gate = new();
    private BinsStrategyConfig desired;
    private readonly Dictionary<string, Row> rows = [];
    private readonly Dictionary<string, IBinsExecutionAdapter> accounts;
    private readonly Dictionary<(string Currency, DateOnly Day), DailyAccount> days = [];
    private readonly Dictionary<string, int> fillsRead = [];
    private readonly HashSet<string> seenSignals = [];
    private readonly Dictionary<string, (EntrySignal Entry, BinsStrategyConfig Config, ExitReason? Exit)> orderEvidence = [];
    private readonly List<BinsDecision> decisions = [];
    private readonly List<BinsClosedTrade> closed = [];
    private readonly List<CurrencyEquityPoint> equity = [];
    private bool entriesEnabled = true;
    private readonly bool evaluationOnly;
    private int orderSequence;
    private DateTimeOffset lastNow;
    public BinsStrategyConfig Configuration { get { lock (gate) return desired.Snapshot(); } }
    public BinsDecision[] Decisions { get { lock (gate) return decisions.ToArray(); } }
    public BinsClosedTrade[] ClosedTrades { get { lock (gate) return closed.ToArray(); } }
    public CurrencyEquityPoint[] Equity { get { lock (gate) return equity.ToArray(); } }
    public IReadOnlyDictionary<string, IBinsExecutionAdapter> Accounts => new Dictionary<string, IBinsExecutionAdapter>(accounts);
    public bool RequiresClock(DateTimeOffset at)
    { lock (gate) return accounts.Values.Any(a => a.Orders.Any(o => o.Active && o.State != OrderState.Unknown)) || rows.Values.Any(r => r.Position != null && r.Session != null && at < r.Session.Close); }
    public BinsStrategyEngine(BinsStrategyConfig configuration, IEnumerable<Instrument> instruments, IEnumerable<IBinsExecutionAdapter> execution, bool evaluationOnly = false)
    {
        this.evaluationOnly = evaluationOnly;
        desired = configuration.Snapshot(); accounts = execution.ToDictionary(a => a.Currency);
        if (accounts.Count == 0 || accounts.Values.Any(a => a.IsLive || !a.Synchronized || a.Orders.Count > 0 || a.Positions.Values.Any(p => p.Quantity > 0))) throw new InvalidOperationException("Bin requires empty synchronized PAPER adapters. Live automatic trading is locked");
        foreach (var i in instruments)
        {
            i.Validate(); if (!desired.Market.EnabledExchanges.Contains(i.Exchange) || !accounts.ContainsKey(i.Currency) || rows.ContainsKey(i.Key)) throw new InvalidOperationException("Unsupported/duplicate instrument or missing currency account");
            var config = desired.Snapshot(); var row = new Row { Instrument = i, Config = config, History = new(i, config), Input = new(i, config.Quality), Classifier = new(config.Regime, config.Quality, config.ConfigurationHash), Stage = new(config.Stage1), Patterns = new(config) };
            row.Builders = Builders(i, config); rows.Add(i.Key, row);
        }
        if (rows.Count == 0) throw new InvalidOperationException("No instruments");
        foreach (var a in accounts.Values) fillsRead[a.Currency] = 0;
    }
    // Trading-day bars (including N sessions) arrive as provider-confirmed completed bars; idle time never closes a synthetic daily bar.
    private static BinsBarBuilder[] Builders(Instrument i, BinsStrategyConfig c)
    { var frames = new List<BarTimeframeSpec> { c.Regime.Timeframe }; if (c.Entries.UptrendPullback.Enabled) frames.Add(c.Entries.UptrendPullback.EmaBar); if (c.Stage1.Enabled) { frames.Add(c.Stage1.MovingAverageBar); frames.Add(c.Stage1.ProfileBar); } return frames.Where(f => f.Unit != BarUnit.TradingDay).Distinct().Select(f => new BinsBarBuilder(i, f)).ToArray(); }
    public BinsSymbolSnapshot Snapshot(string symbol)
    {
        lock (gate)
        {
            var r = rows[symbol]; var a = accounts[r.Instrument.Currency];
            var held = a.Positions.GetValueOrDefault(symbol); var position = r.Position;
            return new(symbol, r.Classifier.Snapshot, r.Stage.Snapshot, r.Selector.Selected, r.Patterns.Candidates, r.Position?.PositionId,
                a.Positions.GetValueOrDefault(symbol)?.Quantity ?? 0, a.Orders.Any(o => o.Symbol == symbol && o.Active), r.AwaitReconciliation || !a.Synchronized, r.Cooldown, r.Status, r.Config.ConfigurationHash)
            {
                Position = position == null || held == null ? null : new(position.PositionId, position.Entry.Family, position.Entry.EntryPattern, position.FirstFillAt, position.InitialConfirmedQuantity, held.Quantity, held.AveragePrice, position.TechnicalStop, position.Peak, position.TrailingStop, position.TrailingActive, position.TP1Filled, position.TP2Filled, position.Configuration.ConfigurationHash),
                Orders = a.Orders.Where(o => o.Symbol == symbol).Select(o => new BinsOrderSnapshot(o.Id, o.Buy, o.Quantity, o.Filled, o.Price, o.State, o.Created, o.CancellationRequestedAt)).ToArray(),
                LastEntryEpisode = r.LastEpisode, PendingExit = r.PendingExit, AccountRisk = r.Session == null ? null : RiskSnapshot(a.Currency, r.Session.TradingDate)
            };
        }
    }
    public BinsAccountRiskSnapshot RiskSnapshot(string currency, DateOnly date)
    {
        lock (gate) { var a = accounts[currency]; var d = days.GetValueOrDefault((currency, date)) ?? new(a.Equity); return new(currency, date, d.Entries, a.Orders.Where(o => o.Buy && o.Active && rows[o.Symbol].Position == null).Select(o => o.Symbol).Distinct().Count(), d.ConsecutiveStops, d.LossLatched, d.OpeningEquity, a.Equity, new Dictionary<string, int>(d.Symbols), new Dictionary<StrategyFamily, int>(d.Families)); }
    }
    public bool LoadCompletedBar(CompletedBar bar, DateTimeOffset availableAt)
    {
        lock (gate)
        {
            if (availableAt < lastNow || !rows.TryGetValue(bar.Instrument.Key, out var r) || !r.History.AddBar(bar, availableAt)) return false;
            lastNow = availableAt;
            if (bar.Timeframe == r.Config.Regime.Timeframe) r.Classifier.OnBar(bar, availableAt);
            return true;
        }
    }
    public bool LoadProfileTrade(HistoricalTrade trade, DateTimeOffset availableAt)
    { lock (gate) { if (availableAt < lastNow || !rows.TryGetValue(trade.Instrument.Key, out var r) || !r.History.AddProfileTrade(trade, availableAt)) return false; lastNow = availableAt; return true; } }
    /// <summary>Changes are deferred per symbol until flat/resolved; held position configuration is immutable. Disabling entries acts immediately.</summary>
    public void UpdateConfiguration(BinsStrategyConfig config, DateTimeOffset at)
    {
        lock (gate)
        {
            var next = config.Snapshot(); desired = next;
            foreach (var r in rows.Values) { r.PendingConfig = next.Snapshot(); if (!next.Enabled) { r.Patterns.ResetCandidates("Immediate global entry disable"); CancelBuys(r, at); } ApplyPendingConfig(r, at); }
            Log(at, "*", "Configuration", "New entries use new snapshot; existing position exits remain frozen", next.ConfigurationHash);
        }
    }
    public void SetEntriesEnabled(bool enabled, DateTimeOffset at)
    { lock (gate) { entriesEnabled = enabled; if (!enabled) foreach (var r in rows.Values) { r.Patterns.ResetCandidates("User stopped entries; protect holdings"); CancelBuys(r, at); } } }
    public void EmergencyStop(DateTimeOffset at) => SetEntriesEnabled(false, at); // No unrequested liquidation/market fallback.
    public void ConfirmReconciled(string symbol, DateTimeOffset at)
    {
        lock (gate) { var r = rows[symbol]; var a = accounts[r.Instrument.Currency]; if (!a.Synchronized || a.Orders.Any(o => o.Symbol == symbol && o.Active)) throw new InvalidOperationException("Unresolved account orders"); r.AwaitReconciliation = false; r.Patterns.ResetCandidates("Explicit reconciliation; fresh signal required"); Log(at, symbol, "Reconciled", "Account state explicitly checked", r.Config.ConfigurationHash); }
    }
    /// <summary>Execution callbacks enter the SAME account lock as market events. FillTime is original; cooldown starts when the full close is known.</summary>
    public void OnExecutionReport(string currency, BinsExecutionReport report, DateTimeOffset now)
    {
        lock (gate)
        {
            if (now < lastNow || report.ReceivedAt > now) throw new InvalidOperationException("Execution availability time reversal/future report");
            lastNow = now; var a = accounts[currency]; a.Report(report); ReadFills(a, now);
            var order = a.Orders.Single(o => o.Id == report.OrderId); var r = rows[order.Symbol];
            if (order.State == OrderState.Unknown) r.AwaitReconciliation = true;
            decisions.Add(new(now, order.Symbol, "ExecutionReport", report.State.ToString(), orderEvidence[order.Id].Config.ConfigurationHash, OrderId: order.Id, StateSnapshot: System.Text.Json.JsonSerializer.Serialize(report, BinsJson.Options)));
            RecordState(r, now); RecordEquity(a, now);
        }
    }
    public void OnMarket(BinsMarketInput input, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!rows.TryGetValue(input.Event.Key, out var r)) throw new InvalidOperationException("Unregistered symbol");
            if (now < lastNow) { Block(r, now, "Engine availability clock reversal"); return; } lastNow = now;
            var e = input.Event; var a = accounts[e.Instrument.Currency]; var admission = r.Input.Observe(input, now);
            if (!admission.Accepted) { if (!admission.Duplicate) Block(r, now, admission.Reason); return; }
            var s = input.Session!; var sessionChanged = r.SessionId != s.Id;
            if (sessionChanged)
            {
                if (r.SessionId != "" && (a.Positions.GetValueOrDefault(e.Key)?.Quantity > 0 || a.Orders.Any(o => o.Symbol == e.Key && o.Active)))
                { r.AwaitReconciliation = true; CancelBuys(r, now); Log(now, e.Key, "UnclosedPosition", "Previous session holdings/orders require reconciliation; no next-day entries", r.Config.ConfigurationHash); }
                r.SessionId = s.Id; r.Stage.ResetSession(s.Id, now); if (r.Classifier.SessionId != s.Id) r.Classifier.ResetSession(s.Id, now); r.Patterns.ResetSession(); r.PreviousPrice = null; r.LastTrade = default; r.Quote = null; r.History.ResetTrades(); foreach (var builder in r.Builders) builder.Reset();
            }
            r.Session = s; r.LastInput = input;
            if (admission.Discontinuity && !sessionChanged)
            { r.Patterns.ResetCandidates("Trade/source gap"); r.Classifier.Invalidate("Trade/source continuity lost", now); r.History.ResetTrades(); r.Stage.InvalidateData(now); r.BoxBreakStarted = null; r.BoxBreakTicks = 0; foreach (var builder in r.Builders) builder.Reset(); }
            var day = Day(a, s.TradingDate);
            var metadataValid = input.Metadata?.Instrument == r.Instrument && input.Metadata.CanExecuteAt(now);
            if (metadataValid && (!r.Config.Market.RegularSessionOnly || s.Phase == SessionPhase.Regular))
                a.OnMarket(e); // Execution sees only accepted, verified tradable events, never stale/phantom prices.
            ReadFills(a, now);
            ExpireOrders(r, now);
            ApplyPendingConfig(r, now);
            if (r.Config.Profile(r.Instrument).MaxDailyLoss is {} loss && day.OpeningEquity - a.Equity >= loss) day.LossLatched = true;
            if (day.LossLatched || day.ConsecutiveStops >= r.Config.SharedRisk.MaxConsecutiveStops)
                foreach (var pending in a.Orders.Where(o => o.Buy && o.Active).ToArray()) a.Cancel(pending, now);
            if (now >= s.Close - r.Config.SharedExit.StopNewEntriesBeforeClose.Elapsed || !metadataValid) CancelBuys(r, now);
            if (e.Kind == EventKind.Quote) { r.Quote = e; r.Status = metadataValid ? "Quote updated; no entry on quote" : "MissingData: verified instrument metadata"; RecordEquity(a, now); RecordState(r, now); return; }
            if (r.LastTrade != default && now - r.LastTrade > r.Config.Quality.MaximumTradeGap.Elapsed) r.Patterns.ResetCandidates("Valid trade gap resets hold/stability");
            r.LastTrade = e.ExchangeTime;
            foreach (var builder in r.Builders)
            {
                var completed = builder.Add(input, input.ContinuityVerified);
                if (completed != null && r.History.AddBar(completed, now) && completed.Timeframe == r.Config.Regime.Timeframe) r.Classifier.OnBar(completed, now);
            }
            r.Stage.OnClock(now, s);
            ObserveBoxValidity(r, input, a);
            if (r.Config.Stage1.Enabled)
            {
                var lastBar = r.History.Bars(r.Config.Stage1.ProfileBar, now).LastOrDefault()?.KnownAt ?? default;
                var maBar = r.History.Bars(r.Config.Stage1.MovingAverageBar, now).LastOrDefault()?.KnownAt ?? default; if (maBar > lastBar) lastBar = maBar;
                var ruleHash = input.Metadata?.PriceRule == null ? null : BinsJson.Hash(System.Text.Json.JsonSerializer.Serialize(input.Metadata.PriceRule));
                if (lastBar != r.LastSupportBar || ruleHash != r.LastRuleHash)
                { var support = SupportCalculator.Calculate(r.Instrument, r.Config.Stage1, r.History, input.Metadata?.PriceRule, now); r.Support = support.Support; r.LastSupportBar = lastBar; r.LastRuleHash = ruleHash; }
                r.Stage.OnTrade(input, r.Support, s.Close - r.Config.SharedExit.StopNewEntriesBeforeClose.Elapsed);
                if (!r.Stage.Snapshot.Allows(now, s.Id)) CancelBuys(r, now);
            }
            if (a.Positions.TryGetValue(e.Key, out var held) && held.Quantity > 0 && r.Position != null)
            {
                var pconfig = r.Position.Configuration;
                var exit = r.Position.Evaluate(held, e.TradePrice!.Value, now, s, FeeSchedule.At(pconfig.Profile(r.Instrument), r.Instrument.Exchange, now));
                if (exit != null && (r.PendingExit == null || exit.Protective && !r.PendingExit.Protective)) SetExit(r, exit, now);
            }
            ProcessExit(r, now);
            var unresolved = a.Orders.Any(o => o.Symbol == e.Key && o.Active) || a.Positions.GetValueOrDefault(e.Key)?.Quantity > 0;
            if (unresolved || !a.Synchronized || r.AwaitReconciliation)
            { r.Selector.Select(r.Config.Selection, r.Classifier.Snapshot, s.TradingDate, true); r.Patterns.ObserveWhileBlocked(e.TradePrice!.Value); r.Status = !a.Synchronized ? "UnknownOrder/AwaitReconciliation" : "Holding/PendingOrder: no additional buy"; FinishTick(r, input, a, now); return; }
            var previousFamily = r.Selector.Selected;
            var family = r.Selector.Select(r.Config.Selection, r.Classifier.Snapshot, s.TradingDate, false);
            if (family != null && family != previousFamily) r.Patterns.ResetCandidates("Selected family changed: old unfinished candidates cannot carry across regimes");
            var blocked = !entriesEnabled || !desired.Enabled || !r.Config.Enabled || family == null || !r.Stage.Snapshot.Allows(now, s.Id) || r.Cooldown > now || RiskBlocked(r, day, a);
            if (blocked || r.Config.Quality.RequireContinuousTrades && !input.ContinuityVerified)
            { r.Patterns.ObserveWhileBlocked(e.TradePrice!.Value); r.Status = family == null ? "UnknownRegime/invalid selected-family structure" : !r.Stage.Snapshot.Allows(now, s.Id) ? "Stage1NotActive" : "Cooldown/disabled/exceeded risk/quality"; FinishTick(r, input, a, now); return; }
            var regimeAtSignal = r.Classifier.Snapshot;
            var evaluation = r.Patterns.Evaluate(new(input, regimeAtSignal, r.History, r.PreviousPrice), family!.Value);
            if (evaluation.BoxBrokenDown) r.Classifier.UpdateBox(BoxState.BrokenDown, now);
            // A locked box emits at most one breakout episode even when downstream risk rejects the order.
            if (evaluation.BoxBrokenUp) r.Classifier.UpdateBox(BoxState.BrokenUp, now);
            if (evaluation.Ready.Length > 0)
            {
                var intent = evaluation.Ready[0];
                foreach (var skipped in evaluation.Ready.Skip(1)) Log(now, e.Key, "PatternPriority", "Suppressed " + skipped.Pattern + " by " + intent.Pattern, r.Config.ConfigurationHash);
                var spread = r.Quote?.Bid > 0 && r.Quote.Ask >= r.Quote.Bid ? 100 * (r.Quote.Ask!.Value - r.Quote.Bid.Value) / ((r.Quote.Ask.Value + r.Quote.Bid.Value) / 2) : (decimal?)null;
                var quoteQuality = spread != null && r.Quote!.BidSize > 0 && r.Quote.AskSize > 0 && now - r.Quote.ExchangeTime <= r.Config.Quality.MaxQuoteAge.Elapsed ? "Observed fresh two-sided quote" : "MissingData/unverified quote";
                var id = $"{e.Key}|{s.Id}|{family}|{intent.Pattern}|{intent.EpisodeId}|{e.Sequence}";
                var signal = new EntrySignal(id, e.Key, e.Instrument.Exchange, family.Value, intent.Pattern, intent.EpisodeId, now, e.Sequence, e.TradePrice!.Value,
                    BinsJson.Export(r.Config), regimeAtSignal, r.Stage.Snapshot, intent.TechnicalAnchor, intent.Low, intent.High, intent.MicroHigh, intent.MicroHighKnownAt,
                    spread ?? -1, quoteQuality, r.Config.ConfigurationHash, intent.Reason);
                if (seenSignals.Add(id) && r.LastEpisode != signal.EpisodeId)
                {
                    Log(now, e.Key, "EntrySignal", signal.Reason, signal.ConfigurationHash, signal: signal);
                    var ownedValue = a.Positions.Values.Sum(p => p.Quantity * p.Last); var reservedValue = a.Orders.Where(o => o.Active && o.Buy).Sum(o => o.Remaining * o.Price);
                    var concurrent = a.Positions.Values.Where(p => p.Quantity > 0).Select(p => p.Symbol).Union(a.Orders.Where(o => o.Active && o.Buy).Select(o => o.Symbol)).Count();
                    var approval = BinsRisk.Approve(r.Config, signal, input, r.Quote, a, ownedValue, reservedValue, concurrent, RiskBlocked(r, day, a));
                    if (approval.Allowed)
                    {
                        if(evaluationOnly){r.Status="ScreenMatched: no order submitted";Log(now,e.Key,"ScreenMatched",signal.Reason,signal.ConfigurationHash,signal:signal);}
                        else SubmitBuy(r, signal, approval, input.Metadata!.PriceRule!, now);
                    }
                    else { r.Status = approval.Reason; Log(now, e.Key, "EntryRejected", approval.Reason, signal.ConfigurationHash, signal: signal); }
                }
            }
            else r.Status = evaluation.InsufficientHistory.Length == 0 ? "Watching " + family : "Watching " + family + "; InsufficientHistory: " + string.Join("; ", evaluation.InsufficientHistory);
            FinishTick(r, input, a, now);
        }
    }
    public void OnClock(DateTimeOffset now)
    {
        lock (gate)
        {
            if (now < lastNow) throw new InvalidOperationException("Clock reversal"); lastNow = now;
            foreach (var a in accounts.Values) { a.OnTime(now); ReadFills(a, now); }
            foreach (var r in rows.Values)
            {
                r.Stage.OnClock(now, r.Session); r.Classifier.OnClock(now); ExpireOrders(r, now); ApplyPendingConfig(r, now);
                if (r.Session is {} entrySession && (!r.Stage.Snapshot.Allows(now, entrySession.Id) || now >= entrySession.Close - r.Config.SharedExit.StopNewEntriesBeforeClose.Elapsed)) CancelBuys(r, now);
                if (r.LastTrade != default && now - r.LastTrade > r.Config.Quality.MaxTradeAge.Elapsed) { r.Patterns.ResetCandidates("No fresh trades; clock cannot confirm a hold"); r.Stage.InvalidateData(now); CancelBuys(r, now); }
                var a = accounts[r.Instrument.Currency];
                if (r.Session is {} session && r.Position != null && a.Positions.GetValueOrDefault(r.Instrument.Key) is { Quantity: > 0 } p)
                {
                    var c = r.Position.Configuration;
                    if (c.SharedExit.ClosePositionsAtSessionEnd && now >= session.Close - c.SharedExit.StartLiquidationBeforeClose.Elapsed && r.PendingExit == null)
                    { var exit = r.Position.Evaluate(p, p.Last, now, session, FeeSchedule.At(c.Profile(r.Instrument), r.Instrument.Exchange, now), true); if (exit != null) SetExit(r, exit, now); }
                    ProcessExit(r, now);
                    if (now >= session.Close && !r.AwaitReconciliation) { r.AwaitReconciliation = true; r.Status = "UnclosedPosition: session ended, limit fill not guaranteed"; Log(now, r.Instrument.Key, "UnclosedPosition", r.Status, c.ConfigurationHash); }
                }
                RecordState(r, now);
            }
        }
    }
    private void ApplyPendingConfig(Row r, DateTimeOffset now)
    {
        var a = accounts[r.Instrument.Currency]; if (r.PendingConfig == null || a.Positions.GetValueOrDefault(r.Instrument.Key)?.Quantity > 0 || a.Orders.Any(o => o.Symbol == r.Instrument.Key && o.Active)) return;
        var previous = r.History; r.Config = r.PendingConfig; r.PendingConfig = null; r.History = new(r.Instrument, r.Config);
        foreach (var f in new[] { r.Config.Regime.Timeframe, r.Config.Entries.UptrendPullback.EmaBar, r.Config.Stage1.MovingAverageBar, r.Config.Stage1.ProfileBar }.Distinct()) foreach (var b in previous.Bars(f, now)) r.History.AddBar(b, now);
        r.Input = new(r.Instrument, r.Config.Quality); r.Classifier = new(r.Config.Regime, r.Config.Quality, r.Config.ConfigurationHash); r.Stage = new(r.Config.Stage1); r.Stage.ResetSession(r.SessionId, now); r.Patterns = new(r.Config); r.Builders = Builders(r.Instrument, r.Config); r.Quote = null; r.PreviousPrice = null; r.Support = null;
        foreach (var b in r.History.Bars(r.Config.Regime.Timeframe, now)) r.Classifier.OnBar(b, now);
    }
    private void ObserveBoxValidity(Row r, BinsMarketInput input, IBinsExecutionAdapter account)
    {
        var box = r.Classifier.Snapshot.Box; var price = input.Event.TradePrice!.Value; var at = input.KnownAt;
        if (box == null || box.State != BoxState.Valid || r.BoxMonitorId != box.Id)
        { r.BoxMonitorId = box?.Id; r.BoxBreakStarted = null; r.BoxBreakTicks = 0; }
        if (box is not { State: BoxState.Valid }) return;
        if (price < box.Lower * (1 - r.Config.Entries.RangeLowerBounce.BreakTolerancePct / 100))
        { r.Classifier.UpdateBox(BoxState.BrokenDown, at); r.Patterns.ResetCandidates("Frozen lower broken, including while holding or Stage1 blocked"); return; }
        // Observe structural consumption while holding without permitting another position/pattern order.
        if (account.Positions.GetValueOrDefault(r.Instrument.Key)?.Quantity is not > 0 && !account.Orders.Any(o => o.Symbol == r.Instrument.Key && o.Active)) { r.BoxBreakStarted = null; return; }
        var p = r.Config.Entries.RangeUpperBreakout; var floor = box.Upper * (1 + p.BreakoutBufferPct / 100);
        if (price < floor || !input.ContinuityVerified) { r.BoxBreakStarted = null; r.BoxBreakTicks = 0; return; }
        if (r.BoxBreakStarted == null) { if (r.PreviousPrice >= floor || r.PreviousPrice == null) return; r.BoxBreakStarted = at; r.BoxBreakTicks = 1; }
        else r.BoxBreakTicks++;
        if (at - r.BoxBreakStarted >= p.HoldDuration.Elapsed && r.BoxBreakTicks >= p.MinValidTicks) { r.Classifier.UpdateBox(BoxState.BrokenUp, at); r.Patterns.ResetCandidates("Frozen upper consumed during existing position/order"); }
    }
    private void Block(Row r, DateTimeOffset now, string reason)
    { r.Status = reason; r.Quote = null; r.Patterns.ResetCandidates(reason); r.Classifier.Invalidate(reason, now); r.Stage.InvalidateData(now); r.History.ResetTrades(); foreach (var builder in r.Builders) builder.Reset(); CancelBuys(r, now); Log(now, r.Instrument.Key, "DataRejected", reason, r.Config.ConfigurationHash); }
    private void FinishTick(Row r, BinsMarketInput input, IBinsExecutionAdapter a, DateTimeOffset now)
    { r.History.AddTrade(new(r.Instrument, input.Event.ExchangeTime, input.KnownAt, input.Event.TradePrice!.Value, input.Event.TradeSize!.Value, input.Event.Source, input.Event.Sequence)); r.PreviousPrice = input.Event.TradePrice; RecordEquity(a, now); RecordState(r, now); }
    private DailyAccount Day(IBinsExecutionAdapter a, DateOnly date)
    { if (!days.TryGetValue((a.Currency, date), out var d)) days[(a.Currency, date)] = d = new(a.Equity); return d; }
    private bool RiskBlocked(Row r, DailyAccount d, IBinsExecutionAdapter a)
    {
        var c = r.Config; var p = c.Profile(r.Instrument);
        if (p.MaxDailyLoss != null && d.OpeningEquity - a.Equity >= p.MaxDailyLoss) d.LossLatched = true;
        var reserved = a.Orders.Where(o => o.Active && o.Buy && rows[o.Symbol].Position == null).Select(o => o.Symbol).Distinct().ToArray();
        var familyReservations = r.Selector.Selected is {} selected ? reserved.Count(symbol => rows[symbol].PendingEntry?.Family == selected) : 0;
        return d.LossLatched || d.ConsecutiveStops >= c.SharedRisk.MaxConsecutiveStops || d.Entries + reserved.Length >= c.SharedRisk.MaxEntriesPerAccountPerDay || d.Symbols.GetValueOrDefault(r.Instrument.Key) + (reserved.Contains(r.Instrument.Key) ? 1 : 0) >= c.SharedRisk.MaxEntriesPerSymbolPerDay || r.Selector.Selected is {} f && d.Families.GetValueOrDefault(f) + familyReservations >= c.SharedRisk.MaxEntriesPerFamilyPerDay;
    }
    private void SubmitBuy(Row r, EntrySignal signal, EntryApproval approved, PriceRule rule, DateTimeOffset now)
    {
        var a = accounts[r.Instrument.Currency]; var order = new ManagedOrder { Id = "bins:" + ++orderSequence, Instrument = r.Instrument, Buy = true, Kind = OrderKind.Limit, Quantity = approved.Quantity, Price = approved.LimitPrice, Created = now, Strategy = "bins-strategy-v1", Reason = signal.Id };
        r.PendingEntry = signal; orderEvidence.Add(order.Id, (signal, r.Config.Snapshot(), null));
        try { a.SubmitIntent(new("intent:" + signal.Id, signal.Id, order, approved.Fees!, rule, r.Config.Execution, signal.ConfigurationHash)); r.Status = "PendingBuy"; Log(now, r.Instrument.Key, "OrderIntent", "Capped limit buy; signal is not a fill", signal.ConfigurationHash, order.Id, signal); }
        catch (Exception error) when (error is not OutOfMemoryException) { order.State = OrderState.Unknown; r.AwaitReconciliation = true; Log(now, r.Instrument.Key, "UnknownOrder", error.GetType().Name + ": no blind retry", signal.ConfigurationHash, order.Id, signal); }
    }
    private void SetExit(Row r, ExitIntent intent, DateTimeOffset now)
    {
        r.PendingExit = intent; r.ExitsFilledAtIntent = accounts[r.Instrument.Currency].Fills.Where(f => f.Instrument == r.Instrument && !f.Buy).Sum(f => f.Quantity);
        Log(now, r.Instrument.Key, "ExitSignal", intent.Reason, r.Position!.Configuration.ConfigurationHash, exit: intent);
        CancelBuys(r, now);
        if (intent.Protective) foreach (var o in accounts[r.Instrument.Currency].Orders.Where(o => o.Symbol == r.Instrument.Key && o.Active && !o.Buy).ToArray()) accounts[r.Instrument.Currency].Cancel(o, now);
    }
    private void ProcessExit(Row r, DateTimeOffset now)
    {
        if (r.PendingExit == null || r.Position == null || r.LastInput?.Session is not {} session) return;
        var a = accounts[r.Instrument.Currency]; var c = r.Position.Configuration; var symbol = r.Instrument.Key;
        if (!a.Synchronized || a.Orders.Any(o => o.Symbol == symbol && o.Active) || a.Positions.GetValueOrDefault(symbol) is not { Quantity: > 0 } p) return;
        var input = r.LastInput; var quote = r.Quote; var meta = input.Metadata;
        if (!session.ValidAt(r.Instrument, now) || meta == null || !meta.CanExecuteAt(now) || quote == null || quote.Bid is not > 0 || quote.ExchangeTime > now || now - quote.ExchangeTime > c.Quality.MaxQuoteAge.Elapsed || meta.PriceRule is not {} rule || !rule.ValidAt(now)) { r.Status = "Exit unfilled: missing valid session/quote/rule; no market fallback"; return; }
        var fees = FeeSchedule.At(c.Profile(r.Instrument), r.Instrument.Exchange, now); if (fees == null) { r.Status = "Exit blocked: dated fees missing"; return; }
        var soldSince = a.Fills.Where(f => f.Instrument == r.Instrument && !f.Buy).Sum(f => f.Quantity) - r.ExitsFilledAtIntent;
        var quantity = Math.Min(p.Quantity, Math.Max(0, r.PendingExit.Quantity - soldSince));
        if (quantity == 0) { r.PendingExit = null; return; }
        var minimum = rule.Ceiling(r.PendingExit.MinimumPrice);
        if (r.PendingExit.Kind is ExitReason.TakeProfit1 or ExitReason.TakeProfit2) minimum = Math.Max(minimum, rule.Ceiling(r.PendingExit.ReferencePrice));
        var price = rule.Ceiling(Math.Max(minimum, quote.Bid.Value));
        if (price < meta.LowerPriceLimit || price > meta.UpperPriceLimit) { r.Status = "Exit outside verified price limit"; return; }
        var order = new ManagedOrder { Id = "bins:" + ++orderSequence, Instrument = r.Instrument, Buy = false, Kind = OrderKind.Limit, Quantity = quantity, Price = price, Created = now, Strategy = "bins-strategy-v1", Reason = r.PendingExit.Kind.ToString() };
        orderEvidence.Add(order.Id, (r.Position.Entry, c, r.PendingExit.Kind));
        try { a.SubmitIntent(new("exit:" + order.Id, r.Position.Entry.Id, order, fees, rule, c.Execution, c.ConfigurationHash)); r.Status = "PendingSell " + r.PendingExit.Kind; Log(now, symbol, "OrderIntent", "Capped exit; cancel-confirm-before-requote remaining only", c.ConfigurationHash, order.Id, exit: r.PendingExit); }
        catch (Exception error) when (error is not OutOfMemoryException) { r.AwaitReconciliation = true; Log(now, symbol, "UnknownOrder", error.GetType().Name + ": exit not guaranteed", c.ConfigurationHash, order.Id); }
    }
    private void ExpireOrders(Row r, DateTimeOffset now)
    {
        var a = accounts[r.Instrument.Currency];
        foreach (var o in a.Orders.Where(o => o.Symbol == r.Instrument.Key && o.Active && o.State is not (OrderState.Unknown or OrderState.CancelRequested)).ToArray())
        { var cfg = orderEvidence.GetValueOrDefault(o.Id).Config ?? r.Config; if (now - o.Created >= (o.Buy ? cfg.Execution.EntryTimeout.Elapsed : cfg.Execution.ProtectiveExitTimeout.Elapsed) || r.Session != null && now >= r.Session.Close) { a.Cancel(o, now); Log(now, o.Symbol, "CancelRequested", "Timeout/session close, awaiting confirmation", cfg.ConfigurationHash, o.Id); } }
    }
    private void CancelBuys(Row r, DateTimeOffset now) { var a = accounts[r.Instrument.Currency]; foreach (var o in a.Orders.Where(o => o.Symbol == r.Instrument.Key && o.Buy && o.Active).ToArray()) a.Cancel(o, now); }
    private void ReadFills(IBinsExecutionAdapter a, DateTimeOffset now)
    {
        for (var n = fillsRead[a.Currency]; n < a.Fills.Count; n++)
        {
            var fill = a.Fills[n]; var r = rows[fill.Instrument.Key]; var evidence = orderEvidence[fill.OrderId];
            var date = MarketCalendars.TradingDate(fill.Instrument.Exchange, fill.Time); var d = Day(a, date);
            if (fill.Buy)
            {
                r.ConfirmedQuantity += fill.Quantity;
                if (r.Position == null)
                { r.Position = new(evidence.Entry, evidence.Config, fill); r.LastEpisode = evidence.Entry.EpisodeId; d.Entries++; d.Symbols[r.Instrument.Key] = d.Symbols.GetValueOrDefault(r.Instrument.Key) + 1; d.Families[evidence.Entry.Family] = d.Families.GetValueOrDefault(evidence.Entry.Family) + 1; }
                else r.Position.OnFill(fill, null);
                if (r.PendingExit is { Protective: true } riskExit)
                { var sold = a.Fills.Take(n + 1).Where(f => f.Instrument == r.Instrument && !f.Buy).Sum(f => f.Quantity) - r.ExitsFilledAtIntent; r.PendingExit = riskExit with { Quantity = r.ConfirmedQuantity + sold }; }
            }
            else if (r.Position != null)
            {
                r.ConfirmedQuantity -= fill.Quantity;
                r.Position.OnFill(fill, evidence.Exit);
                if (r.ConfirmedQuantity == 0)
                {
                    var p = r.Position; var stop = BinsRisk.IsStopExit(evidence.Exit, p.RealizedPnl);
                    var cooldown = stop ? p.Configuration.SharedReentry.AfterStop.Elapsed : p.Configuration.SharedReentry.AfterProfit.Elapsed; if (cooldown < p.Configuration.SharedReentry.MinInterval.Elapsed) cooldown = p.Configuration.SharedReentry.MinInterval.Elapsed;
                    r.Cooldown = now + cooldown; d.ConsecutiveStops = stop ? d.ConsecutiveStops + 1 : 0;
                    closed.Add(new(p.PositionId, r.Instrument.Key, a.Currency, p.Entry.Family, p.Entry.EntryPattern, p.FirstFillAt, fill.Time, p.InitialConfirmedQuantity, p.RealizedPnl, evidence.Exit, p.TP1Filled, p.TP2Filled, p.Configuration.ConfigurationHash) { CooldownUntil = r.Cooldown.Value });
                    r.Position = null; r.PendingExit = null; r.PendingEntry = null; r.Patterns.ResetCandidates("Full close confirmed: cooldown and NEW episode required");
                    Log(now, r.Instrument.Key, "Closed", $"Full close confirmed; cooldown until {r.Cooldown:O}", p.Configuration.ConfigurationHash, fill.OrderId);
                }
            }
            Log(now, r.Instrument.Key, "FillConfirmed", "Confirmed ledger mutation only", evidence.Config.ConfigurationHash, fill.OrderId);
        }
        fillsRead[a.Currency] = a.Fills.Count;
    }
    private void RecordEquity(IBinsExecutionAdapter a, DateTimeOffset now) => equity.Add(new(now, a.Currency, a.Equity, a.Positions.Values.Sum(p => p.Quantity * p.Last) + a.Orders.Where(o => o.Active && o.Buy).Sum(o => o.Remaining * o.Price)));
    private void RecordState(Row row, DateTimeOffset at)
    {
        var text = System.Text.Json.JsonSerializer.Serialize(Snapshot(row.Instrument.Key), BinsJson.Options);
        if (text == row.AuditState) return; row.AuditState = text;
        decisions.Add(new(at, row.Instrument.Key, "StateSnapshot", "Causal regime/stage/family/candidate/position/order state", row.Config.ConfigurationHash, StateSnapshot: text));
    }
    private void Log(DateTimeOffset at, string symbol, string kind, string reason, string hash, string? order = null, EntrySignal? signal = null, ExitIntent? exit = null) => decisions.Add(new(at, symbol, kind, reason, hash, signal?.Id, order, signal, exit));
}
