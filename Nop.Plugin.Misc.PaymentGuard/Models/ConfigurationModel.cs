using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record ConfigurationModel : BaseNopModel, ISettingsModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.IsEnabled")]
        public bool IsEnabled { get; set; }
        public bool IsEnabled_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.MonitoringFrequency")]
        public int MonitoringFrequency { get; set; }
        public bool MonitoringFrequency_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.AlertEmail")]
        [EmailAddress]
        public string AlertEmail { get; set; }
        public bool AlertEmail_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EnableEmailAlerts")]
        public bool EnableEmailAlerts { get; set; }
        public bool EnableEmailAlerts_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EnableCSPHeaders")]
        public bool EnableCSPHeaders { get; set; }
        public bool EnableCSPHeaders_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EnableSRIValidation")]
        public bool EnableSRIValidation { get; set; }
        public bool EnableSRIValidation_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.CSPPolicy")]
        public string CSPPolicy { get; set; }
        public bool CSPPolicy_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.EnableDetailedLogging")]
        public bool EnableDetailedLogging { get; set; }
        public bool EnableDetailedLogging_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.MonitoredPages")]
        public string MonitoredPages { get; set; }
        public bool MonitoredPages_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.MaxAlertFrequency")]
        public int MaxAlertFrequency { get; set; }
        public bool MaxAlertFrequency_OverrideForStore { get; set; }

        public int ActiveStoreScopeConfiguration { get; set; }
    }
}