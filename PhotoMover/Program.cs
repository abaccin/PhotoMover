using System.CommandLine;
using PhotoMover.Models;
using PhotoMover.Services;

var srcOption = new Option<string?>(
    aliases: ["--src", "-s"],
    description: "Source folder path");

var dstOption = new Option<string?>(
    aliases: ["--dst", "-d"],
    description: "Destination folder path");

var modeOption = new Option<string>(
    aliases: ["--mode", "-m"],
    getDefaultValue: () => "copy",
    description: "Operation mode: copy or move");

var dryRunOption = new Option<bool>(
    aliases: ["--dry-run", "-n"],
    getDefaultValue: () => false,
    description: "Preview actions without making changes");

var skipMissingExifOption = new Option<bool>(
    aliases: ["--skip-missing-exif"],
    getDefaultValue: () => true,
    description: "Skip files without EXIF datetime");

var progressEveryOption = new Option<int>(
    aliases: ["--progress-every"],
    getDefaultValue: () => 100,
    description: "Print progress every N files (0 to disable)");

var extensionsOption = new Option<string[]>(
    aliases: ["--ext", "-e"],
    description: "Allowed extensions (repeatable)")
{
    AllowMultipleArgumentsPerToken = true
};

var rootCommand = new RootCommand("PhotoMover - Organize photos/videos by capture date")
{
    srcOption,
    dstOption,
    modeOption,
    dryRunOption,
    skipMissingExifOption,
    progressEveryOption,
    extensionsOption
};

rootCommand.SetHandler(async (src, dst, mode, dryRun, skipMissingExif, progressEvery, extensions) =>
{
    // Prompt for source if not provided
    if (string.IsNullOrWhiteSpace(src))
    {
        Console.Write("Enter source folder path: ");
        src = Console.ReadLine()?.Trim();
    }

    // Prompt for destination if not provided  
    if (string.IsNullOrWhiteSpace(dst))
    {
        Console.Write("Enter destination folder path: ");
        dst = Console.ReadLine()?.Trim();
    }

    if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dst))
    {
        Console.WriteLine("Error: Source and destination paths are required.");
        Environment.Exit(1);
        return;
    }

    var operationMode = mode.ToLowerInvariant() switch
    {
        "move" => OperationMode.Move,
        _ => OperationMode.Copy
    };

    var options = new Options
    {
        SourcePath = src,
        DestinationPath = dst,
        Mode = operationMode,
        DryRun = dryRun,
        SkipMissingExif = skipMissingExif,
        ProgressEvery = progressEvery
    };

    // Override extensions if provided
    if (extensions?.Length > 0)
    {
        options.Extensions.Clear();
        foreach (var ext in extensions)
        {
            var normalized = ext.StartsWith('.') ? ext : $".{ext}";
            options.Extensions.Add(normalized);
        }
    }

    var metadataService = new MetadataService();
    var namingService = new NamingService();
    var fileOperations = new FileOperations();
    var organizer = new OrganizerService(metadataService, namingService, fileOperations);

    organizer.Organize(options);

}, srcOption, dstOption, modeOption, dryRunOption, skipMissingExifOption, progressEveryOption, extensionsOption);

return await rootCommand.InvokeAsync(args);
