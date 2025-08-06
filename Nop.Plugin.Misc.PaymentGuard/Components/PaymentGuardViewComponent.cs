using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Models;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PaymentGuard.Components
{
    public class PaymentGuardViewComponent : NopViewComponent
    {
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;

        public PaymentGuardViewComponent(ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper)
        {
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
        }

        public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData = null)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

            if (!settings.IsEnabled)
                return Content("");

            // Only inject on monitored pages
            var currentPage = _webHelper.GetThisPageUrl(false);
            var monitoredPages = settings.MonitoredPages?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? new[] { "/checkout", "/onepagecheckout" };

            var isMonitoredPage = monitoredPages.Any(page =>
                currentPage.Contains(page.Trim(), StringComparison.OrdinalIgnoreCase));

            if (!isMonitoredPage)
                return Content("");

            var model = new PaymentGuardScriptModel
            {
                IsEnabled = settings.IsEnabled,
                ApiEndpoint = "/Plugins/PaymentGuard/Api",
                CSPPolicy = settings.EnableCSPHeaders ? settings.CSPPolicy : null,
                EnableSRIValidation = settings.EnableSRIValidation,
                CurrentPageUrl = currentPage,
                StoreId = store.Id
            };

            return View(model);
        }
    }
}