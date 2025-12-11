using System.Threading.Tasks;
using NightHunt.Common;
using NightHunt.Data.DTOs;

namespace NightHunt.Services.Backend
{
    public interface IBackendClient
    {
        Task<ApiResult<T>> GetAsync<T>(string endpoint);
        Task<ApiResult<T>> PostAsync<T>(string endpoint, object data = null);
        Task<ApiResult<T>> PutAsync<T>(string endpoint, object data = null);
        Task<ApiResult<T>> DeleteAsync<T>(string endpoint);
        void SetAuthToken(string token);
        void ClearAuthToken();
    }
}

