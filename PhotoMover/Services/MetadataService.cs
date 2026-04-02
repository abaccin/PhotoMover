using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using PhotoMover.Models;

namespace PhotoMover.Services;

public class MetadataService
{
    public PhotoInfo? ExtractMetadata(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        var isVideo = Options.VideoExtensions.Contains(extension);
        var mediaType = isVideo ? MediaType.Video : MediaType.Photo;

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            
            DateTime? captureDateTime = null;
            string? cameraModel = null;

            if (isVideo)
            {
                // Try QuickTime metadata for videos
                var quickTimeDir = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (quickTimeDir != null)
                {
                    if (quickTimeDir.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var created))
                    {
                        captureDateTime = created;
                    }
                }

                // Also try QuickTime metadata directory for camera info
                var metaDir = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                if (metaDir != null)
                {
                    cameraModel = metaDir.GetDescription(QuickTimeMetadataHeaderDirectory.TagModel);
                }

                // Fallback to Exif in video files (some cameras embed it)
                if (captureDateTime == null)
                {
                    captureDateTime = TryGetExifDateTime(directories);
                }
                if (cameraModel == null)
                {
                    cameraModel = TryGetExifCameraModel(directories);
                }
            }
            else
            {
                // Photos - try EXIF
                captureDateTime = TryGetExifDateTime(directories);
                cameraModel = TryGetExifCameraModel(directories);
            }

            return new PhotoInfo(
                SourcePath: filePath,
                CaptureDateTime: captureDateTime,
                CameraModel: SanitizeCameraModel(cameraModel),
                MediaType: mediaType,
                Extension: extension
            );
        }
        catch (Exception)
        {
            // File couldn't be read or has no metadata
            return new PhotoInfo(
                SourcePath: filePath,
                CaptureDateTime: null,
                CameraModel: null,
                MediaType: mediaType,
                Extension: extension
            );
        }
    }

    private static DateTime? TryGetExifDateTime(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var subIfdDir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        
        // Try DateTimeOriginal first
        if (subIfdDir?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) == true)
        {
            return original;
        }

        // Fallback to DateTimeDigitized
        if (subIfdDir?.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized) == true)
        {
            return digitized;
        }

        // Fallback to DateTime
        var ifd0Dir = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0Dir?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime) == true)
        {
            return dateTime;
        }

        return null;
    }

    private static string? TryGetExifCameraModel(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var ifd0Dir = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        return ifd0Dir?.GetDescription(ExifDirectoryBase.TagModel);
    }

    private static string? SanitizeCameraModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        // Replace spaces and special chars with dashes, trim, and limit length
        var sanitized = model
            .Trim()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace("/", "-")
            .Replace("\\", "-");

        // Remove consecutive dashes
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        // Limit to 30 chars for filename sanity
        if (sanitized.Length > 30)
        {
            sanitized = sanitized[..30];
        }

        return sanitized.Trim('-');
    }
}
