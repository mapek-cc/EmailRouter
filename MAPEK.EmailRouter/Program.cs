using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using SmtpServer;
using SmtpServer.ComponentModel;

namespace EmailRouter
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger();

            using var loggerFactory = new SerilogLoggerFactory(Log.Logger, true);
            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                const string serverName = "localhost";
                const int port = 25;

                var options = new SmtpServerOptionsBuilder()
                    .ServerName(serverName)
                    .Port(25)
                    .Build();

                var serviceProvider = new ServiceProvider();
                serviceProvider.Add(new MailboxFilter(loggerFactory.CreateLogger<MailboxFilter>()));
                serviceProvider.Add(new MessageStore(loggerFactory.CreateLogger<MessageStore>()));

                var smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);

                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    logger.LogInformation("CTRL-C received, shutting down SMTP server...");
                    cancellationTokenSource.Cancel();
                };

                AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
                {
                    logger.LogInformation("Process exit detected, shutting down SMTP server...");
                    cancellationTokenSource.Cancel();
                };

                logger.LogInformation("Starting SMTP server {ServerName}:{Port}", serverName, port);
                _ = smtpServer.StartAsync(cancellationToken);
                logger.LogInformation("SMTP server started; press CTRL-C to stop the service.");
                
                try
                {
                    // Instead of waiting for a key press, wait for the cancellation token to be triggered
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected exception when the cancellation token is triggered, can be safely ignored (just log it)
                    logger.LogDebug("Cancellation requested, stopping server loop.");
                }

                logger.LogInformation("SMTP server stopped.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Fatal error running SMTP server. The service has stopped");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}