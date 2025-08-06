using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
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

        public PaymentGuardController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            ISettingService settingService,
            IStoreService storeService,
            IStoreContext storeContext,
            IWorkContext workContext,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IPermissionService permissionService)
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
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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

            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }

        public async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var model = new AuthorizedScriptSearchModel();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> List(AuthorizedScriptSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var model = new AuthorizedScriptModel();
            await PrepareAuthorizedScriptModelAsync(model, null);

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        public async Task<IActionResult> Create(AuthorizedScriptModel model, bool continueEditing)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
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
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var script = await _authorizedScriptService.GetAuthorizedScriptByIdAsync(id);
            if (script != null)
            {
                await _authorizedScriptService.DeleteAuthorizedScriptAsync(script);
                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.PaymentGuard.ScriptDeleted"));
            }

            return RedirectToAction("List");
        }

        public async Task<IActionResult> Dashboard()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePlugins))
                return AccessDeniedView();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var report = await _monitoringService.GenerateComplianceReportAsync(storeScope, DateTime.UtcNow.AddDays(-30));

            var model = new DashboardModel
            {
                TotalScriptsMonitored = report.TotalScriptsMonitored,
                AuthorizedScriptsCount = report.AuthorizedScriptsCount,
                UnauthorizedScriptsCount = report.UnauthorizedScriptsCount,
                ComplianceScore = report.ComplianceScore,
                LastCheckDate = report.LastCheckDate,
                TotalChecksPerformed = report.TotalChecksPerformed,
                AlertsGenerated = report.AlertsGenerated,
                MostCommonUnauthorizedScripts = report.MostCommonUnauthorizedScripts
            };

            return View(model);
        }

        #endregion
    }
}