using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace BillsApi.Services;

public class SmtpSettings
{
    public required string Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string From { get; set; }
}

public class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<SmtpSettings> _options;

    public SmtpEmailSender(IOptionsMonitor<SmtpSettings> options)
    {
        _options = options;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlMessage)
    {
        var _settings = _options.CurrentValue;
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };

        var mail = new MailMessage(_settings.From, toEmail, subject, htmlMessage) { IsBodyHtml = true };
        await client.SendMailAsync(mail);
    }
}
