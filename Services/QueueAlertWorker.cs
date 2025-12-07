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

            // -------------------------------------------
            // 1) RECALCULATE EstimatedWaitMinutes
            //    for all waiting tokens for TODAY
            // -------------------------------------------
            var recalcCmd = new SqlCommand(@"
DECLARE @today DATE = CAST(GETDATE() AS DATE);

;WITH Waiting AS
(
    SELECT
        t.TokenId,
        t.BusinessId,
        -- position in queue (0 = first in line)
        ROW_NUMBER() OVER (PARTITION BY t.BusinessId ORDER BY t.TokenNumber) - 1 AS WaitingAhead
    FROM ServiceQueueToken t
    WHERE t.Status = N'Waiting'
      AND CAST(t.CreatedOn AS DATE) = @today
),
Joined AS
(
    SELECT
        w.TokenId,
        w.WaitingAhead,
        ISNULL(b.AvgTimeMinutes, 5) AS AvgTimeMinutes   -- default 5 if NULL
    FROM Waiting w
    JOIN BusinessRegistration b ON b.BusinessId = w.BusinessId
)
UPDATE t
   SET t.EstimatedWaitMinutes = j.WaitingAhead * j.AvgTimeMinutes
FROM ServiceQueueToken t
JOIN Joined j ON t.TokenId = j.TokenId;
", con);

            await recalcCmd.ExecuteNonQueryAsync(ct);

            // -------------------------------------------
            // 2) READ tokens that just hit the 5min window
            // -------------------------------------------
            var cmd = new SqlCommand(@"
DECLARE @today DATE = CAST(GETDATE() AS DATE);

SELECT  t.TokenId,
        t.CustomerUserId,
        t.TokenNumber,
        t.BusinessId,
        b.BusinessName,
        t.EstimatedWaitMinutes,
        u.FcmToken
FROM ServiceQueueToken t
JOIN BusinessRegistration b ON b.BusinessId = t.BusinessId
JOIN AppUser u              ON u.UserId = t.CustomerUserId
WHERE t.Status = N'Waiting'
  AND CAST(t.CreatedOn AS DATE) = @today
  AND t.EstimatedWaitMinutes IS NOT NULL
  AND t.EstimatedWaitMinutes <= 5
  AND ISNULL(t.Alert5MinSent, 0) = 0
  AND u.FcmToken IS NOT NULL;
", con);

            var alerts = new List<(int TokenId, int UserId, string FcmToken, string BusinessName, int TokenNumber, int? Eta)>();

            await using (var rdr = await cmd.ExecuteReaderAsync(ct))
            {
                while (await rdr.ReadAsync(ct))
                {
                    var tokenId = rdr.GetInt32(0);
                    var userId = rdr.GetInt32(1);
                    var tokenNumber = rdr.GetInt32(2);
                    var businessName = rdr.GetString(4);
                    int? etaMinutes = rdr.IsDBNull(5) ? (int?)null : rdr.GetInt32(5);
                    var fcmToken = rdr.GetString(6);

                    alerts.Add((tokenId, userId, fcmToken, businessName, tokenNumber, etaMinutes));
                }
            }

            // -------------------------------------------
            // 3) Send FCM + mark Alert5MinSent = 1
            // -------------------------------------------
            foreach (var a in alerts)
            {
                try
                {
                    var etaText = a.Eta.HasValue ? a.Eta.Value.ToString() : "a few";

                    var title = "Almost your turn";
                    var body = $"Only ~{etaText} minutes left at {a.BusinessName} (Token {a.TokenNumber}).";

                    var data = new Dictionary<string, string>
                    {
                        ["type"] = "queue_eta_5min",
                        ["tokenId"] = a.TokenId.ToString()
                    };

                    await fcm.SendAsync(a.FcmToken, title, body, data);

                    var updateCmd = new SqlCommand(
                        "UPDATE ServiceQueueToken SET Alert5MinSent = 1 WHERE TokenId = @id;",
                        con);
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
