using System.Security.Cryptography;
using System.Text;
using Nop.Core;
using Nop.Data;
using Nop.Plugin.Misc.PaymentGuard.Domain;

namespace Nop.Plugin.Misc.PaymentGuard.Services
{
    public partial class AuthorizedScriptService : IAuthorizedScriptService
    {
        private readonly IRepository<AuthorizedScript> _authorizedScriptRepository;
        private readonly HttpClient _httpClient;

        public AuthorizedScriptService(IRepository<AuthorizedScript> authorizedScriptRepository,
            HttpClient httpClient)
        {
            _authorizedScriptRepository = authorizedScriptRepository;
            _httpClient = httpClient;
        }

        public virtual async Task<IPagedList<AuthorizedScript>> GetAllAuthorizedScriptsAsync(int storeId = 0,
            bool? isActive = null,
            int pageIndex = 0,
            int pageSize = int.MaxValue)
        {
            var query = _authorizedScriptRepository.Table;

            if (storeId > 0)
                query = query.Where(script => script.StoreId == storeId);

            if (isActive.HasValue)
                query = query.Where(script => script.IsActive == isActive.Value);

            query = query.OrderByDescending(script => script.AuthorizedOnUtc);

            return await query.ToPagedListAsync(pageIndex, pageSize);
        }

        public virtual async Task<AuthorizedScript> GetAuthorizedScriptByIdAsync(int scriptId)
        {
            return await _authorizedScriptRepository.GetByIdAsync(scriptId);
        }

        public virtual async Task<AuthorizedScript> GetAuthorizedScriptByUrlAsync(string scriptUrl, int storeId)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return null;

            var query = _authorizedScriptRepository.Table
                .Where(script => script.ScriptUrl == scriptUrl && script.StoreId == storeId);

            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task InsertAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.InsertAsync(script);
        }

        public virtual async Task UpdateAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.UpdateAsync(script);
        }

        public virtual async Task DeleteAuthorizedScriptAsync(AuthorizedScript script)
        {
            ArgumentNullException.ThrowIfNull(script);

            await _authorizedScriptRepository.DeleteAsync(script);
        }

        public virtual async Task<bool> IsScriptAuthorizedAsync(string scriptUrl, int storeId)
        {
            if (string.IsNullOrEmpty(scriptUrl))
                return false;

            var script = await GetAuthorizedScriptByUrlAsync(scriptUrl, storeId);
            return script != null && script.IsActive;
        }

        public virtual async Task<IList<AuthorizedScript>> GetAuthorizedScriptsByDomainAsync(string domain, int storeId)
        {
            if (string.IsNullOrEmpty(domain))
                return new List<AuthorizedScript>();

            var query = _authorizedScriptRepository.Table
                .Where(script => script.Domain == domain && script.StoreId == storeId && script.IsActive);

            return await query.ToListAsync();
        }

        public virtual async Task<string> GenerateScriptHashAsync(string scriptUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(scriptUrl);
                response.EnsureSuccessStatusCode();

                var scriptContent = await response.Content.ReadAsStringAsync();

                using var sha384 = SHA384.Create();
                var hashBytes = sha384.ComputeHash(Encoding.UTF8.GetBytes(scriptContent));
                return Convert.ToBase64String(hashBytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public virtual async Task<bool> ValidateScriptIntegrityAsync(string scriptUrl, string expectedHash)
        {
            var currentHash = await GenerateScriptHashAsync(scriptUrl);
            return currentHash != null && currentHash == expectedHash;
        }

        public virtual async Task UpdateScriptHashAsync(int scriptId, string newHash)
        {
            var script = await GetAuthorizedScriptByIdAsync(scriptId);
            if (script != null)
            {
                script.ScriptHash = newHash;
                script.LastVerifiedUtc = DateTime.UtcNow;
                await UpdateAuthorizedScriptAsync(script);
            }
        }

        public virtual async Task<IList<AuthorizedScript>> GetExpiredScriptsAsync(int daysSinceLastVerified, int storeId = 0)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastVerified);

            var query = _authorizedScriptRepository.Table
                .Where(script => script.LastVerifiedUtc < cutoffDate && script.IsActive);

            if (storeId > 0)
                query = query.Where(script => script.StoreId == storeId);

            return await query.ToListAsync();
        }
    }
}