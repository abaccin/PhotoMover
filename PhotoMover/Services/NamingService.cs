using PhotoMover.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PhotoMover.Services;

public partial class NamingService
{
    private static readonly string[] MonthNames = 
    {
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    };

    // Matches WhatsApp filenames like IMG-20160530-WA0001.jpg or VID-20160530-WA0001.mp4
    private static readonly Regex WhatsAppPattern = WhatsAppRegex();

    // Matches iOS filenames like 20231101_090308391_iOS.jpg (yyyyMMdd_HHmmssSSS_iOS)
    private static readonly Regex IosPattern = IOSRegex();

    // Matches Windows Phone filenames like WP_20130622_006.mp4 (WP_yyyyMMdd_nnn)
    private static readonly Regex WindowsPhonePattern = WindowsPhoneRegex();

    // Matches old camera filenames like Img2003-04-21_0002.MOV (Imgyyyy-MM-dd_nnnn)
    private static readonly Regex OldCameraPattern = OldCameraRegEx();

    public bool IsWhatsAppFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return WhatsAppPattern.IsMatch(fileName);
    }

    public bool IsIosFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return IosPattern.IsMatch(fileName);
    }

    public bool IsWindowsPhoneFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return WindowsPhonePattern.IsMatch(fileName);
    }

    public bool IsOldCameraFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return OldCameraPattern.IsMatch(fileName);
    }

    public DateTime? ParseWhatsAppDateTime(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = WhatsAppPattern.Match(fileName);
        
        if (!match.Success)
            return null;

        if (int.TryParse(match.Groups[2].Value, out int year) &&
            int.TryParse(match.Groups[3].Value, out int month) &&
            int.TryParse(match.Groups[4].Value, out int day))
        {
            try
            {
                return new DateTime(year, month, day, 12, 0, 0); // Default to noon
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public DateTime? ParseIosDateTime(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = IosPattern.Match(fileName);
        
        if (!match.Success)
            return null;

        if (int.TryParse(match.Groups[1].Value, out int year) &&
            int.TryParse(match.Groups[2].Value, out int month) &&
            int.TryParse(match.Groups[3].Value, out int day) &&
            int.TryParse(match.Groups[4].Value, out int hour) &&
            int.TryParse(match.Groups[5].Value, out int minute) &&
            int.TryParse(match.Groups[6].Value, out int second))
        {
            try
            {
                return new DateTime(year, month, day, hour, minute, second);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public DateTime? ParseWindowsPhoneDateTime(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = WindowsPhonePattern.Match(fileName);
        
        if (!match.Success)
            return null;

        if (int.TryParse(match.Groups[1].Value, out int year) &&
            int.TryParse(match.Groups[2].Value, out int month) &&
            int.TryParse(match.Groups[3].Value, out int day))
        {
            try
            {
                return new DateTime(year, month, day, 12, 0, 0); // Default to noon
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public DateTime? ParseOldCameraDateTime(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = OldCameraPattern.Match(fileName);
        
        if (!match.Success)
            return null;

        if (int.TryParse(match.Groups[1].Value, out int year) &&
            int.TryParse(match.Groups[2].Value, out int month) &&
            int.TryParse(match.Groups[3].Value, out int day))
        {
            try
            {
                return new DateTime(year, month, day, 12, 0, 0); // Default to noon
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public DateTime? GetFileDateTime(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var created = fileInfo.CreationTime;
            var modified = fileInfo.LastWriteTime;
            
            var currentYear = DateTime.Now.Year;
            
            // Check if dates are valid (between 1990 and next year)
            bool createdValid = created.Year >= 1990 && created.Year <= currentYear + 1;
            bool modifiedValid = modified.Year >= 1990 && modified.Year <= currentYear + 1;
            
            // Prefer valid dates, then the earlier one
            if (createdValid && modifiedValid)
            {
                return created < modified ? created : modified;
            }
            else if (modifiedValid)
            {
                return modified;
            }
            else if (createdValid)
            {
                return created;
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public string GetDestinationFolder(string basePath, DateTime captureDateTime)
    {
        var year = captureDateTime.Year.ToString();
        var monthNum = captureDateTime.Month.ToString("D2");
        var monthName = MonthNames[captureDateTime.Month - 1];
        var monthFolder = $"{monthNum}-{monthName}";

        return Path.Combine(basePath, year, monthFolder);
    }

    public string GetVideoDestinationFolder(string basePath, DateTime captureDateTime)
    {
        var year = captureDateTime.Year.ToString();
        var monthNum = captureDateTime.Month.ToString("D2");
        var monthName = MonthNames[captureDateTime.Month - 1];
        var monthFolder = $"{monthNum}-{monthName}";

        return Path.Combine(basePath, "Videos", year, monthFolder);
    }

    public string GetVideoUnsortedFolder(string basePath)
    {
        return Path.Combine(basePath, "Videos", "Unsorted");
    }

    public string GetWhatsAppDestinationFolder(string basePath, DateTime captureDateTime)
    {
        var year = captureDateTime.Year.ToString();
        var monthNum = captureDateTime.Month.ToString("D2");
        var monthName = MonthNames[captureDateTime.Month - 1];
        var monthFolder = $"{monthNum}-{monthName}";

        return Path.Combine(basePath, "WhatsApp", year, monthFolder);
    }

    public string GetFileName(PhotoInfo info)
    {
        if (info.CaptureDateTime == null)
        {
            throw new ArgumentException("CaptureDateTime is required for naming");
        }

        var dt = info.CaptureDateTime.Value;
        var prefix = info.MediaType == MediaType.Video ? "video" : "img";
        var dateTimePart = dt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        
        var baseName = string.IsNullOrEmpty(info.CameraModel)
            ? $"{prefix}_{dateTimePart}"
            : $"{prefix}_{dateTimePart}_{info.CameraModel}";

        return baseName + info.Extension.ToLowerInvariant();
    }

    public string GetWhatsAppFileName(DateTime captureDateTime, string extension, bool isVideo)
    {
        var prefix = isVideo ? "video_wa" : "img_wa";
        var dateTimePart = captureDateTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return $"{prefix}_{dateTimePart}{extension.ToLowerInvariant()}";
    }

    public string GetUniqueFilePath(string folder, string fileName)
    {
        var fullPath = Path.Combine(folder, fileName);
        
        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;

        do
        {
            var newName = $"{nameWithoutExt}_{counter:D2}{extension}";
            fullPath = Path.Combine(folder, newName);
            counter++;
        } while (File.Exists(fullPath));

        return fullPath;
    }

    [GeneratedRegex(@"^(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})\d*_iOS", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IE")]
    private static partial Regex IOSRegex();

    [GeneratedRegex(@"^(IMG|VID)-(\d{4})(\d{2})(\d{2})-WA\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IE")]
    private static partial Regex WhatsAppRegex();

    [GeneratedRegex(@"^WP_(\d{4})(\d{2})(\d{2})_\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IE")]
    private static partial Regex WindowsPhoneRegex();

    [GeneratedRegex(@"^Img(\d{4})-(\d{2})-(\d{2})_\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-IE")]
    private static partial Regex OldCameraRegEx();
}
