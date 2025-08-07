using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.PaymentGuard.Areas.Admin.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class PaymentGuardController : BasePluginController
    {
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IMonitoringService _monitoringService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;
        private readonly IExportService _exportService;
        private readonly IComplianceAlertService _complianceAlertService;
        private readonly ILogger _logger;
        private readonly IDashboardService _dashboardService;

        public PaymentGuardController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            ISettingService settingService,
            IStoreService storeService,
            IStoreContext storeContext,
            IWorkContext workContext,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            IExportService exportService,
            IComplianceAlertService complianceAlertService,
            ILogger logger,
            IDashboardService dashboardService)
        {
            _authorizedScriptService = authorizedScriptService;
            _monitoringService = monitoringService;
            _settingService = settingService;
            _storeService = storeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _notificationService = notificationService;
            _localizationService = localizationService;
            _permissionService = permissionService;
            _exportService = exportService;
            _complianceAlertService = complianceAlertService;
            _logger = logger;
            _dashboardService = dashboardService;
        }

        #region Utilities

        private async Task PrepareAuthorizedScriptModelAsync(AuthorizedScriptModel model, AuthorizedScript script)
        {
            // Prepare dropdown lists, etc.
            model.AvailableRiskLevels = new List<SelectListItem>
            {
                new() { Value = "1", Text = "Low" },
                new() { Value = "2", Text = "Medium" },
                new() { Value = "3", Text = "High" }
            };

            model.AvailableSources = new List<SelectListItem>
            {
                new() { Value = "internal", Text = "Internal" },
                new() { Value = "third-party", Text = "Third Party" },
                new() { Value = "payment-gateway", Text = "Payment Gateway" },
                new() { Value = "analytics", Text = "Analytics" },
                new() { Value = "marketing", Text = "Marketing" }
            };
        }

        private string GetRiskLevelText(int riskLevel)
        {
            return riskLevel switch
            {
                1 => "Low",
                2 => "Medium",
                3 => "High",
                _ => "Unknown"
            };
        }

        #endregion

        #region Methods
        
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(storeScope);

            var model = new ConfigurationModel
            {
                IsEnabled = settings.IsEnabled,
                MonitoringFrequency = settings.MonitoringFrequency,
                AlertEmail = settings.AlertEmail,
                EnableEmailAlerts = settings.EnableEmailAlerts,
                EnableCSPHeaders = settings.EnableCSPHeaders,
                EnableSRIValidation = settings.EnableSRIValidation,
                CSPPolicy = settings.CSPPolicy,
                EnableDetailedLogging = settings.EnableDetailedLogging,
                MonitoredPages = settings.MonitoredPages,
                MaxAlertFrequency = settings.MaxAlertFrequency,

                // Add the missing settings
                LogRetentionDays = settings.LogRetentionDays,
                AlertRetentionDays = settings.AlertRetentionDays,
                EnableAutomaticCleanup = settings.EnableAutomaticCleanup,
                CacheExpirationMinutes = settings.CacheExpirationMinutes,
                EnableApiRateLimit = settings.EnableApiRateLimit,
                ApiRateLimitPerHour = settings.ApiRateLimitPerHour,
                WhitelistedIPs = settings.WhitelistedIPs,

                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.IsEnabled_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.IsEnabled, storeScope);
                model.MonitoringFrequency_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MonitoringFrequency, storeScope);
                model.AlertEmail_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AlertEmail, storeScope);
                model.EnableEmailAlerts_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableEmailAlerts, storeScope);
                model.EnableCSPHeaders_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableCSPHeaders, storeScope);
                model.EnableSRIValidation_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableSRIValidation, storeScope);
                model.CSPPolicy_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CSPPolicy, storeScope);
                model.EnableDetailedLogging_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableDetailedLogging, storeScope);
                model.MonitoredPages_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MonitoredPages, storeScope);
                model.MaxAlertFrequency_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MaxAlertFrequency, storeScope);

                // Add the missing override checks
                model.LogRetentionDays_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.LogRetentionDays, storeScope);
                model.AlertRetentionDays_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.AlertRetentionDays, storeScope);
                model.EnableAutomaticCleanup_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableAutomaticCleanup, storeScope);
                model.CacheExpirationMinutes_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.CacheExpirationMinutes, storeScope);
                model.EnableApiRateLimit_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.EnableApiRateLimit, storeScope);
                model.ApiRateLimitPerHour_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.ApiRateLimitPerHour, storeScope);
                model.WhitelistedIPs_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.WhitelistedIPs, storeScope);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManagePaymentGuard))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(storeScope);

            settings.IsEnabled = model.IsEnabled;
            settings.MonitoringFrequency = model.MonitoringFrequency;
            settings.AlertEmail = model.AlertEmail;
            settings.EnableEmailAlerts = model.EnableEmailAlerts;
            settings.EnableCSPHeaders = model.EnableCSPHeaders;
            settings.EnableSRIValidation = model.EnableSRIValidation;
            settings.CSPPolicy = model.CSPPolicy;
            settings.EnableDetailedLogging = model.EnableDetailedLogging;
            settings.MonitoredPages = model.MonitoredPages;
            settings.MaxAlertFrequency = model.MaxAlertFrequency;

            // Add the missing settings assignments
            settings.LogRetentionDays = model.LogRetentionDays;
            settings.AlertRetentionDays = model.AlertRetentionDays;
            settings.EnableAutomaticCleanup = model.EnableAutomaticCleanup;
            settings.CacheExpirationMinutes = model.CacheExpirationMinutes;
            settings.EnableApiRateLimit = model.EnableApiRateLimit;
            settings.ApiRateLimitPerHour = model.ApiRateLimitPerHour;
            settings.WhitelistedIPs = model.WhitelistedIPs;

            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IsEnabled, model.IsEnabled_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MonitoringFrequency, model.MonitoringFrequency_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AlertEmail, model.AlertEmail_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableEmailAlerts, model.EnableEmailAlerts_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableCSPHeaders, model.EnableCSPHeaders_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableSRIValidation, model.EnableSRIValidation_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CSPPolicy, model.CSPPolicy_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableDetailedLogging, model.EnableDetailedLogging_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MonitoredPages, model.MonitoredPages_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MaxAlertFrequency, model.MaxAlertFrequency_OverrideForStore, storeScope, false);

            // Add the missing SaveSettingOverridablePerStoreAsync calls
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.LogRetentionDays, model.LogRetentionDays_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.AlertRetentionDays, model.AlertRetentionDays_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableAutomaticCleanup, model.EnableAutomaticCleanup_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.CacheExpirationMinutes, model.CacheExpirationMinutes_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.EnableApiRateLimit, model.EnableApiRateLimit_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ApiRateLimitPerHour, model.ApiRateLimitPerHour_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.WhitelistedIPs, model.WhitelistedIPs_OverrideForStore, storeScope, false);

            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }
        
        public async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var model = new AuthorizedScriptSearchModel();
            model.AvailableRiskLevels = new List<SelectListItem>
            {
                new() { Value = "1", Text = "Low" },
                new() { Value = "2", Text = "Medium" },
                new() { Value = "3", Text = "High" }
            };

            model.AvailableSources = new List<SelectListItem>
            {
                new() { Value = "internal", Text = "Internal" },
                new() { Value = "third-party", Text = "Third Party" },
                new() { Value = "payment-gateway", Text = "Payment Gateway" },
                new() { Value = "analytics", Text = "Analytics" },
                new() { Value = "marketing", Text = "Marketing" }
            };

            model.AvailableActiveOptions = new List<SelectListItem>
            {
                new() { Value = "0", Text = "All" },
                new() { Value = "1", Text = "Active" },
                new() { Value = "2", Text = "In-Active" },
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> List(AuthorizedScriptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return await AccessDeniedDataTablesJson();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var scripts = await _authorizedScriptService.GetAllAuthorizedScriptsAsync(
                storeId: storeScope,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = new AuthorizedScriptListModel().PrepareToGrid(searchModel, scripts, () =>
            {
                return scripts.Select(script => new AuthorizedScriptModel
                {
                    Id = script.Id,
                    ScriptUrl = script.ScriptUrl,
                    Purpose = script.Purpose,
                    RiskLevel = script.RiskLevel,
                    RiskLevelText = GetRiskLevelText(script.RiskLevel),
                    IsActive = script.IsActive,
                    AuthorizedBy = script.AuthorizedBy,
                    AuthorizedOnUtc = script.AuthorizedOnUtc,
                    LastVerifiedUtc = script.LastVerifiedUtc,
                    Source = script.Source
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> Create()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var model = new AuthorizedScriptModel();
            await PrepareAuthorizedScriptModelAsync(model, null);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public async Task<IActionResult> Create(AuthorizedScriptModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            if (ModelState.IsValid)
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var currentUser = await _workContext.GetCurrentCustomerAsync();

                // Check if script already exists
                var existingScript = await _authorizedScriptService.GetAuthorizedScriptByUrlAsync(model.ScriptUrl, storeScope);
                if (existingScript != null)
                {
                    ModelState.AddModelError("ScriptUrl", "A script with this URL already exists.");
                    await PrepareAuthorizedScriptModelAsync(model, null);
                    return View(model);
                }

                var script = new AuthorizedScript
                {
                    ScriptUrl = model.ScriptUrl,
                    Purpose = model.Purpose,
                    Justification = model.Justification,
                    RiskLevel = model.RiskLevel,
                    IsActive = model.IsActive,
                    AuthorizedBy = currentUser.Email,
                    AuthorizedOnUtc = DateTime.UtcNow,
                    LastVerifiedUtc = DateTime.UtcNow,
                    Source = model.Source,
                    StoreId = storeScope
                };

                // Extract domain from URL
                try
                {
                    var uri = new Uri(script.ScriptUrl);
                    script.Domain = uri.Host;
                }
                catch
                {
                    script.Domain = "unknown";
                }

                // Generate hash if script is external
                if (model.GenerateHash && script.ScriptUrl.StartsWith("http"))
                {
                    script.ScriptHash = await _authorizedScriptService.GenerateScriptHashAsync(script.ScriptUrl);
                    script.HashAlgorithm = "sha384";
                }

                await _authorizedScriptService.InsertAuthorizedScriptAsync(script);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptAdded"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = script.Id });
            }

            await PrepareAuthorizedScriptModelAsync(model, null);
            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(id);
            if (script == null)
                return RedirectToAction("List");

            var model = new AuthorizedScriptModel
            {
                Id = script.Id,
                ScriptUrl = script.ScriptUrl,
                ScriptHash = script.ScriptHash,
                Purpose = script.Purpose,
                Justification = script.Justification,
                RiskLevel = script.RiskLevel,
                IsActive = script.IsActive,
                AuthorizedBy = script.AuthorizedBy,
                AuthorizedOnUtc = script.AuthorizedOnUtc,
                LastVerifiedUtc = script.LastVerifiedUtc,
                Source = script.Source
            };

            await PrepareAuthorizedScriptModelAsync(model, script);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public async Task<IActionResult> Edit(AuthorizedScriptModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(model.Id);
            if (script == null)
                return RedirectToAction("List");

            if (ModelState.IsValid)
            {
                script.ScriptUrl = model.ScriptUrl;
                script.Purpose = model.Purpose;
                script.Justification = model.Justification;
                script.RiskLevel = model.RiskLevel;
                script.IsActive = model.IsActive;
                script.Source = model.Source;

                // Update domain if URL changed
                try
                {
                    var uri = new Uri(script.ScriptUrl);
                    script.Domain = uri.Host;
                }
                catch
                {
                    script.Domain = "unknown";
                }

                // Regenerate hash if requested
                if (model.GenerateHash && script.ScriptUrl.StartsWith("http"))
                {
                    script.ScriptHash = await _authorizedScriptService.GenerateScriptHashAsync(script.ScriptUrl);
                    script.LastVerifiedUtc = DateTime.UtcNow;
                }

                await _authorizedScriptService.UpdateAuthorizedScriptAsync(script);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptUpdated"));

                if (!continueEditing)
                    return RedirectToAction("List");

                return RedirectToAction("Edit", new { id = script.Id });
            }

            await PrepareAuthorizedScriptModelAsync(model, script);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ManageAuthorizedScripts))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(id);
            if (script != null)
            {
                await _authorizedScriptService.DeleteAuthorizedScriptAsync(script);
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptDeleted"));
            }

            return RedirectToAction("List");
        }

        public async Task<IActionResult> Dashboard(int days = 30)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var model = await _dashboardService.GetDashboardDataAsync(storeScope, days);

                return View(model);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error loading PaymentGuard dashboard", ex);
                _notificationService.ErrorNotification("Error loading dashboard data");

                // Return basic model in case of error
                var basicModel = new DashboardModel
                {
                    TotalScriptsMonitored = 0,
                    AuthorizedScriptsCount = 0,
                    UnauthorizedScriptsCount = 0,
                    ComplianceScore = 0,
                    LastCheckDate = DateTime.UtcNow,
                    TotalChecksPerformed = 0,
                    AlertsGenerated = 0
                };

                return View(basicModel);
            }
        }

        // Add method for dashboard data refresh via AJAX
        [HttpPost]
        public async Task<IActionResult> RefreshDashboardData(int days = 30)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var model = await _dashboardService.GetDashboardDataAsync(storeScope, days);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        complianceScore = model.ComplianceScore,
                        totalScripts = model.TotalScriptsMonitored,
                        authorizedScripts = model.AuthorizedScriptsCount,
                        unauthorizedScripts = model.UnauthorizedScriptsCount,
                        alertsGenerated = model.AlertsGenerated,
                        lastCheck = model.LastCheckDate.ToString("MMM dd, yyyy HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error refreshing dashboard data", ex);
                return Json(new { success = false, message = "Error refreshing dashboard" });
            }
        }

        #endregion

        #region Monitoring Logs

        public async Task<IActionResult> MonitoringLogs()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            var model = new MonitoringLogSearchModel();

            // Prepare dropdown lists
            model.AvailableUnauthorizedOptions = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "false", Text = "Compliant" },
                new() { Value = "true", Text = "Has Issues" }
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> MonitoringLogsList(MonitoringLogSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return await AccessDeniedDataTablesJson();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

            bool? hasUnauthorizedScripts = null;
            if (!string.IsNullOrEmpty(searchModel.SearchHasUnauthorizedScripts) &&
                bool.TryParse(searchModel.SearchHasUnauthorizedScripts, out var hasIssues))
                hasUnauthorizedScripts = hasIssues;

            var logs = await _monitoringService.GetMonitoringLogsAsync(
                storeId: storeScope,
                hasUnauthorizedScripts: hasUnauthorizedScripts,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = new MonitoringLogListModel().PrepareToGrid(searchModel, logs, () =>
            {
                return logs.Select(log => new MonitoringLogModel
                {
                    Id = log.Id,
                    PageUrl = log.PageUrl,
                    TotalScriptsFound = log.TotalScriptsFound,
                    AuthorizedScriptsCount = log.AuthorizedScriptsCount,
                    UnauthorizedScriptsCount = log.UnauthorizedScriptsCount,
                    HasUnauthorizedScripts = log.HasUnauthorizedScripts,
                    CheckedOnUtc = log.CheckedOnUtc,
                    CheckType = log.CheckType,
                    AlertSent = log.AlertSent
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> MonitoringLogDetails(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return AccessDeniedView();

            var log = await _monitoringService.GetMonitoringLogByIdAsync(id);
            if (log == null)
                return RedirectToAction("MonitoringLogs");

            var model = new MonitoringLogModel
            {
                Id = log.Id,
                PageUrl = log.PageUrl,
                TotalScriptsFound = log.TotalScriptsFound,
                AuthorizedScriptsCount = log.AuthorizedScriptsCount,
                UnauthorizedScriptsCount = log.UnauthorizedScriptsCount,
                HasUnauthorizedScripts = log.HasUnauthorizedScripts,
                CheckedOnUtc = log.CheckedOnUtc,
                CheckType = log.CheckType,
                AlertSent = log.AlertSent
            };

            // Deserialize JSON data
            try
            {
                if (!string.IsNullOrEmpty(log.DetectedScripts))
                    model.DetectedScripts = JsonSerializer.Deserialize<List<string>>(log.DetectedScripts) ?? new List<string>();

                if (!string.IsNullOrEmpty(log.UnauthorizedScripts))
                    model.UnauthorizedScripts = JsonSerializer.Deserialize<List<string>>(log.UnauthorizedScripts) ?? new List<string>();

                if (!string.IsNullOrEmpty(log.HttpHeaders))
                    model.SecurityHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(log.HttpHeaders) ?? new Dictionary<string, string>();
            }
            catch (JsonException ex)
            {
                await _logger.ErrorAsync($"Error deserializing monitoring log data for log ID {id}", ex);
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> RunManualCheck(string pageUrl)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceReports))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var log = await _monitoringService.PerformMonitoringCheckAsync(pageUrl, storeScope);

                return Json(new
                {
                    success = true,
                    message = $"Manual check completed. Found {log.TotalScriptsFound} scripts, {log.UnauthorizedScriptsCount} unauthorized.",
                    logId = log.Id
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error performing manual monitoring check for {pageUrl}", ex);
                return Json(new { success = false, message = "Error performing check: " + ex.Message });
            }
        }

        #endregion

        #region Export Actions

        [HttpPost]
        public async Task<IActionResult> ExportScriptsToCsv(AuthorizedScriptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var overrideIsActive = searchModel.SearchIsActiveId == 0 ? null : (bool?)(searchModel.SearchIsActiveId == 1);

                // Get all scripts based on search criteria
                var scripts = await _authorizedScriptService.GetAllAuthorizedScriptsAsync(
                    storeId: storeScope,
                    isActive: overrideIsActive);

                var csvData = await _exportService.ExportAuthorizedScriptsToCsvAsync(scripts.ToList());

                var fileName = $"authorized-scripts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting authorized scripts to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("List");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportAlertsToCsv(ComplianceAlertSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

                bool? isResolved = null;
                if (!string.IsNullOrEmpty(searchModel.SearchIsResolved) && bool.TryParse(searchModel.SearchIsResolved, out var resolved))
                    isResolved = resolved;

                var alerts = await _complianceAlertService.GetAllComplianceAlertsAsync(
                    storeId: storeScope,
                    alertType: searchModel.SearchAlertType,
                    alertLevel: searchModel.SearchAlertLevel,
                    isResolved: isResolved);

                var csvData = await _exportService.ExportComplianceAlertsToCsvAsync(alerts.ToList());

                var fileName = $"compliance-alerts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting compliance alerts to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("List", "ComplianceAlert");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportMonitoringLogsToCsv(MonitoringLogSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

                bool? hasUnauthorizedScripts = null;
                if (!string.IsNullOrEmpty(searchModel.SearchHasUnauthorizedScripts) &&
                    bool.TryParse(searchModel.SearchHasUnauthorizedScripts, out var hasIssues))
                    hasUnauthorizedScripts = hasIssues;

                var logs = await _monitoringService.GetMonitoringLogsAsync(
                    storeId: storeScope,
                    hasUnauthorizedScripts: hasUnauthorizedScripts);

                var csvData = await _exportService.ExportMonitoringLogsToCsvAsync(logs.ToList());

                var fileName = $"monitoring-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(csvData, "text/csv", fileName);
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error exporting monitoring logs to CSV", ex);
                _notificationService.ErrorNotification("Error exporting data to CSV");
                return RedirectToAction("MonitoringLogs");
            }
        }

        public async Task<IActionResult> GenerateComplianceReport(DateTime? fromDate = null, DateTime? toDate = null, string format = "pdf")
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            try
            {
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

                if (format.ToLower() == "pdf")
                {
                    var pdfData = await _exportService.GenerateComplianceReportPdfAsync(storeScope, fromDate, toDate);
                    var fileName = $"compliance-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
                    return File(pdfData, "application/pdf", fileName);
                }
                else
                {
                    // Default to HTML preview
                    var report = await _monitoringService.GenerateComplianceReportAsync(storeScope, fromDate, toDate);
                    var model = new ComplianceReportModel
                    {
                        Report = report,
                        FromDate = fromDate,
                        ToDate = toDate,
                        StoreName = (await _storeContext.GetCurrentStoreAsync()).Name
                    };
                    return View("ComplianceReport", model);
                }
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error generating compliance report", ex);
                _notificationService.ErrorNotification("Error generating compliance report");
                return RedirectToAction("Dashboard");
            }
        }

        #endregion

        #region Bulk Operations

        [HttpPost]
        public async Task<IActionResult> BulkResolveAlerts(IList<int> alertIds)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                if (alertIds == null || !alertIds.Any())
                    return Json(new { success = false, message = "No alerts selected" });

                var currentUser = await _workContext.GetCurrentCustomerAsync();
                var resolvedCount = 0;

                foreach (var alertId in alertIds)
                {
                    var alert = await _complianceAlertService.ResolveAlertAsync(alertId, currentUser.Email);
                    if (alert != null)
                        resolvedCount++;
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully resolved {resolvedCount} alert(s)",
                    resolvedCount = resolvedCount
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in bulk resolve alerts operation", ex);
                return Json(new { success = false, message = "Error resolving alerts" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> BulkDeleteAlerts(IList<int> alertIds)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return Json(new { success = false, message = "Access denied" });

            try
            {
                if (alertIds == null || !alertIds.Any())
                    return Json(new { success = false, message = "No alerts selected" });

                var deletedCount = 0;

                foreach (var alertId in alertIds)
                {
                    var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(alertId);
                    if (alert != null)
                    {
                        await _complianceAlertService.DeleteComplianceAlertAsync(alert);
                        deletedCount++;
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Successfully deleted {deletedCount} alert(s)",
                    deletedCount = deletedCount
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in bulk delete alerts operation", ex);
                return Json(new { success = false, message = "Error deleting alerts" });
            }
        }

        #endregion
    }
}