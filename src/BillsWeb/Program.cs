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

app.UseHttpsRedirection();
app.UseStaticFiles();

// If the app is hosted under a sub-path (e.g. https://site/billsweb), ensure the app knows its PathBase so
// redirects and cookie handling work correctly. Only enable in non-development (your production host).
if (!app.Environment.IsDevelopment())
{
    // When hosted at /billsweb behind a reverse proxy or IIS virtual application
    app.UsePathBase("/billsweb");
}

app.UseRouting();
app.UseSession();
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
