using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.ComponentModel;

namespace Invoice.API.Services
{
    public class BlobUploader
    {
        private string _connectionString;
        private string _containerName="invoices";
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

  

        public BlobUploader(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }
        private string GetConnectionString()
        {
            // ✅ 1.  appsettings (works in local dev)
            _connectionString = _config["AzureStorage:BlobConnectionString"];
            if (!string.IsNullOrWhiteSpace(_connectionString))
                return _connectionString;

            // ✅ 3. Fallback to Key Vault
            var vaultUri = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");
            if (!string.IsNullOrWhiteSpace(vaultUri))
            {
                var client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
                var secret = client.GetSecret("teststorageconnection");
                _connectionString = secret.Value.Value;
                return _connectionString;
            }

            // ❌ If nothing found
            throw new InvalidOperationException("No blob connection string found in appsettings, env var, or key vault.");
        }


        public async Task<string> UploadAsync(byte[] fileBytes, string fileName)
        {
            _connectionString = GetConnectionString();
            var client = new BlobContainerClient(_connectionString, _containerName);

            try
            {
                // 🧠 For Azurite or Azure, silently skip if already exists
                await client.CreateIfNotExistsAsync(PublicAccessType.None);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                Console.WriteLine("⚠️ Container already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to create container: {ex.Message}");
                throw;
            }

            var blobClient = client.GetBlobClient(fileName);

            try
            {
                using var stream = new MemoryStream(fileBytes);
                await blobClient.UploadAsync(stream, overwrite: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Upload failed: {ex.Message}");
                throw;
            }

            return GenerateSasUrl(blobClient);
        }

        private string GenerateSasUrl(BlobClient blobClient)
        {
            StorageSharedKeyCredential credential;

            // For local development with Azurite
            if (_env.IsDevelopment())
            {
                credential = new StorageSharedKeyCredential(
                    "devstoreaccount1",
                    "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
                );
            }
            else
            {
                credential = GetCredentialFromConnectionString(_connectionString); //On production Use the connection string to get the credential
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = blobClient.BlobContainerName,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(6)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
            return $"{blobClient.Uri}?{sasToken}";
        }
        private StorageSharedKeyCredential GetCredentialFromConnectionString(string conn)
        {
            var connParts = conn.Split(';');
            string accountName = null, accountKey = null;

            foreach (var part in connParts)
            {
                if (part.StartsWith("AccountName="))
                    accountName = part.Substring("AccountName=".Length);
                else if (part.StartsWith("AccountKey="))
                    accountKey = part.Substring("AccountKey=".Length);
            }

            if (accountName == null || accountKey == null)
                throw new InvalidOperationException("Missing AccountName or AccountKey in connection string.");

            return new StorageSharedKeyCredential(accountName, accountKey);
        }


    }
}

