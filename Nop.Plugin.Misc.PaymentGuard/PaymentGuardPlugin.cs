using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Core.Domain.Security;
using Nop.Core.Infrastructure;
using Nop.Plugin.Misc.PaymentGuard.Components;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Security;
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
        private readonly IPermissionService _permissionService;
        
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
            PaymentGuardSettings paymentGuardSettings,
            IPermissionService permissionService)
        {
            _localizationService = localizationService;
            _webHelper = webHelper;
            _settingService = settingService;
            _scheduleTaskService = scheduleTaskService;
            _paymentGuardSettings = paymentGuardSettings;
            _permissionService = permissionService;
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
                EnableSRIValidation = true,
                CSPPolicy = "script-src 'self' 'unsafe-inline';",
                EnableDetailedLogging = true,
                MonitoredPages = "/checkout,/onepagecheckout",
                MaxAlertFrequency = 24,
                LogRetentionDays = 90,
                AlertRetentionDays = 30,
                EnableAutomaticCleanup = true,
                CacheExpirationMinutes = 60,
                EnableApiRateLimit = true,
                ApiRateLimitPerHour = 1000,
                WhitelistedIPs = ""
            });

            // Install scheduled tasks
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

            // Install cleanup task
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Cleanup Task",
                    Seconds = 86400, // Daily (24 hours * 60 minutes * 60 seconds)
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask",
                    Enabled = true,
                    StopOnError = false,
                });
            }

            // Install maintenance task for script verification
            if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask") == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Name = "PaymentGuard Script Verification Task",
                    Seconds = 604800, // Weekly
                    Type = "Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask",
                    Enabled = true,
                    StopOnError = false,
                });
            }

            // Install permissions
            var permissionProvider = new PaymentGuardPermissionProvider();
            var allPermissions = await _permissionService.GetAllPermissionRecordsAsync();

            foreach (var permission in permissionProvider.GetPermissions())
            {
                // Check if permission already exists
                var existingPermission = allPermissions.Where(x => x.SystemName == permission.SystemName).FirstOrDefault();
                if (existingPermission == null)
                {
                    await _permissionService.InsertPermissionRecordAsync(permission);
                }
                else
                {
                    // Update the existing permission's Id for role mapping
                    permission.Id = existingPermission.Id;
                }
            }

            // Install default permissions for administrators
            var customerService = EngineContext.Current.Resolve<ICustomerService>();
            var adminRole = await customerService.GetCustomerRoleBySystemNameAsync(NopCustomerDefaults.AdministratorsRoleName);

            if (adminRole != null)
            {
                foreach (var permission in permissionProvider.GetPermissions())
                {
                    // Check if the permission is already assigned to the admin role
                    var existingMapping = await _permissionService.GetMappingByPermissionRecordIdAsync(permission.Id);
                    var isAlreadyAssigned = existingMapping.Any(m => m.CustomerRoleId == adminRole.Id);

                    if (!isAlreadyAssigned)
                    {
                        await _permissionService.InsertPermissionRecordCustomerRoleMappingAsync(new PermissionRecordCustomerRoleMapping
                        {
                            CustomerRoleId = adminRole.Id,
                            PermissionRecordId = permission.Id
                        });
                    }
                }
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
                ["Plugins.Misc.PaymentGuard.Menu.Alerts"] = "Compliance Alerts",
                ["Plugins.Misc.PaymentGuard.Menu.MonitoringLogs"] = "Monitoring Logs",

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
                ["Plugins.Misc.PaymentGuard.Fields.LogRetentionDays"] = "Log Retention (days)",
                ["Plugins.Misc.PaymentGuard.Fields.LogRetentionDays.Hint"] = "Number of days to retain monitoring logs",
                ["Plugins.Misc.PaymentGuard.Fields.AlertRetentionDays"] = "Alert Retention (days)",
                ["Plugins.Misc.PaymentGuard.Fields.AlertRetentionDays.Hint"] = "Number of days to retain resolved alerts",
                ["Plugins.Misc.PaymentGuard.Fields.EnableAutomaticCleanup"] = "Enable Automatic Cleanup",
                ["Plugins.Misc.PaymentGuard.Fields.EnableAutomaticCleanup.Hint"] = "Automatically clean up old logs and alerts",
                ["Plugins.Misc.PaymentGuard.Fields.ApiRateLimitPerHour"] = "API Rate Limit (per hour)",
                ["Plugins.Misc.PaymentGuard.Fields.ApiRateLimitPerHour.Hint"] = "Maximum API calls per hour per IP address",
                ["Plugins.Misc.PaymentGuard.Fields.WhitelistedIPs"] = "Whitelisted IPs",
                ["Plugins.Misc.PaymentGuard.Fields.WhitelistedIPs.Hint"] = "Comma-separated list of IP addresses that bypass rate limiting",
                ["Plugins.Misc.PaymentGuard.Fields.CacheExpirationMinutes"] = "Cache Expiration (minutes)",
                ["Plugins.Misc.PaymentGuard.Fields.CacheExpirationMinutes.Hint"] = "How long to cache script authorization lookups (recommended: 30-120 minutes)",
                
                // Validation Messages
                ["Plugins.Misc.PaymentGuard.Fields.LogRetentionDays.Range"] = "Log retention must be between 1 and 365 days",
                ["Plugins.Misc.PaymentGuard.Fields.AlertRetentionDays.Range"] = "Alert retention must be between 1 and 365 days",
                ["Plugins.Misc.PaymentGuard.Fields.CacheExpirationMinutes.Range"] = "Cache expiration must be between 1 and 1440 minutes (24 hours)",
                ["Plugins.Misc.PaymentGuard.Fields.ApiRateLimitPerHour.Range"] = "API rate limit must be between 1 and 100000 requests per hour",


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

                // Compliance Report Resources  
                ["Plugins.Misc.PaymentGuard.ComplianceReport.PageTitle"] = "PaymentGuard Compliance Report",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.DownloadPDF"] = "Download PDF",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.BackToDashboard"] = "Back to Dashboard",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ReportPeriod"] = "Report Period:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Generated"] = "Generated:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.TotalScripts"] = "Total Scripts",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Authorized"] = "Authorized",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Unauthorized"] = "Unauthorized",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ChecksPerformed"] = "Checks Performed",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ComplianceAssessment"] = "Compliance Assessment",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Recommendations"] = "Recommendations",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.HighPriority"] = "High Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.MediumPriority"] = "Medium Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.LowPriority"] = "Low Priority",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.BestPractices"] = "Best Practices",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.MostCommonIssues"] = "Most Common Issues",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.AdditionalInformation"] = "Additional Information",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.LastCheckDate"] = "Last Check Date:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.TotalAlertsGenerated"] = "Total Alerts Generated:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.IssueRate"] = "Issue Rate:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.ReportType"] = "Report Type:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.RequirementsCovered"] = "Requirements Covered:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.NextRecommendedReview"] = "Next Recommended Review:",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.PCIDSSCompliance"] = "PCI DSS Compliance Assessment",
                ["Plugins.Misc.PaymentGuard.ComplianceReport.Requirements"] = "6.4.3, 11.6.1",
                
                // Compliance Alert Fields
                ["Plugins.Misc.PaymentGuard.Fields.AlertType"] = "Alert Type",
                ["Plugins.Misc.PaymentGuard.Fields.AlertType.Hint"] = "The type of security alert",
                ["Plugins.Misc.PaymentGuard.Fields.AlertLevel"] = "Alert Level",
                ["Plugins.Misc.PaymentGuard.Fields.AlertLevel.Hint"] = "The severity level of the alert",
                ["Plugins.Misc.PaymentGuard.Fields.Message"] = "Message",
                ["Plugins.Misc.PaymentGuard.Fields.Message.Hint"] = "Brief description of the alert",
                ["Plugins.Misc.PaymentGuard.Fields.Details"] = "Details",
                ["Plugins.Misc.PaymentGuard.Fields.Details.Hint"] = "Detailed information about the alert",
                ["Plugins.Misc.PaymentGuard.Fields.IsResolved"] = "Resolved",
                ["Plugins.Misc.PaymentGuard.Fields.IsResolved.Hint"] = "Whether this alert has been resolved",
                ["Plugins.Misc.PaymentGuard.Fields.ResolvedBy"] = "Resolved By",
                ["Plugins.Misc.PaymentGuard.Fields.ResolvedBy.Hint"] = "User who resolved this alert",
                ["Plugins.Misc.PaymentGuard.Fields.ResolvedOnUtc"] = "Resolved On",
                ["Plugins.Misc.PaymentGuard.Fields.ResolvedOnUtc.Hint"] = "Date and time when this alert was resolved",
                ["Plugins.Misc.PaymentGuard.Fields.EmailSent"] = "Email Sent",
                ["Plugins.Misc.PaymentGuard.Fields.EmailSent.Hint"] = "Whether an email notification was sent",
                ["Plugins.Misc.PaymentGuard.Fields.EmailSentOnUtc"] = "Email Sent On",
                ["Plugins.Misc.PaymentGuard.Fields.EmailSentOnUtc.Hint"] = "Date and time when email was sent",
                ["Plugins.Misc.PaymentGuard.Fields.CreatedOnUtc"] = "Created On",
                ["Plugins.Misc.PaymentGuard.Fields.CreatedOnUtc.Hint"] = "Date and time when this alert was created",

                // Search Fields for Alerts
                ["Plugins.Misc.PaymentGuard.Fields.SearchAlertType"] = "Alert Type",
                ["Plugins.Misc.PaymentGuard.Fields.SearchAlertType.Hint"] = "Filter by alert type",
                ["Plugins.Misc.PaymentGuard.Fields.SearchAlertLevel"] = "Alert Level",
                ["Plugins.Misc.PaymentGuard.Fields.SearchAlertLevel.Hint"] = "Filter by alert severity level",
                ["Plugins.Misc.PaymentGuard.Fields.SearchIsResolved"] = "Resolution Status",
                ["Plugins.Misc.PaymentGuard.Fields.SearchIsResolved.Hint"] = "Filter by resolution status",

                // Monitoring Log Fields
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.View"] = "View",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Compliant"] = "Compliant",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.HasIssues"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Manual"] = "Manual",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Scheduled"] = "Scheduled",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.ManualCheckUrl"] = "Page URL:",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.UrlPlaceholder"] = "https://yourstore.com/checkout",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Cancel"] = "Cancel",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.RunCheck"] = "Run Check",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.EnterValidUrl"] = "Please enter a valid URL",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.ErrorPerformingCheck"] = "Error performing manual check",

                ["Plugins.Misc.PaymentGuard.Fields.PageUrl"] = "Page URL",
                ["Plugins.Misc.PaymentGuard.Fields.PageUrl.Hint"] = "The URL of the monitored page",
                ["Plugins.Misc.PaymentGuard.Fields.TotalScriptsFound"] = "Total Scripts",
                ["Plugins.Misc.PaymentGuard.Fields.TotalScriptsFound.Hint"] = "Total number of scripts found",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedScriptsCount"] = "Authorized Scripts",
                ["Plugins.Misc.PaymentGuard.Fields.AuthorizedScriptsCount.Hint"] = "Number of authorized scripts",
                ["Plugins.Misc.PaymentGuard.Fields.UnauthorizedScriptsCount"] = "Unauthorized Scripts",
                ["Plugins.Misc.PaymentGuard.Fields.UnauthorizedScriptsCount.Hint"] = "Number of unauthorized scripts",
                ["Plugins.Misc.PaymentGuard.Fields.HasUnauthorizedScripts"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.Fields.HasUnauthorizedScripts.Hint"] = "Whether unauthorized scripts were detected",
                ["Plugins.Misc.PaymentGuard.Fields.CheckedOnUtc"] = "Checked On",
                ["Plugins.Misc.PaymentGuard.Fields.CheckedOnUtc.Hint"] = "Date and time when check was performed",
                ["Plugins.Misc.PaymentGuard.Fields.CheckType"] = "Check Type",
                ["Plugins.Misc.PaymentGuard.Fields.CheckType.Hint"] = "Type of monitoring check",
                ["Plugins.Misc.PaymentGuard.Fields.AlertSent"] = "Alert Sent",
                ["Plugins.Misc.PaymentGuard.Fields.AlertSent.Hint"] = "Whether an alert was sent",

                // Search Fields for Monitoring Logs
                ["Plugins.Misc.PaymentGuard.Fields.SearchPageUrl"] = "Page URL",
                ["Plugins.Misc.PaymentGuard.Fields.SearchPageUrl.Hint"] = "Filter by page URL",
                ["Plugins.Misc.PaymentGuard.Fields.SearchHasUnauthorizedScripts"] = "Compliance Status",
                ["Plugins.Misc.PaymentGuard.Fields.SearchHasUnauthorizedScripts.Hint"] = "Filter by compliance status",
                ["Plugins.Misc.PaymentGuard.Fields.SearchDateFrom"] = "Date From",
                ["Plugins.Misc.PaymentGuard.Fields.SearchDateFrom.Hint"] = "Filter from this date",
                ["Plugins.Misc.PaymentGuard.Fields.SearchDateTo"] = "Date To",
                ["Plugins.Misc.PaymentGuard.Fields.SearchDateTo.Hint"] = "Filter to this date",

                // Page Titles
                ["Plugins.Misc.PaymentGuard.AddNewScript"] = "Add New Authorized Script",
                ["Plugins.Misc.PaymentGuard.EditScript"] = "Edit Authorized Script",
                ["Plugins.Misc.PaymentGuard.BackToList"] = "back to script list",
                ["Plugins.Misc.PaymentGuard.ManageScripts"] = "Manage Scripts",
                ["Plugins.Misc.PaymentGuard.Alerts.List"] = "Compliance Alerts",
                ["Plugins.Misc.PaymentGuard.Alerts.Details"] = "Alert Details",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.List"] = "Monitoring Logs",
                ["Plugins.Misc.PaymentGuard.MonitoringLogs.Details"] = "Log Details",
                ["Plugins.Misc.PaymentGuard.BackToAlerts"] = "back to alerts list",
                ["Plugins.Misc.PaymentGuard.BackToLogs"] = "back to monitoring logs",

                // Configuration Sections
                ["Plugins.Misc.PaymentGuard.Configuration.GeneralSettings"] = "General Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.AlertSettings"] = "Alert Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.SecuritySettings"] = "Security Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.MaintenanceSettings"] = "Maintenance Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.MaintenanceSettings.Help"] = "Configure automatic cleanup and caching behavior",
                ["Plugins.Misc.PaymentGuard.Configuration.ApiSettings"] = "API Settings",
                ["Plugins.Misc.PaymentGuard.Configuration.ApiSettings.Help"] = "Configure API rate limiting and security settings",

                // Dashboard Labels
                ["Plugins.Misc.PaymentGuard.Dashboard.PageTitle"] = "PaymentGuard Dashboard",
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalScriptsMonitoredLabel"] = "Total Scripts Monitored",
                ["Plugins.Misc.PaymentGuard.Dashboard.AuthorizedScriptsLabel"] = "Authorized Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.UnauthorizedScriptsLabel"] = "Unauthorized Scripts",
                
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationLabel"] = "Scripts Need Verification",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationDetail"] = "You have <strong>{0}</strong> authorized scripts that haven't been verified in over 30 days.",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationDaysDue"] = "days overdue",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsVerificationReview"] = "Review Scripts",


                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceScoreLabel"] = "Compliance Score",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview"] = "Compliance Overview",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceOverview.Score"] = "Compliance Score:",
                ["Plugins.Misc.PaymentGuard.Dashboard.RecentActivity"] = "Recent Activity",
                ["Plugins.Misc.PaymentGuard.Dashboard.Authorized"] = "AUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Unauthorized"] = "UNAUTHORIZED",
                ["Plugins.Misc.PaymentGuard.Dashboard.Checks"] = "CHECKS",
                ["Plugins.Misc.PaymentGuard.Dashboard.LastMonitoringCheck"] = "Last monitoring check:",
                ["Plugins.Misc.PaymentGuard.Dashboard.TotalAlertsGenerated"] = "Total alerts generated:",
                ["Plugins.Misc.PaymentGuard.Dashboard.ScriptsUnderProtection"] = "Scripts under protection:",
                ["Plugins.Misc.PaymentGuard.Dashboard.MostCommonUnauthorizedScripts"] = "Most Common Unauthorized Scripts:",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceExcellent"] = "Excellent",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceGood"] = "Good",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceNeedsAttention"] = "Needs Attention",
                ["Plugins.Misc.PaymentGuard.Dashboard.ComplianceTrends"] = "Compliance Trends (Last 30 Days)",
                ["Plugins.Misc.PaymentGuard.Dashboard.AlertTypesDistribution"] = "Alert Types Distribution",
                ["Plugins.Misc.PaymentGuard.Dashboard.MonitoringActivity"] = "Monitoring Activity",
                ["Plugins.Misc.PaymentGuard.Dashboard.RiskLevelBreakdown"] = "Risk Level Breakdown",
                ["Plugins.Misc.PaymentGuard.Dashboard.PerformanceMetrics"] = "Performance Metrics",
                ["Plugins.Misc.PaymentGuard.Dashboard.TopViolatingScripts"] = "Top Violating Scripts",
                ["Plugins.Misc.PaymentGuard.Dashboard.AvgResponseTime"] = "Avg Response Time",
                ["Plugins.Misc.PaymentGuard.Dashboard.SystemUptime"] = "System Uptime",
                ["Plugins.Misc.PaymentGuard.Dashboard.CacheHitRate"] = "Cache Hit Rate",
                ["Plugins.Misc.PaymentGuard.Dashboard.ApiCallsThisWeek"] = "API Calls This Week",
                ["Plugins.Misc.PaymentGuard.Dashboard.SuccessRate"] = "Success Rate",
                ["Plugins.Misc.PaymentGuard.Dashboard.AvgResolutionTime"] = "Avg Resolution Time",
                ["Plugins.Misc.PaymentGuard.Dashboard.ResolvedThisWeek"] = "Resolved This Week",
                ["Plugins.Misc.PaymentGuard.Dashboard.NewAlerts"] = "New Alerts",
                ["Plugins.Misc.PaymentGuard.Dashboard.SecurityPosture"] = "Security Posture",
                ["Plugins.Misc.PaymentGuard.Dashboard.Script"] = "Script",
                ["Plugins.Misc.PaymentGuard.Dashboard.Violations"] = "Violations",
                ["Plugins.Misc.PaymentGuard.Dashboard.Risk"] = "Risk",
                ["Plugins.Misc.PaymentGuard.Dashboard.LastSeen"] = "Last Seen",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoViolationsDetected"] = "No violations detected",
                ["Plugins.Misc.PaymentGuard.Dashboard.Refresh"] = "Refresh",
                ["Plugins.Misc.PaymentGuard.Dashboard.Loading"] = "Loading...",
                ["Plugins.Misc.PaymentGuard.Dashboard.SecureCompliant"] = "Secure & Compliant",
                ["Plugins.Misc.PaymentGuard.Dashboard.NewThisWeek"] = "new this week",
                ["Plugins.Misc.PaymentGuard.Dashboard.VsLastWeek"] = "vs last week",
                ["Plugins.Misc.PaymentGuard.Dashboard.NoAlertsInSelectedPeriod"] = "No alerts in selected period",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last7Days"] = "Last 7 Days",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last30Days"] = "Last 30 Days",
                ["Plugins.Misc.PaymentGuard.Dashboard.Last90Days"] = "Last 90 Days",


                // Action Messages
                ["Plugins.Misc.PaymentGuard.ScriptAdded"] = "Script has been added successfully",
                ["Plugins.Misc.PaymentGuard.ScriptUpdated"] = "Script has been updated successfully",
                ["Plugins.Misc.PaymentGuard.ScriptDeleted"] = "Script has been deleted successfully",
                ["Plugins.Misc.PaymentGuard.Alerts.Resolved"] = "Alert has been resolved successfully",
                ["Plugins.Misc.PaymentGuard.Alerts.AlreadyResolved"] = "Alert is already resolved",
                ["Plugins.Misc.PaymentGuard.Alerts.NotFound"] = "Alert not found",
                ["Plugins.Misc.PaymentGuard.Alerts.Deleted"] = "Alert has been deleted successfully",
                ["Plugins.Misc.PaymentGuard.MonitoringCheck.Completed"] = "Manual monitoring check completed",
                ["Plugins.Misc.PaymentGuard.MonitoringCheck.Failed"] = "Manual monitoring check failed",
                ["Plugins.Misc.PaymentGuard.Export.Success"] = "Data exported successfully",
                ["Plugins.Misc.PaymentGuard.Export.Failed"] = "Error exporting data",
                ["Plugins.Misc.PaymentGuard.BulkResolve.Success"] = "Successfully resolved selected alerts",
                ["Plugins.Misc.PaymentGuard.BulkDelete.Success"] = "Successfully deleted selected alerts",

                // Buttons and Actions
                ["Plugins.Misc.PaymentGuard.ResolveAlert"] = "Resolve Alert",
                ["Plugins.Misc.PaymentGuard.ViewDetails"] = "View Details",
                ["Plugins.Misc.PaymentGuard.RunManualCheck"] = "Run Manual Check",
                ["Plugins.Misc.PaymentGuard.RefreshAlerts"] = "Refresh Alerts",
                ["Plugins.Misc.PaymentGuard.ExportToCsv"] = "Export to CSV",
                ["Plugins.Misc.PaymentGuard.ExportToPdf"] = "Export to PDF",
                ["Plugins.Misc.PaymentGuard.GenerateReport"] = "Generate Report",
                ["Plugins.Misc.PaymentGuard.BulkResolve"] = "Bulk Resolve",
                ["Plugins.Misc.PaymentGuard.BulkDelete"] = "Bulk Delete",
                ["Plugins.Misc.PaymentGuard.SelectAll"] = "Select All",

                // Status Labels
                ["Plugins.Misc.PaymentGuard.Status.Resolved"] = "Resolved",
                ["Plugins.Misc.PaymentGuard.Status.Unresolved"] = "Unresolved",
                ["Plugins.Misc.PaymentGuard.Status.Compliant"] = "Compliant",
                ["Plugins.Misc.PaymentGuard.Status.HasIssues"] = "Has Issues",
                ["Plugins.Misc.PaymentGuard.AlertType.UnauthorizedScript"] = "Unauthorized Script",
                ["Plugins.Misc.PaymentGuard.AlertType.CSPViolation"] = "CSP Violation",
                ["Plugins.Misc.PaymentGuard.AlertType.IntegrityFailure"] = "Integrity Failure",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Critical"] = "Critical",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Warning"] = "Warning",
                ["Plugins.Misc.PaymentGuard.AlertLevel.Info"] = "Info"
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

            task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardCleanupTask");
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Misc.PaymentGuard.Tasks.PaymentGuardMaintenanceTask");
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