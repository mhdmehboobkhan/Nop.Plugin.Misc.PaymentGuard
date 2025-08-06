using Nop.Core.Domain.Logging;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Tasks
{
    public partial class MonitoringTask : IScheduleTask
    {
        private readonly IMonitoringService _monitoringService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly ILogger _logger;

        public MonitoringTask(IMonitoringService monitoringService,
            IEmailAlertService emailAlertService,
            ISettingService settingService,
            IStoreService storeService,
            ILogger logger)
        {
            _monitoringService = monitoringService;
            _emailAlertService = emailAlertService;
            _settingService = settingService;
            _storeService = storeService;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                await _logger.InformationAsync("PaymentGuard monitoring task started");

                var stores = await _storeService.GetAllStoresAsync();

                foreach (var store in stores)
                {
                    var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                    if (!settings.IsEnabled)
                        continue;

                    var monitoredPages = settings.MonitoredPages?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        ?? new[] { "/checkout", "/onepagecheckout" };

                    foreach (var page in monitoredPages)
                    {
                        try
                        {
                            var pageUrl = $"{store.Url.TrimEnd('/')}{page.Trim()}";
                            var log = await _monitoringService.PerformMonitoringCheckAsync(pageUrl, store.Id);

                            if (log.HasUnauthorizedScripts)
                            {
                                await _logger.WarningAsync($"Unauthorized scripts detected on {page} for store {store.Id}: {log.UnauthorizedScriptsCount} scripts");

                                // TODO: Send alert email if enabled
                                if (settings.EnableEmailAlerts && !string.IsNullOrEmpty(settings.AlertEmail))
                                {
                                    await _emailAlertService.SendUnauthorizedScriptAlertAsync(
                                        settings.AlertEmail, log, store.Name);
                                }
                            }
                            else
                            {
                                await _logger.InformationAsync($"Monitoring check completed successfully for {page} - no issues found");
                            }
                        }
                        catch (Exception ex)
                        {
                            await _logger.ErrorAsync($"Error monitoring page {page} for store {store.Id}", ex);
                        }
                    }
                }

                await _logger.InformationAsync("PaymentGuard monitoring task completed successfully");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync($"Error executing PaymentGuard monitoring task.", ex);
                throw;
            }
        }
    }
}