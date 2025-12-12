// Services/FcmService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Queueless.Services
{
    public class FcmService
    {
        private static bool _initialized = false;
        private readonly ILogger<FcmService> _logger;

        public FcmService(IConfiguration configuration, ILogger<FcmService> logger)
        {
            _logger = logger;

            if (_initialized)
                return;

            // 1) Try to read full JSON from env var (cloud-friendly, no file needed)
            var jsonFromEnv = Environment.GetEnvironmentVariable("FIREBASE_ADMIN_JSON");
            if (!string.IsNullOrWhiteSpace(jsonFromEnv))
            {
                _logger.LogInformation("Initializing FirebaseApp from FIREBASE_ADMIN_JSON env var.");

                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonFromEnv));
                var credential = GoogleCredential.FromStream(ms);

                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential
                });

                _initialized = true;
                return;
            }

            // 2) Fallback: read from file (for your local dev machine)
            // appsettings.json:
            // "Firebase": { "AdminSdkPath": "Secure/firebase-admin-key.json" }
            var keyPath = configuration["Firebase:AdminSdkPath"];

            if (string.IsNullOrWhiteSpace(keyPath))
            {
                keyPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Secure",
                    "firebase-admin-key.json");
            }

            if (!File.Exists(keyPath))
            {
                _logger.LogError("Firebase admin key file not found at {Path}", keyPath);
                throw new FileNotFoundException($"Firebase admin key file not found: {keyPath}");
            }

            var options = new AppOptions
            {
                Credential = GoogleCredential.FromFile(keyPath)
            };

            FirebaseApp.Create(options);
            _initialized = true;

            _logger.LogInformation("FirebaseApp initialized from file {Path}.", keyPath);
        }

        // --------------------------------------------------------------------
        // 1) Single-token send (generic)  — your existing method
        // --------------------------------------------------------------------
        public async Task SendAsync(
            string fcmToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null)
        {
            IReadOnlyDictionary<string, string> payload =
                data ?? new Dictionary<string, string>();

            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = payload
            };

            try
            {
                var messageId = await FirebaseMessaging
                    .DefaultInstance
                    .SendAsync(message);

                _logger.LogInformation("FCM sent. MessageId={MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error sending FCM notification to token {Token}",
                    fcmToken);
            }
        }

        // --------------------------------------------------------------------
        // 2) Multi-token send (generic) — useful for owner with many devices
        // --------------------------------------------------------------------
        public async Task SendToManyAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null)
        {
            var tokenList = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (!tokenList.Any())
            {
                _logger.LogInformation("SendToManyAsync called with no valid tokens.");
                return;
            }

            IReadOnlyDictionary<string, string> payload =
                data ?? new Dictionary<string, string>();

            var message = new MulticastMessage
            {
                Tokens = tokenList,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = payload
            };

            try
            {
                var response = await FirebaseMessaging
                    .DefaultInstance
                    .SendMulticastAsync(message);

                _logger.LogInformation(
                    "FCM multicast sent. Success={SuccessCount}, Failure={FailureCount}",
                    response.SuccessCount,
                    response.FailureCount);

                if (response.FailureCount > 0)
                {
                    for (int i = 0; i < response.Responses.Count; i++)
                    {
                        var r = response.Responses[i];
                        if (!r.IsSuccess)
                        {
                            _logger.LogWarning(
                                "FCM error for token {Token}: {Error}",
                                tokenList[i],
                                r.Exception?.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM multicast notification.");
            }
        }

        // --------------------------------------------------------------------
        // 3) Business queue update — called when a new token joins the queue
        // --------------------------------------------------------------------
        public Task SendQueueUpdateToBusinessAsync(
            int businessId,
            string? tokenNumber,
            string? customerName,
            IEnumerable<string> deviceTokens)
        {
            string title = "New customer in queue";

            string body;
            if (string.IsNullOrWhiteSpace(customerName))
            {
                body = string.IsNullOrWhiteSpace(tokenNumber)
                    ? "A new customer joined your queue."
                    : $"New token {tokenNumber} joined your queue.";
            }
            else
            {
                body = string.IsNullOrWhiteSpace(tokenNumber)
                    ? $"{customerName} joined your queue."
                    : $"{customerName} joined with token {tokenNumber}.";
            }

            var data = new Dictionary<string, string>
            {
                ["type"] = "queue_update",
                ["businessId"] = businessId.ToString(),
                ["tokenNumber"] = tokenNumber ?? string.Empty,
                ["customerName"] = customerName ?? string.Empty
            };

            return SendToManyAsync(deviceTokens, title, body, data);
        }
    }
}
