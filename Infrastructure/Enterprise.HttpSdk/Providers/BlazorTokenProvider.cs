
using Blazored.LocalStorage;
using Enterprise.HttpSdk.Interfaces;

namespace Enterprise.HttpSdk.Providers
{
    public class BlazorTokenProvider : ITokenProvider
    {
        private readonly ILocalStorageService _localStorage;

        public BlazorTokenProvider(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
        {
            return await _localStorage.GetItemAsync<string>("access_token", cancellationToken);
        }
    }
}
