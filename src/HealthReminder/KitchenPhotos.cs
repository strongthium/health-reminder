using System.Windows.Media.Imaging;

namespace Strongthium.HealthReminder;

/// <summary>Photos of groceries made ready to send: turned upright, at most 1568 pixels on the long side (more isn't
/// read, only paid for), as JPEG.</summary>
internal static class KitchenPhotos
{
    private const int LongSide = 1568;

    public static readonly string Filter = "Photos (*.jpg; *.jpeg; *.png; *.webp; *.heic)|*.jpg;*.jpeg;*.png;*.webp;*.heic";

    public static (byte[] Data, string MediaType) Prepare(string path)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        BitmapSource image = frame;
        // Phone photos are often stored sideways, with the turn written in their metadata.
        if (frame.Metadata is BitmapMetadata metadata && metadata.ContainsQuery("System.Photo.Orientation") && metadata.GetQuery("System.Photo.Orientation") is ushort orientation)
        {
            var angle = orientation switch { 6 => 90, 3 => 180, 8 => 270, _ => 0 };
            if (angle != 0) image = new TransformedBitmap(image, new System.Windows.Media.RotateTransform(angle));
        }
        var scale = Math.Min(1.0, LongSide / (double)Math.Max(image.PixelWidth, image.PixelHeight));
        if (scale < 1) image = new TransformedBitmap(image, new System.Windows.Media.ScaleTransform(scale, scale));
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = new MemoryStream();
        encoder.Save(output);
        return (output.ToArray(), "image/jpeg");
    }
}
