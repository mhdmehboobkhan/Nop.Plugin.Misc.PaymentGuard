using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
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

namespace Nop.Plugin.Misc.PaymentGuard.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    [AutoValidateAntiforgeryToken]
    public class ComplianceAlertController : BasePluginController
    {
        #region Fields

        private readonly IComplianceAlertService _complianceAlertService;
        private readonly IStoreService _storeService;
        private readonly IStoreContext _storeContext;
        private readonly IWorkContext _workContext;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public ComplianceAlertController(
            IComplianceAlertService complianceAlertService,
            IStoreService storeService,
            IStoreContext storeContext,
            IWorkContext workContext,
            INotificationService notificationService,
            ILocalizationService localizationService,
            IPermissionService permissionService)
        {
            _complianceAlertService = complianceAlertService;
            _storeService = storeService;
            _storeContext = storeContext;
            _workContext = workContext;
            _notificationService = notificationService;
            _localizationService = localizationService;
            _permissionService = permissionService;
        }

        #endregion

        #region Methods

        public async Task<IActionResult> List()
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var model = new ComplianceAlertSearchModel();

            // Prepare dropdown lists
            model.AvailableAlertTypes = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "unauthorized-script", Text = "Unauthorized Script" },
                new() { Value = "csp-violation", Text = "CSP Violation" },
                new() { Value = "integrity-failure", Text = "Integrity Failure" }
            };

            model.AvailableAlertLevels = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "info", Text = "Info" },
                new() { Value = "warning", Text = "Warning" },
                new() { Value = "critical", Text = "Critical" }
            };

            model.AvailableResolvedOptions = new List<SelectListItem>
            {
                new() { Value = "", Text = await _localizationService.GetResourceAsync("Admin.Common.All") },
                new() { Value = "false", Text = "Unresolved" },
                new() { Value = "true", Text = "Resolved" }
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> List(ComplianceAlertSearchModel searchModel)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return await AccessDeniedDataTablesJson();

            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();

            bool? isResolved = null;
            if (!string.IsNullOrEmpty(searchModel.SearchIsResolved) && bool.TryParse(searchModel.SearchIsResolved, out var resolved))
                isResolved = resolved;

            var alerts = await _complianceAlertService.GetAllComplianceAlertsAsync(
                storeId: storeScope,
                alertType: searchModel.SearchAlertType,
                alertLevel: searchModel.SearchAlertLevel,
                isResolved: isResolved,
                pageIndex: searchModel.Page - 1,
                pageSize: searchModel.PageSize);

            var model = new ComplianceAlertListModel().PrepareToGrid(searchModel, alerts, () =>
            {
                return alerts.Select(alert => new ComplianceAlertModel
                {
                    Id = alert.Id,
                    AlertType = alert.AlertType,
                    AlertLevel = alert.AlertLevel,
                    Message = alert.Message,
                    ScriptUrl = alert.ScriptUrl,
                    PageUrl = alert.PageUrl,
                    IsResolved = alert.IsResolved,
                    CreatedOnUtc = alert.CreatedOnUtc,
                    ResolvedOnUtc = alert.ResolvedOnUtc,
                    ResolvedBy = alert.ResolvedBy,
                    EmailSent = alert.EmailSent
                });
            });

            return Json(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(id);
            if (alert == null)
                return RedirectToAction("List");

            var model = new ComplianceAlertModel
            {
                Id = alert.Id,
                AlertType = alert.AlertType,
                AlertLevel = alert.AlertLevel,
                Message = alert.Message,
                Details = alert.Details,
                ScriptUrl = alert.ScriptUrl,
                PageUrl = alert.PageUrl,
                IsResolved = alert.IsResolved,
                CreatedOnUtc = alert.CreatedOnUtc,
                ResolvedOnUtc = alert.ResolvedOnUtc,
                ResolvedBy = alert.ResolvedBy,
                EmailSent = alert.EmailSent,
                EmailSentOnUtc = alert.EmailSentOnUtc
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Resolve(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var currentUser = await _workContext.GetCurrentCustomerAsync();
            var alert = await _complianceAlertService.ResolveAlertAsync(id, currentUser.Email);

            if (alert != null)
            {
                _notificationService.SuccessNotification("Alert has been resolved successfully.");
            }
            else
            {
                _notificationService.ErrorNotification("Alert not found or already resolved.");
            }

            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await _permissionService.AuthorizeAsync(PaymentGuardPermissionProvider.ViewComplianceAlerts))
                return AccessDeniedView();

            var alert = await _complianceAlertService.GetComplianceAlertByIdAsync(id);
            if (alert != null)
            {
                await _complianceAlertService.DeleteComplianceAlertAsync(alert);
                _notificationService.SuccessNotification("Alert has been deleted successfully.");
            }

            return RedirectToAction("List");
        }

        #endregion
    }
}