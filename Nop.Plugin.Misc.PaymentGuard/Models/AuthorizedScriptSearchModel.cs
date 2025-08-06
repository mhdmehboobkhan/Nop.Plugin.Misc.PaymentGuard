using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record AuthorizedScriptSearchModel : BaseSearchModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchScriptUrl")]
        public string SearchScriptUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchIsActive")]
        public int SearchIsActiveId { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchRiskLevel")]
        public int SearchRiskLevel { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.SearchSource")]
        public string SearchSource { get; set; }

        public IList<SelectListItem> AvailableActiveOptions { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableRiskLevels { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableSources { get; set; } = new List<SelectListItem>();
    }
}