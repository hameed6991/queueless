// Services/FcmService.cs
using System;
using System.Collections.Generic;
using System.IO;
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

            // Path to firebase-admin-key.json
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
                throw new FileNotFoundException(
                    $"Firebase admin key file not found: {keyPath}");
            }

            var options = new AppOptions
            {
                Credential = GoogleCredential.FromFile(keyPath)
            };

            FirebaseApp.Create(options);
            _initialized = true;

            _logger.LogInformation("FirebaseApp initialized for FCM.");
        }

        /// <summary>
        /// Send push notification to a single FCM token.
        /// </summary>
        public async Task SendAsync(
            string fcmToken,
            string title,
            string body,
            IReadOnlyDictionary<string, string>? data = null)
        {
            // Make sure we pass the right type to Message.Data
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
    }
}
