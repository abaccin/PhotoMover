using PhotoMover.Models;
using Spectre.Console;

namespace PhotoMover.Services;

public class OrganizerService
{
    private readonly MetadataService _metadataService;
    private readonly NamingService _namingService;
    private readonly FileOperations _fileOperations;

    public OrganizerService(MetadataService metadataService, NamingService namingService, FileOperations fileOperations)
    {
        _metadataService = metadataService;
        _namingService = namingService;
        _fileOperations = fileOperations;
    }

    // Escape special characters for Spectre.Console markup
    private static string Esc(string text) => Markup.Escape(text);

    public void Organize(Options options)
    {
        AnsiConsole.MarkupLine($"[bold]PhotoMover[/]");
        AnsiConsole.MarkupLine($"Source: [blue]{options.SourcePath}[/]");
        AnsiConsole.MarkupLine($"Destination: [blue]{options.DestinationPath}[/]");
        AnsiConsole.MarkupLine($"Mode: [yellow]{options.Mode}[/]");
        if (options.DryRun)
        {
            AnsiConsole.MarkupLine("[yellow]DRY RUN - No changes will be made[/]");
        }
        AnsiConsole.WriteLine();

        var stats = new Stats();
        var files = EnumerateFiles(options, stats);
        stats.TotalToProcess = files.Count;

        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .Start(ctx =>
            {
                var task = ctx.AddTask("[green]Processing files[/]", maxValue: files.Count);

                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    ProcessFile(file, options, stats);
                    task.Increment(1);

                    if (options.ProgressEvery > 0 && (i + 1) % options.ProgressEvery == 0)
                    {
                        AnsiConsole.MarkupLine($"  Processed {i + 1} / {files.Count} files...");
                    }
                }
            });

