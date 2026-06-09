using BillsWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddRouting(options => 
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = false;
});
builder.Services.AddHttpContextAccessor();
// In-memory cache required for session state
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Environment-sensitive cookie settings: use relaxed settings for development to avoid SameSite/Secure
    // blocking during local testing, and stricter settings in production.
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    else
    {
        // Use SameSite=None + Secure in production so the cookie is sent across redirects/sub-paths
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }

    options.Cookie.Path = "/";
    options.Cookie.Name = ".BillsWeb.Session";
});

builder.Services.AddHttpClient<IBillsApiClient, BillsApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["BillsApi:BaseUrl"] ?? "https://bills.dukesducks.ca/billsapi";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// UsePathBase MUST be first so all subsequent middleware (routing, static files, HTTPS redirect)
// see the correct path when hosted under a sub-path (e.g. https://site/billsweb).
if (!app.Environment.IsDevelopment())
{
    app.UsePathBase("/billsweb");
}

// Skip HTTPS redirect in production when hosted behind a reverse proxy that terminates SSL.
// The proxy already handles HTTPS on port 443; redirecting internally causes a 443 connection error.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();
app.UseSession();

// Middleware to recover session from backup cookies when in-memory session data is lost.
// If the session Token is missing but a refresh token cookie exists, call the API to get
// a fresh access token and restore the session so the user stays logged in.
app.Use(async (context, next) =>
{
    if (string.IsNullOrEmpty(context.Session.GetString("Token"))
        && context.Request.Cookies.TryGetValue("BillsWeb.RefreshToken", out var refreshToken)
        && !string.IsNullOrEmpty(refreshToken))
    {
        try
        {
            var apiClient = context.RequestServices.GetRequiredService<IBillsApiClient>();
            var result = await apiClient.RefreshTokenAsync(refreshToken);

            if (result != null && !string.IsNullOrEmpty(result.AccessToken))
            {
                context.Session.SetString("Token", result.AccessToken);
                context.Session.SetString("RefreshToken", result.RefreshToken);

                if (context.Request.Cookies.TryGetValue("BillsWeb.Username", out var username)
                    && !string.IsNullOrEmpty(username))
                {
                    context.Session.SetString("Username", username);
                }

                await context.Session.CommitAsync();

                // Update backup cookies with the new tokens
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                };
                context.Response.Cookies.Append("BillsWeb.AuthToken", result.AccessToken, cookieOptions);
                context.Response.Cookies.Append("BillsWeb.RefreshToken", result.RefreshToken, cookieOptions);
            }
            else
            {
                // Refresh failed — clear stale cookies so we don't retry on every request
                context.Response.Cookies.Delete("BillsWeb.AuthToken");
                context.Response.Cookies.Delete("BillsWeb.RefreshToken");
                context.Response.Cookies.Delete("BillsWeb.Username");
            }
        }
        catch
        {
            // Refresh failed — clear stale cookies
            context.Response.Cookies.Delete("BillsWeb.AuthToken");
            context.Response.Cookies.Delete("BillsWeb.RefreshToken");
            context.Response.Cookies.Delete("BillsWeb.Username");
        }
    }

    await next();
});

app.UseAuthorization();

app.MapRazorPages();

// Temporary debug endpoint to inspect cookies and session values when troubleshooting auth/session issues.
// Remove this in production.
app.MapGet("/debug/session", async (HttpContext context) =>
{
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("Request Cookies:");
    foreach (var c in context.Request.Cookies)
    {
        sb.AppendLine($"{c.Key} = {c.Value}");
    }

    sb.AppendLine();
    sb.AppendLine("Session State:");
    try
    {
        sb.AppendLine($"Session Id: {context.Session.Id}");
        sb.AppendLine($"Token: {context.Session.GetString("Token") ?? "<null>"}");
        sb.AppendLine($"RefreshToken: {context.Session.GetString("RefreshToken") ?? "<null>"}");
        sb.AppendLine($"Username: {context.Session.GetString("Username") ?? "<null>"}");
    }
    catch (Exception ex)
    {
        sb.AppendLine($"Session read error: {ex.GetType().Name} - {ex.Message}");
    }

    return Results.Text(sb.ToString(), "text/plain");
});

app.Run();
