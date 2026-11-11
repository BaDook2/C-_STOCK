using System.Text.Json;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public enum ReplayPrecision { OriginalTicks, CompletedBarsOnly }
public sealed class UnsupportedPrecisionException(string message) : InvalidOperationException(message);
public sealed record BinsReplayRequest(BinsStrategyConfig Configuration, Instrument[] Instruments, BinsMarketInput[] Events,
    CompletedBar[] WarmupBars, HistoricalTrade[] ProfileTrades, DateTimeOffset From, DateTimeOffset To,
    IReadOnlyDictionary<string, decimal> InitialCash, string Source, ReplayPrecision Precision, bool OriginalReceiveOrder,
    bool OriginalExchangeTimes, DurationSpec ClockInterval, bool SyntheticFixture = false);
public sealed record PatternPerformance(StrategyFamily Family, EntryPattern Pattern, int Entries, int ClosedTrades,
    decimal? WinRatePct, decimal? Expectancy, decimal? ProfitFactor, bool InfiniteProfitFactor, decimal? AverageHoldingSeconds,
    decimal? TP1ExecutionRatio, decimal? TP2ExecutionRatio);
public sealed record CurrencyPerformance(string Currency, decimal InitialCash, decimal FinalEquity, decimal RealizedProfit,
    decimal UnrealizedProfit, int ClosedTrades, int OpenPositions, int UnresolvedOrders, decimal MaximumDrawdownPct,
    decimal MaximumExposure, decimal? UnfilledOrderRatio, decimal? AverageSlippagePct, PatternPerformance[] Patterns,
    IReadOnlyDictionary<string, decimal> SessionProfit)
{ public decimal? OrderFillRatio { get; init; } public decimal? PartialFillRatio { get; init; } }
public sealed record BinsReplayResult(string ConfigurationHash, string DataHash, string AuditHash, string StateHash,
    string Source, ReplayPrecision Precision, bool SyntheticFixture, BinsDecision[] Decisions, BinsClosedTrade[] ClosedTrades,
    ManagedOrder[] Orders, Fill[] Fills, BinsSymbolSnapshot[] FinalStates, CurrencyPerformance[] Currencies, string[] Notices)
{ public CurrencyEquityPoint[] Equity {get;init;}=[]; }
/// <summary>One engine for replay and paper operation. Arrival ties preserve original input order; no future tick or OHLC path fabrication.</summary>
public sealed class BinsReplayEngine
{
    public BinsReplayResult Run(BinsReplayRequest request, CancellationToken cancellationToken = default)
    {
        var r = request; var config = r.Configuration.Snapshot();
        if (r.Precision != ReplayPrecision.OriginalTicks) throw new UnsupportedPrecisionException("UnsupportedPrecision: completed OHLC cannot establish tick holds, spread, event order or limit fills");
        if (!r.OriginalExchangeTimes || !r.OriginalReceiveOrder || string.IsNullOrWhiteSpace(r.Source) || r.From >= r.To || r.To > DateTimeOffset.UtcNow || r.ClockInterval.Elapsed <= TimeSpan.Zero) throw new InvalidOperationException("Original times/arrival order, source, positive clock interval and valid historical period required");
        if (r.InitialCash.Any(p => p.Key is not ("KRW" or "USD") || p.Value <= 0) || r.InitialCash.Count == 0) throw new InvalidOperationException("Explicit per-currency synthetic budgets required");
        var adapters = r.InitialCash.Select(p => (IBinsExecutionAdapter)new BinsPaperExecution(p.Key, p.Value)).ToArray();
        var engine = new BinsStrategyEngine(config, r.Instruments, adapters); engine.SetEntriesEnabled(false, DateTimeOffset.MinValue);
        var rows = r.Events.Select((e, n) => (At: e.AvailableAt, Order: n, Market: (BinsMarketInput?)e, Bar: (CompletedBar?)null, Trade: (HistoricalTrade?)null))
            .Concat(r.WarmupBars.Select((b, n) => (b.KnownAt, n, (BinsMarketInput?)null, (CompletedBar?)b, (HistoricalTrade?)null)))
            .Concat(r.ProfileTrades.Select((t, n) => (t.KnownAt, n, (BinsMarketInput?)null, (CompletedBar?)null, (HistoricalTrade?)t)))
            .OrderBy(x => x.Item1).ThenBy(x => x.Item3 == null ? 0 : 1).ThenBy(x => x.Item2).ToArray();
        var now = rows.Length > 0 && rows[0].Item1 < r.From ? rows[0].Item1 : r.From; var activated = false;
        foreach (var item in rows)
        {
            cancellationToken.ThrowIfCancellationRequested(); var at = item.Item1; if (at > r.To) continue;
            if (!activated && at >= r.From) { engine.SetEntriesEnabled(true, r.From); activated = true; }
            while (now < at)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!engine.RequiresClock(now)) { now = at; break; }
                var next = now + r.ClockInterval.Elapsed; if (next > at) next = at; engine.OnClock(next); now = next;
            }
            if (item.Item4 != null) engine.LoadCompletedBar(item.Item4, at);
            else if (item.Item5 != null) engine.LoadProfileTrade(item.Item5, at);
            else if (item.Item3 != null) engine.OnMarket(item.Item3, at);
            now = at;
        }
        while (now < r.To && engine.RequiresClock(now)) { cancellationToken.ThrowIfCancellationRequested(); now = now + r.ClockInterval.Elapsed < r.To ? now + r.ClockInterval.Elapsed : r.To; engine.OnClock(now); }
        engine.OnClock(r.To);
        var orders = adapters.SelectMany(a => a.Orders).ToArray(); var fills = adapters.SelectMany(a => a.Fills).ToArray();
        var states = r.Instruments.Select(i => engine.Snapshot(i.Key)).ToArray(); var decisions = engine.Decisions; var closed = engine.ClosedTrades;
        var performance = adapters.Select(a => Performance(a, r.InitialCash[a.Currency], closed.Where(t => t.Currency == a.Currency).ToArray(), engine.Equity.Where(e => e.Currency == a.Currency).ToArray(), decisions)).ToArray();
        return new(config.ConfigurationHash, BinsJson.Hash(JsonSerializer.Serialize(new { r.Events, r.WarmupBars, r.ProfileTrades, r.InitialCash, r.From, r.To, r.Source, r.ClockInterval }, BinsJson.Options)),
            BinsJson.Hash(JsonSerializer.Serialize(decisions, BinsJson.Options)), BinsJson.Hash(JsonSerializer.Serialize(states, BinsJson.Options)), r.Source, r.Precision, r.SyntheticFixture,
            decisions, closed, orders, fills, states, performance,
            ["Original tick/quote arrival replay using the same Bin engine; queue position unknown and conservative partial fills", "Currencies are reported separately; no unverified FX aggregation", "Open/unfilled positions remain unresolved, never fictional closing fills", config.Stage1.Enabled && config.Stage1.ProfileSource == ProfileSource.ApproximateOHLCV ? "Stage1 volume profile is ApproximateOHLCV" : "Stage1 disabled or genuine trade profile", r.SyntheticFixture ? "Synthetic fixtures: not market profitability evidence" : "Provider data quality and dated rules limit results"]){Equity=engine.Equity};
    }
    private static CurrencyPerformance Performance(IBinsExecutionAdapter a, decimal initial, BinsClosedTrade[] closed, CurrencyEquityPoint[] equity, BinsDecision[] decisions)
    {
        decimal peak = initial, mdd = 0; foreach (var e in equity) { peak = Math.Max(peak, e.Equity); if (peak > 0) mdd = Math.Max(mdd, 100 * (peak - e.Equity) / peak); }
        var patterns = Enum.GetValues<EntryPattern>().Select(pattern =>
        {
            var trades = closed.Where(t => t.Pattern == pattern).ToArray(); var wins = trades.Where(t => t.Profit > 0).Sum(t => t.Profit); var losses = -trades.Where(t => t.Profit < 0).Sum(t => t.Profit);
            var family = pattern == EntryPattern.DowntrendRebound ? StrategyFamily.DowntrendRebound : pattern is EntryPattern.UptrendBreakout or EntryPattern.UptrendPullback ? StrategyFamily.Uptrend : StrategyFamily.Range;
            var positions = decisions.Where(d => d.Kind == "OrderIntent" && d.Entry?.EntryPattern == pattern && Instrument.Parse(d.Entry.Symbol).Currency == a.Currency).Select(d => d.Entry!.Id).Distinct().ToArray();
            return new PatternPerformance(family, pattern, a.Fills.Where(f => f.Buy).Select(f => a.Orders.Single(o => o.Id == f.OrderId).Reason).Distinct().Count(id => positions.Contains(id)), trades.Length,
                trades.Length == 0 ? null : 100m * trades.Count(t => t.Profit > 0) / trades.Length, trades.Length == 0 ? null : trades.Average(t => t.Profit), losses > 0 ? wins / losses : null, losses == 0 && wins > 0,
                trades.Length == 0 ? null : trades.Average(t => (decimal)(t.ExitedAt - t.EnteredAt).TotalSeconds), trades.Length == 0 ? null : (decimal)trades.Count(t => t.TP1Filled > 0) / trades.Length, trades.Length == 0 ? null : (decimal)trades.Count(t => t.TP2Filled > 0) / trades.Length);
        }).ToArray();
        var slips = a.Fills.Select(f => { var intent = decisions.First(d => d.Kind == "OrderIntent" && d.OrderId == f.OrderId); var reference = f.Buy ? intent.Entry!.ReferencePrice : intent.Exit!.ReferencePrice; return (f.Price / reference - 1) * 100 * (f.Buy ? 1 : -1); }).ToArray();
        return new(a.Currency, initial, a.Equity, a.Fills.Where(f => !f.Buy).Sum(f => f.RealizedPnl), a.Positions.Values.Sum(p => p.Unrealized), closed.Length,
            a.Positions.Values.Count(p => p.Quantity > 0), a.Orders.Count(o => o.Active), mdd, equity.Length == 0 ? 0 : equity.Max(e => e.Exposure),
            a.Orders.Count == 0 ? null : (decimal)a.Orders.Count(o => o.Remaining > 0) / a.Orders.Count, slips.Length == 0 ? null : slips.Average(), patterns,
            a.Fills.Where(f => !f.Buy).GroupBy(f => MarketCalendars.TradingDate(f.Instrument.Exchange, f.Time).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).ToDictionary(g => g.Key, g => g.Sum(f => f.RealizedPnl)))
        { OrderFillRatio = a.Orders.Count == 0 ? null : (decimal)a.Orders.Count(o => o.Filled > 0) / a.Orders.Count,
          PartialFillRatio = a.Orders.Count == 0 ? null : (decimal)a.Orders.Count(o => o.Filled > 0 && (o.Remaining > 0 || a.Fills.Count(f => f.OrderId == o.Id) > 1)) / a.Orders.Count };
    }
}
