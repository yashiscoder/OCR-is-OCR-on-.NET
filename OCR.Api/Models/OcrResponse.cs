namespace OCR.Api.Models;

public class OcrResponse
{
    public bool Success { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string TableText { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public long ProcessingTimeMs { get; set; }

    public List<OcrLine> Lines { get; set; } = new();
}

public class OcrLine
{
    public string Text { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public BoundingBox BoundingBox { get; set; } = new();
}

public class BoundingBox
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}