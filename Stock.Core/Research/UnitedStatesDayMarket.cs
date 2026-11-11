using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using Stock.Core.Trading;

namespace Stock.Core.Research;

public sealed record DayMarketBatch(IReadOnlyDictionary<string, CurrentMarketSnapshot> Quotes, IReadOnlySet<string> OpenCodes, IReadOnlySet<string> UnknownCodes);

/// <summary>Ported from the user-owned StockViewer: read-only public US daytime quotes with a fresh anonymous visitor session.</summary>
public sealed class UnitedStatesDayMarket : IDisposable
{
    private const string Api = "https://wts-api.tossinvest.com";
    private const string Info = "https://wts-info-api.tossinvest.com";
    private readonly HttpClient http;
    private readonly CookieContainer cookies = new();
    private readonly string tabId = Guid.NewGuid().ToString();
    private readonly TimeProvider clock;
    private readonly ConcurrentDictionary<string, string> symbols = new();
    private readonly ConcurrentDictionary<string, StockItem> stocks = new();
    private readonly Dictionary<string, Product> products = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private JsonElement calendar;
    private DateTimeOffset calendarExpires, blockedUntil;
    private bool initialized;
    private sealed record Product(string? Code, bool Supported, DateTimeOffset Expires);

    public UnitedStatesDayMarket(HttpMessageHandler? handler = null, TimeProvider? clock = null)
    {
        this.clock = clock ?? TimeProvider.System;
        cookies.Add(new Cookie("deviceId", "WTS-" + Guid.NewGuid().ToString("N"), "/", ".tossinvest.com") { Secure = true });
        http = new(handler ?? new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies })
        { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("StockResearch/2.0");
        http.DefaultRequestHeaders.Add("browser-tab-id", tabId);
        http.DefaultRequestHeaders.CacheControl = new() { NoCache = true };
    }

    public void Register(StockItem stock)
    {
        if (stock.Exchange != Exchange.KRX && !string.IsNullOrWhiteSpace(stock.Symbol))
            {symbols[stock.Key] = stock.Symbol;stocks[stock.Key]=stock;}
    }

