using CSharpFunctionalExtensions;
using SkiaSharp;
using Svg.Skia;

namespace SvgBuild.Tasks;

static class SvgConverter
{
    static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

    public static Result Convert(ConvertSvg.ConversionRequest request) => Result.Success()
        .Bind(() => LoadSvg(request.Input))
        .Bind(svg => request.Format switch
        {
            "PNG" => SavePng(svg, request.Output),
            "ICO" => SaveIco(svg, request.Output),
            _ => Result.Failure($"Unsupported output format '{request.Format}'.")
        });

    static Result<SKSvg> LoadSvg(string path) => Result.Try(() =>
    {
        var svg = new SKSvg();
        using var stream = File.OpenRead(path);
        svg.Load(stream);
        return svg.Picture is null
            ? throw new InvalidOperationException($"The SVG '{path}' does not contain drawable content.")
            : svg;
    }, exception => $"Failed to load SVG '{path}': {exception.Message}");

    static Result SavePng(SKSvg svg, string outputPath)
    {
        try
        {
            return RenderBitmap(svg, GetNaturalSize(svg))
                .Bind(bitmap =>
                {
                    using (bitmap)
                    {
                        return WritePng(bitmap, outputPath);
                    }
                });
        }
        finally
        {
            svg.Dispose();
        }
    }

    static Result SaveIco(SKSvg svg, string outputPath)
    {
        try
        {
            return RenderIconBitmaps(svg)
                .Bind(bitmaps =>
                {
                    try
                    {
                        return WriteIco(bitmaps, outputPath);
                    }
                    finally
                    {
                        DisposeAll(bitmaps);
                    }
                });
        }
        finally
        {
            svg.Dispose();
        }
    }

    static Result<SKBitmap> RenderBitmap(SKSvg svg, SKSizeI targetSize)
    {
        var picture = svg.Picture ?? throw new InvalidOperationException("Invalid SVG picture.");
        var bounds = picture.CullRect;
        var boundedWidth = Math.Max(bounds.Width, 1f);
        var boundedHeight = Math.Max(bounds.Height, 1f);
        var imageInfo = new SKImageInfo(targetSize.Width, targetSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var scale = Math.Min(
            targetSize.Width / boundedWidth,
            targetSize.Height / boundedHeight);

        var xOffset = (targetSize.Width - boundedWidth * scale) / 2f;
        var yOffset = (targetSize.Height - boundedHeight * scale) / 2f;

        canvas.Translate(xOffset, yOffset);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        return Result.Success(SKBitmap.FromImage(snapshot));
    }

    static SKSizeI GetNaturalSize(SKSvg svg)
    {
        var bounds = svg.Picture?.CullRect ?? SKRect.Empty;
        var width = (int)Math.Ceiling(bounds.Width);
        var height = (int)Math.Ceiling(bounds.Height);

        if (width <= 0 || height <= 0)
        {
            width = 256;
            height = 256;
        }

        return new SKSizeI(Math.Max(width, 1), Math.Max(height, 1));
    }

    static Result WritePng(SKBitmap bitmap, string outputPath) => Result.Try(() =>
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(stream);
    }, exception => $"Failed to save PNG '{outputPath}': {exception.Message}");

    static Result<List<SKBitmap>> RenderIconBitmaps(SKSvg svg)
    {
        var renderings = new List<SKBitmap>();
        foreach (var size in IconSizes)
        {
            var result = RenderBitmap(svg, new SKSizeI(size, size));

            if (result.IsFailure)
            {
                DisposeAll(renderings);
                return Result.Failure<List<SKBitmap>>(result.Error);
            }

            renderings.Add(result.Value);
        }

        return Result.Success(renderings);
    }

    static Result WriteIco(List<SKBitmap> bitmaps, string outputPath) => Result.Try(() =>
    {
        if (bitmaps.Count == 0)
        {
            throw new InvalidOperationException("No icon images were generated.");
        }

        var pngData = bitmaps.Select(EncodePng).ToList();
        var offsets = new List<uint>(bitmaps.Count);
        var offset = 6 + 16 * bitmaps.Count;
        foreach (var data in pngData)
        {
            offsets.Add((uint)offset);
            offset += data.Length;
        }

        using var stream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)bitmaps.Count);

        for (var i = 0; i < bitmaps.Count; i++)
        {
            var bitmap = bitmaps[i];
            var width = (byte)(bitmap.Width >= 256 ? 0 : bitmap.Width);
            var height = (byte)(bitmap.Height >= 256 ? 0 : bitmap.Height);
            var data = pngData[i];

            writer.Write(width);
            writer.Write(height);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)bitmap.Info.BitsPerPixel);
            writer.Write((uint)data.Length);
            writer.Write(offsets[i]);
        }

        foreach (var data in pngData)
        {
            writer.Write(data);
        }
    }, exception => $"Failed to save ICO '{outputPath}': {exception.Message}");

    static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    static void DisposeAll(IEnumerable<SKBitmap> bitmaps)
    {
        foreach (var bitmap in bitmaps)
        {
            bitmap.Dispose();
        }
    }
}
