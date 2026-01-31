using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Ticketing.Api.DTOs;

namespace Ticketing.Api.Services
{
    public interface IAzureBlobProvider
    {
        Task<BlobDto?> DownloadBlobAsync(string fileName);
        Task<BlobResponseDto> UploadFileBlobAsync(IFormFile file, string? ticketId);
        Task<List<BlobDto>> ListByTicketAsync(string? ticketId);
        Task<BlobResponseDto> DeleteBlobAsync(string fileName);
    }
    public class AzureBlobProvider : IAzureBlobProvider
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private readonly BlobContainerClient _filesContainer;
        private readonly ILogger<AzureBlobProvider> _logger;

        public AzureBlobProvider(
            BlobServiceClient blobServiceClient, 
            IConfiguration configuration,
            ILogger<AzureBlobProvider> logger)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
            _logger = logger;
            var containerName = _configuration["FileStorage:Azure:ContainerName"];
            _filesContainer = _blobServiceClient.GetBlobContainerClient(containerName);
            
            _logger.LogInformation(
                "AzureBlobProvider initialized with container: {ContainerName}",
                containerName
            );
        }

        public async Task<BlobResponseDto> DeleteBlobAsync(string fileName)
        {
            try
            {
                _logger.LogInformation("Attempting to delete blob: {FileName}", fileName);
                
                BlobClient file = _filesContainer.GetBlobClient(fileName);

                bool exists = await file.ExistsAsync();
                if (!exists)
                {
                    _logger.LogWarning("Blob not found for deletion: {FileName}", fileName);
                    return new BlobResponseDto 
                    { 
                        Error = true, 
                        Status = $"File '{fileName}' not found." 
                    };
                }

                await file.DeleteAsync();
                
                _logger.LogInformation("Blob deleted successfully: {FileName}", fileName);
                
                return new BlobResponseDto 
                { 
                    Error = false, 
                    Status = $"File '{fileName}' has been successfully deleted." 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob: {FileName}. Error: {ErrorMessage}", fileName, ex.Message);
                throw;
            }
        }

        public async Task<BlobDto?> DownloadBlobAsync(string fileName)
        {
            try
            {
                _logger.LogInformation("Attempting to download blob: {FileName}", fileName);
                
                BlobClient file = _filesContainer.GetBlobClient(fileName);
                
                if (!await file.ExistsAsync())
                {
                    _logger.LogWarning("Blob not found for download: {FileName}", fileName);
                    return null;
                }

                var data = await file.OpenReadAsync();
                Stream blobContent = data;

                var content = await file.DownloadContentAsync();
                string name = fileName;
                string contentType = content.Value.Details.ContentType;

                _logger.LogInformation(
                    "Blob downloaded successfully. FileName: {FileName}, ContentType: {ContentType}, Size: {Size} bytes",
                    fileName,
                    contentType,
                    content.Value.Details.ContentLength
                );

                return new BlobDto 
                { 
                    Content = blobContent, 
                    Name = name, 
                    ContentType = contentType 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob: {FileName}. Error: {ErrorMessage}", fileName, ex.Message);
                throw;
            }
        }

        public async Task<List<BlobDto>> ListByTicketAsync(string? ticketId)
        {
            _logger.LogInformation(
                "List blobs request. TicketId filter: {TicketId}",
                ticketId ?? "None (all blobs)"
            );

            try
            {
                var baseUri = _filesContainer.Uri.ToString();
                var files = new List<BlobDto>();

                if (!string.IsNullOrEmpty(ticketId))
                {
                    var tagFilter = $"\"ticketId\" = '{ticketId}'";
                    
                    await foreach (var taggedBlob in _filesContainer.FindBlobsByTagsAsync(tagFilter))
                    {
                        // Get additional properties for the blob
                        var blobClient = _filesContainer.GetBlobClient(taggedBlob.BlobName);
                        var properties = await blobClient.GetPropertiesAsync();

                        files.Add(new BlobDto
                        {
                            Uri = $"{baseUri}/{taggedBlob.BlobName}",
                            Name = taggedBlob.BlobName,
                            ContentType = properties.Value.ContentType
                        });
                    }
                }
                else
                {
                    await foreach (var blobItem in _filesContainer.GetBlobsAsync())
                    {
                        files.Add(new BlobDto
                        {
                            Uri = $"{baseUri}/{blobItem.Name}",
                            Name = blobItem.Name,
                            ContentType = blobItem.Properties.ContentType
                        });
                    }
                }

                _logger.LogInformation(
                    "List blobs completed. Found {Count} blobs. TicketId filter: {TicketId}",
                    files.Count,
                    ticketId ?? "None"
                );

                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error listing blobs for TicketId: {TicketId}. Error: {ErrorMessage}",
                    ticketId,
                    ex.Message
                );
                throw;
            }
        }

        public async Task<BlobResponseDto> UploadFileBlobAsync(IFormFile file, string? ticketId)
        {
            BlobResponseDto response = new();

            try
            {
                if (file == null)
                {
                    response.Status = "No file provided";
                    response.Error = true;
                    return response;
                }

                var allowedMimeTypes = _configuration.GetSection("FileStorage:AllowedMimeTypes").Get<string[]>() ?? [];
                if (allowedMimeTypes.Length > 0 && !allowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                {
                    response.Status = $"File type '{file.ContentType}' is not allowed. Allowed types: {string.Join(", ", allowedMimeTypes)}";
                    _logger.LogWarning(
                        "File upload rejected due to invalid MIME type. FileName: {FileName}, ContentType: {ContentType}, TicketId: {TicketId}, AllowedTypes: {AllowedTypes}",
                        file.FileName,
                        file.ContentType,
                        ticketId ?? "None",
                        string.Join(", ", allowedMimeTypes)
                    );

                    response.Error = true;
                    return response;
                }

                var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? [];
                var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (allowedExtensions.Length > 0 && !allowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    response.Status = $"File extension '{fileExtension}' is not allowed. Allowed extensions: {string.Join(", ", allowedExtensions)}";
                    response.Error = true;
                    _logger.LogWarning(
                        "File upload rejected due to invalid extension. FileName: {FileName}, Extension: {Extension}, TicketId: {TicketId}, AllowedExtensions: {AllowedExtensions}",
                        file.FileName,
                        fileExtension ?? "None",
                        ticketId ?? "None",
                        string.Join(", ", allowedExtensions)
                    );
                    return response;
                }

                var maxFileSizeBytes = _configuration.GetValue<long>("FileStorage:MaxFileSizeBytes", 10485760);
                if (file.Length > maxFileSizeBytes)
                {
                    var maxSizeMB = maxFileSizeBytes / 1024.0 / 1024.0;
                    var fileSizeMB = file.Length / 1024.0 / 1024.0;
                    response.Status = $"File size ({fileSizeMB:F2} MB) exceeds the maximum allowed size ({maxSizeMB:F2} MB)";
                    response.Error = true;
                    _logger.LogWarning(
                        "File upload rejected due to size limit. FileName: {FileName}, FileSize: {FileSizeMB} MB, MaxSize: {MaxSizeMB} MB, TicketId: {TicketId}",
                        file.FileName,
                        fileSizeMB,
                        maxSizeMB,
                        ticketId ?? "None"
                    );
                    return response;
                }

                var fileName = !string.IsNullOrEmpty(ticketId) ? $"{ticketId}_{file.FileName}" : file.FileName;
                BlobClient client = _filesContainer.GetBlobClient(fileName);

                bool exists = await client.ExistsAsync();
                if (exists)
                {
                    _logger.LogWarning(
                        "File already exists and will be overwritten. FileName: {FileName}, TicketId: {TicketId}",
                        fileName,
                        ticketId ?? "None"
                    );
                }

                var options = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    }
                };

                if (!string.IsNullOrEmpty(ticketId))
                {
                    options.Tags = new Dictionary<string, string>
                    {
                        { "ticketId", ticketId }
                    };
                }

                await using (Stream data = file.OpenReadStream())
                {
                    await client.UploadAsync(data, options);
                }

                response.Status = $"File '{file.FileName}' was uploaded successfully";
                _logger.LogInformation(
                    "File uploaded successfully. FileName: {FileName}, StoredName: {StoredName}, Size: {SizeBytes} bytes, ContentType: {ContentType}, TicketId: {TicketId}",
                    file.FileName,
                    fileName,
                    file.Length,
                    file.ContentType,
                    ticketId ?? "None"
                );
                response.Error = false;
                response.Blob.Uri = client.Uri.AbsoluteUri;
                response.Blob.Name = client.Name;
                response.Blob.ContentType = file.ContentType;
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error uploading file. FileName: {FileName}, TicketId: {TicketId}, Error: {ErrorMessage}",
                    file?.FileName ?? "Unknown",
                    ticketId ?? "None",
                    ex.Message
                );
                throw;
            }
        }
    }
}
