# PhotoMover

A powerful command-line tool to organize photos and videos by capture date. PhotoMover extracts EXIF metadata from your media files and organizes them into a structured folder hierarchy based on when they were taken.

## Features

- ?? **Automatic Date Organization**: Organizes files into `Year/Month` folder structure
- ?? **EXIF Support**: Reads capture date from photo/video metadata
- ?? **Special Format Handling**: 
  - WhatsApp images/videos (IMG-YYYYMMDD-WA####)
  - iOS screenshots (YYYYMMDD_HHmmss_iOS)
  - Windows Phone (WP_YYYYMMDD_###)
  - Old camera formats (ImgYYYY-MM-DD_####)
- ?? **Video Support**: Separate organization for videos with dedicated folder structure
- ?? **Copy or Move**: Choose to copy files (safe) or move them (cleanup)
- ??? **Dry Run**: Preview all changes before committing
- ?? **Progress Tracking**: Real-time progress with summary and reconciliation
- ?? **File Fallback**: Uses file timestamps when EXIF data is missing
- ?? **Skipped Files Report**: Generates a report of files that couldn't be processed

## Supported File Types

### Photos
- JPEG (`.jpg`, `.jpeg`)
- PNG (`.png`)
- TIFF (`.tif`, `.tiff`)
- RAW formats (`.cr2`, `.nef`, `.arw`, `.dng`)
- HEIC (`.heic`)

### Videos
- MP4 (`.mp4`)
- MOV (`.mov`)
- AVI (`.avi`)
- MKV (`.mkv`)
- M4V (`.m4v`)
- WebM (`.webm`)

## Installation

### Building from Source

#### Windows
```powershell
dotnet build -c Release
```

#### Linux (Self-contained)
```powershell
./build-linux.ps1
# With trimming for smaller size
./build-linux.ps1 -SingleFile -Trimmed
```

#### Synology NAS (DS718+)
```powershell
./build-synology.ps1
```

The self-contained builds include the .NET runtime, so no .NET installation is needed on the target system.

## Usage

### Basic Syntax

```bash
PhotoMover [options]
```

### Command-Line Options

| Option | Alias | Default | Description |
|--------|-------|---------|-------------|
| `--src <path>` | `-s` | *(interactive)* | Source folder path containing media files |
| `--dst <path>` | `-d` | *(interactive)* | Destination folder path for organized files |
| `--mode <mode>` | `-m` | `copy` | Operation mode: `copy` or `move` |
| `--dry-run` | `-n` | `false` | Preview actions without making changes |
| `--skip-missing-exif` | | `true` | Skip files without EXIF datetime |
| `--progress-every <n>` | | `100` | Print progress every N files (0 to disable) |
| `--ext <extensions>` | `-e` | *(see below)* | Allowed file extensions (repeatable) |

### Default Extensions

If not specified, PhotoMover processes these extensions:
```
.jpg, .jpeg, .png, .tif, .tiff, .cr2, .nef, .arw, .dng, .heic, .mp4, .mov, .avi, .mkv
```

## Examples

### 1. Basic Usage (Interactive)

```bash
./PhotoMover
```
The tool will prompt for source and destination paths.

### 2. Copy Files (Safe, Recommended for First Run)

```bash
./PhotoMover --src "/path/to/photos" --dst "/path/to/organized"
```

### 3. Move Files (Cleanup Source)

```bash
./PhotoMover --src "/path/to/photos" --dst "/path/to/organized" --mode move
```

### 4. Dry Run (Preview Changes)

```bash
./PhotoMover --src "/path/to/photos" --dst "/path/to/organized" --dry-run
```

### 5. Process Only JPEG Files

```bash
./PhotoMover -s "/path/to/photos" -d "/path/to/organized" -e .jpg -e .jpeg
```

### 6. Process Videos Only

```bash
./PhotoMover -s "/path/to/videos" -d "/path/to/organized" -e .mp4 -e .mov -e .avi
```

### 7. Verbose Progress

```bash
./PhotoMover -s "/path/to/photos" -d "/path/to/organized" --progress-every 10
```

### 8. Move Files with Dry Run First

```bash
# First, preview the changes
./PhotoMover -s "/path/to/photos" -d "/path/to/organized" -m move --dry-run

# If satisfied, run for real
./PhotoMover -s "/path/to/photos" -d "/path/to/organized" -m move
```

## Output Structure

### Regular Photos

```
destination/
??? 2023/
?   ??? 01-Jan/
?   ?   ??? img_20230115_143022.jpg
?   ?   ??? img_20230120_091545_Canon.jpg
?   ??? 12-Dec/
?       ??? img_20231225_120000.jpg
??? 2024/
    ??? 03-Mar/
        ??? img_20240301_083012.jpg
```

### Videos

```
destination/
??? Videos/
    ??? 2023/
    ?   ??? 06-Jun/
    ?       ??? video_20230615_140530.mp4
    ??? 2024/
    ?   ??? 01-Jan/
    ?       ??? video_20240101_000015.mov
    ??? Unsorted/
        ??? video_without_date.avi
```

### WhatsApp Media

```
destination/
??? WhatsApp/
    ??? 2024/
        ??? 02-Feb/
            ??? img_wa_20240214_120000.jpg
            ??? video_wa_20240214_153045.mp4
```

## File Naming Convention

PhotoMover uses a consistent naming scheme:

- **Photos**: `img_YYYYMMDD_HHmmss[_CameraModel].ext`
  - Example: `img_20240315_143022_Canon.jpg`
  
- **Videos**: `video_YYYYMMDD_HHmmss[_CameraModel].ext`
  - Example: `video_20240315_143022.mp4`
  
- **WhatsApp**: `img_wa_YYYYMMDD_HHmmss.ext` or `video_wa_YYYYMMDD_HHmmss.ext`
  - Example: `img_wa_20240315_120000.jpg`

If a file with the same name exists, a counter is appended: `img_20240315_143022_01.jpg`

## Ignored Items

PhotoMover automatically ignores:

- **Folders**: `Screenshots`, `Download`, `Downloads`
- **Files**: Files containing "DownloadConflict" (case-insensitive)

## Processing Logic

1. **WhatsApp Files**: Detected by pattern `IMG-YYYYMMDD-WA####` or `VID-YYYYMMDD-WA####`
   - Goes to: `destination/WhatsApp/Year/Month/`

2. **iOS Screenshots**: Detected by pattern `YYYYMMDD_HHmmss###_iOS`
   - Goes to: `destination/Year/Month/`

3. **Windows Phone**: Detected by pattern `WP_YYYYMMDD_###`
   - Goes to: `destination/Year/Month/` or `destination/Videos/Year/Month/`

4. **Old Camera**: Detected by pattern `ImgYYYY-MM-DD_####`
   - Goes to: `destination/Year/Month/` or `destination/Videos/Year/Month/`

5. **Regular Files with EXIF**:
   - Photos: `destination/Year/Month/`
   - Videos: `destination/Videos/Year/Month/`

6. **Files without EXIF**:
   - Tries file creation/modification timestamps
   - Videos without dates: `destination/Videos/Unsorted/`
   - Photos without dates: Skipped (if `--skip-missing-exif` is true)

## Reports

After processing, PhotoMover provides:

### Summary Table
- Files Processed
- Photos count
- Videos count
- WhatsApp files count
- Skipped (no EXIF) count
- Errors count

### Reconciliation Check
- Verifies all files are accounted for
- Confirms no files were lost
- Reports any discrepancies

### Skipped Files Report
If files were skipped, a report is written to:
```
destination/skipped_files_report.txt
```

## Deployment to Synology NAS

### Build
```powershell
./build-synology.ps1
```

### Upload
```bash
scp ./publish/synology/PhotoMover admin@synology-ip:/volume1/your-path/
```

### Execute
```bash
ssh admin@synology-ip
cd /volume1/your-path
chmod +x PhotoMover
./PhotoMover --help
```

### Example Synology Usage
```bash
./PhotoMover \
  --src "/volume1/photo/Camera Upload" \
  --dst "/volume1/photo/Organized" \
  --mode copy \
  --dry-run
```

## Best Practices

1. **Always Dry Run First**: Use `--dry-run` to preview changes
   ```bash
   ./PhotoMover -s source -d dest --dry-run
   ```

2. **Start with Copy Mode**: Don't use move until you're confident
   ```bash
   ./PhotoMover -s source -d dest --mode copy
   ```

3. **Backup Important Files**: Always have backups before moving files

4. **Check Skipped Files Report**: Review `skipped_files_report.txt` for files that couldn't be processed

5. **Test with Small Sample**: Try with a small subset of files first

6. **Verify Reconciliation**: Check that the reconciliation passes before considering the job complete

## Troubleshooting

### "No EXIF datetime" Warnings
Some files don't have EXIF data. PhotoMover will:
1. Try to extract date from filename (WhatsApp, iOS, etc.)
2. Try file creation/modification timestamps
3. Skip the file if nothing works (if `--skip-missing-exif` is true)

### Files Not Moving/Copying
- Check file permissions on source and destination
- Verify destination path exists or can be created
- Check available disk space

### Reconciliation Failed
- Review the reconciliation table in the output
- Check the skipped files report
- Verify no errors occurred during processing

## Technical Details

- **Framework**: .NET 10
- **EXIF Library**: MetadataExtractor 2.8.1
- **UI**: Spectre.Console 0.49.1
- **CLI**: System.CommandLine 2.0.0-beta4

## License

MIT License - See repository for details

## Contributing

Contributions are welcome! Please visit the [GitHub repository](https://github.com/abaccin/PhotoMover).

## Support

For issues, questions, or feature requests, please open an issue on GitHub.
