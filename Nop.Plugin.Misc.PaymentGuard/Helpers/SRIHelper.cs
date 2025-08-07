using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Nop.Core;

namespace Nop.Plugin.Misc.PaymentGuard.Helpers
{
    /// <summary>
    /// Helper for generating and managing Subresource Integrity (SRI) hashes
    /// </summary>
    public partial class SRIHelper
    {
        #region Fields

        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IWebHelper _webHelper;
        private static readonly Dictionary<string, string> _hashCache = new();
        private static readonly object _lockObject = new();

        #endregion

        #region Ctor

        public SRIHelper(IWebHostEnvironment webHostEnvironment, IWebHelper webHelper)
        {
            _webHostEnvironment = webHostEnvironment;
            _webHelper = webHelper;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Convert virtual path to physical path
        /// </summary>
        /// <param name="virtualPath">Virtual path starting with ~/</param>
        /// <returns>Physical file path</returns>
        private string GetPhysicalPath(string virtualPath)
        {
            if (virtualPath.StartsWith("~/"))
            {
                virtualPath = virtualPath.Substring(2);
            }

            return Path.Combine(_webHostEnvironment.WebRootPath, virtualPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Generate hash for content using specified algorithm
        /// </summary>
        /// <param name="content">File content</param>
        /// <param name="algorithm">Hash algorithm</param>
        /// <returns>Base64 encoded hash</returns>
        private static string GenerateHash(string content, string algorithm)
        {
            var bytes = Encoding.UTF8.GetBytes(content);

            return algorithm.ToLower() switch
            {
                "sha256" => Convert.ToBase64String(SHA256.HashData(bytes)),
                "sha384" => Convert.ToBase64String(SHA384.HashData(bytes)),
                "sha512" => Convert.ToBase64String(SHA512.HashData(bytes)),
                _ => Convert.ToBase64String(SHA384.HashData(bytes)) // Default to SHA384
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Generate SRI hash for a local script file
        /// </summary>
        /// <param name="scriptPath">Relative path to script (e.g., "~/Plugins/Misc.PaymentGuard/scripts/paymentguard-monitor.js")</param>
        /// <param name="algorithm">Hash algorithm (sha256, sha384, sha512)</param>
        /// <returns>SRI hash string</returns>
        public virtual string GenerateSRIHash(string scriptPath, string algorithm = "sha384")
        {
            if (string.IsNullOrEmpty(scriptPath))
                return string.Empty;

            var cacheKey = $"{scriptPath}_{algorithm}";

            // Check cache first
            lock (_lockObject)
            {
                if (_hashCache.TryGetValue(cacheKey, out var cachedHash))
                    return cachedHash;
            }

            try
            {
                // Convert virtual path to physical path
                var physicalPath = GetPhysicalPath(scriptPath);

                if (!File.Exists(physicalPath))
                    return string.Empty;

                // Read file content
                var fileContent = File.ReadAllText(physicalPath, Encoding.UTF8);

                // Generate hash
                var hash = GenerateHash(fileContent, algorithm);
                var sriHash = $"{algorithm}-{hash}";

                // Cache the result
                lock (_lockObject)
                {
                    _hashCache[cacheKey] = sriHash;
                }

                return sriHash;
            }
            catch (Exception)
            {
                // Return empty string on error - SRI is optional
                return string.Empty;
            }
        }

        /// <summary>
        /// Generate SRI hash for external script URL
        /// </summary>
        /// <param name="scriptUrl">External script URL</param>
        /// <param name="algorithm">Hash algorithm</param>
        /// <returns>SRI hash string</returns>
        public virtual async Task<string> GenerateExternalSRIHashAsync(string scriptUrl, string algorithm = "sha384")
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return string.Empty;

            var cacheKey = $"{scriptUrl}_{algorithm}";

            // Check cache first
            lock (_lockObject)
            {
                if (_hashCache.TryGetValue(cacheKey, out var cachedHash))
                    return cachedHash;
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10); // Set timeout

                var content = await httpClient.GetStringAsync(scriptUrl);
                var hash = GenerateHash(content, algorithm);
                var sriHash = $"{algorithm}-{hash}";

                // Cache the result
                lock (_lockObject)
                {
                    _hashCache[cacheKey] = sriHash;
                }

                return sriHash;
            }
            catch (Exception)
            {
                // Return empty string on error - SRI is optional
                return string.Empty;
            }
        }

        /// <summary>
        /// Clear SRI hash cache (useful after file updates)
        /// </summary>
        public virtual void ClearCache()
        {
            lock (_lockObject)
            {
                _hashCache.Clear();
            }
        }

        /// <summary>
        /// Clear specific hash from cache
        /// </summary>
        /// <param name="scriptPath">Script path to clear</param>
        public virtual void ClearCache(string scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath))
                return;

            lock (_lockObject)
            {
                var keysToRemove = _hashCache.Keys
                    .Where(key => key.StartsWith(scriptPath))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _hashCache.Remove(key);
                }
            }
        }

        #endregion
    }
}