using Google.Cloud.SecretManager.V1;
using Dejan_Camilleri_SWD63B.Interfaces;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class GoogleSecretManagerService : ISecretManagerService
    {
        private readonly string _projectId;
        private readonly SecretManagerServiceClient _client;
        private readonly ICloudLoggingService _logger;

        public GoogleSecretManagerService(string projectId, ICloudLoggingService logger)
        {
            _projectId = projectId;
            _client = SecretManagerServiceClient.Create();
            _logger = logger;

            _logger.LogInformationAsync($"GoogleSecretManagerService initialized for project: {projectId}").Wait();
        }

        /// <summary>
        /// Retrieves a secret from Google Cloud Secret Manager.
        /// </summary>
        /// <param name="secretName"></param>
        /// <returns></returns>
        public async Task<string> GetSecretAsync(string secretName)
        {
            await _logger.LogDebugAsync($"Retrieving secret: {secretName}");

            try
            {
                var secretVersionName = new SecretVersionName(_projectId, secretName, "latest");
                var result = await _client.AccessSecretVersionAsync(secretVersionName);

                await _logger.LogDebugAsync($"Successfully retrieved secret: {secretName}");
                return result.Payload.Data.ToStringUtf8();
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Error retrieving secret: {secretName}", ex);
                throw;
            }
        }


        /// <summary>
        /// Loads secrets into the configuration.
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public async Task LoadSecretsIntoConfigurationAsync(IConfiguration configuration)
        {
            await _logger.LogInformationAsync("Loading secrets into configuration");

            try
            {
                var map = new Dictionary<string, string>
                {
                    ["ClientId"] = "Authentication:Google:ClientId",
                    ["ClientSecret"] = "Authentication:Google:ClientSecret"
                };

                foreach (var secret in map)
                {
                    configuration[secret.Key] = secret.Value;
                    await _logger.LogDebugAsync($"Secret loaded into configuration: {secret.Key}");
                }

                await _logger.LogInformationAsync($"Successfully loaded {map.Count} secrets into configuration");
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync("Error loading secrets into configuration", ex);
                throw;
            }
        }
    }
}
