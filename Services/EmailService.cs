using Azure.Communication.Email;
using Azure;

public class EmailService
{
    private readonly EmailClient _client;
    private readonly string _sender;

    public EmailService(IConfiguration config)
    {
        var connectionString = config["Email:ConnectionString"]
            ?? throw new InvalidOperationException("Email:ConnectionString is not configured");

        _sender = config["Email:SenderAddress"]
            ?? throw new InvalidOperationException("Email:SenderAddress is not configured");

        _client = new EmailClient(connectionString);
    }

    public async Task SendResetPasswordEmailAsync(string toEmail, string resetLink)
    {
        var subject = "Скидання паролю в BeTendly";

        var htmlBody = $@"
            <html>
            <body>
                <p>Вітаємо!</p>
                <p>Ви запросили скидання паролю для акаунта BeTendly.</p>
                <p>Перейдіть за посиланням, щоб задати новий пароль:</p>
                <p><a href=""{resetLink}"">{resetLink}</a></p>
                <p>Якщо це були не ви — просто проігноруйте цей лист.</p>
            </body>
            </html>";

        try
        {
            var operation = await _client.SendAsync(
                wait: Azure.WaitUntil.Completed,
                senderAddress: _sender,
                recipientAddress: toEmail,
                subject: subject,
                htmlContent: htmlBody);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }
    }
}
