using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Tasks
{
    public partial class PaymentGuardMaintenanceTask : IScheduleTask
    {
        #region Fields

        private readonly IAuthorizedScriptService _authorizedScriptService;
        private readonly IEmailAlertService _emailAlertService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public PaymentGuardMaintenanceTask(IAuthorizedScriptService authorizedScriptService,
            IEmailAlertService emailAlertService,
            ISettingService settingService,
            IStoreService storeService,
            ILogger logger)
        {
            _authorizedScriptService = authorizedScriptService;
            _emailAlertService = emailAlertService;
            _settingService = settingService;
            _storeService = storeService;
            _logger = logger;
        }

        #endregion

        #region Methods

        public async Task ExecuteAsync()
        {
            try
            {
                await _logger.InformationAsync("PaymentGuard maintenance task started");

                var stores = await _storeService.GetAllStoresAsync();

                foreach (var store in stores)
                {
                    var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

                    if (!settings.IsEnabled)
                        continue;

                    // Check for expired scripts (haven't been verified in 30 days)
                    var expiredScripts = await _authorizedScriptService.GetExpiredScriptsAsync(30, store.Id);

                    if (expiredScripts.Any())
                    {
                        await _logger.WarningAsync($"Found {expiredScripts.Count} expired scripts for store {store.Name}");

                        // Send alert email about expired scripts
                        if (settings.EnableEmailAlerts && !string.IsNullOrEmpty(settings.AlertEmail))
                        {
                            await _emailAlertService.SendExpiredScriptsAlertAsync(settings.AlertEmail, expiredScripts, store.Name);
                        }

                        // Auto-update hashes for scripts that are still accessible
                        foreach (var script in expiredScripts.Take(10)) // Limit to 10 per run to avoid overload
                        {
                            try
                            {
                                var isValid = await _authorizedScriptService.ValidateScriptIntegrityAsync(script.ScriptUrl, script.ScriptHash);
                                if (!isValid)
                                {
                                    // Try to update the hash
                                    var newHash = await _authorizedScriptService.GenerateScriptHashAsync(script.ScriptUrl);
                                    if (!string.IsNullOrEmpty(newHash))
                                    {
                                        await _authorizedScriptService.UpdateScriptHashAsync(script.Id, newHash);
                                        await _logger.InformationAsync($"Updated hash for script: {script.ScriptUrl}");
                                    }
                                }
                                else
                                {
                                    // Hash is still valid, just update the verification date
                                    script.LastVerifiedUtc = DateTime.UtcNow;
                                    await _authorizedScriptService.UpdateAuthorizedScriptAsync(script);
                                }
                            }
                            catch (Exception ex)
                            {
                                await _logger.ErrorAsync($"Error processing expired script {script.ScriptUrl}", ex);
                            }
                        }
                    }
                }

                await _logger.InformationAsync("PaymentGuard maintenance task completed");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error executing PaymentGuard maintenance task", ex);
                throw;
            }
        }

        #endregion
    }
}