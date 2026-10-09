using Microsoft.AspNetCore.Mvc;
using OCR.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace OCR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".tif",
        ".tiff"
    };

    private readonly IOcrService _ocrService;

    public OcrController(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> ExtractText(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        // 1. Check whether a file was uploaded.
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please upload a non-empty image."
            });
        }

        // 2. Check the file size.
        if (file.Length > MaxFileSize)
        {
            return BadRequest(new
            {
                success = false,
                message = "Maximum file size is 10 MB."
            });
        }

        // 3. Check the file extension.
        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                success = false,
                message = "Supported formats: JPG, JPEG, PNG, BMP, TIFF."
            });
        }

        // 4. Check whether the uploaded content is a valid image.
        if (!await IsValidImageAsync(file, cancellationToken))
        {
            return BadRequest(new
            {
                success = false,
                message = "The uploaded file is not a valid or supported image."
            });
        }

        // 5. Process the image using the OCR service.
        try
        {
            var result = await _ocrService.ProcessAsync(
                file,
                cancellationToken);

            return Ok(result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "OCR processing failed. Please try another image."
            });
        }
    }

    private static async Task<bool> IsValidImageAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();

            var info = await Image.IdentifyAsync(
                stream,
                cancellationToken);

            return info is not null
                && info.Width > 0
                && info.Height > 0;
        }
        catch (UnknownImageFormatException)
        {
            return false;
        }
        catch (InvalidImageContentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
