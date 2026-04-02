using PhotoMover.Models;

namespace PhotoMover.Services;

public class FileOperations
{
    public void EnsureDirectory(string path)
    {
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }
    }

    public void CopyFile(string source, string destination, DateTime? captureDateTime)
    {
        File.Copy(source, destination, overwrite: false);
        
        if (captureDateTime.HasValue)
        {
            SetFileTimestamps(destination, captureDateTime.Value);
        }
    }

    public void MoveFile(string source, string destination, DateTime? captureDateTime)
    {
        File.Move(source, destination, overwrite: false);
        
        if (captureDateTime.HasValue)
        {
            SetFileTimestamps(destination, captureDateTime.Value);
        }
    }

    public void ProcessFile(string source, string destination, OperationMode mode, DateTime? captureDateTime)
    {
        EnsureDirectory(Path.GetDirectoryName(destination)!);

        if (mode == OperationMode.Copy)
        {
            CopyFile(source, destination, captureDateTime);
        }
        else
        {
            MoveFile(source, destination, captureDateTime);
        }
    }

    private static void SetFileTimestamps(string filePath, DateTime dateTime)
    {
        try
        {
            File.SetCreationTime(filePath, dateTime);
            File.SetLastWriteTime(filePath, dateTime);
            File.SetLastAccessTime(filePath, dateTime);
        }
        catch (Exception)
        {
            // Silently ignore timestamp errors - not critical
        }
    }
}
