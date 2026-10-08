using OCR.Api.Models;

namespace OCR.Api.Services;

public class OcrService : IOcrService
{
    public async Task<OcrResponse> ProcessAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        return new OcrResponse
        {
            Success = true,
            FileName = file.FileName,
            Text = "OCR service is working.",
            Confidence = 1.0
        };
    }
}