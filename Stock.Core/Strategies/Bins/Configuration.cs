using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public enum DurationUnit { Second, Minute, Hour, Day }
public enum BarUnit { Second, Minute, Hour, TradingDay }
public enum MarketRegime { Unknown, Downtrend, Uptrend, Range }
public enum StrategySelectionMode { Manual, Automatic }
public enum StrategyFamily { DowntrendRebound, Uptrend, Range }
public enum EntryPattern { DowntrendRebound, UptrendBreakout, UptrendPullback, RangeLowerBounce, RangeUpperBreakout }
public enum DowntrendEntryMode { Fast, Confirmed }
public enum StageOneState { Disabled, Watching, Active, Invalidated, Expired }
public enum EntryCandidateState { None, Watching, Confirming, Signaled, Invalidated, Expired }
public enum BoxState { Valid, BrokenDown, BrokenUp, Expired }
public enum ProfileSource { ApproximateOHLCV, TradeBased }
public enum ExitMode { PartialTakeProfitWithTrailing }
public enum TrailingActivation { AfterTP1Filled }
public enum TrailingPeakBasis { FromEntry, FromTP1Fill }
public enum SignalEvaluation { OnTick }

/// <summary>Elapsed time only. Day is exactly 24 hours, never a count of exchange sessions.</summary>
public sealed record DurationSpec(decimal Value, DurationUnit Unit)
{
    [JsonIgnore] public TimeSpan Elapsed
    {
        get
        {
            if (!Enum.IsDefined(Unit) || Value < 0) throw new InvalidOperationException("Invalid duration value/unit");
            var seconds = Unit switch { DurationUnit.Second => 1m, DurationUnit.Minute => 60m, DurationUnit.Hour => 3600m, _ => 86400m };
            try { var ticks = checked(Value * seconds * TimeSpan.TicksPerSecond); if (ticks != decimal.Truncate(ticks)) throw new InvalidOperationException("Duration precision is below one clock tick"); return TimeSpan.FromTicks(checked((long)ticks)); }
            catch (OverflowException e) { throw new InvalidOperationException("Duration is too large", e); }
        }
    }
    public static DurationSpec Seconds(decimal value) => new(value, DurationUnit.Second);
    public static DurationSpec Minutes(decimal value) => new(value, DurationUnit.Minute);
}
public sealed record BarTimeframeSpec(int Value, BarUnit Unit)
{
    [JsonIgnore] public TimeSpan? Elapsed => Unit == BarUnit.TradingDay ? null : new DurationSpec(Value, Unit switch { BarUnit.Second => DurationUnit.Second, BarUnit.Minute => DurationUnit.Minute, _ => DurationUnit.Hour }).Elapsed;
    public void Validate() { if (!Enum.IsDefined(Unit) || Value < 1) throw new InvalidOperationException("Use a positive bar timeframe"); _ = Elapsed; }
}
[AttributeUsage(AttributeTargets.Property)]
public sealed class BinParameterAttribute(string description, string unit = "", double minimum = 0, double maximum = 1e15) : Attribute
{
    public string Description => description;
    public string Unit => unit;
    public decimal Minimum => (decimal)minimum;
    public decimal Maximum => (decimal)maximum;
}
public sealed record ParameterMetadata(string Path, string Description, string Unit, string Type, object? DefaultValue, decimal? Minimum, decimal? Maximum);

