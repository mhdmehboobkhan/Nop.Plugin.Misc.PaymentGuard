using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record ComplianceAlertSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchAlertType")]
        public string SearchAlertType { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchAlertLevel")]
        public string SearchAlertLevel { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchIsResolved")]
        public string SearchIsResolved { get; set; }

        public IList<SelectListItem> AvailableAlertTypes { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableAlertLevels { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableResolvedOptions { get; set; } = new List<SelectListItem>();
    }
}