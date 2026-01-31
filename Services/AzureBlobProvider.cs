using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services
{
    public interface IAzureBlobProvider
    {
        Task<BlobInfo> GetBlobAsync(string fileName);
        Task<BlobResponseDto> UploadFileBlobAsync(IFormFile file, string? ticketId);
        Task<List<BlobDto>> ListByTicketAsync(string? ticketId);
        Task DeleteBlobAsync(string fileName);
    }
    public class AzureBlobProvider : IAzureBlobProvider
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _filesContainer;

        public AzureBlobProvider(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            var containerName = _configuration["FileStorage:Azure:ContainerName"];
            _filesContainer = _blobServiceClient.GetBlobContainerClient(containerName);
        }

        public Task DeleteBlobAsync(string fileName)
        {
            return null;
        }

        public async Task<BlobInfo> GetBlobAsync(string fileName)
        {
            //var containerName = _configuration["FileStorage:Azure:ContainerName"];
            //var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            //var fileBlobClient = containerClient.GetBlobClient(fileName);
            //var fileDownload = await fileBlobClient.DownloadContentAsync().ConfigureAwait(false);
            return null;
        }

        public async Task<List<BlobDto>> ListByTicketAsync(string? ticketId)
        {
            var baseUri = _filesContainer.Uri.ToString();
            var files = new List<BlobDto>();
            
            await foreach (var blobItem in _filesContainer.GetBlobsAsync())
            {
                // Filter by ticketId if provided - only include blobs whose name contains the ticketId
                if (!string.IsNullOrEmpty(ticketId) && !blobItem.Name.Contains(ticketId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                files.Add(new BlobDto
                {
                    Uri = $"{baseUri}/{blobItem.Name}",
                    Name = blobItem.Name,
                    ContentType = blobItem.Properties.ContentType
                });
            }

            return files;
        }

        public async Task<BlobResponseDto> UploadFileBlobAsync(IFormFile file, string? ticketId)
        {
            BlobResponseDto response = new();
            var fileName = !string.IsNullOrEmpty(ticketId) ? $"{ticketId}_{file?.FileName}" : file?.FileName;
            BlobClient client = _filesContainer.GetBlobClient(fileName);

            if (file == null)
            {
                response.Status = "No file provided";
                response.Error = true;
                return response;
            }

            await using (Stream data = file.OpenReadStream())
            {
                await client.UploadAsync(data);
            }

            //TODO: add log for fileName variable cause
            //i dont want return the name of saved file to user only Log to admin logs
            response.Status = $"File {file.FileName} was uploaded succesfully";
            response.Error = false;
            return response;
        }
    }
}
