using System;
using System.Windows.Media;

namespace SimpleDesktopFence.Models;

/// <summary>
/// File Item Information
/// </summary>
public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;

    // File Size, Folder = Emypty
    public string DisplaySize { get; set; } = string.Empty;

    // For Sorting
    public long SizeBytes { get; set; }

    public DateTime ModifiedDate { get; set; }
    public string FileType { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }

    // Shell IMG
    public ImageSource? Icon { get; set; }
}
