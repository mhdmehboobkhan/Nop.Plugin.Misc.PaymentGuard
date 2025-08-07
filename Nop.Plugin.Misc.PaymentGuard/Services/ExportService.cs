using System.Text;
using System.Text.Json;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class ExportService : IExportService
    {
        #region Fields

        private readonly IMonitoringService _monitoringService;
        private readonly IStoreService _storeService;

        #endregion

        #region Ctor

        public ExportService(
            IMonitoringService monitoringService,
            IStoreService storeService)
        {
            _monitoringService = monitoringService;
            _storeService = storeService;
        }

        #endregion

        #region Methods

        public virtual async Task<byte[]> ExportComplianceAlertsToCsvAsync(IList<ComplianceAlert> alerts)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,AlertType,AlertLevel,Message,ScriptUrl,PageUrl,IsResolved,CreatedOnUtc,ResolvedOnUtc,ResolvedBy,EmailSent");

            // CSV Data
            foreach (var alert in alerts)
            {
                csv.AppendLine($"{alert.Id}," +
                              $"\"{EscapeCsvValue(alert.AlertType)}\"," +
                              $"\"{EscapeCsvValue(alert.AlertLevel)}\"," +
                              $"\"{EscapeCsvValue(alert.Message)}\"," +
                              $"\"{EscapeCsvValue(alert.ScriptUrl)}\"," +
                              $"\"{EscapeCsvValue(alert.PageUrl)}\"," +
                              $"{alert.IsResolved}," +
                              $"{alert.CreatedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"{alert.ResolvedOnUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""}," +
                              $"\"{EscapeCsvValue(alert.ResolvedBy)}\"," +
                              $"{alert.EmailSent}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> ExportMonitoringLogsToCsvAsync(IList<ScriptMonitoringLog> logs)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,PageUrl,TotalScriptsFound,AuthorizedScriptsCount,UnauthorizedScriptsCount,HasUnauthorizedScripts,CheckedOnUtc,CheckType,AlertSent");

            // CSV Data
            foreach (var log in logs)
            {
                csv.AppendLine($"{log.Id}," +
                              $"\"{EscapeCsvValue(log.PageUrl)}\"," +
                              $"{log.TotalScriptsFound}," +
                              $"{log.AuthorizedScriptsCount}," +
                              $"{log.UnauthorizedScriptsCount}," +
                              $"{log.HasUnauthorizedScripts}," +
                              $"{log.CheckedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"\"{EscapeCsvValue(log.CheckType)}\"," +
                              $"{log.AlertSent}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> ExportAuthorizedScriptsToCsvAsync(IList<AuthorizedScript> scripts)
        {
            var csv = new StringBuilder();

            // CSV Headers
            csv.AppendLine("Id,ScriptUrl,Purpose,Justification,RiskLevel,Source,Domain,IsActive,AuthorizedBy,AuthorizedOnUtc,LastVerifiedUtc");

            // CSV Data
            foreach (var script in scripts)
            {
                csv.AppendLine($"{script.Id}," +
                              $"\"{EscapeCsvValue(script.ScriptUrl)}\"," +
                              $"\"{EscapeCsvValue(script.Purpose)}\"," +
                              $"\"{EscapeCsvValue(script.Justification)}\"," +
                              $"{script.RiskLevel}," +
                              $"\"{EscapeCsvValue(script.Source)}\"," +
                              $"\"{EscapeCsvValue(script.Domain)}\"," +
                              $"{script.IsActive}," +
                              $"\"{EscapeCsvValue(script.AuthorizedBy)}\"," +
                              $"{script.AuthorizedOnUtc:yyyy-MM-dd HH:mm:ss}," +
                              $"{script.LastVerifiedUtc:yyyy-MM-dd HH:mm:ss}");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public virtual async Task<byte[]> GenerateComplianceReportPdfAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // This would require a PDF generation library like iTextSharp or similar
            // For now, returning a simple HTML-to-PDF conversion approach

            var store = await _storeService.GetStoreByIdAsync(storeId);
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate, toDate);

            var html = GenerateComplianceReportHtml(store.Name, report, fromDate, toDate);

            // Convert HTML to PDF (would need PDF library implementation)
            // For demonstration, returning HTML as bytes
            return Encoding.UTF8.GetBytes(html);
        }

        #endregion

        #region Utilities

        private static string EscapeCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Escape quotes and handle multiline values
            return value.Replace("\"", "\"\"");
        }

        private string GenerateComplianceReportHtml(string storeName, ComplianceReport report, DateTime? fromDate, DateTime? toDate)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><title>PaymentGuard Compliance Report</title>");
            html.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px;}table{border-collapse:collapse;width:100%;}th,td{border:1px solid #ddd;padding:8px;text-align:left;}th{background-color:#f2f2f2;}.header{text-align:center;margin-bottom:30px;}.summary{margin:20px 0;}</style>");
            html.AppendLine("</head><body>");

            html.AppendLine($"<div class='header'><h1>PaymentGuard Compliance Report</h1><h2>{storeName}</h2>");
            html.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
            if (fromDate.HasValue || toDate.HasValue)
                html.AppendLine($"<p>Period: {fromDate?.ToString("yyyy-MM-dd") ?? "Beginning"} to {toDate?.ToString("yyyy-MM-dd") ?? "End"}</p>");
            html.AppendLine("</div>");

            html.AppendLine("<div class='summary'><h3>Compliance Summary</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td><strong>Compliance Score</strong></td><td>{report.ComplianceScore:F1}%</td></tr>");
            html.AppendLine($"<tr><td><strong>Total Scripts Monitored</strong></td><td>{report.TotalScriptsMonitored}</td></tr>");
            html.AppendLine($"<tr><td><strong>Authorized Scripts</strong></td><td>{report.AuthorizedScriptsCount}</td></tr>");
            html.AppendLine($"<tr><td><strong>Unauthorized Scripts</strong></td><td>{report.UnauthorizedScriptsCount}</td></tr>");
            html.AppendLine($"<tr><td><strong>Total Checks Performed</strong></td><td>{report.TotalChecksPerformed}</td></tr>");
            html.AppendLine($"<tr><td><strong>Alerts Generated</strong></td><td>{report.AlertsGenerated}</td></tr>");
            html.AppendLine($"<tr><td><strong>Last Check Date</strong></td><td>{report.LastCheckDate:yyyy-MM-dd HH:mm:ss}</td></tr>");
            html.AppendLine("</table></div>");

            if (report.MostCommonUnauthorizedScripts.Any())
            {
                html.AppendLine("<div><h3>Most Common Unauthorized Scripts</h3><ul>");
                foreach (var script in report.MostCommonUnauthorizedScripts)
                {
                    html.AppendLine($"<li>{script}</li>");
                }
                html.AppendLine("</ul></div>");
            }

            html.AppendLine("</body></html>");
            return html.ToString();
        }

        #endregion
    }
}