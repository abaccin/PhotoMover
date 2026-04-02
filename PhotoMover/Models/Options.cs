namespace PhotoMover.Models;

public enum OperationMode
{
    Copy,
    Move
}

public class Options
{
    public required string SourcePath { get; init; }
    public required string DestinationPath { get; init; }
    public OperationMode Mode { get; init; } = OperationMode.Copy;
    public bool DryRun { get; init; } = false;
    public bool SkipMissingExif { get; init; } = true;
    public int ProgressEvery { get; init; } = 100;
    public HashSet<string> Extensions { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff",
        ".cr2", ".nef", ".arw", ".dng", ".heic",
        ".mp4", ".mov", ".avi", ".mkv"
    };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".m4v", ".webm"
    };

    public static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Screenshots", "Download", "Downloads"
    };
}
