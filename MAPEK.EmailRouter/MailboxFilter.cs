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
        if (size != 0)
            _logger.LogInformation("Accepting message from {User} (Server: {Host}) with size {Size}", from.User, from.Host, size);
        else
            _logger.LogInformation("Accepting message from {User} (Server: {Host})", from.User, from.Host);
            
        return Task.FromResult(true);
    }

    public Task<bool> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox from,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Delivering message from {from} (Server: {fromHost}) to {to} (Server: {toHost})",
            from?.User, from?.Host, to?.User, to?.Host);
        return Task.FromResult(true);
    }
}