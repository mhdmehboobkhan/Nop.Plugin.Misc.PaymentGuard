using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.PaymentGuard.Models
{
    public record DashboardModel : BaseNopModel
    {
        public int TotalScriptsMonitored { get; set; }

        public int AuthorizedScriptsCount { get; set; }

        public int UnauthorizedScriptsCount { get; set; }

        public double ComplianceScore { get; set; }

        public DateTime LastCheckDate { get; set; }

        public int TotalChecksPerformed { get; set; }

        public int AlertsGenerated { get; set; }

        public IList<string> MostCommonUnauthorizedScripts { get; set; } = new List<string>();

        public IList<string> RecentAlerts { get; set; } = new List<string>();
    }
}