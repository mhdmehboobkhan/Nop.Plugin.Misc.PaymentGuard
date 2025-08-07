using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.PaymentGuard
{
    public class PaymentGuardSettings : ISettings
    {
        public bool IsEnabled { get; set; } = true;

        public int MonitoringFrequency { get; set; } = 7; // Days

        public string AlertEmail { get; set; } = "";

        public bool EnableEmailAlerts { get; set; } = true;

        public bool EnableCSPHeaders { get; set; } = true;

        public bool EnableSRIValidation { get; set; } = true;

        public string CSPPolicy { get; set; } = "script-src 'self' 'unsafe-inline';";

        public bool EnableDetailedLogging { get; set; } = true;

        public string MonitoredPages { get; set; } = "/checkout,/onepagecheckout"; // Comma separated

        public int MaxAlertFrequency { get; set; } = 24; // Hours between same alerts
                                                         // New Maintenance Settings
        public int LogRetentionDays { get; set; } = 90; // Days to keep monitoring logs

        public int AlertRetentionDays { get; set; } = 30; // Days to keep resolved alerts

        public bool EnableAutomaticCleanup { get; set; } = true;

        public int CacheExpirationMinutes { get; set; } = 60; // Cache expiration for script authorization

        public bool EnableApiRateLimit { get; set; } = true;

        public int ApiRateLimitPerHour { get; set; } = 1000; // API calls per hour per IP

        public string WhitelistedIPs { get; set; } = ""; // Comma separated IPs that bypass rate limiting
    }
}