    private async Task<JsonElement> SendAsync(string url, CancellationToken cancellation, string? body = null)
    {
        if (clock.GetUtcNow() < blockedUntil) throw new IOException("데이마켓 요청 제한 · 재시도 대기");
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, url);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            if (cookies.GetCookies(new Uri(Info))["XSRF-TOKEN"] is { } csrf)
                request.Headers.Add("X-XSRF-TOKEN", csrf.Value);
        }
        using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || !response.IsSuccessStatusCode && response.Headers.RetryAfter is not null)
        {
            var delay = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - clock.GetUtcNow() ?? TimeSpan.FromSeconds(30);
            if (delay <= TimeSpan.Zero) delay = TimeSpan.FromSeconds(30);
            blockedUntil = clock.GetUtcNow() + delay;
            throw new IOException("데이마켓 요청 제한 · 재시도 대기");
        }
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest) initialized = false;
            throw new IOException($"데이마켓 연결 실패 (HTTP {(int)response.StatusCode})");
        }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("result", out var result))
            throw new InvalidDataException("데이마켓 응답 형식 오류");
        return result.Clone();
    }

    private async Task InitializeAsync(CancellationToken cancellation)
    {
        if (initialized) return;
        // This is the website's public visitor initialization. No existing browser
        // cookies, account IDs, login tickets or authentication APIs are used.
        await SendAsync(Api + "/api/v3/init?tabId=" + tabId, cancellation).ConfigureAwait(false);
        initialized = true;
    }

    public static bool IsDayMarketOpen(JsonElement usCalendar, DateTimeOffset now)
    {
        if (usCalendar.ValueKind != JsonValueKind.Object) throw new InvalidDataException("데이마켓 거래일 형식 오류");
        var valid = false;
        foreach (var key in new[] { "prevBizDay", "today", "nextBizDay" })
        {
            if (!usCalendar.TryGetProperty(key, out var day) || day.ValueKind != JsonValueKind.Object) continue;
            if (!DateOnly.TryParseExact(PublicMarketData.Text(day, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) continue;
            valid = true;
            var date = PublicMarketData.Text(day, "date");
            if (DateTimeOffset.TryParse(date + "T" + PublicMarketData.Text(day, "dayMarketStart") + "+09:00", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
                && DateTimeOffset.TryParse(date + "T" + PublicMarketData.Text(day, "dayMarketEnd") + "+09:00", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
                && now >= start && now < end) return true;
        }
        if (!valid) throw new InvalidDataException("데이마켓 거래일 정보가 없습니다.");
        return false;
    }

    private static string NormalizeSymbol(string symbol) => symbol.Trim().Replace(' ', '.').ToUpperInvariant();
    public static string? FindProduct(JsonElement sections, string symbol)
    {
        if (sections.ValueKind != JsonValueKind.Array) throw new InvalidDataException("데이마켓 종목 검색 형식 오류");
        foreach (var section in sections.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object || !section.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) continue;
            foreach (var item in items.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object && NormalizeSymbol(PublicMarketData.Text(item, "symbol")) == NormalizeSymbol(symbol)
                    && PublicMarketData.Text(item, "market") is "NSQ" or "NYS" or "NYSE" or "AMX")
                {
                    var product = PublicMarketData.Text(item, "productCode");
                    if (product.Length is > 0 and <= 40 && product.All(char.IsAsciiLetterOrDigit)) return product;
                }
        }
        return null;
    }

    private async Task<Product> ResolveAsync(string symbol, CancellationToken cancellation)
    {
        if (products.TryGetValue(symbol, out var cached) && cached.Expires > clock.GetUtcNow()) return cached;
        var query = JsonSerializer.Serialize(new { query = symbol, sections = new[] { new { type = "PRODUCT" } } });
        var results = await SendAsync(Info + "/api/v3/search-all/wts-auto-complete", cancellation, query).ConfigureAwait(false);
        var productCode = FindProduct(results, symbol);
        var supported = false;
        if (productCode is not null)
        {
            var info = await SendAsync(Info + "/api/v2/stock-infos/" + productCode, cancellation).ConfigureAwait(false);
            if (info.ValueKind != JsonValueKind.Object || PublicMarketData.Text(info, "code") != productCode || NormalizeSymbol(PublicMarketData.Text(info, "symbol")) != NormalizeSymbol(symbol)
                || PublicMarketData.Text(info, "currency") != "USD") throw new InvalidDataException("데이마켓 종목 식별자가 일치하지 않습니다.");
            supported = info.TryGetProperty("daytimePriceSupported", out var daytime) && daytime.ValueKind == JsonValueKind.True;
        }
        return products[symbol] = new(productCode, supported, clock.GetUtcNow() + TimeSpan.FromMinutes(10));
    }

    public static CurrentMarketSnapshot[] ParsePrices(JsonElement prices, IReadOnlyDictionary<string, StockItem> productToCode, DateTimeOffset receivedAt)
    {
        if (prices.ValueKind != JsonValueKind.Object || !prices.TryGetProperty("prices", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("데이마켓 가격 형식 오류");
        var result = new List<CurrentMarketSnapshot>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || !productToCode.TryGetValue(PublicMarketData.Text(row, "productCode"), out var stock)
                || PublicMarketData.Text(row, "marketCurrency") != "USD" || PublicMarketData.Text(row, "session") != "DAY_PRE_MAIN") continue;
            try
            {
                var price = ReadNumber(row, "close"); var basis = ReadNumber(row, "base");
                if (price <= 0 || basis <= 0 || !DateTimeOffset.TryParse(PublicMarketData.Text(row, "dt"), CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var sourceTime) || sourceTime > receivedAt.AddMinutes(1)) continue;
                result.Add(new(stock,new(stock.Name,price,price-basis,(price/basis-1)*100,sourceTime,"OPEN"),receivedAt,
                    null,null,null,null,null,"토스증권 공개 시세 · 미국 데이마켓 · OHLCV 미제공",TimeSpan.FromSeconds(1)));
            }
            catch (Exception e) when (e is InvalidDataException or OverflowException) { }
        }
        return result.ToArray();
    }

    public async Task<DayMarketBatch> GetQuotesAsync(IEnumerable<string> codes, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            await InitializeAsync(cancellation).ConfigureAwait(false);
            if (calendar.ValueKind != JsonValueKind.Object || clock.GetUtcNow() >= calendarExpires)
            {
                var hours = await SendAsync(Api + "/api/v2/system/trading-hours/integrated", cancellation).ConfigureAwait(false);
                if (hours.ValueKind != JsonValueKind.Object || !hours.TryGetProperty("us", out calendar))
                    throw new InvalidDataException("데이마켓 거래일 정보가 없습니다.");
                IsDayMarketOpen(calendar, clock.GetUtcNow());
                calendarExpires = clock.GetUtcNow() + TimeSpan.FromMinutes(1);
            }
            var quotes = new Dictionary<string, CurrentMarketSnapshot>(); var open = new HashSet<string>(); var unknown = new HashSet<string>();
            if (!IsDayMarketOpen(calendar, clock.GetUtcNow())) return new(quotes, open, unknown);
            var mapping = new Dictionary<string, StockItem>();
            foreach (var code in codes.Distinct())
            {
                if (!symbols.TryGetValue(code, out var symbol)) { unknown.Add(code); continue; }
                try
                {
                    var product = await ResolveAsync(symbol, cancellation).ConfigureAwait(false);
                    if (product.Code is null) { unknown.Add(code); continue; }
                    if (!product.Supported) { unknown.Add(code); continue; }
                    open.Add(code); mapping[product.Code] = stocks[code];
                }
                catch (Exception e) when (!cancellation.IsCancellationRequested
                    && e is IOException or InvalidDataException or HttpRequestException or JsonException or OperationCanceledException)
                {
                    unknown.Add(code);
                    products[symbol] = new(null, false, clock.GetUtcNow() + TimeSpan.FromSeconds(5));
                }
            }
            if (mapping.Count > 0)
            {
                var frame = await SendAsync(Info + "/api/v5/stock-prices?productCodes=" + string.Join(',', mapping.Keys)
                    + "&localizedCurrency=KRW", cancellation).ConfigureAwait(false);
                foreach (var quote in ParsePrices(frame, mapping, clock.GetUtcNow())) quotes[quote.Stock.Key] = quote;
            }
            return new(quotes, open, unknown);
        }
        finally { gate.Release(); }
    }

    private static decimal ReadNumber(JsonElement row,string key)=>decimal.TryParse(PublicMarketData.Text(row,key),NumberStyles.Number,CultureInfo.InvariantCulture,out var value)?value:throw new InvalidDataException("데이마켓 숫자 형식 오류");
    public void Dispose() => http.Dispose();
}
