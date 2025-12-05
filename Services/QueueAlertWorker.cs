// Services/QueueAlertWorker.cs
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace Queueless.Services
{
    public class QueueAlertWorker : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<QueueAlertWorker> _logger;

        public QueueAlertWorker(IServiceProvider sp, ILogger<QueueAlertWorker> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendAlertsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in QueueAlertWorker loop");
                }

                // Run every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndSendAlertsAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var fcm = scope.ServiceProvider.GetRequiredService<FcmService>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var connStr = config.GetConnectionString("DefaultConnection");
            await using var con = new SqlConnection(connStr);
            await con.OpenAsync(ct);

            var cmd = new SqlCommand(@"
SELECT  t.TokenId,
        t.CustomerUserId,
        t.TokenNumber,
        t.BusinessId,
        b.BusinessName,
        t.EstimatedWaitMinutes,
        u.FcmToken
FROM ServiceQueueToken t
JOIN BusinessRegistration b ON b.BusinessId = t.BusinessId
JOIN CustomerUser u         ON u.CustomerUserId = t.CustomerUserId
WHERE t.Status = 'Waiting'
  AND t.EstimatedWaitMinutes <= 5
  AND ISNULL(t.Alert5MinSent, 0) = 0
  AND u.FcmToken IS NOT NULL;", con);

            var alerts = new List<(int TokenId, int UserId, string FcmToken, string BusinessName, int TokenNumber)>();

            await using (var rdr = await cmd.ExecuteReaderAsync(ct))
            {
                while (await rdr.ReadAsync(ct))
                {
                    var tokenId = rdr.GetInt32(0);
                    var userId = rdr.GetInt32(1);
                    var tokenNumber = rdr.GetInt32(2);
                    var businessName = rdr.GetString(4);
                    var fcmToken = rdr.GetString(6);

                    alerts.Add((tokenId, userId, fcmToken, businessName, tokenNumber));
                }
            }

            foreach (var a in alerts)
            {
                try
                {
                    var title = "Almost your turn";
                    var body = $"Only ~5 minutes left at {a.BusinessName} (Token {a.TokenNumber}).";

                    var data = new Dictionary<string, string>
                    {
                        ["type"] = "queue_eta_5min",
                        ["tokenId"] = a.TokenId.ToString()
                    };

                    await fcm.SendAsync(a.FcmToken, title, body, data);

                    var updateCmd = new SqlCommand(
                        "UPDATE ServiceQueueToken SET Alert5MinSent = 1 WHERE TokenId = @id", con);
                    updateCmd.Parameters.AddWithValue("@id", a.TokenId);
                    await updateCmd.ExecuteNonQueryAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error sending 5-min alert for TokenId {TokenId}", a.TokenId);
                }
            }
        }
    }
}
