using System.Threading.Tasks;

namespace GeminiGUI.Services
{
    public interface IConfigurationService
    {
        Task<string?> GetApiKeyAsync();
        Task SetApiKeyAsync(string apiKey);
        Task<bool> HasApiKeyAsync();
    }
}