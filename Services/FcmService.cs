// Services/FcmService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Builder.Extensions;
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

            // 1) First try env / config value "FcmKeyPath"
            //    (for Render: /etc/secrets/firebase-admin-key.json)
            var keyPath = configuration["FcmKeyPath"];

            // 2) Fallback to your old setting "Firebase:AdminSdkPath"
            if (string.IsNullOrWhiteSpace(keyPath))
            {
                keyPath = configuration["Firebase:AdminSdkPath"];
            }

            // 3) If still empty, use default Secure/firebase-admin-key.json
            if (string.IsNullOrWhiteSpace(keyPath))
            {
                keyPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "Secure",
                    "firebase-admin-key.json");
            }
            else
            {
                // If it's a relative path, make it relative to app base directory
                if (!Path.IsPathRooted(keyPath))
                {
                    keyPath = Path.Combine(AppContext.BaseDirectory, keyPath);
                }
            }

            if (!File.Exists(keyPath))
            {
                _logger.LogError("Firebase admin key file not found at {Path}", keyPath);
                throw new FileNotFoundException(
                    $"Firebase admin key file not found: {keyPath}", keyPath);
            }

            _logger.LogInformation("Initializing FirebaseApp with key at {Path}", keyPath);

            var options = new AppOptions
            {
                Credential = GoogleCredential.FromFile(keyPath)
            };

            // Avoid creating multiple instances
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(options);
                _logger.LogInformation("FirebaseApp initialized for FCM.");
            }
            else
            {
                _logger.LogInformation("FirebaseApp already initialized, reusing existing instance.");
            }

            _initialized = true;
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
