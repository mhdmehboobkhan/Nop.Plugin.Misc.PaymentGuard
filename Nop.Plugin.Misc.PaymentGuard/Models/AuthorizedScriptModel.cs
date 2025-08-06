using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record AuthorizedScriptModel : BaseNopEntityModel
    {
        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ScriptUrl")]
        public string ScriptUrl { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.ScriptHash")]
        public string ScriptHash { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.Purpose")]
        public string Purpose { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.Justification")]
        public string Justification { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.RiskLevel")]
        public int RiskLevel { get; set; }
        
        public string RiskLevelText { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.IsActive")]
        public bool IsActive { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.AuthorizedBy")]
        public string AuthorizedBy { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.AuthorizedOnUtc")]
        public DateTime AuthorizedOnUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.LastVerifiedUtc")]
        public DateTime LastVerifiedUtc { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.Source")]
        public string Source { get; set; }

        [NopResourceDisplayName("Plugins.Misc.PaymentGuard.Fields.GenerateHash")]
        public bool GenerateHash { get; set; }

        public IList<SelectListItem> AvailableRiskLevels { get; set; } = new List<SelectListItem>();
        public IList<SelectListItem> AvailableSources { get; set; } = new List<SelectListItem>();
    }
}