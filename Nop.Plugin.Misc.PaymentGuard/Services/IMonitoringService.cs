using Nop.Core;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial interface IMonitoringService
    {
        //Task<ScriptMonitoringLog> PerformMonitoringCheckAsync(string pageUrl, int storeId);

        Task<IPagedList<ScriptMonitoringLog>> GetMonitoringLogsAsync(int storeId = 0,
            bool? hasUnauthorizedScripts = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue);

        Task<ScriptMonitoringLog> GetMonitoringLogByIdAsync(int logId);

        Task InsertMonitoringLogAsync(ScriptMonitoringLog log);

        Task<IList<string>> ExtractScriptsFromPageAsync(string pageUrl);

        Task<IDictionary<string, string>> ExtractSecurityHeadersAsync(string pageUrl);

        Task<ComplianceReport> GenerateComplianceReportAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null);

        Task<ScriptValidationResult> ValidateScriptWithSRIAsync(PaymentGuardSettings guardSettings, 
            int storeId, string scriptUrl, string integrity = null);
    }
}