using Nop.Web.Framework.Models;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record PaymentGuardScriptModel : BaseNopModel
    {
        public bool IsEnabled { get; set; }
        public string ApiEndpoint { get; set; }
        public string CSPPolicy { get; set; }
        public bool EnableSRIValidation { get; set; }
        public string CurrentPageUrl { get; set; }
        public int StoreId { get; set; }
    }
}