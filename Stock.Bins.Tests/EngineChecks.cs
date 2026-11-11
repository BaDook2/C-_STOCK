using System.Text.Json;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void EngineAndReplayChecks()
    {
        var fixture = EngineFixture(); var engine = fixture.Engine; var account = fixture.Account;
        StartBreakout(engine);
        Check("INTEGRATION", account.Orders is [{ Buy: true, Kind: OrderKind.Limit }] && account.Fills.Count == 0, "Completed-bar up classification leads to one capped limit intent on the held breakout tick");
        Check("A02", engine.Decisions.Single(d => d.Kind == "EntrySignal").Time == Epoch.AddSeconds(843), "Tick entry occurs at14:03, before next classification bar close");
        Push(engine, Input(844, 102.82m, true)); Push(engine, Input(844, 102.82m));
        Check("C12", account.Positions[Us.Key].Quantity == 9 && account.Fills.Count == 1 && engine.Snapshot(Us.Key).Quantity == 9, "Confirmed next-event fill creates one position; entry signal alone did not");
        for (int n = 0; n < 3; n++) { Push(engine, Input(845 + n, 102.84m, true)); Push(engine, Input(845 + n, 102.84m)); }
        Check("C12", account.Orders.Count(o => o.Buy) == 1, "Repeated valid entry prices while holding create no pyramiding orders");
        var frozenHash = engine.Snapshot(Us.Key).ConfigurationHash;
        var updated = fixture.Config with { SharedExit = fixture.Config.SharedExit with { FixedStopLossPct = 20 } };
        engine.UpdateConfiguration(updated, Epoch.AddSeconds(847));
        Check("CONFIG", engine.Snapshot(Us.Key).ConfigurationHash == frozenHash, "Held position and pending row retain old configuration after edit");
        Push(engine, Input(848, 101.7m, true)); Push(engine, Input(848, 101.7m));
        Check("D06", account.Orders.Count(o => !o.Buy) == 1 && engine.Decisions.Any(d => d.Kind == "ExitSignal" && d.Exit!.Protective), "Original1% stop remains active despite new20% input and generates one protection intent");
        Check("D05", account.Positions[Us.Key].Quantity == 9 && account.Orders.Last().Filled == 0, "Stop signal cannot retroactively fill its triggering tick");
        Push(engine, Input(849, 101.7m, true)); engine.OnClock(Epoch.AddSeconds(849));
        Check("D09", account.Positions[Us.Key].Quantity == 0 && engine.ClosedTrades.Length == 1 && engine.Snapshot(Us.Key).CooldownUntil >= Epoch.AddSeconds(849).AddMinutes(5), "Full confirmed stop close begins shared five-minute cooldown");
        Push(engine, Input(850, 102.8m, true)); Push(engine, Input(850, 102.8m));
        Check("D08", account.Orders.Count(o => o.Buy) == 1 && engine.Snapshot(Us.Key).ConfigurationHash == updated.ConfigurationHash, "No reentry from unchanged true condition; new config applies only once flat");
        engine.EmergencyStop(Epoch.AddSeconds(851));
        Check("E10", account.Orders.All(o => o.Kind == OrderKind.Limit) && !account.IsLive, "User stop never enables market fallback or a live sender");
        var gap = EngineFixture(); Push(gap.Engine, Input(840, 102.6m, true)); Push(gap.Engine, Input(840, 102.6m)); Push(gap.Engine, Input(841, 102.78m, true)); Push(gap.Engine, Input(841, 102.78m));
        gap.Engine.OnClock(Epoch.AddSeconds(850)); Push(gap.Engine, Input(850, 102.8m, true)); Push(gap.Engine, Input(850, 102.8m));
        Check("C03", gap.Account.Orders.Count == 0, "Clock-only elapsed time and later gap cannot complete a breakout hold");
        var quoteOnly = EngineFixture(); Push(quoteOnly.Engine, Input(840, 102.6m, true)); Push(quoteOnly.Engine, Input(841, 102.8m, true)); quoteOnly.Engine.OnClock(Epoch.AddSeconds(845));
        Check("C03", quoteOnly.Account.Orders.Count == 0, "Quote updates alone do not produce rebound/breakout signals");
        var stageConfig = Config() with { Stage1 = new() { Enabled = true } }; var stageBlocked = EngineFixture(stageConfig); StartBreakout(stageBlocked.Engine);
        Check("B02", stageBlocked.Account.Orders.Count == 0 && stageBlocked.Engine.Snapshot(Us.Key).Status == "Stage1NotActive", "Full engine cannot bypass Stage1 missing MA240/profile");
        var timeout = EngineFixture(); StartBreakout(timeout.Engine); timeout.Engine.OnClock(Epoch.AddSeconds(846));
        Check("E03", timeout.Account.Orders[0].State == OrderState.CancelRequested && timeout.Account.Fills.Count == 0, "Entry timeout requests cancellation without inventing fill");
        timeout.Engine.OnClock(Epoch.AddSeconds(846.2));
        Check("E03", timeout.Account.Orders[0].State == OrderState.Cancelled, "Later explicit paper acknowledgement resolves timeout remainder");
        var end = EngineFixture(); StartBreakout(end.Engine); Push(end.Engine, Input(844, 102.82m, true)); end.Engine.OnClock(Session().Close.AddMinutes(-.25)); end.Engine.OnClock(Session().Close);
        Check("D11", end.Engine.Snapshot(Us.Key).AwaitReconciliation && end.Account.Positions[Us.Key].Quantity > 0 && end.Engine.Decisions.Any(d => d.Kind == "UnclosedPosition"), "No fresh close quote leaves explicit unclosed holding/reconciliation, never a fictional closing trade");
        var safety = EngineFixture(); StartBreakout(safety.Engine); Push(safety.Engine, Input(844, 102.82m, true)); safety.Engine.SetEntriesEnabled(false, Epoch.AddSeconds(844)); Push(safety.Engine, Input(845, 101.7m, true)); Push(safety.Engine, Input(845, 101.7m));
        Check("CONFIG", safety.Account.Orders.Any(o => !o.Buy), "Disabling entries immediately leaves existing protective exit logic active");
        var events = BreakoutEvents().ToArray();
        var request = new BinsReplayRequest(Config(), [Us], events, TrendBars(), [], Epoch.AddSeconds(840), Epoch.AddSeconds(850), new Dictionary<string, decimal> { ["USD"] = 10000 }, "SYNTHETIC_ORIGINAL_EVENT_FIXTURE", ReplayPrecision.OriginalTicks, true, true, DurationSpec.Seconds(.25m), true);
        var replay = new BinsReplayEngine(); var first = replay.Run(request); var second = replay.Run(request);
        Check("E09", first.AuditHash == second.AuditHash && first.StateHash == second.StateHash && first.DataHash == second.DataHash && first.Orders.Length == 1, "Identical replay twice gives identical decisions/state/audit hashes and one order");
        Check("REPLAY", first.Decisions.Where(d => d.Kind == "EntrySignal").Select(d => (d.Time, d.Entry!.EntryPattern)).SequenceEqual(safety.Engine.Decisions.Where(d => d.Kind == "EntrySignal").Select(d => (d.Time, d.Entry!.EntryPattern))), "Replay and direct paper stream share exact entry decision time and pattern");
        Check("REPLAY", first.Currencies.Single().OpenPositions == 1 && first.Currencies.Single().ClosedTrades == 0 && first.ClosedTrades.Length == 0, "Open positions are not counted as completed or winning trades");
        Throws("E08", () => replay.Run(request with { Precision = ReplayPrecision.CompletedBarsOnly }), "OHLC-only input returns explicit UnsupportedPrecision instead of tick profitability");
        Throws("E08", () => replay.Run(request with { OriginalExchangeTimes = false }), "Missing original exchange times cannot be claimed precise replay");
        var both = replay.Run(request with { Instruments = [Us, Kr], InitialCash = new Dictionary<string, decimal> { ["USD"] = 10000, ["KRW"] = 1000000 } });
        Check("E06", both.Currencies.Length == 2 && both.Currencies.Single(p => p.Currency == "KRW").FinalEquity == 1000000, "US activity cannot borrow KRW cash; reports remain separate without invented FX");
        File.WriteAllText(Path.Combine(output, "replay-result.json"), JsonSerializer.Serialize(first, BinsJson.Options));
    }
    private static IEnumerable<BinsMarketInput> BreakoutEvents()
    {
        foreach (var (second, price) in new[] { (840, 102.6m), (841, 102.78m), (842, 102.79m), (843, 102.8m), (844, 102.82m), (845, 102.84m), (846, 102.84m), (847, 102.84m) }) { yield return Input(second, price, true); yield return Input(second, price); }
    }
}
