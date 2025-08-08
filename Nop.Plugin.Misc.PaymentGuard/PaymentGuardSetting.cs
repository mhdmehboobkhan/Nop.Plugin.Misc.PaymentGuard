using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.PaymentGuard
{
    public class PaymentGuardSettings : ISettings
    {
        public bool IsEnabled { get; set; }

        //public int MonitoringFrequency { get; set; } = 7; // Days

        public string AlertEmail { get; set; }

        public bool EnableEmailAlerts { get; set; }

        public bool EnableCSPHeaders { get; set; }

        public bool EnableSRIValidation { get; set; }

        public string CSPPolicy { get; set; }

        public bool EnableDetailedLogging { get; set; }

        public string MonitoredPages { get; set; }

        public int MaxAlertFrequency { get; set; } // Hours between same alerts
        
        public int LogRetentionDays { get; set; } // Days to keep monitoring logs

        public int AlertRetentionDays { get; set; } // Days to keep resolved alerts

        public bool EnableAutomaticCleanup { get; set; } 

        public int CacheExpirationMinutes { get; set; } // Cache expiration for script authorization

        public bool EnableApiRateLimit { get; set; }

        public int ApiRateLimitPerHour { get; set; } // API calls per hour per IP

        public string WhitelistedIPs { get; set; } // Comma separated IPs that bypass rate limiting

        public string TrustedDomains { get; set; }

        public string PaymentProviders { get; set; }
    }
}