public sealed record SelectionConfig
{
    [BinParameter("Manual family selection or confirmed regime selection")] public StrategySelectionMode Mode { get; init; } = StrategySelectionMode.Automatic;
    [BinParameter("Manual mode still requires this family's valid market structure")] public StrategyFamily ManualFamily { get; init; } = StrategyFamily.Uptrend;
    [BinParameter("Switch only while flat with every previous order resolved")] public bool AutoSwitchAfterExit { get; init; } = true;
}
public sealed record RegimeConfig
{
    [BinParameter("Classification completed bar size")] public BarTimeframeSpec Timeframe { get; init; } = new(5, BarUnit.Minute);
    [BinParameter("Actual classification observation interval", "duration", .0000001)] public DurationSpec Lookback { get; init; } = DurationSpec.Minutes(60);
    [BinParameter("Consecutive new completed bars to commit a regime", "bars", 1, 10000)] public int ConfirmBars { get; init; } = 2;
    [BinParameter("Minimum bars inside the observation interval", "bars", 4, 100000)] public int MinimumBars { get; init; } = 8;
    [BinParameter("Wilder ATR seed/window", "bars", 2, 100000)] public int AtrPeriod { get; init; } = 14;
    [BinParameter("Minimum directional efficiency for trend", "ratio", 0, 1)] public decimal ERTrend { get; init; } = .4m;
    [BinParameter("Maximum efficiency for range", "ratio", 0, 1)] public decimal ERRange { get; init; } = .2m;
    [BinParameter("Minimum directional net change", "%", .0000001, 10000)] public decimal MinTrendMovePct { get; init; } = .6m;
    [BinParameter("ATR percent multiplier for directional move", "multiple")] public decimal AtrTrendMultiplier { get; init; } = 2;
    [BinParameter("ATR structure tolerance", "multiple")] public decimal StructureAtrTolerance { get; init; } = .1m;
    [BinParameter("Quantile of completed highs", "ratio", 0, 1)] public decimal RangeUpperQuantile { get; init; } = .85m;
    [BinParameter("Quantile of completed lows", "ratio", 0, 1)] public decimal RangeLowerQuantile { get; init; } = .15m;
    [BinParameter("Distinct reactions needed on each box side", "episodes", 2, 100000)] public int RangeMinTouchesPerSide { get; init; } = 2;
    [BinParameter("Minimum separation of independent reactions", "duration", .0000001)] public DurationSpec RangeTouchSeparation { get; init; } = DurationSpec.Minutes(5);
    [BinParameter("Minimum reversal after approach", "ATR multiple")] public decimal RangeReversalMinAtr { get; init; } = .3m;
    [BinParameter("Distance to count a box side approach", "%", 0, 100)] public decimal RangeTouchTolerancePct { get; init; } = .3m;
    [BinParameter("Minimum box width", "%", 0, 10000)] public decimal RangeMinWidthPct { get; init; } = 1.5m;
    [BinParameter("Maximum box width", "%", .0000001, 10000)] public decimal RangeMaxWidthPct { get; init; } = 12;
    [BinParameter("Required closes inside candidate box", "ratio", 0, 1)] public decimal RangeMinInsideRatio { get; init; } = .8m;
    [BinParameter("Frozen box lifetime", "duration", .0000001)] public DurationSpec RangeSnapshotLifetime { get; init; } = DurationSpec.Minutes(60);
    [BinParameter("Allow real completed bars from prior sessions in observation interval")] public bool IncludePreviousSessionForLookback { get; init; }
}
public sealed record StageOneConfig
{
    [BinParameter("Optional support permission; never an entry signal")] public bool Enabled { get; init; }
    [BinParameter("Completed support MA bars", "bars", 1, 100000)] public int MovingAveragePeriod { get; init; } = 240;
    [BinParameter("MA bar size, distinct from elapsed days")] public BarTimeframeSpec MovingAverageBar { get; init; } = new(1, BarUnit.TradingDay);
    [BinParameter("Completed profile bars", "bars", 1, 100000)] public int ProfileLookbackBars { get; init; } = 240;
    [BinParameter("Profile bar size")] public BarTimeframeSpec ProfileBar { get; init; } = new(1, BarUnit.TradingDay);
    [BinParameter("Modeled OHLCV or genuine historical trades")] public ProfileSource ProfileSource { get; init; }
    [BinParameter("Verified tick multiples per profile bin", "ticks", 1, 100000)] public int ProfileBinSizeTicks { get; init; } = 5;
    [BinParameter("Maximum profile bin count; overflow blocks, never silently coarsens", "bins", 1, 1000000)] public int ProfileMaxBins { get; init; } = 512;
    [BinParameter("Threshold as percent of MAXIMUM bin volume, not value area", "%", .0000001, 100)] public decimal VolumeZoneThresholdPct { get; init; } = 70;
    [BinParameter("Maximum merge gap and merged region width", "%", 0, 100)] public decimal ProfileMergeRangePct { get; init; } = 1;
    [BinParameter("Maximum gap between MA and volume zone", "%", 0, 100)] public decimal MaxOverlapPct { get; init; } = .5m;
    [BinParameter("Maximum volume zone width", "%", 0, 100)] public decimal MaxZoneWidthPct { get; init; } = 2;
    [BinParameter("Approach distance around support", "%", 0, 100)] public decimal ApproachPct { get; init; } = 1;
    [BinParameter("Fresh low-relative rebound crossing", "%", .0000001, 100)] public decimal MinReboundPct { get; init; } = .5m;
    [BinParameter("Support candidate lifetime", "duration", .0000001)] public DurationSpec CandidateLifetime { get; init; } = DurationSpec.Minutes(15);
    [BinParameter("Stage two permission lifetime", "duration", .0000001)] public DurationSpec ActivationDuration { get; init; } = DurationSpec.Minutes(30);
    [BinParameter("Strict below-lower invalidation", "%", 0, 100)] public decimal BreakBufferPct { get; init; } = .3m;
    [BinParameter("Gap between independent support episodes", "duration", .0000001)] public DurationSpec MinDistinctEpisodeGap { get; init; } = DurationSpec.Seconds(15);
    [BinParameter("Identity tolerance for the same physical support", "%", 0, 100)] public decimal ZoneIdentityTolerancePct { get; init; } = .5m;
}
public sealed record DowntrendReboundConfig
{
    [BinParameter("Evaluate the downtrend rebound pattern")] public bool Enabled { get; init; } = true;
    [BinParameter("One mode, not two simultaneous patterns")] public DowntrendEntryMode EntryMode { get; init; } = DowntrendEntryMode.Confirmed;
    [BinParameter("Past observed drop peak interval", "duration", .0000001)] public DurationSpec DropLookback { get; init; } = DurationSpec.Minutes(10);
    [BinParameter("Minimum drop", "%", .0000001, 100)] public decimal MinDropPct { get; init; } = 1;
    [BinParameter("Drop ATR multiplier", "multiple")] public decimal DropAtrMultiplier { get; init; } = 2;
    [BinParameter("Valid trade stability after last new low", "duration", 0)] public DurationSpec LowStabilization { get; init; } = DurationSpec.Seconds(5);
    [BinParameter("Fresh rebound crossing", "%", .0000001, 100)] public decimal MinReboundPct { get; init; } = .4m;
    [BinParameter("Prior micro high observation interval", "duration", .0000001)] public DurationSpec MicroHighLookback { get; init; } = DurationSpec.Seconds(10);
    [BinParameter("Confirmed-mode micro high break buffer", "%", 0, 100)] public decimal BreakBufferPct { get; init; } = .1m;
    [BinParameter("Drop candidate lifetime", "duration", .0000001)] public DurationSpec CandidateLifetime { get; init; } = DurationSpec.Minutes(3);
    [BinParameter("Maximum chase above candidate low", "%", .0000001, 100)] public decimal MaxChasePct { get; init; } = 1;
}
public sealed record BreakoutConfig
{
    [BinParameter("Enable pattern")] public bool Enabled { get; init; } = true;
    [BinParameter("Confirmed prior high observation interval", "duration", .0000001)] public DurationSpec Lookback { get; init; } = DurationSpec.Minutes(20);
    [BinParameter("Breakout buffer", "%", 0, 100)] public decimal BreakoutBufferPct { get; init; } = .1m;
    [BinParameter("Price hold supported by continuous valid trades", "duration", .0000001)] public DurationSpec HoldDuration { get; init; } = DurationSpec.Seconds(2);
    [BinParameter("Minimum distinct trades during price hold", "ticks", 2, 1000000)] public int MinValidTicks { get; init; } = 2;
    [BinParameter("Breakout candidate lifetime", "duration", .0000001)] public DurationSpec CandidateLifetime { get; init; } = DurationSpec.Minutes(2);
    [BinParameter("Maximum chase above locked resistance", "%", .0000001, 100)] public decimal MaxChasePct { get; init; } = .5m;
}
public sealed record PullbackConfig
{
    [BinParameter("Enable pattern")] public bool Enabled { get; init; } = true;
    [BinParameter("EMA completed bar count", "bars", 2, 100000)] public int EmaPeriod { get; init; } = 20;
    [BinParameter("EMA completed timeframe")] public BarTimeframeSpec EmaBar { get; init; } = new(1, BarUnit.Minute);
    [BinParameter("Confirmed impulse high interval", "duration", .0000001)] public DurationSpec ImpulseLookback { get; init; } = DurationSpec.Minutes(30);
    [BinParameter("Minimum correction", "%", 0, 100)] public decimal MinPullbackPct { get; init; } = .3m;
    [BinParameter("Maximum correction", "%", .0000001, 100)] public decimal MaxPullbackPct { get; init; } = 2;
    [BinParameter("Maximum distance to confirmed EMA", "%", 0, 100)] public decimal MaxEMADistancePct { get; init; } = .3m;
    [BinParameter("Fresh rebound crossing", "%", .0000001, 100)] public decimal MinReboundPct { get; init; } = .25m;
    [BinParameter("Prior micro high interval", "duration", .0000001)] public DurationSpec MicroHighLookback { get; init; } = DurationSpec.Seconds(10);
    [BinParameter("Micro break buffer", "%", 0, 100)] public decimal MicroBreakBufferPct { get; init; } = .1m;
    [BinParameter("Stability supported by valid trades", "duration")] public DurationSpec LowStabilization { get; init; } = DurationSpec.Seconds(3);
    [BinParameter("Candidate lifetime", "duration", .0000001)] public DurationSpec CandidateLifetime { get; init; } = DurationSpec.Minutes(5);
    [BinParameter("Maximum chase above pullback low", "%", .0000001, 100)] public decimal MaxChasePct { get; init; } = .8m;
}
public sealed record RangeBounceConfig
{
    [BinParameter("Enable pattern")] public bool Enabled { get; init; } = true;
    [BinParameter("Upper distance from box lower", "%", 0, 100)] public decimal ApproachPct { get; init; } = .3m;
    [BinParameter("Fresh rebound crossing", "%", .0000001, 100)] public decimal MinReboundPct { get; init; } = .25m;
    [BinParameter("Box lower invalidation", "%", 0, 100)] public decimal BreakTolerancePct { get; init; } = .3m;
    [BinParameter("Low stability with valid trades", "duration")] public DurationSpec LowStabilization { get; init; } = DurationSpec.Seconds(3);
    [BinParameter("Candidate lifetime", "duration", .0000001)] public DurationSpec CandidateLifetime { get; init; } = DurationSpec.Minutes(3);
    [BinParameter("Maximum chase above box lower", "%", .0000001, 100)] public decimal MaxChasePct { get; init; } = 1;
}
public sealed record EntryConfig
{
    public DowntrendReboundConfig DowntrendRebound { get; init; } = new();
    public BreakoutConfig UptrendBreakout { get; init; } = new();
    public PullbackConfig UptrendPullback { get; init; } = new();
    public RangeBounceConfig RangeLowerBounce { get; init; } = new();
    public BreakoutConfig RangeUpperBreakout { get; init; } = new();
    [BinParameter("Priority permutation; one entry winner per symbol/event")] public EntryPattern[] PatternPriority { get; init; } = [EntryPattern.RangeUpperBreakout, EntryPattern.UptrendPullback, EntryPattern.UptrendBreakout, EntryPattern.RangeLowerBounce, EntryPattern.DowntrendRebound];
}
public sealed record CommonExitConfig
{
    [BinParameter("Shared exit policy for all five patterns")] public ExitMode Mode { get; init; }
    [BinParameter("TP1 above actual average fill", "%", .0000001, 10000)] public decimal TakeProfit1Pct { get; init; } = 1;
    [BinParameter("TP1 fraction of confirmed initial entry quantity", "%", 0, 100)] public decimal TakeProfit1RatioPct { get; init; } = 30;
    [BinParameter("TP2 above actual average fill", "%", .0000001, 10000)] public decimal TakeProfit2Pct { get; init; } = 2;
    [BinParameter("TP2 fraction of confirmed initial entry quantity", "%", 0, 100)] public decimal TakeProfit2RatioPct { get; init; } = 30;
    [BinParameter("Trailing distance", "%", .0000001, 99.999)] public decimal TrailingStopPct { get; init; } = .7m;
    [BinParameter("Activation requires confirmed TP1 fill")] public TrailingActivation TrailingActivation { get; init; }
    [BinParameter("Peak basis after trailing activation")] public TrailingPeakBasis TrailingPeakBasis { get; init; }
    [BinParameter("Use common fixed stop")] public bool FixedStopEnabled { get; init; } = true;
    [BinParameter("Positive fixed stop distance", "%", .0000001, 99.999)] public decimal FixedStopLossPct { get; init; } = 1;
    [BinParameter("Use immutable pattern-specific technical anchor")] public bool TechnicalStopEnabled { get; init; } = true;
    [BinParameter("Technical anchor buffer", "%", 0, 99.999)] public decimal TechnicalBreakBufferPct { get; init; } = .3m;
    [BinParameter("Cost-aware break even after confirmed TP1")] public bool BreakEvenAfterTP1 { get; init; }
    [BinParameter("Protection evaluated on real trade ticks")] public SignalEvaluation ExitSignalEvaluation { get; init; }
    [BinParameter("Attempt liquidation with limits, never assume a close fill")] public bool ClosePositionsAtSessionEnd { get; init; } = true;
    [BinParameter("New entry cutoff before verified session close", "duration")] public DurationSpec StopNewEntriesBeforeClose { get; init; } = DurationSpec.Minutes(10);
    [BinParameter("Attempt liquidation before verified session close", "duration")] public DurationSpec StartLiquidationBeforeClose { get; init; } = DurationSpec.Minutes(5);
}
public sealed record CommonReentryConfig
{
    [BinParameter("Minimum time after full close confirmation", "duration")] public DurationSpec MinInterval { get; init; } = DurationSpec.Seconds(60);
    [BinParameter("Profit cooldown after full close", "duration")] public DurationSpec AfterProfit { get; init; } = DurationSpec.Seconds(60);
    [BinParameter("Loss/stop cooldown, strictly longer than profit", "duration", .0000001)] public DurationSpec AfterStop { get; init; } = DurationSpec.Minutes(5);
}
public sealed record CommonRiskConfig
{
    [BinParameter("Fixed single-position invariant", "positions", 1, 1)] public int MaxConcurrentPositionsPerSymbol { get; init; } = 1;
    [BinParameter("Fixed no-pyramiding invariant")] public bool AllowPyramiding { get; init; }
    [BinParameter("Confirmed daily entry positions per symbol", "entries", 1, 1000000)] public int MaxEntriesPerSymbolPerDay { get; init; } = 10;
    [BinParameter("Confirmed daily entry positions per family/account", "entries", 1, 1000000)] public int MaxEntriesPerFamilyPerDay { get; init; } = 50;
    [BinParameter("Confirmed daily account entries", "entries", 1, 1000000)] public int MaxEntriesPerAccountPerDay { get; init; } = 100;
    [BinParameter("Holdings plus first-entry reservations", "positions", 1, 100000)] public int MaxConcurrentPositionsPerAccount { get; init; } = 5;
    [BinParameter("Consecutive closed stops/losses", "stops", 1, 100000)] public int MaxConsecutiveStops { get; init; } = 3;
}
public sealed record QualityConfig
{
    [BinParameter("Trade exchange-time freshness", "duration", .0000001)] public DurationSpec MaxTradeAge { get; init; } = DurationSpec.Seconds(2);
    [BinParameter("Quote exchange-time freshness", "duration", .0000001)] public DurationSpec MaxQuoteAge { get; init; } = DurationSpec.Seconds(1);
    [BinParameter("Receive-to-process maximum lag", "duration", .0000001)] public DurationSpec MaximumProcessingLag { get; init; } = DurationSpec.Seconds(2);
    [BinParameter("Maximum gap of valid trades supporting hold/stability", "duration", .0000001)] public DurationSpec MaximumTradeGap { get; init; } = DurationSpec.Seconds(2);
    [BinParameter("Require continuous trade evidence")] public bool RequireContinuousTrades { get; init; } = true;
    [BinParameter("Require verified exchange timestamps")] public bool RequireVerifiedExchangeTime { get; init; } = true;
    [BinParameter("Require verified listing/halt/risk status")] public bool RequireTradableInstrument { get; init; } = true;
    [BinParameter("Explicit memory bound for warmup/indicators", "bars", 32, 1000000)] public int MaximumHistoryBars { get; init; } = 20000;
    [BinParameter("Explicit memory bound for observation ticks", "ticks", 32, 2000000)] public int MaximumHistoryTicks { get; init; } = 200000;
}
public sealed record CountryProfile
{
    [BinParameter("Maximum spread", "%", 0, 100)] public decimal MaxSpreadPct { get; init; } = .5m;
    [BinParameter("Required best bid quantity", "shares")] public decimal MinBestBidQuantity { get; init; } = 100;
    [BinParameter("Required best ask quantity", "shares")] public decimal MinBestAskQuantity { get; init; } = 100;
    [BinParameter("Optional minimum bid notional", "currency")] public decimal? MinBidNotional { get; init; }
    [BinParameter("Optional minimum ask notional", "currency")] public decimal? MinAskNotional { get; init; }
    [BinParameter("Minimum net reward after round-trip costs", "%", 0, 100)] public decimal MinEntryNetRewardPct { get; init; } = .2m;
    [BinParameter("Explicit max order cash; null prevents entries", "currency", .0000001)] public decimal? MaxOrderCash { get; init; }
    [BinParameter("Explicit exposure cap; null prevents entries", "currency", .0000001)] public decimal? MaxConcurrentExposure { get; init; }
    [BinParameter("Explicit daily loss cap; null prevents entries", "currency", .0000001)] public decimal? MaxDailyLoss { get; init; }
    [BinParameter("Explicit position risk budget; null prevents entries", "currency", .0000001)] public decimal? RiskBudgetPerPosition { get; init; }
    [BinParameter("Explicit planned investment; null prevents entries", "currency", .0000001)] public decimal? PlannedInvestment { get; init; }
    [BinParameter("Dated fee assumptions; empty prevents entries")] public FeeRate[] Fees { get; init; } = [];
}
public sealed record CountryProfiles { public CountryProfile KRX { get; init; } = new(); public CountryProfile US { get; init; } = new(); }
public sealed record MarketConfig
{
    [BinParameter("Supported exchanges, no automatic cross-currency aggregation")] public Exchange[] EnabledExchanges { get; init; } = [Exchange.KRX, Exchange.NASDAQ, Exchange.NYSE, Exchange.AMEX];
    [BinParameter("Extended sessions require explicit provider permissions even when false")] public bool RegularSessionOnly { get; init; } = true;
    public CountryProfiles CountryProfiles { get; init; } = new();
}
public sealed record ExecutionPolicy
{
    [BinParameter("Capped limit entry only")] public OrderKind EntryOrderKind { get; init; } = OrderKind.Limit;
    [BinParameter("Max entry slippage from signal trade", "%", 0, 100)] public decimal MaxEntrySlippagePct { get; init; } = .2m;
    [BinParameter("Cancel remainder after entry timeout", "duration", .0000001)] public DurationSpec EntryTimeout { get; init; } = DurationSpec.Seconds(3);
    [BinParameter("Limit exit, no implicit market conversion")] public OrderKind ExitOrderKind { get; init; } = OrderKind.Limit;
    [BinParameter("Lowest permitted exit quote", "%", 0, 99.999)] public decimal ExitLimitSlippagePct { get; init; } = .3m;
    [BinParameter("Cancel, confirm, then requote remaining exit quantity", "duration", .0000001)] public DurationSpec ProtectiveExitTimeout { get; init; } = DurationSpec.Seconds(2);
    [BinParameter("Maximum usable fraction of observed ask depth", "%", .0000001, 100)] public decimal MaxEntryQuoteParticipationPct { get; init; } = 25;
    [BinParameter("Explicit optional capacity reduction; default insufficient capacity BLOCKS")] public bool ReduceQuantityToQuoteCapacity { get; init; }
    [BinParameter("Fallback remains unsupported/disabled without separate authorization")] public bool EmergencyMarketFallback { get; init; }
    [BinParameter("Always false")] public bool AllowUnverifiedLiveExecution { get; init; }
    [BinParameter("Simulator submission latency", "duration")] public DurationSpec SubmissionLatency { get; init; } = DurationSpec.Seconds(.1m);
    [BinParameter("Simulator cancellation acknowledgement latency", "duration")] public DurationSpec CancellationLatency { get; init; } = DurationSpec.Seconds(.1m);
    [BinParameter("Conservative shared event liquidity participation", "%", .0000001, 100)] public decimal SimulationParticipationPct { get; init; } = 10;
}
public sealed record BinsStrategyConfig
{
    public int SchemaVersion { get; init; } = 1;
    public string StrategyId { get; init; } = "bins-strategy-v1";
    public string StrategyName { get; init; } = "Bin's Strategy";
    [BinParameter("Global entry enable; disabling never turns off held-position protection")] public bool Enabled { get; init; } = true;
    public MarketConfig Market { get; init; } = new();
    public SelectionConfig Selection { get; init; } = new();
    public RegimeConfig Regime { get; init; } = new();
    public StageOneConfig Stage1 { get; init; } = new();
    public EntryConfig Entries { get; init; } = new();
    public CommonExitConfig SharedExit { get; init; } = new();
    public CommonReentryConfig SharedReentry { get; init; } = new();
    public CommonRiskConfig SharedRisk { get; init; } = new();
    public QualityConfig Quality { get; init; } = new();
    public ExecutionPolicy Execution { get; init; } = new();
    [JsonIgnore] public string ConfigurationHash => BinsJson.Hash(BinsJson.Export(this));
    public CountryProfile Profile(Instrument instrument) => instrument.Exchange == Exchange.KRX ? Market.CountryProfiles.KRX : Market.CountryProfiles.US;
    public void Validate() => BinsValidation.Validate(this);
    public BinsStrategyConfig Snapshot() { Validate(); return BinsJson.Import(BinsJson.Export(this)); }
}
public static class BinsJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public static string Export(BinsStrategyConfig config) { config.Validate(); return JsonSerializer.Serialize(config, Options); }
    public static BinsStrategyConfig Import(string json)
    {
        try { using var document = JsonDocument.Parse(json, new() { MaxDepth = 64 }); Unique(document.RootElement); Require(document.RootElement, typeof(BinsStrategyConfig)); var c = JsonSerializer.Deserialize<BinsStrategyConfig>(json, Options) ?? throw new InvalidOperationException("Empty Bin configuration"); c.Validate(); return c; }
        catch (JsonException e) { throw new InvalidOperationException("Invalid Bin JSON at " + e.Path, e); }
    }
    private static void Unique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var p in node.EnumerateObject()) { if (!names.Add(p.Name)) throw new InvalidOperationException("Duplicate JSON field: " + p.Name); Unique(p.Value); } }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var x in node.EnumerateArray()) Unique(x);
    }
    private static void Require(JsonElement node, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is {} inner) { if (node.ValueKind != JsonValueKind.Null) Require(node, inner); return; }
        if (type.IsArray) { if (node.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Array required"); foreach (var e in node.EnumerateArray()) Require(e, type.GetElementType()!); return; }
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTimeOffset) || type == typeof(DateOnly)) return;
        if (node.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Configuration object required: " + type.Name);
        var properties = type.GetProperties().Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null).ToArray();
        var allowed = properties.Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var field in node.EnumerateObject()) if (!allowed.Contains(field.Name)) throw new InvalidOperationException("Unknown/read-only configuration field: " + field.Name);
        foreach (var p in properties)
        { var key = JsonNamingPolicy.CamelCase.ConvertName(p.Name); if (!node.TryGetProperty(key, out var v)) throw new InvalidOperationException("Missing configuration field: " + key); Require(v, p.PropertyType); }
    }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
