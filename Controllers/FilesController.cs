using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Api.DTOs;
using Ticketing.Api.Services;

namespace Ticketing.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IAzureBlobProvider _azureBlobProvider;

    public FilesController(IAzureBlobProvider azureBlobProvider)
    {
        _azureBlobProvider = azureBlobProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BlobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFiles([FromQuery] string? ticketId)
    {
        try
        {
            var files = await _azureBlobProvider.ListByTicketAsync(ticketId);
            return Ok(files);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while retrieving files." });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(BlobDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] string? ticketId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided or file is empty." });
        }

        try
        {
            var result = await _azureBlobProvider.UploadFileBlobAsync(file, ticketId);
            
            if (result.Error)
            {
                return BadRequest(new { message = result.Status });
            }

            return CreatedAtAction(
                nameof(GetFile),
                new { fileName = result.Blob.Name },
                result.Blob);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while uploading the file." });
        }
    }

    [HttpGet("{fileName}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { message = "File name is required." });
        }

        try
        {
            var result = await _azureBlobProvider.DownloadBlobAsync(fileName);
            
            if (result == null)
            {
                return NotFound(new { message = $"File '{fileName}' not found." });
            }

            return File(result.Content!, result.ContentType ?? "application/octet-stream", result.Name);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while downloading the file." });
        }
    }

    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest(new { message = "File name is required." });
        }

        try
        {
            var result = await _azureBlobProvider.DeleteBlobAsync(fileName);
            
            if (result.Error)
            {
                return NotFound(new { message = result.Status });
            }

            return NoContent();
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "An error occurred while deleting the file." });
        }
    }
}
