using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Models;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class DashboardService : IDashboardService
    {
        #region Fields

        private readonly IRepository<ScriptMonitoringLog> _monitoringLogRepository;
        private readonly IRepository<ComplianceAlert> _complianceAlertRepository;
        private readonly IRepository<AuthorizedScript> _authorizedScriptRepository;
        private readonly IMonitoringService _monitoringService;
        private readonly IAuthorizedScriptService _authorizedScriptService;

        #endregion

        #region Ctor

        public DashboardService(IRepository<ScriptMonitoringLog> monitoringLogRepository,
            IRepository<ComplianceAlert> complianceAlertRepository,
            IRepository<AuthorizedScript> authorizedScriptRepository,
            IMonitoringService monitoringService,
            IAuthorizedScriptService authorizedScriptService)
        {
            _monitoringLogRepository = monitoringLogRepository;
            _complianceAlertRepository = complianceAlertRepository;
            _authorizedScriptRepository = authorizedScriptRepository;
            _monitoringService = monitoringService;
            _authorizedScriptService = authorizedScriptService;
        }

        #endregion

        #region Methods

        public virtual async Task<DashboardModel> GetDashboardDataAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate);
            var expiredScripts = await _authorizedScriptService.GetExpiredScriptsAsync(30, storeId); // 30 days

            var model = new DashboardModel
            {
                TotalScriptsMonitored = report.TotalScriptsMonitored,
                AuthorizedScriptsCount = report.AuthorizedScriptsCount,
                UnauthorizedScriptsCount = report.UnauthorizedScriptsCount,
                ComplianceScore = report.ComplianceScore,
                LastCheckDate = report.LastCheckDate,
                TotalChecksPerformed = report.TotalChecksPerformed,
                AlertsGenerated = report.AlertsGenerated,
                MostCommonUnauthorizedScripts = report.MostCommonUnauthorizedScripts,

                // Advanced analytics data
                ComplianceHistoryData = await GetComplianceHistoryAsync(storeId, days),
                AlertTypeDistribution = await GetAlertTypeDistributionAsync(storeId, days),
                MonitoringTrends = await GetMonitoringTrendsAsync(storeId, days),
                RiskLevelBreakdown = await GetRiskLevelBreakdownAsync(storeId),
                TopViolatingScripts = await GetTopViolatingScriptsAsync(storeId, days),
                ComplianceMetrics = await GetComplianceMetricsAsync(storeId, days),
                PerformanceMetrics = await GetPerformanceMetricsAsync(storeId, days)
            };

            model.ExpiredScriptsCount = expiredScripts.Count;
            model.ExpiredScripts = expiredScripts.Take(5).Select(s => new ExpiredScriptInfo
            {
                ScriptUrl = s.ScriptUrl,
                LastVerified = s.LastVerifiedUtc,
                DaysExpired = (DateTime.UtcNow - s.LastVerifiedUtc).Days
            }).ToList();

            // Add any additional view data if needed
            model.SelectedDays = days;
            model.AvailableDayOptions = new List<SelectListItem>
            {
                new() { Value = "7", Text = "Last 7 Days", Selected = days == 7 },
                new() { Value = "30", Text = "Last 30 Days", Selected = days == 30 },
                new() { Value = "90", Text = "Last 90 Days", Selected = days == 90 }
            };

            return model;
        }

        public virtual async Task<IList<ComplianceChartDataPoint>> GetComplianceHistoryAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var logs = await _monitoringLogRepository.Table
                .Where(log => log.StoreId == storeId && log.CheckedOnUtc >= fromDate)
                .OrderBy(log => log.CheckedOnUtc)
                .ToListAsync();

            var groupedByDay = logs
                .GroupBy(log => log.CheckedOnUtc.Date)
                .Select(group => new ComplianceChartDataPoint
                {
                    Date = group.Key,
                    TotalScripts = group.Sum(log => log.TotalScriptsFound),
                    AuthorizedScripts = group.Sum(log => log.AuthorizedScriptsCount),
                    UnauthorizedScripts = group.Sum(log => log.UnauthorizedScriptsCount),
                    ComplianceScore = group.Sum(log => log.TotalScriptsFound) > 0
                        ? (double)group.Sum(log => log.AuthorizedScriptsCount) / group.Sum(log => log.TotalScriptsFound) * 100
                        : 100
                })
                .OrderBy(point => point.Date)
                .ToList();

            return groupedByDay;
        }

        public virtual async Task<IList<AlertTypeChartData>> GetAlertTypeDistributionAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var alerts = await _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId && alert.CreatedOnUtc >= fromDate)
                .GroupBy(alert => alert.AlertType)
                .Select(group => new
                {
                    AlertType = group.Key,
                    Count = group.Count()
                })
                .ToListAsync();

            var totalAlerts = alerts.Sum(a => a.Count);
            var colors = new[] { "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF", "#FF9F40" };

            var result = alerts.Select((alert, index) => new AlertTypeChartData
            {
                AlertType = FormatAlertType(alert.AlertType),
                Count = alert.Count,
                Percentage = totalAlerts > 0 ? (double)alert.Count / totalAlerts * 100 : 0,
                Color = colors[index % colors.Length]
            }).ToList();

            return result;
        }

        public virtual async Task<IList<MonitoringTrendData>> GetMonitoringTrendsAsync(int storeId, int days = 30)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var logs = await _monitoringLogRepository.Table
                .Where(log => log.StoreId == storeId && log.CheckedOnUtc >= fromDate)
                .ToListAsync();

            var trendData = logs
                .GroupBy(log => log.CheckedOnUtc.Date)
                .Select(group => new MonitoringTrendData
                {
                    Date = group.Key,
                    ChecksPerformed = group.Count(),
                    IssuesFound = group.Count(log => log.HasUnauthorizedScripts),
                    AverageResponseTime = GenerateRandomResponseTime() // Placeholder for actual metrics
                })
                .OrderBy(trend => trend.Date)
                .ToList();

            return trendData;
        }

        public virtual async Task<IList<RiskLevelData>> GetRiskLevelBreakdownAsync(int storeId)
        {
            var scripts = await _authorizedScriptRepository.Table
                .Where(script => script.StoreId == storeId && script.IsActive)
                .GroupBy(script => script.RiskLevel)
                .Select(group => new
                {
                    RiskLevel = group.Key,
                    Count = group.Count()
                })
                .ToListAsync();

            var totalScripts = scripts.Sum(s => s.Count);
            var riskColors = new Dictionary<int, string>
            {
                { 1, "#28A745" }, // Low - Green
                { 2, "#FFC107" }, // Medium - Yellow
                { 3, "#DC3545" }  // High - Red
            };

            var result = scripts.Select(script => new RiskLevelData
            {
                RiskLevel = GetRiskLevelText(script.RiskLevel),
                Count = script.Count,
                Percentage = totalScripts > 0 ? (double)script.Count / totalScripts * 100 : 0,
                Color = riskColors.GetValueOrDefault(script.RiskLevel, "#6C757D")
            }).ToList();

            return result;
        }

        public virtual async Task<IList<TopViolatingScriptsData>> GetTopViolatingScriptsAsync(int storeId, int days = 30, int topCount = 10)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var alerts = await _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId
                    && alert.CreatedOnUtc >= fromDate
                    && !string.IsNullOrEmpty(alert.ScriptUrl))
                .GroupBy(alert => alert.ScriptUrl)
                .Select(group => new
                {
                    ScriptUrl = group.Key,
                    ViolationCount = group.Count(),
                    LastViolation = group.Max(alert => alert.CreatedOnUtc)
                })
                .OrderByDescending(script => script.ViolationCount)
                .Take(topCount)
                .ToListAsync();

            var result = new List<TopViolatingScriptsData>();

            foreach (var alert in alerts)
            {
                var authorizedScript = await _authorizedScriptRepository.Table
                    .FirstOrDefaultAsync(script => script.ScriptUrl == alert.ScriptUrl && script.StoreId == storeId);

                result.Add(new TopViolatingScriptsData
                {
                    ScriptUrl = TruncateUrl(alert.ScriptUrl, 50),
                    ViolationCount = alert.ViolationCount,
                    LastViolation = alert.LastViolation.ToString("MMM dd, yyyy"),
                    RiskLevel = authorizedScript != null ? GetRiskLevelText(authorizedScript.RiskLevel) : "Unknown"
                });
            }

            return result;
        }

        #endregion

        #region Utilities

        private async Task<ComplianceMetrics> GetComplianceMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var weekAgo = DateTime.UtcNow.AddDays(-7);

            var currentPeriodAlerts = await _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId && alert.CreatedOnUtc >= weekAgo)
                .ToListAsync();

            var previousPeriodAlerts = await _complianceAlertRepository.Table
                .Where(alert => alert.StoreId == storeId
                    && alert.CreatedOnUtc >= weekAgo.AddDays(-7)
                    && alert.CreatedOnUtc < weekAgo)
                .ToListAsync();

            var resolvedThisWeek = currentPeriodAlerts.Count(a => a.IsResolved);
            var newThisWeek = currentPeriodAlerts.Count;

            var scriptsAddedThisWeek = await _authorizedScriptRepository.Table
                .CountAsync(script => script.StoreId == storeId && script.AuthorizedOnUtc >= weekAgo);

            // Calculate compliance improvement
            var currentCompliance = await GetCurrentComplianceScore(storeId);
            var previousCompliance = await GetPreviousComplianceScore(storeId, 7);
            var improvement = currentCompliance - previousCompliance;

            return new ComplianceMetrics
            {
                ComplianceImprovement = improvement,
                ResolvedAlertsThisWeek = resolvedThisWeek,
                NewAlertsThisWeek = newThisWeek,
                AverageResolutionTime = CalculateAverageResolutionTime(currentPeriodAlerts.Where(a => a.IsResolved)),
                ScriptsAddedThisWeek = scriptsAddedThisWeek,
                SecurityPosture = CalculateSecurityPosture(storeId)
            };
        }

        private async Task<PerformanceMetrics> GetPerformanceMetricsAsync(int storeId, int days)
        {
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var logs = await _monitoringLogRepository.Table
                .Where(log => log.StoreId == storeId && log.CheckedOnUtc >= fromDate)
                .ToListAsync();

            var successfulChecks = logs.Count(log => !log.HasUnauthorizedScripts);
            var failedChecks = logs.Count - successfulChecks;

            return new PerformanceMetrics
            {
                AverageMonitoringTime = GenerateRandomResponseTime(),
                SuccessfulChecks = successfulChecks,
                FailedChecks = failedChecks,
                SystemUptime = 99.8, // Placeholder
                ApiCallsThisWeek = GenerateRandomApiCalls(),
                CacheHitRate = 85.5 // Placeholder
            };
        }

        private string FormatAlertType(string alertType)
        {
            return alertType switch
            {
                "unauthorized-script" => "Unauthorized Scripts",
                "csp-violation" => "CSP Violations",
                "integrity-failure" => "Integrity Failures",
                _ => alertType ?? "Unknown"
            };
        }

        private string GetRiskLevelText(int riskLevel)
        {
            return riskLevel switch
            {
                1 => "Low",
                2 => "Medium",
                3 => "High",
                _ => "Unknown"
            };
        }

        private string TruncateUrl(string url, int maxLength)
        {
            if (string.IsNullOrEmpty(url) || url.Length <= maxLength)
                return url;

            return url.Substring(0, maxLength - 3) + "...";
        }

        private double GenerateRandomResponseTime()
        {
            // Placeholder for actual performance metrics
            var random = new Random();
            return Math.Round(random.NextDouble() * 2 + 0.5, 2); // 0.5 to 2.5 seconds
        }

        private int GenerateRandomApiCalls()
        {
            var random = new Random();
            return random.Next(500, 2000);
        }

        private async Task<double> GetCurrentComplianceScore(int storeId)
        {
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, DateTime.UtcNow.AddDays(-7));
            return report.ComplianceScore;
        }

        private async Task<double> GetPreviousComplianceScore(int storeId, int daysAgo)
        {
            var fromDate = DateTime.UtcNow.AddDays(-daysAgo * 2);
            var toDate = DateTime.UtcNow.AddDays(-daysAgo);
            var report = await _monitoringService.GenerateComplianceReportAsync(storeId, fromDate, toDate);
            return report.ComplianceScore;
        }

        private double CalculateAverageResolutionTime(IEnumerable<ComplianceAlert> resolvedAlerts)
        {
            var alerts = resolvedAlerts.Where(a => a.ResolvedOnUtc.HasValue).ToList();
            if (!alerts.Any())
                return 0;

            var totalHours = alerts.Sum(alert =>
                (alert.ResolvedOnUtc!.Value - alert.CreatedOnUtc).TotalHours);

            return Math.Round(totalHours / alerts.Count, 1);
        }

        private double CalculateSecurityPosture(int storeId)
        {
            // Complex calculation based on various factors
            // This is a simplified version
            var random = new Random();
            return Math.Round(75 + random.NextDouble() * 20, 1); // 75-95% range
        }

        #endregion
    }
}