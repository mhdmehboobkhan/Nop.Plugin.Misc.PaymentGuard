using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Misc.PaymentGuard.Components;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Misc.PaymentGuard
{
    /// <summary>
    /// The License Spring plugin
    /// </summary>
    public partial class PaymentGuardPlugin : BasePlugin, IWidgetPlugin, IMiscPlugin, IAdminMenuPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly ISettingService _settingService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly PaymentGuardSettings _paymentGuardSettings;

        #endregion

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentGuardPlugin" /> class.
        /// </summary>
        /// <param name="emailAccountService">The email account service.</param>
        /// <param name="genericAttributeService">The generic attribute service.</param>
        /// <param name="localizationService">The localization service.</param>
        /// <param name="messageTemplateService">The message template service.</param>
        /// <param name="scheduleTaskService">The schedule task service.</param>
        /// <param name="settingService">The setting service.</param>
        /// <param name="storeService">The store service.</param>
        /// <param name="webHelper">The web helper.</param>
        /// <param name="widgetSettings">The widget settings.</param>
        public PaymentGuardPlugin(ILocalizationService localizationService,
            IWebHelper webHelper,
            ISettingService settingService,
            IScheduleTaskService scheduleTaskService,
            PaymentGuardSettings paymentGuardSettings)
        {
            _localizationService = localizationService;
            _webHelper = webHelper;
            _settingService = settingService;
            _scheduleTaskService = scheduleTaskService;
            _paymentGuardSettings = paymentGuardSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets widget zones where this widget should be rendered
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the widget zones
        /// </returns>
        public Task<IList<string>> GetWidgetZonesAsync()
        {
            return Task.FromResult<IList<string>>(new List<string> { PublicWidgetZones.HeadHtmlTag });
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        /// <returns></returns>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentGuard/Configure";
        }

        /// <summary>
        /// Gets a type of a view component for displaying widget
        /// </summary>
        /// <param name="widgetZone">Name of the widget zone</param>
        /// <returns>View component type</returns>
        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(PaymentGuardViewComponent);
        }

        public async Task ManageSiteMapAsync(SiteMapNode rootNode)
        {
            var menuItem = new SiteMapNode()
            {
                Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu"),
                SystemName = "PaymentGuard.Menu",
                Visible = true,
                IconClass = "fas fa-shield-alt"
            };

            menuItem.ChildNodes.Add(new SiteMapNode()
            {
                Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.ScriptManagement"),
                SystemName = "PaymentGuard.Menu.ScriptManagement",
                Url = "~/Admin/PaymentGuard/List",
                Visible = true,
                IconClass = "fas fa-code"
            });

            menuItem.ChildNodes.Add(new SiteMapNode()
            {
                Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.Dashboard"),
                SystemName = "PaymentGuard.Menu.Dashboard",
                Url = "~/Admin/PaymentGuard/Dashboard",
                Visible = true,
                IconClass = "fas fa-chart-line"
            });

            menuItem.ChildNodes.Add(new SiteMapNode()
            {
                Title = await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.Menu.Configuration"),
                SystemName = "PaymentGuard.Menu.Configure",
                Url = "~/Admin/PaymentGuard/Configure",
                Visible = true,
                IconClass = "fas fa-cog"
            });

            var pluginNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Third party plugins");
            if (pluginNode != null)
                pluginNode.ChildNodes.Add(menuItem);
            else
                rootNode.ChildNodes.Add(menuItem);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// </returns>
        public override async Task InstallAsync()
        {
            // Install default settings
            await _settingService.SaveSettingAsync(new PaymentGuardSettings
            {
                IsEnabled = true,
                MonitoringFrequency = 7, // Weekly
                AlertEmail = "",
                EnableEmailAlerts = true,
                EnableCSPHeaders = true,
                EnableSRIValidation = true
            });

            // Install scheduled task
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Monitoring Task",
                    Seconds = 604800, // Weekly (7 days * 24 hours * 60 minutes * 60 seconds)
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask",
                    Enabled = true,
                    StopOnError = false,
                });
            }

            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                // Plugin Info
                ["Plugins.Misc.PaymentGuard.FriendlyName"] = "PaymentGuard - PCI DSS Compliance",
                ["Plugins.Misc.PaymentGuard.Description"] = "PCI DSS v4.0 compliance solution for payment page script monitoring and security",

                // Menu Items
                ["Plugins.Misc.PaymentGuard.Menu"] = "Payment Guard",
                ["Plugins.Misc.PaymentGuard.Menu.ScriptManagement"] = "Script Management",
                ["Plugins.Misc.PaymentGuard.Menu.Dashboard"] = "Monitoring Dashboard",
                ["Plugins.Misc.PaymentGuard.Menu.Configuration"] = "Configuration",

                // Configuration Fields
                ["Plugins.Misc.PaymentGuard.Fields.IsEnabled"] = "Enabled",
                ["Plugins.Misc.PaymentGuard.Fields.IsEnabled.Hint"] = "Enable or disable PaymentGuard monitoring for this store",
                ["Plugins.Misc.PaymentGuard.Fields.AlertEmail"] = "Alert Email",
                ["Plugins.Misc.PaymentGuard.Fields.AlertEmail.Hint"] = "Email address to receive security alerts and notifications",
                ["Plugins.Misc.PaymentGuard.Fields.EnableEmailAlerts"] = "Enable Email Alerts",
                ["Plugins.Misc.PaymentGuard.Fields.EnableEmailAlerts.Hint"] = "Send email notifications when unauthorized scripts are detected",
                ["Plugins.Misc.PaymentGuard.Fields.MonitoringFrequency"] = "Monitoring Frequency (days)",
                ["Plugins.Misc.PaymentGuard.Fields.MonitoringFrequency.Hint"] = "How often to perform automated monitoring checks (minimum 7 days for PCI DSS compliance)",
                ["Plugins.Misc.PaymentGuard.Fields.EnableCSPHeaders"] = "Enable CSP Headers",
                ["Plugins.Misc.PaymentGuard.Fields.EnableCSPHeaders.Hint"] = "Automatically inject Content Security Policy headers on monitored pages",
                ["Plugins.Misc.PaymentGuard.Fields.EnableSRIValidation"] = "Enable SRI Validation",
                ["Plugins.Misc.PaymentGuard.Fields.EnableSRIValidation.Hint"] = "Enable Subresource Integrity validation for external scripts",
                ["Plugins.Misc.PaymentGuard.Fields.CSPPolicy"] = "Content Security Policy",
                ["Plugins.Misc.PaymentGuard.Fields.CSPPolicy.Hint"] = "Content Security Policy directives. Use 'self' for same-origin scripts.",
                ["Plugins.Misc.PaymentGuard.Fields.MonitoredPages"] = "Monitored Pages",
                ["Plugins.Misc.PaymentGuard.Fields.MonitoredPages.Hint"] = "Comma-separated list of pages to monitor (e.g., /checkout,/onepagecheckout)",
                ["Plugins.Misc.PaymentGuard.Fields.MaxAlertFrequency"] = "Max Alert Frequency (hours)",
                ["Plugins.Misc.PaymentGuard.Fields.MaxAlertFrequency.Hint"] = "Minimum hours between duplicate alerts",

                // Script Fields
                ["Plugins.Misc.PaymentGuard.Fields.SearchScriptUrl"] = "Search Script URL",
                ["Plugins.Misc.PaymentGuard.Fields.SearchScriptUrl.Hint"] = "Enter script URL to search for",
                ["Plugins.Misc.PaymentGuard.Fields.SearchIsActive"] = "Search Active Status",
                ["Plugins.Misc.PaymentGuard.Fields.SearchIsActive.Hint"] = "Filter by active/inactive status",
                ["Plugins.Misc.PaymentGuard.Fields.SearchRiskLevel"] = "Search Risk Level",
                ["Plugins.Misc.PaymentGuard.Fields.SearchRiskLevel.Hint"] = "Filter by risk level (Low, Medium, High)",
                ["Plugins.Misc.PaymentGuard.Fields.SearchSource"] = "Search Source Type",
                ["Plugins.Misc.PaymentGuard.Fields.SearchSource.Hint"] = "Filter by source type (Internal, Third-party, Payment Gateway, etc.)",

                ["Plugins.Misc.PaymentGuard.Fields.ScriptUrl"] = "Script URL",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptUrl.Hint"] = "Full URL of the JavaScript file (e.g., https://example.com/script.js)",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptUrl.Required"] = "Script URL of the JavaScript file required.",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptUrl.InvalidFormat"] = "Please enter a valid URL format (e.g., https://example.com/script.js).",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptUrl.MustBeHttps"] = "External script URLs must use HTTPS for security compliance.",
                ["Plugins.Misc.PaymentGuard.Fields.Purpose"] = "Purpose",
                ["Plugins.Misc.PaymentGuard.Fields.Purpose.Hint"] = "Brief description of what this script does",
                ["Plugins.Misc.PaymentGuard.Fields.Purpose.Required"] = "Brief description of file is required.",
                ["Plugins.Misc.PaymentGuard.Fields.Justification"] = "Business Justification",
                ["Plugins.Misc.PaymentGuard.Fields.Justification.Hint"] = "Business justification for why this script is necessary (required for PCI DSS compliance)",
                ["Plugins.Misc.PaymentGuard.Fields.Justification.Required"] = "Business justification of file is required.",
                ["Plugins.Misc.PaymentGuard.Fields.GenerateHash"] = "Generate SRI Hash",
                ["Plugins.Misc.PaymentGuard.Fields.GenerateHash.Hint"] = "Automatically generate SRI hash for this script",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptHash"] = "Script Hash",
                ["Plugins.Misc.PaymentGuard.Fields.ScriptHash.Hint"] = "SHA-384 hash for Subresource Integrity validation",
                ["Plugins.Misc.PaymentGuard.Fields.RiskLevel"] = "Risk Level",
                ["Plugins.Misc.PaymentGuard.Fields.RiskLevel.Hint"] = "Security risk level of this script (Low, Medium, High)",
                ["Plugins.Misc.PaymentGuard.Fields.Source"] = "Source Type",
                ["Plugins.Misc.PaymentGuard.Fields.Source.Hint"] = "Where this script originates from (Internal, Third-party, Payment Gateway, etc.)",
                ["Plugins.Misc.PaymentGuard.Fields.IsActive"] = "Active",
                ["Plugins.Misc.PaymentGuard.Fields.IsActive.Hint"] = "Whether this script is currently authorized for use",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedBy"] = "Authorized By",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedBy.Hint"] = "User who authorized this script",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedOnUtc"] = "Authorized On",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedOnUtc.Hint"] = "Date and time when this script was authorized",
                ["Plugins.Misc.PaymentGuard.Fields.LastVerifiedUtc"] = "Last Verified",
                ["Plugins.Misc.PaymentGuard.Fields.LastVerifiedUtc.Hint"] = "Date and time when this script was last verified for integrity",


                // Page Titles
                ["Plugins.Misc.PaymentGuard.AddNewScript"] = "Add New Authorized Script",
                ["Plugins.Misc.PaymentGuard.EditScript"] = "Edit Authorized Script",
                ["Plugins.Misc.PaymentGuard.BackToList"] = "back to script list",
                ["Plugins.Misc.PaymentGuard.ManageScripts"] = "Manage Scripts",

                // Configuration Sections
                ["Plugins.Misc.PaymentGuard.Configuration.GeneralSettings"] = "General Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.AlertSettings"] = "Alert Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.SecuritySettings"] = "Security Settings",

                // Dashboard

                // Dashboard Page Title
                ["Plugins.Misc.PaymentGuard.Dashboard.PageTitle"] = "PaymentGuard Dashboard",

                // Stats Cards Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalScriptsMonitoredLabel"] = "Total Scripts Monitored",
                ["Plugins.Misc.PaymentGuard.Dashboard.AuthorizedScriptsLabel"] = "Authorized Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.UnauthorizedScriptsLabel"] = "Unauthorized Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceScoreLabel"] = "Compliance Score",

                // Section Headers
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview"] = "Compliance Overview",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview.Score"] = "Compliance Score:",
                ["Plugins.Misc.PaymentGuard.Dashboard.RecentActivity"] = "Recent Activity",

                // Status Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.Authorized"] = "AUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Unauthorized"] = "UNAUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Checks"] = "CHECKS",

                // Activity Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.LastMonitoringCheck"] = "Last monitoring check:",
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalAlertsGenerated"] = "Total alerts generated:",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsUnderProtection"] = "Scripts under protection:",
                ["Plugins.Misc.PaymentGuard.Dashboard.MostCommonUnauthorizedScripts"] = "Most Common Unauthorized Scripts:",

                // Compliance Score Status
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceExcellent"] = "Excellent",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceGood"] = "Good",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceNeedsAttention"] = "Needs Attention",

                // Messages
                ["Plugins.Misc.PaymentGuard.ScriptAdded"] = "Script has been added successfully",
                ["Plugins.Misc.PaymentGuard.ScriptUpdated"] = "Script has been updated successfully",
                ["Plugins.Misc.PaymentGuard.ScriptDeleted"] = "Script has been deleted successfully"
            });

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// </returns>
        public override async Task UninstallAsync()
        {
            // Remove settings
            await _settingService.DeleteSettingAsync<PaymentGuardSettings>();

            // Remove scheduled task
            var task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.MonitoringTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.PaymentGuard");

            await base.UninstallAsync();
        }

        #endregion

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => false;
    }
}