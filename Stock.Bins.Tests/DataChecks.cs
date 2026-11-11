using System.Text.Json;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void ConfigurationChecks()
    {
        var c = new BinsStrategyConfig(); var json = BinsJson.Export(c); var clone = BinsJson.Import(json);
        Check("CONFIG", json == BinsJson.Export(clone) && c.ConfigurationHash == clone.ConfigurationHash, "All default config groups including every duration unit roundtrip with stable hash");
        Check("DECISIONS", Enum.GetValues<StrategyFamily>().Length == 3 && Enum.GetValues<EntryPattern>().Length == 5 && Enum.GetValues<MarketRegime>().Length == 4 && Enum.GetValues<DowntrendEntryMode>().Length == 2, "Exactly 3 families/5 patterns/4 regimes/2 downtrend modes");
        Check("TIME", new DurationSpec(1, DurationUnit.Day).Elapsed == TimeSpan.FromHours(24) && new DurationSpec(1, DurationUnit.Hour).Elapsed == new DurationSpec(60, DurationUnit.Minute).Elapsed, "Duration Day is 24 hours; equivalent units are exact");
        Throws("TIME", () => _ = new DurationSpec(-1, DurationUnit.Second).Elapsed, "Negative durations rejected");
        Throws("TIME", () => _ = new DurationSpec(.00000001m, DurationUnit.Second).Elapsed, "Sub-clock precision rejected instead of rounded");
        Throws("JSON", () => BinsJson.Import(json.Replace("bins-strategy-v1", "inje")), "Other strategy IDs cannot execute as Bin");
        Throws("JSON", () => BinsJson.Import(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")), "Duplicate JSON keys rejected");
        Throws("JSON", () => BinsJson.Import(json.Replace("\"enabled\": true,", "\"misspelled\": true,")), "Unknown/missing configuration fields rejected");
        Throws("JSON", () => BinsJson.Import(json.Replace("\"autoSwitchAfterExit\": true", "\"autoSwitchAfterExit\": 2")), "Automatic switching is a boolean, not auto numeric tuning");
        Throws("JSON", () => BinsJson.Import(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"configurationHash\": \"forged\"")), "Read-only computed hash cannot be smuggled into strict input as an ignored field");
        Throws("JSON", () => BinsJson.Import(json.Replace("\"unit\": \"Minute\"", "\"unit\": \"Minute\", \"elapsed\": 999")), "Computed elapsed duration cannot override or be silently ignored beside value/unit");
        Throws("A03", () => (c with { Regime = c.Regime with { Timeframe = new(15, BarUnit.Minute) } }).Validate(), "60-minute /15-minute /minimum8 cannot silently become 120 minutes");
        Throws("CONFIG", () => (c with { SharedExit = c.SharedExit with { TakeProfit2Pct = .5m } }).Validate(), "TP2 must exceed TP1");
        Throws("CONFIG", () => (c with { SharedExit = c.SharedExit with { TakeProfit1RatioPct = 80 } }).Validate(), "TP ratio sum cannot exceed 100");
        Throws("CONFIG", () => (c with { SharedReentry = c.SharedReentry with { AfterStop = DurationSpec.Seconds(60) } }).Validate(), "Stop cooldown strictly exceeds profit cooldown");
        Throws("CONFIG", () => (c with { SharedRisk = c.SharedRisk with { AllowPyramiding = true } }).Validate(), "Pyramiding cannot be enabled");
        Throws("E10", () => (c with { Execution = c.Execution with { AllowUnverifiedLiveExecution = true } }).Validate(), "Unverified live execution cannot be enabled");
        Check("META", BinsValidation.Metadata().Length > 140 && BinsValidation.Metadata().Any(m => m.Path == "stage1.activationDuration" && m.Unit == "duration"), "Parameter descriptions/units/defaults are sourced from configuration types");
        File.WriteAllText(Path.Combine(output, "default-config.json"), json);
    }
    private static void DataAndRegimeChecks()
    {
        var c = Config(); var at = Epoch.AddMinutes(15); var history = new BinsHistory(Us, c); var classifier = new MarketRegimeClassifier(c.Regime, c.Quality, c.ConfigurationHash);
        var future = Bar(50, 999); var incomplete = Bar(0, 100) with { IsComplete = false };
        Check("A01", !history.AddBar(future, at) && !history.AddBar(incomplete, at) && !classifier.OnBar(future, at) && !classifier.OnBar(incomplete, at), "Future/incomplete bars cannot enter history, classifier or previous-high references");
        foreach (var b in TrendBars().Take(8)) classifier.OnBar(b, b.KnownAt);
        Check("A04", classifier.Snapshot.Regime == MarketRegime.Unknown && classifier.Snapshot.CandidateConsecutiveBars == 1, "First up candidate does not commit a family");
        classifier.OnBar(TrendBars()[8], TrendBars()[8].KnownAt);
        Check("A04", classifier.Snapshot.Regime == MarketRegime.Uptrend && classifier.Snapshot.EntryAllowed, "Up structure + ER + directional move commits after second completed bar");
        Check("A08", !classifier.OnBar(TrendBars()[8], at) && classifier.Snapshot.CandidateConsecutiveBars == 2, "Repeated same completed bar cannot advance confirmation");
        var down = new MarketRegimeClassifier(c.Regime, c.Quality, c.ConfigurationHash);
        foreach (var b in TrendBars(false)) down.OnBar(b, b.KnownAt);
        Check("A05", down.Snapshot.Regime == MarketRegime.Downtrend && down.Snapshot.EntryAllowed, "Decreasing completed highs/lows and net direction commit Downtrend");
        var rc = c.Regime with { RangeTouchSeparation = DurationSpec.Minutes(1) };
        var range = new MarketRegimeClassifier(rc, c.Quality, c.ConfigurationHash);
        foreach (var b in RangeBars()) range.OnBar(b, b.KnownAt);
        Check("A06", range.Snapshot is { Regime: MarketRegime.Range, EntryAllowed: true, Box.UpperReactions: >= 2, Box.LowerReactions: >= 2 }, "Independent alternating side reactions + low ER + width/inside ratio form a frozen box");
        var originalBox = range.Snapshot.Box;
        range.OnBar(Bar(18, 100.6m, 101.05m, 100), Epoch.AddMinutes(19));
        Check("A06", range.Snapshot.Box!.Upper == originalBox!.Upper && range.Snapshot.Box.Lower == originalBox.Lower, "Later completed bars cannot trail a frozen live breakout boundary");
        var flat = new MarketRegimeClassifier(rc, c.Quality, c.ConfigurationHash);
        foreach (var n in Enumerable.Range(0, 18)) flat.OnBar(Bar(n, 100), Epoch.AddMinutes(n + 1));
        Check("A07", flat.Snapshot.Regime == MarketRegime.Unknown, "Low ER without independent repeated reactions is not a box");
        classifier.Invalidate("MissingData", at);
        Check("A08", classifier.Snapshot.Regime == MarketRegime.Uptrend && !classifier.Snapshot.EntryAllowed && classifier.Snapshot.CandidateConsecutiveBars == 0, "Old committed label does not authorize entries through Unknown/uncertain data");
        var builds = new List<int>();
        foreach (var minutes in new[] { 1, 3, 5, 15 })
        {
            var builder = new BinsBarBuilder(Us, new(minutes, BarUnit.Minute)); var count = 0;
            for (int n = 0; n <= 60; n++) if (builder.Add(Input(n * 60, 100 + n), true) != null) count++;
            builds.Add(count);
        }
        Check("A02", builds.SequenceEqual(new[] { 60, 20, 12, 4 }), "1/3/5/15 minute choices produce distinct true completed bars with no gap filling");
        var cb = new BinsBarBuilder(Us, new(1, BarUnit.Minute)); cb.Add(Input(0, 100), true); var completed = cb.Add(Input(60, 999), true);
        Check("A01", completed is { Close: 100, High: 100 } && cb.Active!.Close == 999, "Tick closing old bucket belongs only to the next bucket");
        Check("A10", BinsMath.EfficiencyRatio(new[] { 100m, 102m, 101m, 103m }) == .6m && BinsMath.EfficiencyRatio(new[] { 1m, 1m }) == 0, "ER equals |103-100|/(2+1+2)=0.6; zero path is zero");
        Check("A10", BinsMath.Ema(new[] { 1m, 2m, 3m, 4m }, 3) == 3m, "EMA3 seeds SMA(1,2,3)=2 then alpha0.5 gives3");
        var atrBars = new[] { Bar(0, 10, 11, 9), Bar(1, 12, 13, 11), Bar(2, 13, 14, 12), Bar(3, 12, 13, 11) };
        Check("A10", BinsMath.WilderAtr(atrBars, 2) == 2.25m && BinsMath.Quantile(new[] { 0m, 10m, 20m, 30m }, .25m) == 7.5m, "Wilder ATR2: seed(3+2)/2=2.5 then(2.5+2)/2=2.25; linear quantile verified");
        var gate = new BinsCausalInput(Us, c.Quality); var first = Input(1);
        Check("A09", gate.Observe(first, first.KnownAt).Accepted && gate.Observe(first, first.KnownAt).Duplicate, "Duplicate admission is explicit and cannot create a new event");
        var reversed = Input(.5m); Check("A09", !gate.Observe(reversed, reversed.KnownAt).Accepted, "Time reversal rejected");
        Check("A09", !new BinsCausalInput(Us, c.Quality).Observe(first with { Session = first.Session! with { ProviderConfirmed = false } }, first.KnownAt).Accepted, "Unconfirmed/closed market session rejected");
        Check("A09", new BinsCausalInput(Us, c.Quality).Observe(first with { ContinuityVerified = false }, first.KnownAt).Discontinuity, "Unverified sequence continuity is not silently treated as continuous");
        var unverified = first with { Event = first.Event with { TimeQuality = "local" } };
        Check("A09", !new BinsCausalInput(Us, c.Quality).Observe(unverified, unverified.KnownAt).Accepted, "Unverified exchange time rejected by default");
        var winter = MarketCalendars.FromLocal(Exchange.NASDAQ, new(2026, 12, 1), new(9, 30), new(16, 0));
        var summer = MarketCalendars.FromLocal(Exchange.NASDAQ, new(2026, 7, 1), new(9, 30), new(16, 0));
        Check("E07", winter.Open.Offset == TimeSpan.FromHours(-5) && summer.Open.Offset == TimeSpan.FromHours(-4) && MarketCalendars.Timezone(Exchange.KRX).GetUtcOffset(Epoch) == TimeSpan.FromHours(9), "US DST and KR exchange timezone are independent");
        var bands = Rule() with { Bands = [new(0, 1, .0001m), new(1, null, .01m)] };
        Check("E07", bands.Floor(.99999m) == .9999m && bands.Ceiling(.99999m) == 1 && bands.Floor(1.009m) == 1, "Provided variable price bands round across sub-dollar boundary without guessed tick rules");
    }
    private static CompletedBar[] RangeBars() => Enumerable.Range(0, 18).Select(n => Bar(n, n % 2 == 0 ? 100.6m : 99.4m, n % 2 == 0 ? 101 : 100, n % 2 == 0 ? 100 : 99)).ToArray();
}
