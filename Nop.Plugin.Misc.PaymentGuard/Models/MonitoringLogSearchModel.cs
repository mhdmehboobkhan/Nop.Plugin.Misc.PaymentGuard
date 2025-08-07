using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record MonitoringLogSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchPageUrl")]
        public string SearchPageUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchHasUnauthorizedScripts")]
        public string SearchHasUnauthorizedScripts { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchDateFrom")]
        public DateTime? SearchDateFrom { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchDateTo")]
        public DateTime? SearchDateTo { get; set; }

        public IList<SelectListItem> AvailableUnauthorizedOptions { get; set; } = new List<SelectListItem>();
    }
}