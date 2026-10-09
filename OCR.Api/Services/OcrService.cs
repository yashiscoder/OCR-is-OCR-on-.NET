using System.Diagnostics;
using OCR.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace OCR.Api.Services;

public class OcrService : IOcrService
{
    private readonly IWebHostEnvironment _environment;

    public OcrService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }
    private static string ExtractTableText(
    TesseractEngine engine,
    byte[] pngBytes)
    {
        using var pix = Pix.LoadFromMemory(pngBytes);

        using var page = engine.Process(
            pix,
            PageSegMode.SparseText);

        return page.GetText().Trim();
    }
    private static byte[] CropAndPrepareTable(
    Image image)
    {
        int x = (int)(image.Width * 0.01);
        int y = (int)(image.Height * 0.61);

        int width = (int)(image.Width * 0.95);
        int height = (int)(image.Height * 0.24);

        using var croppedImage = image.Clone(
            context => context.Crop(
                new Rectangle(x, y, width, height)));

        croppedImage.Mutate(context =>
        {
            context
                .Grayscale()
                .Contrast(1.3f)
                .Resize(
                    croppedImage.Width * 2,
                    croppedImage.Height * 2);
        });

        using var output = new MemoryStream();

        croppedImage.SaveAsPng(output);

        return output.ToArray();
    }
    public async Task<OcrResponse> ProcessAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var tessDataPath = Path.Combine(
            _environment.ContentRootPath,
            "tessdata");

        // 1. Load the uploaded image.
        await using var inputStream = new MemoryStream();

        await file.CopyToAsync(inputStream, cancellationToken);
        inputStream.Position = 0;

        using var originalImage = await Image.LoadAsync(
            inputStream,
            cancellationToken);

        int originalWidth = originalImage.Width;
        int originalHeight = originalImage.Height;

        // 2. Prepare the image for OCR.
        // Keep the original image unchanged so we can preserve
        // the original dimensions in the API response.
        using var processedImage = originalImage.Clone(image =>
        {
            image
                .Grayscale()
                .Contrast(1.3f)
                .Resize(
                    checked(originalWidth * 2),
                    checked(originalHeight * 2));
        });

        // 3. Encode the processed image as PNG.
        await using var pngStream = new MemoryStream();

        await processedImage.SaveAsPngAsync(
            pngStream,
            cancellationToken);

        byte[] pngBytes = pngStream.ToArray();

        cancellationToken.ThrowIfCancellationRequested();

        // 4. Initialize Tesseract.
        using var engine = new TesseractEngine(
            tessDataPath,
            "eng",
            EngineMode.Default);

        // Preserve spacing where possible, especially in documents.
        engine.SetVariable("preserve_interword_spaces", "1");

        // 5. Run OCR.
        using var pix = Pix.LoadFromMemory(pngBytes);
        using var page = engine.Process(pix, PageSegMode.Auto);

        string fullText = page.GetText().Trim();

        // Additional table OCR
        byte[] tableBytes = CropAndPrepareTable(originalImage);

        string tableText = ExtractTableText(engine, tableBytes);
        double overallConfidence = Math.Clamp(
            page.GetMeanConfidence(),
            0.0,
            1.0);

        // 6. Extract recognized lines and their bounding boxes.
        var lines = new List<OcrLine>();

        double scaleX =
            (double)originalWidth / processedImage.Width;

        double scaleY =
            (double)originalHeight / processedImage.Height;

        using (var iterator = page.GetIterator())
        {
            iterator.Begin();

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!iterator.TryGetBoundingBox(
                    PageIteratorLevel.TextLine,
                    out var rectangle))
                {
                    continue;
                }

                string? lineText = iterator
                    .GetText(PageIteratorLevel.TextLine)
                    ?.Trim();

                if (string.IsNullOrWhiteSpace(lineText))
                {
                    continue;
                }

                double lineConfidence =
                    iterator.GetConfidence(
                        PageIteratorLevel.TextLine) / 100.0;

                lines.Add(new OcrLine
                {
                    Text = lineText,

                    Confidence = Math.Clamp(
                        lineConfidence,
                        0.0,
                        1.0),

                    // Convert processed-image coordinates
                    // back to coordinates in the original image.
                    BoundingBox = new BoundingBox
                    {
                        X = (int)Math.Round(
                            rectangle.X1 * scaleX),

                        Y = (int)Math.Round(
                            rectangle.Y1 * scaleY),

                        Width = (int)Math.Round(
                            rectangle.Width * scaleX),

                        Height = (int)Math.Round(
                            rectangle.Height * scaleY)
                    }
                });
            }
            while (iterator.Next(PageIteratorLevel.TextLine));
        }

        stopwatch.Stop();

        // 7. Return the existing API response structure.
        return new OcrResponse
        {
            Success = true,
            FileName = Path.GetFileName(file.FileName),
            Text = fullText,
            TableText = tableText,
            Confidence = overallConfidence,
            Width = originalWidth,
            Height = originalHeight,
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
            Lines = lines
        }; 
    }
}