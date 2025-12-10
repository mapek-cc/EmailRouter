using System.Buffers;
using Microsoft.Extensions.Logging;
using System.Text;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;
using SmtpServer;
using SmtpServer.Protocol;
using SmtpServer.Storage;

namespace EmailRouter;

public class MessageStore : IMessageStore
{
    private readonly ILogger<MessageStore> _logger;

    public MessageStore(ILogger<MessageStore> logger)
    {
        _logger = logger;
    }

    public async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction,
        ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
    {
        var text = Encoding.UTF8.GetString(buffer.ToArray());
        var payload = buffer.ToArray();
        var message = await MimeMessage.LoadAsync(new MemoryStream(payload), cancellationToken);
        var payloadSizeKb = Math.Round(payload.Length / 1024d, 2);

        await HandleMessageAsync(message, payloadSizeKb);

        return SmtpResponse.Ok;
    }

    private async Task HandleMessageAsync(MimeMessage message, double? payloadSizeKb = null)
    {
        var fromSummary = message.From?.ToString() ?? "<unknown>";
        var toSummary = message.To.Mailboxes.Any()
            ? string.Join(", ", message.To.Mailboxes.Select(mb => mb.ToString()))
            : "<none>";
        var payloadDescriptor = payloadSizeKb.HasValue
            ? $"({payloadSizeKb.Value:F2} KB)"
            : string.Empty;

        _logger.LogInformation("Relaying email '{Subject}' from {From} to {To} {PayloadDescriptor}",
            message.Subject, fromSummary, toSummary, payloadDescriptor);

        try
        {
            var apiKey = Environment.GetEnvironmentVariable("SG_API_KEY");
            var client = new SendGridClient(apiKey);

            var from = message.From.Mailboxes.First();

            var sendGridMessage = new SendGridMessage
            {
                From = new EmailAddress(from.Address, from.Name),
                Subject = message.Subject,
                HtmlContent = message.HtmlBody ?? message.TextBody ?? "No Content Provided",
                PlainTextContent = message.TextBody ?? "No Content Provided"
            };

            foreach (var to in message.To.Mailboxes)
                sendGridMessage.AddTo(new EmailAddress(to.Address, to.Name));

            foreach (var cc in message.Cc.Mailboxes)
                sendGridMessage.AddCc(new EmailAddress(cc.Address, cc.Name));

            var attachmentCount = 0;
            foreach (var attachment in message.Attachments)
            {
                using var memoryStream = new MemoryStream();
                if (attachment is not MimePart part) continue;

                await part.Content.DecodeToAsync(memoryStream);
                var bytes = memoryStream.ToArray();
                sendGridMessage.AddAttachment(part.FileName, Convert.ToBase64String(bytes),
                    part.ContentType.MimeType);
                ++attachmentCount;
            }

            var response = await client.SendEmailAsync(sendGridMessage);
            _logger.LogInformation(
                "Email sent to SendGrid with status code {StatusCode}. Attachments: {AttachmentCount}",
                response.StatusCode, attachmentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding email '{Subject}'", message.Subject);
        }
    }
}