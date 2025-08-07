using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Nop.Plugin.Misc.PaymentGuard.Helpers;
using Nop.Services.Configuration;
using Nop.Core;

namespace Nop.Plugin.Misc.PaymentGuard.TagHelpers
{
    /// <summary>
    /// Tag helper to automatically add SRI attributes to script tags
    /// </summary>
    [HtmlTargetElement("script", Attributes = "src")]
    public class SRITagHelper : TagHelper
    {
        #region Fields

        private readonly SRIHelper _sriHelper;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;

        #endregion

        #region Properties

        /// <summary>
        /// ViewContext
        /// </summary>
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        /// <summary>
        /// Enable automatic SRI generation (default: true for external scripts)
        /// </summary>
        [HtmlAttributeName("enable-sri")]
        public bool EnableSRI { get; set; } = true;

        /// <summary>
        /// Force SRI generation even for local scripts
        /// </summary>
        [HtmlAttributeName("force-sri")]
        public bool ForceSRI { get; set; } = false;

        /// <summary>
        /// Hash algorithm to use (sha256, sha384, sha512)
        /// </summary>
        [HtmlAttributeName("sri-algorithm")]
        public string Algorithm { get; set; } = "sha384";

        #endregion

        #region Ctor

        public SRITagHelper(SRIHelper sriHelper, ISettingService settingService, IStoreContext storeContext)
        {
            _sriHelper = sriHelper;
            _settingService = settingService;
            _storeContext = storeContext;
        }

        #endregion

        #region Methods

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // Get PaymentGuard settings
            var store = await _storeContext.GetCurrentStoreAsync();
            var settings = await _settingService.LoadSettingAsync<PaymentGuardSettings>(store.Id);

            // Only process if PaymentGuard is enabled and SRI validation is enabled
            if (!settings.IsEnabled || !settings.EnableSRIValidation)
            {
                return;
            }

            var srcAttribute = output.Attributes.FirstOrDefault(attr => attr.Name.Equals("src", StringComparison.OrdinalIgnoreCase));
            if (srcAttribute?.Value == null)
                return;

            var scriptSrc = srcAttribute.Value.ToString();
            if (string.IsNullOrEmpty(scriptSrc))
                return;

            // Skip if integrity attribute already exists
            if (output.Attributes.Any(attr => attr.Name.Equals("integrity", StringComparison.OrdinalIgnoreCase)))
                return;

            string sriHash = null;

            try
            {
                // SMART AUTOMATIC BEHAVIOR - NO CLIENT CONFIGURATION NEEDED
                if (ShouldAutomaticallyAddSRI(scriptSrc, settings))
                {
                    if (IsLocalScript(scriptSrc))
                    {
                        sriHash = _sriHelper.GenerateSRIHash(scriptSrc, Algorithm);
                    }
                    else if (IsExternalScript(scriptSrc))
                    {
                        sriHash = await _sriHelper.GenerateExternalSRIHashAsync(scriptSrc, Algorithm);
                    }
                }

                // MANUAL OVERRIDES (Optional client enhancement)
                else if (ForceSRI) // Client added force-sri="true"
                {
                    if (IsLocalScript(scriptSrc))
                    {
                        sriHash = _sriHelper.GenerateSRIHash(scriptSrc, Algorithm);
                    }
                    else if (IsExternalScript(scriptSrc))
                    {
                        sriHash = await _sriHelper.GenerateExternalSRIHashAsync(scriptSrc, Algorithm);
                    }
                }
                else if (!EnableSRI) // Client added enable-sri="false"
                {
                    // Skip SRI generation entirely
                    return;
                }

                // Add integrity and crossorigin attributes if hash was generated
                if (!string.IsNullOrEmpty(sriHash))
                {
                    output.Attributes.Add("integrity", sriHash);

                    if (IsExternalScript(scriptSrc) &&
                        !output.Attributes.Any(attr => attr.Name.Equals("crossorigin", StringComparison.OrdinalIgnoreCase)))
                    {
                        output.Attributes.Add("crossorigin", "anonymous");
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail - SRI is optional
            }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Determine if SRI should be automatically added (no client configuration needed)
        /// </summary>
        private static bool ShouldAutomaticallyAddSRI(string scriptSrc, PaymentGuardSettings settings)
        {
            // ALWAYS add SRI for PaymentGuard's own scripts
            if (IsPaymentGuardScript(scriptSrc))
                return true;

            // ALWAYS add SRI for trusted CDNs (jQuery, Bootstrap, etc.)
            if (IsExternalScript(scriptSrc) && IsWhitelistedExternalScript(scriptSrc, settings))
                return true;

            // DON'T automatically add SRI for:
            // - Custom local scripts (unless client forces it)
            // - Untrusted external scripts (unless client enables it)
            // - Payment gateway scripts (too dynamic, causes issues)

            return false;
        }

        /// <summary>
        /// Check if script is local (same origin)
        /// </summary>
        private static bool IsLocalScript(string scriptSrc)
        {
            return scriptSrc.StartsWith("/") || scriptSrc.StartsWith("~/");
        }

        /// <summary>
        /// Check if script is external
        /// </summary>
        private static bool IsExternalScript(string scriptSrc)
        {
            return scriptSrc.StartsWith("http://") || scriptSrc.StartsWith("https://") || scriptSrc.StartsWith("//");
        }

        /// <summary>
        /// Check if this is a PaymentGuard script
        /// </summary>
        private static bool IsPaymentGuardScript(string scriptSrc)
        {
            return scriptSrc.Contains("PaymentGuard", StringComparison.OrdinalIgnoreCase) ||
                   scriptSrc.Contains("paymentguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if external script is whitelisted for SRI generation
        /// </summary>
        private static bool IsWhitelistedExternalScript(string scriptSrc, PaymentGuardSettings settings)
        {
            // Trusted CDNs that ALWAYS support SRI
            var trustedCDNs = new[]
            {
                // JavaScript Libraries
                "cdnjs.cloudflare.com",
                "cdn.jsdelivr.net",
                "unpkg.com",
                "ajax.googleapis.com",
                "code.jquery.com",
        
                // UI Frameworks
                "stackpath.bootstrapcdn.com",
                "maxcdn.bootstrapcdn.com",
                "cdn.datatables.net",
                "use.fontawesome.com",
        
                // Charts & Analytics (non-tracking)
                "cdn.plot.ly",
                "d3js.org"
            };

            var isTrustedCDN = trustedCDNs.Any(cdn => scriptSrc.Contains(cdn, StringComparison.OrdinalIgnoreCase));

            // DON'T auto-add SRI for payment gateways (they change frequently)
            var isPaymentGateway = IsPaymentGatewayScript(scriptSrc);

            return isTrustedCDN && !isPaymentGateway;
        }

        /// <summary>
        /// Check if this is a payment gateway script that typically has SRI issues
        /// </summary>
        private static bool IsPaymentGatewayScript(string scriptSrc)
        {
            var paymentDomains = new[]
            {
                "js.stripe.com",
                "www.paypalobjects.com",
                "js.braintreegateway.com",
                "pay.google.com",
                "applepay.cdn-apple.com",
                "sdk.amazonaws.com" // Amazon Pay
            };

            return paymentDomains.Any(domain => scriptSrc.Contains(domain, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}