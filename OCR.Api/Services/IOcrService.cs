using OCR.Api.Models;

namespace OCR.Api.Services;

public interface IOcrService
{
    Task<OcrResponse> ProcessAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}