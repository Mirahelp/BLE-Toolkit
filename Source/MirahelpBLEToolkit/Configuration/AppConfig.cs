using System;

namespace MirahelpBLEToolkit.Configuration
{
    public static class AppConfig
    {
        public const int DeviceTtlSeconds = 30;
        public const int ReservedUiRows = 6;
        public const int ScrollbackMultiplier = 3;
        public const int NameResolveMaxConcurrency = 16;

        public static readonly TimeSpan NameResolveTimeout = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan NameResolveHeartbeatInterval = TimeSpan.FromMilliseconds(5000);

        public static readonly TimeSpan ConnectAttemptTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan ConnectRetryInitialDelay = TimeSpan.FromMilliseconds(800);
        public static readonly TimeSpan ConnectRetryMaxDelay = TimeSpan.FromSeconds(2);
    }
}