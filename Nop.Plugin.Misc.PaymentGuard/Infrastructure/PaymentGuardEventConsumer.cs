using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.Misc.PaymentGuard.Services;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Services.Logging;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.PaymentGuard.Infrastructure
{
    /// <summary>
    /// PaymentGuard event consumer
    /// </summary>
    public partial class PaymentGuardEventConsumer : IConsumer<OrderPlacedEvent>,
        IConsumer<OrderPaidEvent>
    {
        #region Fields

        private readonly IMonitoringService _monitoringService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly ILogger _logger;

        #endregion

        #region Ctor

        public PaymentGuardEventConsumer(
            IMonitoringService monitoringService,
            ISettingService settingService,
            IStoreService storeService,
            ILogger logger)
        {
            _monitoringService = monitoringService;
            _settingService = settingService;
            _storeService = storeService;
            _logger = logger;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle order placed event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(OrderPlacedEvent eventMessage)
        {
            try
            {
                var order = eventMessage.Order;
                var store = await _storeService.GetStoreByIdAsync(order.StoreId);
                var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(order.StoreId);

                if (!settings.IsEnabled)
                    return;

                // Perform a monitoring check on the checkout page after successful order
                var checkoutUrl = $"{store.Url.TrimEnd('/')}/checkout";
                await _monitoringService.PerformMonitoringCheckAsync(checkoutUrl, order.StoreId);

                await _logger.InformationAsync($"PaymentGuard post-order monitoring check completed for store {store.Name}");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in PaymentGuard order placed event handler", ex);
            }
        }

        /// <summary>
        /// Handle order paid event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(OrderPaidEvent eventMessage)
        {
            try
            {
                var order = eventMessage.Order;
                var store = await _storeService.GetStoreByIdAsync(order.StoreId);
                var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(order.StoreId);

                if (!settings.IsEnabled)
                    return;

                // Log successful payment completion for compliance audit trail
                await _logger.InformationAsync($"PaymentGuard: Order {order.Id} payment completed successfully in store {store.Name}");
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Error in PaymentGuard order paid event handler", ex);
            }
        }

        #endregion
    }
}