namespace PhotoMover.Models;

public enum MediaType
{
    Photo,
    Video
}

public record PhotoInfo(
    string SourcePath,
    DateTime? CaptureDateTime,
    string? CameraModel,
    MediaType MediaType,
    string Extension
);
