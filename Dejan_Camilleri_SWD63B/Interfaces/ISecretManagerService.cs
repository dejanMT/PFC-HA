namespace Dejan_Camilleri_SWD63B.Interfaces
{
    public interface ISecretManagerService
    {
        Task<string> GetSecretAsync(string secretName);
        Task LoadSecretsIntoConfigurationAsync(IConfiguration configuration);
    }
}
