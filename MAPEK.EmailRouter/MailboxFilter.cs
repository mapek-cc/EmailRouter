using Microsoft.Extensions.Logging;
using SmtpServer;
using SmtpServer.Mail;
using SmtpServer.Storage;

namespace EmailRouter;

public class MailboxFilter : IMailboxFilter
{
    private readonly ILogger<MailboxFilter> _logger;

    public MailboxFilter(ILogger<MailboxFilter> logger)
    {
        _logger = logger;
    }

    public Task<bool> CanAcceptFromAsync(ISessionContext context, IMailbox from, int size,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Accepting message from {From} with size {Size}", from?.ToString(), size);
        return Task.FromResult(true);
    }

    public Task<bool> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox from,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Delivering message from {From} to {To}", from?.ToString(),
            to?.ToString());
        return Task.FromResult(true);
    }
}