public static class BinsValidation
{
    public static void Validate(BinsStrategyConfig c)
    {
        if (c.SchemaVersion != 1 || c.StrategyId != "bins-strategy-v1" || string.IsNullOrWhiteSpace(c.StrategyName) || c.StrategyName.Any(char.IsControl)) throw new InvalidOperationException("Bin schema/strategy ID/name mismatch");
        Visit(c, "config");
        if (c.Regime.ERRange >= c.Regime.ERTrend || c.Regime.RangeUpperQuantile <= c.Regime.RangeLowerQuantile || c.Regime.RangeMaxWidthPct <= c.Regime.RangeMinWidthPct) throw new InvalidOperationException("Invalid regime threshold combination");
        if (c.Regime.Timeframe.Elapsed is {} span && c.Regime.Lookback.Elapsed.Ticks / span.Ticks < c.Regime.MinimumBars) throw new InvalidOperationException("InsufficientHistory: lookback/timeframe provides fewer than MinimumRegimeBars; no automatic adjustment");
        if (c.SharedExit.TakeProfit2Pct <= c.SharedExit.TakeProfit1Pct || c.SharedExit.TakeProfit1RatioPct + c.SharedExit.TakeProfit2RatioPct > 100 || !c.SharedExit.FixedStopEnabled && !c.SharedExit.TechnicalStopEnabled) throw new InvalidOperationException("Invalid shared exit thresholds/protection");
        if (c.Entries.UptrendPullback.MaxPullbackPct < c.Entries.UptrendPullback.MinPullbackPct || c.SharedReentry.AfterStop.Elapsed <= c.SharedReentry.AfterProfit.Elapsed) throw new InvalidOperationException("Invalid correction range or stop/profit cooldown order");
        if (c.SharedRisk.AllowPyramiding || c.SharedRisk.MaxConcurrentPositionsPerSymbol != 1 || c.Execution.EntryOrderKind != OrderKind.Limit || c.Execution.ExitOrderKind != OrderKind.Limit || c.Execution.AllowUnverifiedLiveExecution || c.Execution.EmergencyMarketFallback) throw new InvalidOperationException("Single position, no pyramiding, capped limits, live/fallback lock are fixed policies");
        if (c.Entries.PatternPriority.Length != Enum.GetValues<EntryPattern>().Length || c.Entries.PatternPriority.Distinct().Count() != c.Entries.PatternPriority.Length) throw new InvalidOperationException("PatternPriority must be a permutation of five patterns");
        if (c.Market.EnabledExchanges.Length == 0 || c.Market.EnabledExchanges.Distinct().Count() != c.Market.EnabledExchanges.Length) throw new InvalidOperationException("Enabled exchanges must be unique/nonempty");
        if (c.Regime.AtrPeriod + 1 > c.Quality.MaximumHistoryBars || c.Entries.UptrendPullback.Enabled && c.Entries.UptrendPullback.EmaPeriod > c.Quality.MaximumHistoryBars || c.Stage1.Enabled && (c.Stage1.MovingAveragePeriod > c.Quality.MaximumHistoryBars || c.Stage1.ProfileLookbackBars > c.Quality.MaximumHistoryBars)) throw new InvalidOperationException("History capacity cannot satisfy configured indicator warmup");
        var windows = new List<DurationSpec> { c.Regime.Lookback }; if (c.Entries.UptrendBreakout.Enabled) windows.Add(c.Entries.UptrendBreakout.Lookback); if (c.Entries.UptrendPullback.Enabled) windows.Add(c.Entries.UptrendPullback.ImpulseLookback);
        if (c.Regime.Timeframe.Elapsed is {} interval && windows.Any(d => decimal.Ceiling((decimal)d.Elapsed.Ticks / interval.Ticks) > c.Quality.MaximumHistoryBars)) throw new InvalidOperationException("Explicit history capacity cannot cover configured observation windows");
        foreach (var p in new[] { c.Market.CountryProfiles.KRX, c.Market.CountryProfiles.US })
        { foreach (var f in p.Fees) f.Validate(); var sorted = p.Fees.OrderBy(f => f.From).ToArray(); if (sorted.Zip(sorted.Skip(1)).Any(pair => pair.First.Until > pair.Second.From)) throw new InvalidOperationException("Fee date ranges overlap"); }
    }
    private static void Visit(object? obj, string path)
    {
        if (obj == null) throw new InvalidOperationException("Missing configuration object: " + path);
        if (obj is DurationSpec d) { _ = d.Elapsed; return; }
        if (obj is BarTimeframeSpec b) { b.Validate(); return; }
        foreach (var p in obj.GetType().GetProperties().Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null))
        {
            var v = p.GetValue(obj); var a = p.GetCustomAttribute<BinParameterAttribute>();
            if (v == null) { if (Nullable.GetUnderlyingType(p.PropertyType) == null) throw new InvalidOperationException("Missing " + path + "." + p.Name); continue; }
            if (v is decimal or int) { if (a != null) { var n = Convert.ToDecimal(v, CultureInfo.InvariantCulture); if (n < a.Minimum || n > a.Maximum) throw new InvalidOperationException(path + "." + p.Name + " outside metadata range"); } }
            else if (v is DurationSpec duration) { if (a != null && (decimal)duration.Elapsed.Ticks / TimeSpan.TicksPerSecond < a.Minimum) throw new InvalidOperationException("Invalid duration: " + path + "." + p.Name); }
            else if (v.GetType().IsEnum) { if (!Enum.IsDefined(v.GetType(), v)) throw new InvalidOperationException("Unknown enum: " + path + "." + p.Name); }
            else if (v is Array array) { foreach (var element in array) { if (element == null) throw new InvalidOperationException("Null array item"); if (element.GetType().IsEnum) { if (!Enum.IsDefined(element.GetType(), element)) throw new InvalidOperationException("Unknown enum item"); } else Visit(element, path + "." + p.Name); } }
            else if (v is not (string or bool or DateTimeOffset or DateOnly)) Visit(v, path + "." + p.Name);
        }
    }
    public static ParameterMetadata[] Metadata()
    {
        var result = new List<ParameterMetadata>();
        void Walk(object obj, string prefix)
        {
            foreach (var p in obj.GetType().GetProperties().Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null))
            {
                var v = p.GetValue(obj); var a = p.GetCustomAttribute<BinParameterAttribute>(); var key = prefix + JsonNamingPolicy.CamelCase.ConvertName(p.Name);
                var numeric = p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int) || Nullable.GetUnderlyingType(p.PropertyType) == typeof(decimal);
                if (a != null) result.Add(new(key, a.Description, a.Unit, p.PropertyType.Name, v, numeric ? a.Minimum : null, numeric ? a.Maximum : null));
                if (p.PropertyType == typeof(FeeRate[]))
                    foreach (var field in typeof(FeeRate).GetProperties())
                        if (field.GetCustomAttribute<BinParameterAttribute>() is {} fee) result.Add(new(key + "[]." + JsonNamingPolicy.CamelCase.ConvertName(field.Name), fee.Description, fee.Unit, field.PropertyType.Name, null, fee.Minimum, fee.Maximum));
                if (v != null && v is not (string or bool or decimal or int or DurationSpec or BarTimeframeSpec or Array) && !v.GetType().IsEnum) Walk(v, key + ".");
            }
        }
        Walk(new BinsStrategyConfig(), ""); return result.ToArray();
    }
}
