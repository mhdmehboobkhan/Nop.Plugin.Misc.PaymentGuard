using System.Text.Json;
using HtmlAgilityPack;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Dto;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class MonitoringService : IMonitoringService
    {
        private readonly IRepository<ScriptMonitoringLog> _monitoringLogRepository;
        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly HttpClient _httpClient;
        private readonly ISRIValidationService _sriValidationService;

        public MonitoringService(IRepository<ScriptMonitoringLog> monitoringLogRepository,
            IAuthorizedScriptService authorizedScriptService,
            HttpClient httpClient,
            ISRIValidationService sriValidationService)
        {
            _monitoringLogRepository = monitoringLogRepository;
            _authorizedScriptService = authorizedScriptService;
            _httpClient = httpClient;
            _sriValidationService = sriValidationService;
        }

        public virtual async Task<ScriptMonitoringLog> PerformMonitoringCheckAsync(string pageUrl, int storeId)
        {
            var detectedScripts = await ExtractScriptsFromPageAsync(pageUrl);
            var securityHeaders = await ExtractSecurityHeadersAsync(pageUrl);

            var unauthorizedScripts = new List<string>();
            var authorizedCount = 0;

            foreach (var scriptUrl in detectedScripts)
            {
                var isAuthorized = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, storeId);
                if (isAuthorized)
                    authorizedCount++;
                else
                    unauthorizedScripts.Add(scriptUrl);
            }

            var log = new ScriptMonitoringLog
            {
                StoreId = storeId,
                PageUrl = pageUrl,
                DetectedScripts = JsonSerializer.Serialize(detectedScripts),
                HttpHeaders = JsonSerializer.Serialize(securityHeaders),
                HasUnauthorizedScripts = unauthorizedScripts.Any(),
                UnauthorizedScripts = JsonSerializer.Serialize(unauthorizedScripts),
                CheckedOnUtc = DateTime.UtcNow,
                CheckType = "scheduled",
                TotalScriptsFound = detectedScripts.Count,
                AuthorizedScriptsCount = authorizedCount,
                UnauthorizedScriptsCount = unauthorizedScripts.Count
            };

            await InsertMonitoringLogAsync(log);
            return log;
        }

        public virtual async Task<IList<string>> ExtractScriptsFromPageAsync(string pageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(pageUrl);
                response.EnsureSuccessStatusCode();

                var htmlContent = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                var scriptUrls = new List<string>();

                // Extract external scripts
                var scriptNodes = doc.DocumentNode.SelectNodes("//script[@src]");
                if (scriptNodes != null)
                {
                    foreach (var node in scriptNodes)
                    {
                        var src = node.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src))
                        {
                            // Convert relative URLs to absolute
                            if (src.StartsWith("//"))
                                src = "https:" + src;
                            else if (src.StartsWith("/"))
                                src = new Uri(new Uri(pageUrl), src).ToString();
                            else if (!src.StartsWith("http"))
                                src = new Uri(new Uri(pageUrl), src).ToString();

                            scriptUrls.Add(src);
                        }
                    }
                }

                // Extract inline scripts (for monitoring purposes)
                var inlineScripts = doc.DocumentNode.SelectNodes("//script[not(@src)]");
                if (inlineScripts != null)
                {
                    foreach (var node in inlineScripts)
                    {
                        var content = node.InnerText?.Trim();
                        if (!string.IsNullOrEmpty(content))
                        {
                            // Create a hash-based identifier for inline scripts
                            var hash = Convert.ToBase64String(
                                System.Security.Cryptography.SHA256.HashData(
                                    System.Text.Encoding.UTF8.GetBytes(content)))[..16];
                            scriptUrls.Add($"inline-script-{hash}");
                        }
                    }
                }

                return scriptUrls.Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public virtual async Task<IDictionary<string, string>> ExtractSecurityHeadersAsync(string pageUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(pageUrl);
                var securityHeaders = new Dictionary<string, string>();

                // Check for security-related headers
                var headersToCheck = new[]
                {
                    "Content-Security-Policy",
                    "X-Content-Type-Options",
                    "X-Frame-Options",
                    "X-XSS-Protection",
                    "Strict-Transport-Security",
                    "Referrer-Policy"
                };

                foreach (var headerName in headersToCheck)
                {
                    if (response.Headers.TryGetValues(headerName, out var values))
                    {
                        securityHeaders[headerName] = string.Join(", ", values);
                    }
                    else if (response.Content.Headers.TryGetValues(headerName, out values))
                    {
                        securityHeaders[headerName] = string.Join(", ", values);
                    }
                    else
                    {
                        securityHeaders[headerName] = ""; // Missing header
                    }
                }

                return securityHeaders;
            }
            catch (Exception)
            {
                return new Dictionary<string, string>();
            }
        }

        public virtual async Task<IPagedList<ScriptMonitoringLog>> GetMonitoringLogsAsync(int storeId = 0,
            bool? hasUnauthorizedScripts = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            var query = _monitoringLogRepository.Table;

            if (storeId > 0)
                query = query.Where(log => log.StoreId == storeId);

            if (hasUnauthorizedScripts.HasValue)
                query = query.Where(log => log.HasUnauthorizedScripts == hasUnauthorizedScripts.Value);

            query = query.OrderByDescending(log => log.CheckedOnUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }

        public virtual async Task<ScriptMonitoringLog> GetMonitoringLogByIdAsync(int logId)
        {
            return await _monitoringLogRepository.GetByIdAsync(logId);
        }

        public virtual async Task InsertMonitoringLogAsync(ScriptMonitoringLog log)
        {
            ArgumentNullException.ThrowIfNull(log);
            await _monitoringLogRepository.InsertAsync(log);
        }

        public virtual async Task<ComplianceReport> GenerateComplianceReportAsync(int storeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _monitoringLogRepository.Table.Where(log => log.StoreId == storeId);

            if (fromDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(log => log.CheckedOnUtc <= toDate.Value);

            var logs = await query.ToListAsync();

            var report = new ComplianceReport
            {
                TotalChecksPerformed = logs.Count,
                AlertsGenerated = logs.Count(l => l.HasUnauthorizedScripts),
                LastCheckDate = logs.Any() ? logs.Max(l => l.CheckedOnUtc) : DateTime.MinValue
            };

            if (logs.Any())
            {
                report.TotalScriptsMonitored = logs.Sum(l => l.TotalScriptsFound);
                report.AuthorizedScriptsCount = logs.Sum(l => l.AuthorizedScriptsCount);
                report.UnauthorizedScriptsCount = logs.Sum(l => l.UnauthorizedScriptsCount);

                // Calculate compliance score (percentage of authorized scripts)
                var totalScripts = report.TotalScriptsMonitored;
                report.ComplianceScore = totalScripts > 0
                    ? (double)report.AuthorizedScriptsCount / totalScripts * 100
                    : 100;

                // Get most common unauthorized scripts
                var unauthorizedScripts = new List<string>();
                foreach (var log in logs.Where(l => l.HasUnauthorizedScripts))
                {
                    try
                    {
                        var scripts = JsonSerializer.Deserialize<List<string>>(log.UnauthorizedScripts ?? "[]");
                        unauthorizedScripts.AddRange(scripts);
                    }
                    catch { }
                }

                report.MostCommonUnauthorizedScripts = unauthorizedScripts
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => $"{g.Key} ({g.Count()} times)")
                    .ToList();
            }

            return report;
        }

        /// <summary>
        /// Enhanced script validation with SRI checking
        /// </summary>
        public async Task<ScriptValidationResult> ValidateScriptWithSRIAsync(string scriptUrl, string integrity = null)
        {
            var result = new ScriptValidationResult { ScriptUrl = scriptUrl };

            // 1. Check if script is authorized
            var isAuthorized = await _authorizedScriptService.IsScriptAuthorizedAsync(scriptUrl, 1); // TODO: pass actual store ID
            result.IsAuthorized = isAuthorized;

            // 2. If script has integrity attribute, validate it
            if (!string.IsNullOrEmpty(integrity))
            {
                var sriResult = await _sriValidationService.ValidateScriptIntegrityAsync(scriptUrl, integrity);
                result.SRIValidation = sriResult;
                result.HasValidSRI = sriResult.IsValid;
            }
            else
            {
                result.HasValidSRI = false;
                result.SRIValidation = new SRIValidationResult
                {
                    IsValid = false,
                    Error = "No integrity attribute present",
                    ScriptUrl = scriptUrl
                };
            }

            return result;
        }
    }
}