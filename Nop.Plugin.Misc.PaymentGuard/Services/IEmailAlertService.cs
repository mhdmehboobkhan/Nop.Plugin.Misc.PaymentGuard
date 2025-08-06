using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IEmailAlertService
    {
        Task SendUnauthorizedScriptAlertAsync(string alertEmail, ScriptMonitoringLog log, string storeName);

        Task SendComplianceReportAsync(string alertEmail, string storeName, ComplianceReport report);

        Task SendScriptChangeAlertAsync(string alertEmail, string scriptUrl, string storeName);

        Task SendCSPViolationAlertAsync(string alertEmail, string violationDetails, string storeName);
    }
}