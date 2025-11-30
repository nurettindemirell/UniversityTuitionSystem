using System.Collections.Concurrent;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// University.Api'nin adresi (Swagger da burada)
builder.Services.AddHttpClient("backend", client =>
{
    client.BaseAddress = new Uri("http://localhost:5099");
});

// CORS: Swagger (5099) -> Gateway (5207) çağırabilsin
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins("http://localhost:5207")   // Swagger'ın origin'i
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

// CORS middleware (preflight OPTIONS burada handle edilecek)
app.UseCors();

// -------------------------------------------------------------------
// 1) MOBILE RATE LIMIT  -> /api/v1/mobile/tuition için
//    Aynı studentNo için bir günde en fazla 3 istek
// -------------------------------------------------------------------

var mobileRateStore = new ConcurrentDictionary<string, int>();

app.Use(async (context, next) =>
{
    // Sadece GET /api/v1/mobile/tuition isteklerini yakala
    if (context.Request.Path.StartsWithSegments("/api/v1/mobile/tuition") &&
        HttpMethods.IsGet(context.Request.Method))
    {
        var studentNo = context.Request.Query["studentNo"].ToString();

        if (!string.IsNullOrWhiteSpace(studentNo))
        {
            // Tarihi yyyyMMdd formatında al (ör: 20241201)
            var dateKey = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyyMMdd");
            var key = $"{dateKey}:{studentNo}";

            // Sayacı artır (atomik)
            var current = mobileRateStore.AddOrUpdate(key, 1, (_, old) => old + 1);

            // 3’ten fazlaysa 429 dön ve backend’e gitme
            if (current > 3)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync("Daily limit exceeded (3 per student)");
                return;
            }
        }
    }

    // Limit aşılmadıysa pipe’a devam
    await next();
});

// -------------------------------------------------------------------
// 2) LOGGING  -> Gateway seviyesinde request/response logları
// -------------------------------------------------------------------

app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();

    var request = context.Request;
    var logger = app.Logger;

    var requestSize = request.ContentLength ?? 0;
    var headers = string.Join("; ", request.Headers.Select(h => $"{h.Key}={h.Value}"));
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var timestamp = DateTime.UtcNow.ToString("o");

    await next();

    sw.Stop();

    var response = context.Response;
    var responseSize = response.ContentLength ?? 0;

    logger.LogInformation(
        "GW {Method} {Path}{Query} Status={Status} Req={ReqSize} Res={ResSize} " +
        "Latency={Latency}ms IP={IP} Time={Time} Headers={Headers}",
        request.Method,
        request.Path,
        request.QueryString,
        response.StatusCode,
        requestSize,
        responseSize,
        sw.ElapsedMilliseconds,
        ip,
        timestamp,
        headers
    );
});

// -------------------------------------------------------------------
// 3) REVERSE PROXY  -> Gelen her isteği University.Api'ye forward et
// -------------------------------------------------------------------

app.Map("/{**catch-all}", async context =>
{
    var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client = clientFactory.CreateClient("backend");

    var request = context.Request;

    var targetUri = new Uri(
        client.BaseAddress!,
        request.Path + request.QueryString.ToUriComponent()
    );

    using var forwardMessage = new HttpRequestMessage
    {
        RequestUri = targetUri,
        Method = new HttpMethod(request.Method)
    };

    // Body kopyala (POST, PUT vs.)
    if (!HttpMethods.IsGet(request.Method) &&
        !HttpMethods.IsHead(request.Method) &&
        !HttpMethods.IsDelete(request.Method) &&
        !HttpMethods.IsTrace(request.Method))
    {
        forwardMessage.Content = new StreamContent(request.Body);
        if (request.ContentType != null)
        {
            forwardMessage.Content.Headers.TryAddWithoutValidation(
                "Content-Type", request.ContentType);
        }
    }

    // Header’ları kopyala
    foreach (var header in request.Headers)
    {
        if (!forwardMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {
            forwardMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    // Backend'e isteği gönder
    using var responseMessage = await client.SendAsync(
        forwardMessage,
        HttpCompletionOption.ResponseHeadersRead
    );

    var response = context.Response;
    response.StatusCode = (int)responseMessage.StatusCode;

    // Response header’larını kopyala
    foreach (var header in responseMessage.Headers)
    {
        response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in responseMessage.Content.Headers)
    {
        response.Headers[header.Key] = header.Value.ToArray();
    }

    // transfer-encoding’i kaldır (chunked çakışmasın)
    response.Headers.Remove("transfer-encoding");

    // Response body’yi kopyala
    await responseMessage.Content.CopyToAsync(response.Body);
});

app.Run();
