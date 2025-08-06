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
    }
}