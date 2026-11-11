using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public enum SessionPhase { PreMarket, Regular, AfterMarket }
public sealed record MarketSession(string Id, Exchange Exchange, DateOnly TradingDate, SessionPhase Phase,
    DateTimeOffset Open, DateTimeOffset Close, DateTimeOffset KnownAt, bool ProviderConfirmed,
    bool TradingAllowed, OrderKind[] AllowedOrderKinds)
{
    public bool ValidAt(Instrument i, DateTimeOffset at) => ProviderConfirmed && TradingAllowed && Exchange == i.Exchange && Enum.IsDefined(Phase) &&
        !string.IsNullOrWhiteSpace(Id) && KnownAt != default && KnownAt <= at && Open < Close && at >= Open && at < Close && AllowedOrderKinds != null && AllowedOrderKinds.Contains(OrderKind.Limit) && AllowedOrderKinds.All(k => Enum.IsDefined(k)) &&
        TradingDate == MarketCalendars.TradingDate(i.Exchange, Open);
}
public static class MarketCalendars
{
    public static TimeZoneInfo Timezone(Exchange exchange) => TimeZoneInfo.FindSystemTimeZoneById(exchange == Exchange.KRX ? "Asia/Seoul" : "America/New_York");
    public static DateOnly TradingDate(Exchange exchange, DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, Timezone(exchange)).DateTime);
    /// <summary>Converts provider supplied local open/close, including DST. This is not a holiday calendar or trading permission.</summary>
    public static (DateTimeOffset Open, DateTimeOffset Close) FromLocal(Exchange exchange, DateOnly date, TimeOnly open, TimeOnly close)
    {
        var zone = Timezone(exchange); var a = date.ToDateTime(open, DateTimeKind.Unspecified); var b = date.ToDateTime(close, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(a) || zone.IsAmbiguousTime(a) || zone.IsInvalidTime(b) || zone.IsAmbiguousTime(b) || b <= a) throw new InvalidOperationException("Invalid/ambiguous exchange session bounds");
        return (new(a, zone.GetUtcOffset(a)), new(b, zone.GetUtcOffset(b)));
    }
}
public sealed record TickBand(decimal FromInclusive, decimal? UntilExclusive, decimal Tick);
/// <summary>Provider-confirmed dated tick bands. Never calls Instrument.Tick or infers US sub-dollar rules.</summary>
public sealed record PriceRule(string Source, DateTimeOffset KnownAt, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveUntil, TickBand[] Bands)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Source) || KnownAt == default || EffectiveFrom >= EffectiveUntil || Bands == null || Bands.Length == 0) throw new InvalidOperationException("Missing dated price rule");
        for (int n = 0; n < Bands.Length; n++) { var b = Bands[n]; if (b.Tick <= 0 || b.FromInclusive < 0 || b.UntilExclusive <= b.FromInclusive || n > 0 && Bands[n - 1].UntilExclusive != b.FromInclusive || n == Bands.Length - 1 && b.UntilExclusive != null) throw new InvalidOperationException("Invalid/overlapping/gapped tick bands"); }
    }
    public decimal TickAt(decimal price) { Validate(); return Bands.FirstOrDefault(b => price >= b.FromInclusive && (b.UntilExclusive == null || price < b.UntilExclusive))?.Tick ?? throw new InvalidOperationException("Price outside verified rule"); }
    public bool ValidAt(DateTimeOffset at) => KnownAt <= at && EffectiveFrom <= at && at < EffectiveUntil;
    public decimal Floor(decimal price) => Round(price, false);
    public decimal Ceiling(decimal price) => Round(price, true);
    private decimal Round(decimal price, bool up)
    {
        Validate(); var candidates = new List<decimal>();
        foreach (var b in Bands)
        {
            var raw = up ? Math.Max(price, b.FromInclusive) : Math.Min(price, b.UntilExclusive ?? price);
            var n = (up ? decimal.Ceiling(raw / b.Tick) : decimal.Floor(raw / b.Tick)) * b.Tick;
            if (!up && b.UntilExclusive != null && n >= b.UntilExclusive) n -= b.Tick;
            if (n > 0 && n >= b.FromInclusive && (b.UntilExclusive == null || n < b.UntilExclusive) && (up ? n >= price : n <= price)) candidates.Add(n);
        }
        return candidates.Count == 0 ? throw new InvalidOperationException("No valid tick within price cap") : up ? candidates.Min() : candidates.Max();
    }
}
public interface IPriceRuleProvider { PriceRule? Get(Instrument instrument, DateTimeOffset eventTime, DateTimeOffset knownAt); }
public sealed record InstrumentMetadata(Instrument Instrument, DateTimeOffset KnownAt, DateTimeOffset ValidUntil,
    bool? Listed, bool? Halted, bool? RiskCleared, PriceRule? PriceRule, decimal? LowerPriceLimit = null, decimal? UpperPriceLimit = null)
{
    public bool CanExecuteAt(DateTimeOffset at) => KnownAt <= at && at < ValidUntil && Listed == true && Halted == false && PriceRule is {} r && r.ValidAt(at);
    public bool VerifiedAt(DateTimeOffset at) => KnownAt <= at && at < ValidUntil && Listed == true && Halted == false && RiskCleared == true && PriceRule is {} r && r.ValidAt(at);
}
public sealed record FeeRate(DateOnly From, DateOnly Until, DateTimeOffset KnownAt, string Source,
    [property: BinParameter("Dated buy commission", "%", 0, 99.999)] decimal BuyPercent,
    [property: BinParameter("Dated sell commission", "%", 0, 99.999)] decimal SellPercent,
    [property: BinParameter("Dated market sell tax, including explicit zero assumptions", "%", 0, 99.999)] decimal SellTaxPercent,
    [property: BinParameter("Dated per-share broker cost", "currency/share")] decimal PerShare)
{
    public void Validate() { if (From >= Until || KnownAt == default || string.IsNullOrWhiteSpace(Source) || new[] { BuyPercent, SellPercent, SellTaxPercent }.Any(x => x < 0 || x >= 100) || PerShare < 0 || PerShare > 1000000000000000m) throw new InvalidOperationException("Explicit valid dated fee schedule required (including zero-fee assumptions)"); }
    public decimal Fee(bool buy, int quantity, decimal price) => quantity * (price * (buy ? BuyPercent : SellPercent + SellTaxPercent) / 100 + PerShare);
}
public static class FeeSchedule
{
    public static FeeRate? At(CountryProfile profile, Exchange exchange, DateTimeOffset at) => profile.Fees.SingleOrDefault(f => f.KnownAt <= at && f.From <= MarketCalendars.TradingDate(exchange, at) && MarketCalendars.TradingDate(exchange, at) < f.Until);
}
public sealed record BinsMarketInput(MarketEvent Event, DateTimeOffset KnownAt, MarketSession? Session,
    InstrumentMetadata? Metadata, bool ContinuityVerified = false)
{
    public DateTimeOffset AvailableAt => KnownAt > Event.ReceivedTime ? KnownAt : Event.ReceivedTime;
}
public sealed record CompletedBar(Instrument Instrument, BarTimeframeSpec Timeframe, string SessionId, DateOnly TradingDate,
    DateTimeOffset OpenedAt, DateTimeOffset ClosedAt, DateTimeOffset KnownAt, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume,
    string Source, bool IsComplete = true, bool QualityVerified = true)
{
    public bool Usable(DateTimeOffset at) => IsComplete && QualityVerified && OpenedAt < ClosedAt && ClosedAt <= KnownAt && KnownAt <= at &&
        Open > 0 && Low > 0 && High >= Math.Max(Open, Close) && Low <= Math.Min(Open, Close) && High >= Low && Close > 0 && Volume >= 0 && !string.IsNullOrWhiteSpace(Source);
}
public sealed record HistoricalTrade(Instrument Instrument, DateTimeOffset Time, DateTimeOffset KnownAt, decimal Price, decimal Size, string Source, long Sequence);
public sealed record DataAdmission(bool Accepted, bool Discontinuity, bool Duplicate, string Reason);
/// <summary>Per-symbol ordering includes BOTH trade and quote channels; a duplicate never advances an episode.</summary>
public sealed class BinsCausalInput(Instrument instrument, QualityConfig policy)
{
    private readonly Dictionary<(string, EventKind), MarketEvent> prior = [];
    private DateTimeOffset lastExchange, lastKnown, lastTrade;
    private string session = "", source = "";
    public DataAdmission Observe(BinsMarketInput row, DateTimeOffset now)
    {
        var e = row.Event;
        try { e.Validate(); } catch (InvalidOperationException error) { return new(false, true, false, error.Message); }
        if (e.Sequence < 0 || e.Kind == EventKind.Trade && e.TradeSize is not > 0) return new(false, true, false, "Not a genuine positive-size trade/sequence");
        if (row.Metadata is {} metadata)
        {
            try { metadata.PriceRule?.Validate(); }
            catch (InvalidOperationException error) { return new(false, true, false, error.Message); }
            if (metadata.LowerPriceLimit <= 0 || metadata.UpperPriceLimit <= 0 || metadata.LowerPriceLimit > metadata.UpperPriceLimit) return new(false, true, false, "Invalid provider price limits");
        }
        if (e.Instrument != instrument || e.ReceivedTime == default || row.KnownAt < e.ReceivedTime || e.ExchangeTime > row.KnownAt || row.KnownAt > now || e.ReceivedTime > now || now - e.ReceivedTime > policy.MaximumProcessingLag.Elapsed || now - row.KnownAt > policy.MaximumProcessingLag.Elapsed) return new(false, true, false, "Future/reversed/processing-lagged data");
        if (row.Session is not {} s || !s.ValidAt(instrument, e.ExchangeTime) || s.KnownAt > now) return new(false, true, false, "Unverified/closed session");
        if (now - e.ExchangeTime > (e.Kind == EventKind.Trade ? policy.MaxTradeAge.Elapsed : policy.MaxQuoteAge.Elapsed) || policy.RequireVerifiedExchangeTime && e.TimeQuality != "거래소시간") return new(false, true, false, "Stale/unverified exchange timestamp");
        var reset = session != s.Id || source != e.Source;
        if (e.ExchangeTime < lastExchange || row.KnownAt < lastKnown) return new(false, true, false, "Cross-channel time reversal");
        if (!reset && prior.TryGetValue((e.Source, e.Kind), out var p) && e.Sequence <= p.Sequence) return new(false, e != p, e == p, e == p ? "Duplicate event" : "Reversed/conflicting sequence");
        var broken = reset || !row.ContinuityVerified || e.Kind == EventKind.Trade && lastTrade != default && e.ExchangeTime - lastTrade > policy.MaximumTradeGap.Elapsed;
        if (reset) { prior.Clear(); lastTrade = default; }
        prior[(e.Source, e.Kind)] = e; lastExchange = e.ExchangeTime; lastKnown = row.KnownAt; session = s.Id; source = e.Source;
        if (e.Kind == EventKind.Trade) lastTrade = e.ExchangeTime;
        return new(true, broken, false, broken ? "New/discontinuous observation segment" : "Verified causal event");
    }
}
/// <summary>No empty synthetic bars. The triggering next-bucket tick is NEVER included in the previous bar.</summary>
public sealed class BinsBarBuilder(Instrument instrument, BarTimeframeSpec timeframe)
{
    private CompletedBar? active;
    private DateTimeOffset last;
    public CompletedBar? Active => active;
    public void Reset() { active = null; last = default; }
    public CompletedBar? Add(BinsMarketInput input, bool continuous)
    {
        var e = input.Event; var s = input.Session!; var price = e.TradePrice!.Value;
        if (e.Kind != EventKind.Trade) throw new InvalidOperationException("Bar builder accepts real trades only");
        var span = timeframe.Elapsed;
        var start = span == null ? s.Open : s.Open.AddTicks((e.ExchangeTime - s.Open).Ticks / span.Value.Ticks * span.Value.Ticks);
        var end = span == null ? s.Close : (start + span.Value < s.Close ? start + span.Value : s.Close);
        CompletedBar? closed = null;
        if (active != null && (start != active.OpenedAt || s.Id != active.SessionId))
        { if (active.ClosedAt <= e.ExchangeTime && active.SessionId == s.Id && active.QualityVerified && continuous) closed = active with { IsComplete = true, KnownAt = input.KnownAt }; active = null; }
        if (active == null) active = new(instrument, timeframe, s.Id, s.TradingDate, start, end, input.KnownAt, price, price, price, price, 0, e.Source, false, continuous && (e.ExchangeTime == start || last != default && last <= start));
        active = active with { High = Math.Max(active.High, price), Low = Math.Min(active.Low, price), Close = price, Volume = active.Volume + e.TradeSize!.Value, KnownAt = input.KnownAt, QualityVerified = active.QualityVerified && continuous && last <= e.ExchangeTime };
        last = e.ExchangeTime; return closed;
    }
}
