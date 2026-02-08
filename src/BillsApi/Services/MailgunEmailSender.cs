using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BillsApi.Services;

public class MailgunSettings
{
    public required string Domain { get; set; }
    public required string ApiKey { get; set; }
    public required string From { get; set; }
}

public class MailgunEmailSender : IEmailSender
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly MailgunSettings _settings;

    public MailgunEmailSender(IHttpClientFactory httpFactory, IOptions<MailgunSettings> options)
    {
        _httpFactory = httpFactory;
        _settings = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlMessage)
    {
        var client = _httpFactory.CreateClient("mailgun");
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{_settings.ApiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var content = new MultipartFormDataContent
        {
            { new StringContent(_settings.From), "from" },
            { new StringContent(toEmail), "to" },
            { new StringContent(subject), "subject" },
            { new StringContent(htmlMessage), "html" }
        };

        var url = $"/v3/{_settings.Domain}/messages";
        var res = await client.PostAsync(url, content);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Mailgun send failed: {res.StatusCode} {body}");
        }
    }
}
