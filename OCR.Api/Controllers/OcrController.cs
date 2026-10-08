using Microsoft.AspNetCore.Mvc;
using OCR.Api.Services;

namespace OCR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly IOcrService _ocrService;

    public OcrController(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    [HttpPost]
    public async Task<IActionResult> ExtractText(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                message = "Please upload an image."
            });
        }

        var result = await _ocrService.ProcessAsync(
            file,
            cancellationToken);

        return Ok(result);
    }
}