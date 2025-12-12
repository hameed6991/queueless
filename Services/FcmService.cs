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

            // 1) Try to read full JSON from env var (Render / cloud)
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

            // 2) Fallback: local file for your dev machine
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

        /// <summary>
        /// Send a push notification to a single device.
        /// </summary>
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

        /// <summary>
        /// Send the same notification to many devices.
        /// Implementation: simply loop and call SendAsync for each token.
        /// This avoids the Firebase /batch endpoint that is returning 404.
        /// </summary>
        public async Task SendToManyAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null)
        {
            var list = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (list.Count == 0)
            {
                _logger.LogWarning("SendToManyAsync called with no tokens.");
                return;
            }

            _logger.LogInformation(
                "Sending FCM notification to {Count} tokens (looped, no multicast).",
                list.Count);

            foreach (var token in list)
            {
                await SendAsync(token, title, body, data);
            }
        }
    }
}
