using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    public class PaymentGuardApiController : BasePluginController
    {
        #region Fields

        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IMonitoringService _monitoringService;
        private readonly IComplianceAlertService _complianceAlertService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public PaymentGuardApiController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            IComplianceAlertService complianceAlertService,
            IEmailAlertService emailAlertService,
            IStoreContext storeContext,
            IStoreService storeService,
            ISettingService settingService,
            ILogger logger)
        {
            _authorizedScriptService = authorizedScriptService;
            _monitoringService = monitoringService;
            _complianceAlertService = complianceAlertService;
            _emailAlertService = emailAlertService;
            _storeContext = storeContext;
            _storeService = storeService;
            _settingService = settingService;
            _logger = logger;
        }

        #endregion

        #region Methods

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ValidateScript")]
        public async Task<IActionResult> ValidateScript([FromBody] ValidateScriptRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var isAuthorized = await _authorizedScriptService.IsScriptAuthorizedAsync(request.ScriptUrl, store.Id);

                return Json(new { isAuthorized = isAuthorized });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error validating script {request.ScriptUrl}", ex);
                return Json(new { isAuthorized = false, error = "Validation failed" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportScripts")]
        public async Task<IActionResult> ReportScripts([FromBody] ReportScriptsRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();

                await _logger.InformationAsync($"Scripts reported from {request.PageUrl}: {request.Scripts.Count} scripts");

                // Process the reported scripts
                var unauthorizedScripts = new List<string>();

                foreach (var scriptUrl in request.Scripts)
                {
                    var isAuthorized = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, store.Id);
                    if (!isAuthorized)
                    {
                        unauthorizedScripts.Add(scriptUrl);
                    }
                }

                if (unauthorizedScripts.Any())
                {
                    await _logger.InsertLogAsync(LogLevel.Error, "Unauthorized scripts detected from client-side",
                        string.Join(", ", unauthorizedScripts));
                }

                return Json(new
                {
                    success = true,
                    unauthorizedCount = unauthorizedScripts.Count,
                    unauthorizedScripts = unauthorizedScripts
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing reported scripts from {request.PageUrl}", ex);
                return Json(new { success = false, error = "Processing failed" });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportViolation")]
        public async Task<IActionResult> ReportViolation([FromBody] ReportViolationRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                await _logger.WarningAsync($"Security violation reported: {request.ViolationType} - {request.ScriptUrl} on {request.PageUrl}");

                // Create compliance alert based on violation type
                ComplianceAlert alert = null;

                switch (request.ViolationType?.ToLower())
                {
                    case "unauthorized-script":
                        alert = await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent,
                                Source = "client-side-monitoring"
                            }));
                        break;

                    case "missing-sri-hash":
                        alert = await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Issue = "Script loaded without SRI hash",
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent
                            }));
                        break;

                    case "invalid-sri-format":
                        alert = await _complianceAlertService.CreateIntegrityFailureAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(new
                            {
                                ViolationType = request.ViolationType,
                                Issue = "Invalid SRI hash format",
                                Timestamp = request.Timestamp,
                                UserAgent = request.UserAgent
                            }));
                        break;

                    default:
                        // Generic security violation
                        alert = await _complianceAlertService.CreateUnauthorizedScriptAlertAsync(
                            store.Id,
                            request.ScriptUrl,
                            request.PageUrl,
                            JsonSerializer.Serialize(request));
                        break;
                }

                // Send email alert if enabled and alert was created (not duplicate)
                if (alert != null && settings.EnableEmailAlerts && !string.IsNullOrEmpty(settings.AlertEmail))
                {
                    // Check alert frequency to avoid spam
                    var shouldSendEmail = true;
                    if (settings.MaxAlertFrequency > 0)
                    {
                        var recentAlerts = await _complianceAlertService.GetRecentAlertsAsync(store.Id, settings.MaxAlertFrequency);
                        var similarRecentAlert = recentAlerts.FirstOrDefault(a =>
                            a.AlertType == request.ViolationType &&
                            a.ScriptUrl == request.ScriptUrl);

                        if (similarRecentAlert != null && similarRecentAlert.EmailSent)
                            shouldSendEmail = false;
                    }

                    if (shouldSendEmail)
                    {
                        try
                        {
                            if (request.ViolationType?.ToLower().Contains("unauthorized") == true)
                            {
                                await _emailAlertService.SendUnauthorizedScriptAlertAsync(
                                    settings.AlertEmail,
                                    new Domain.ScriptMonitoringLog
                                    {
                                        PageUrl = request.PageUrl,
                                        HasUnauthorizedScripts = true,
                                        UnauthorizedScripts = JsonSerializer.Serialize(new[] { request.ScriptUrl }),
                                        CheckedOnUtc = DateTime.UtcNow,
                                        TotalScriptsFound = 1,
                                        UnauthorizedScriptsCount = 1,
                                        AuthorizedScriptsCount = 0
                                    },
                                    store.Name);
                            }
                            else
                            {
                                await _emailAlertService.SendScriptChangeAlertAsync(
                                    settings.AlertEmail,
                                    request.ScriptUrl,
                                    store.Name);
                            }

                            // Mark alert as email sent
                            if (alert != null)
                            {
                                alert.EmailSent = true;
                                alert.EmailSentOnUtc = DateTime.UtcNow;
                                await _complianceAlertService.UpdateComplianceAlertAsync(alert);
                            }
                        }
                        catch (Exception emailEx)
                        {
                            await _logger.ErrorAsync($"Failed to send violation alert email", emailEx);
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    alertId = alert?.Id,
                    message = "Violation reported and processed successfully"
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing violation report: {request.ViolationType}", ex);
                return Json(new
                {
                    success = false,
                    error = "Failed to process violation report"
                });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportCSPViolation")]
        public async Task<IActionResult> ReportCSPViolation([FromBody] ReportCSPViolationRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                var violationDetails = JsonSerializer.Serialize(new
                {
                    BlockedURI = request.Violation.BlockedURI,
                    ViolatedDirective = request.Violation.ViolatedDirective,
                    EffectiveDirective = request.Violation.EffectiveDirective,
                    OriginalPolicy = request.Violation.OriginalPolicy,
                    SourceFile = request.Violation.SourceFile,
                    LineNumber = request.Violation.LineNumber,
                    ColumnNumber = request.Violation.ColumnNumber,
                    Timestamp = request.Timestamp,
                    UserAgent = request.UserAgent,
                    PageUrl = request.PageUrl
                }, new JsonSerializerOptions { WriteIndented = true });

                await _logger.WarningAsync($"CSP violation reported on {request.PageUrl}: {request.Violation.BlockedURI} violated {request.Violation.ViolatedDirective}");

                // Create compliance alert for CSP violation
                var alert = await _complianceAlertService.CreateCSPViolationAlertAsync(
                    store.Id,
                    request.PageUrl,
                    violationDetails);

                // Send email alert if enabled and alert was created (not duplicate)
                if (alert != null && settings.EnableEmailAlerts && !string.IsNullOrEmpty(settings.AlertEmail))
                {
                    try
                    {
                        await _emailAlertService.SendCSPViolationAlertAsync(
                            settings.AlertEmail,
                            violationDetails,
                            store.Name);

                        // Mark alert as email sent
                        alert.EmailSent = true;
                        alert.EmailSentOnUtc = DateTime.UtcNow;
                        await _complianceAlertService.UpdateComplianceAlertAsync(alert);
                    }
                    catch (Exception emailEx)
                    {
                        await _logger.ErrorAsync($"Failed to send CSP violation alert email", emailEx);
                    }
                }

                return Json(new
                {
                    success = true,
                    alertId = alert?.Id,
                    message = "CSP violation reported and processed successfully"
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing CSP violation report", ex);
                return Json(new
                {
                    success = false,
                    error = "Failed to process CSP violation report"
                });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ValidateScriptWithSRI")]
        public async Task<IActionResult> ValidateScriptWithSRI([FromBody] ValidateScriptWithSRIRequest request)
        {
            try
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var result = await _monitoringService.ValidateScriptWithSRIAsync(store.Id, request.ScriptUrl, request.Integrity);

                return Json(new
                {
                    success = true,
                    isAuthorized = result.IsAuthorized,
                    hasValidSRI = result.HasValidSRI,
                    sriError = result.SRIValidation?.Error
                });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error validating script with SRI {request.ScriptUrl}", ex);
                return Json(new { success = false, error = "Validation failed" });
            }
        }

        #endregion
    }
}