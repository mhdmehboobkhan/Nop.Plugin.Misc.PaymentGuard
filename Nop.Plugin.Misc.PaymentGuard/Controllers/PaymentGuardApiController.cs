using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Logging;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    public class PaymentGuardApiController : BasePluginController
    {
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IMonitoringService _monitoringService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;

        public PaymentGuardApiController(IAuthorizedScriptService authorizedScriptService,
            IMonitoringService monitoringService,
            IStoreContext storeContext,
            ILogger logger)
        {
            _authorizedScriptService = authorizedScriptService;
            _monitoringService = monitoringService;
            _storeContext = storeContext;
            _logger = logger;
        }

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
                await _logger.WarningAsync($"Security violation reported: {request.ViolationType} - {request.ScriptUrl} on {request.PageUrl}");

                // TODO: Store violation in database and potentially send alerts


                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing violation report", ex);
                return Json(new { success = false });
            }
        }

        [HttpPost]
        [Route("Plugins/PaymentGuard/Api/ReportCSPViolation")]
        public async Task<IActionResult> ReportCSPViolation([FromBody] ReportCSPViolationRequest request)
        {
            try
            {
                await _logger.WarningAsync($"CSP violation reported on {request.PageUrl}: {request.Violation.BlockedURI} violated {request.Violation.ViolatedDirective}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error processing CSP violation report", ex);
                return Json(new { success = false });
            }
        }
    }
}