        PrintSummary(stats, options);
        Reconcile(stats, options);
    }

    private List<string> EnumerateFiles(Options options, Stats stats)
    {
        var files = new List<string>();

        if (!System.IO.Directory.Exists(options.SourcePath))
        {
            AnsiConsole.MarkupLine($"[red]Source folder does not exist: {options.SourcePath}[/]");
            return files;
        }

        var allFiles = System.IO.Directory.EnumerateFiles(options.SourcePath, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            // Check if in ignored folder
            var relativePath = Path.GetRelativePath(options.SourcePath, file);
            var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            if (pathParts.Any(part => Options.IgnoredFolders.Contains(part)))
            {
                stats.IgnoredFolders++;
                continue;
            }

            // Check for DownloadConflict in filename
            var fileName = Path.GetFileName(file);
            if (fileName.Contains("DownloadConflict", StringComparison.OrdinalIgnoreCase))
            {
                stats.IgnoredConflicts++;
                continue;
            }

            // Check extension
            var ext = Path.GetExtension(file);
            if (options.Extensions.Contains(ext))
            {
                files.Add(file);
            }
            else
            {
                stats.IgnoredExtensions++;
            }
        }

        AnsiConsole.MarkupLine($"Found [green]{files.Count}[/] files to process");
        return files;
    }

    private void ProcessFile(string filePath, Options options, Stats stats)
    {
        var info = _metadataService.ExtractMetadata(filePath);
        
        if (info == null)
        {
            stats.Errors++;
            return;
        }

        // Check if this is a WhatsApp file
        if (_namingService.IsWhatsAppFile(filePath))
        {
            ProcessWhatsAppFile(filePath, info, options, stats);
            return;
        }

        // Check if this is an iOS file (yyyyMMdd_HHmmss_iOS pattern)
        if (_namingService.IsIosFile(filePath))
        {
            ProcessIosFile(filePath, info, options, stats);
            return;
        }

        // Check if this is a Windows Phone file (WP_yyyyMMdd_nnn pattern)
        if (_namingService.IsWindowsPhoneFile(filePath))
        {
            ProcessWindowsPhoneFile(filePath, info, options, stats);
            return;
        }

        // Check if this is an old camera file (Imgyyyy-MM-dd_nnnn pattern)
        if (_namingService.IsOldCameraFile(filePath))
        {
            ProcessOldCameraFile(filePath, info, options, stats);
            return;
        }

        // For videos without EXIF, try file attributes before skipping
        if (info.CaptureDateTime == null && info.MediaType == MediaType.Video)
        {
            ProcessVideoWithoutExif(filePath, info, options, stats);
            return;
        }

        // For photos without EXIF, try file attributes as fallback
        if (info.CaptureDateTime == null)
        {
            var fileDateTime = _namingService.GetFileDateTime(filePath);
            if (fileDateTime != null)
            {
                // Use file date as fallback
                ProcessPhotoWithFileDate(filePath, info, fileDateTime.Value, options, stats);
                return;
            }
            
            if (options.SkipMissingExif)
            {
                stats.SkippedNoExif++;
                stats.SkippedFiles.Add(filePath);
                return;
            }
            else
            {
                stats.Errors++;
                AnsiConsole.MarkupLine($"[yellow]No EXIF datetime: {Esc(filePath)}[/]");
                return;
            }
        }

        // Videos go to Videos folder, photos go to regular folders
        var destFolder = info.MediaType == MediaType.Video
            ? _namingService.GetVideoDestinationFolder(options.DestinationPath, info.CaptureDateTime.Value)
            : _namingService.GetDestinationFolder(options.DestinationPath, info.CaptureDateTime.Value);
        var fileName = _namingService.GetFileName(info);
        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, info.CaptureDateTime);
            stats.Processed++;

            if (info.MediaType == MediaType.Video)
                stats.Videos++;
            else
                stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessWhatsAppFile(string filePath, PhotoInfo info, Options options, Stats stats)
    {
        // Try to get datetime from filename first, then file attributes
        var captureDateTime = _namingService.ParseWhatsAppDateTime(filePath) 
                              ?? _namingService.GetFileDateTime(filePath);

        if (captureDateTime == null)
        {
            stats.SkippedNoExif++;
            stats.SkippedFiles.Add(filePath);
            return;
        }

        var isVideo = info.MediaType == MediaType.Video;
        var destFolder = _namingService.GetWhatsAppDestinationFolder(options.DestinationPath, captureDateTime.Value);
        var fileName = _namingService.GetWhatsAppFileName(captureDateTime.Value, info.Extension, isVideo);
        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            stats.WhatsApp++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, captureDateTime);
            stats.Processed++;
            stats.WhatsApp++;

            if (isVideo)
                stats.Videos++;
            else
                stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing WhatsApp file {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessIosFile(string filePath, PhotoInfo info, Options options, Stats stats)
    {
        // Parse datetime from iOS filename (yyyyMMdd_HHmmss_iOS)
        var captureDateTime = _namingService.ParseIosDateTime(filePath)
                              ?? _namingService.GetFileDateTime(filePath);

        if (captureDateTime == null)
        {
            stats.SkippedNoExif++;
            stats.SkippedFiles.Add(filePath);
            return;
        }

        var isVideo = info.MediaType == MediaType.Video;
        // iOS files go to normal folder structure (they're real photos, just without EXIF)
        var destFolder = _namingService.GetDestinationFolder(options.DestinationPath, captureDateTime.Value);
        var prefix = isVideo ? "video" : "img";
        var dateTimePart = captureDateTime.Value.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"{prefix}_{dateTimePart}{info.Extension.ToLowerInvariant()}";
        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, captureDateTime);
            stats.Processed++;

            if (isVideo)
                stats.Videos++;
            else
                stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing iOS file {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessWindowsPhoneFile(string filePath, PhotoInfo info, Options options, Stats stats)
    {
        // Parse datetime from filename first, fallback to file attributes
        var captureDateTime = _namingService.ParseWindowsPhoneDateTime(filePath)
                              ?? _namingService.GetFileDateTime(filePath);

        if (captureDateTime == null)
        {
            stats.SkippedNoExif++;
            stats.SkippedFiles.Add(filePath);
            return;
        }

        var isVideo = info.MediaType == MediaType.Video;
        var destFolder = isVideo
            ? _namingService.GetVideoDestinationFolder(options.DestinationPath, captureDateTime.Value)
            : _namingService.GetDestinationFolder(options.DestinationPath, captureDateTime.Value);
        var prefix = isVideo ? "video" : "img";
        var dateTimePart = captureDateTime.Value.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"{prefix}_{dateTimePart}{info.Extension.ToLowerInvariant()}";
        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, captureDateTime);
            stats.Processed++;

            if (isVideo)
                stats.Videos++;
            else
                stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing Windows Phone file {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessOldCameraFile(string filePath, PhotoInfo info, Options options, Stats stats)
    {
        // Parse datetime from filename (Imgyyyy-MM-dd_nnnn), fallback to file attributes
        var captureDateTime = _namingService.ParseOldCameraDateTime(filePath)
                              ?? _namingService.GetFileDateTime(filePath);

        var isVideo = info.MediaType == MediaType.Video;
        string destFolder;
        string fileName;

        if (captureDateTime != null)
        {
            destFolder = isVideo
                ? _namingService.GetVideoDestinationFolder(options.DestinationPath, captureDateTime.Value)
                : _namingService.GetDestinationFolder(options.DestinationPath, captureDateTime.Value);
            var prefix = isVideo ? "video" : "img";
            var dateTimePart = captureDateTime.Value.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            fileName = $"{prefix}_{dateTimePart}{info.Extension.ToLowerInvariant()}";
        }
        else if (isVideo)
        {
            // No date available, put in unsorted videos folder with original name
            destFolder = _namingService.GetVideoUnsortedFolder(options.DestinationPath);
            fileName = Path.GetFileName(filePath);
        }
        else
        {
            stats.SkippedNoExif++;
            stats.SkippedFiles.Add(filePath);
            return;
        }

        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, captureDateTime);
            stats.Processed++;

            if (isVideo)
                stats.Videos++;
            else
                stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing old camera file {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessVideoWithoutExif(string filePath, PhotoInfo info, Options options, Stats stats)
    {
        // Try to get date from file attributes
        var captureDateTime = _namingService.GetFileDateTime(filePath);

        string destFolder;
        string fileName;

        if (captureDateTime != null)
        {
            destFolder = _namingService.GetVideoDestinationFolder(options.DestinationPath, captureDateTime.Value);
            var dateTimePart = captureDateTime.Value.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            fileName = $"video_{dateTimePart}{info.Extension.ToLowerInvariant()}";
        }
        else
        {
            // No date available, put in unsorted videos folder with original name
            destFolder = _namingService.GetVideoUnsortedFolder(options.DestinationPath);
            fileName = Path.GetFileName(filePath);
        }

        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, captureDateTime);
            stats.Processed++;
            stats.Videos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing video {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private void ProcessPhotoWithFileDate(string filePath, PhotoInfo info, DateTime fileDateTime, Options options, Stats stats)
    {
        var destFolder = _namingService.GetDestinationFolder(options.DestinationPath, fileDateTime);
        var dateTimePart = fileDateTime.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"img_{dateTimePart}{info.Extension.ToLowerInvariant()}";
        var destPath = _namingService.GetUniqueFilePath(destFolder, fileName);

        if (options.DryRun)
        {
            var action = options.Mode == OperationMode.Copy ? "COPY" : "MOVE";
            AnsiConsole.MarkupLine($"[dim]{action}: {Esc(filePath)} -> {Esc(destPath)}[/]");
            stats.Processed++;
            return;
        }

        try
        {
            _fileOperations.ProcessFile(filePath, destPath, options.Mode, fileDateTime);
            stats.Processed++;
            stats.Photos++;
        }
        catch (Exception ex)
        {
            stats.Errors++;
            AnsiConsole.MarkupLine($"[red]Error processing photo {Esc(filePath)}: {Esc(ex.Message)}[/]");
        }
    }

    private static void PrintSummary(Stats stats, Options options)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Summary[/]"));

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Count");

        table.AddRow("Files Processed", stats.Processed.ToString());
        table.AddRow("Photos", stats.Photos.ToString());
        table.AddRow("Videos", stats.Videos.ToString());
        table.AddRow("WhatsApp files", stats.WhatsApp.ToString());
        table.AddRow("Skipped (no EXIF)", stats.SkippedNoExif.ToString());
        table.AddRow("Errors", stats.Errors.ToString());

        AnsiConsole.Write(table);

        if (options.DryRun)
        {
            AnsiConsole.MarkupLine("\n[yellow]This was a DRY RUN. No files were actually modified.[/]");
        }

        // Write skipped files report
        if (stats.SkippedFiles.Count > 0)
        {
            var reportPath = Path.Combine(options.DestinationPath, "skipped_files_report.txt");
            try
            {
                Directory.CreateDirectory(options.DestinationPath);
                File.WriteAllLines(reportPath, stats.SkippedFiles);
                AnsiConsole.MarkupLine($"\n[blue]Skipped files report written to:[/] {reportPath}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"\n[red]Failed to write skipped files report: {Esc(ex.Message)}[/]");
            }
        }
    }

    private void Reconcile(Stats stats, Options options)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold]Reconciliation[/]"));

        var expectedProcessed = stats.TotalToProcess;
        var actualAccounted = stats.Processed + stats.SkippedNoExif + stats.Errors;

        var table = new Table();
        table.AddColumn("Check");
        table.AddColumn("Expected");
        table.AddColumn("Actual");
        table.AddColumn("Status");

        // Check 1: All files accounted for
        var allAccountedFor = expectedProcessed == actualAccounted;
        table.AddRow(
            "Files accounted for",
            expectedProcessed.ToString(),
            actualAccounted.ToString(),
            allAccountedFor ? "[green]✓ PASS[/]" : "[red]✗ FAIL[/]"
        );

        // Check 2: Processed = Photos + Videos (for non-dry-run)
        if (!options.DryRun)
        {
            var mediaMatch = stats.Processed == stats.Photos + stats.Videos;
            table.AddRow(
                "Processed = Photos + Videos",
                stats.Processed.ToString(),
                $"{stats.Photos + stats.Videos}",
                mediaMatch ? "[green]✓ PASS[/]" : "[red]✗ FAIL[/]"
            );
        }

        // Check 3: No unaccounted files
        var unaccounted = expectedProcessed - actualAccounted;
        if (unaccounted != 0)
        {
            table.AddRow(
                "Unaccounted files",
                "0",
                unaccounted.ToString(),
                "[red]✗ FAIL[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary
        AnsiConsole.WriteLine();
        var ignoredTotal = stats.IgnoredFolders + stats.IgnoredConflicts + stats.IgnoredExtensions;
        AnsiConsole.MarkupLine($"[dim]Ignored during scan: {ignoredTotal} files[/]");
        AnsiConsole.MarkupLine($"[dim]  - In ignored folders: {stats.IgnoredFolders}[/]");
        AnsiConsole.MarkupLine($"[dim]  - DownloadConflict files: {stats.IgnoredConflicts}[/]");
        AnsiConsole.MarkupLine($"[dim]  - Non-media extensions: {stats.IgnoredExtensions}[/]");

        if (allAccountedFor && unaccounted == 0)
        {
            AnsiConsole.MarkupLine("\n[green bold]✓ RECONCILIATION PASSED - No files lost[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[red bold]✗ RECONCILIATION FAILED - Please investigate[/]");
        }
    }

    private class Stats
    {
        public int TotalToProcess { get; set; }
        public int Processed { get; set; }
        public int Photos { get; set; }
        public int Videos { get; set; }
        public int WhatsApp { get; set; }
        public int SkippedNoExif { get; set; }
        public int Errors { get; set; }
        public int IgnoredFolders { get; set; }
        public int IgnoredConflicts { get; set; }
        public int IgnoredExtensions { get; set; }
        public List<string> SkippedFiles { get; } = new();
    }
}
