namespace OCR.Api.Models;

public class OcrResponse
{
    public bool Success { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public double Confidence { get; set; }
}