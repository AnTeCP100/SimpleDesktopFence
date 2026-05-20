using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimpleDesktopFence.Helpers;

/// <summary>
/// Loads shell icons via SHGetFileInfo.
/// Icons are cached by file extension so each unique type is only loaded once,
/// keeping memory usage proportional to the number of distinct file types (not file count).
/// </summary>
public static class ShellHelper
{
    // Key: lowercase extension (e.g. ".txt") or "__dir__" / "__no_ext__"
    // Value: frozen BitmapSource, or null if loading failed
    private static readonly Dictionary<string, ImageSource?> _iconCache = new();

    public static ImageSource? GetFileIcon(string path, bool isDirectory = false)
    {
        string key = isDirectory
            ? "__dir__"
            : Path.GetExtension(path) is { Length: > 0 } ext
                ? ext.ToLowerInvariant()
                : "__no_ext__";

        if (_iconCache.TryGetValue(key, out var cached))
            return cached;

        var shfi = new SHFILEINFO();
        uint fileAttr = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

        // SHGFI_USEFILEATTRIBUTES: does not access the disk ¡X fast and safe for any path
        IntPtr hResult = SHGetFileInfo(
            path, fileAttr, ref shfi,
            (uint)Marshal.SizeOf(shfi),
            SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

        if (hResult == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
        {
            _iconCache[key] = null;
            return null;
        }

        ImageSource? imageSource = null;
        try
        {
            using var icon = Icon.FromHandle(shfi.hIcon);
            using var bitmap = icon.ToBitmap();
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze(); // thread-safe; prevents further heap allocation
                imageSource = bs;
            }
            finally { DeleteObject(hBitmap); }
        }
        finally { DestroyIcon(shfi.hIcon); }

        _iconCache[key] = imageSource;
        return imageSource;
    }

    public static string GetFileTypeName(string path, bool isDirectory = false)
    {
        var shfi = new SHFILEINFO();
        uint fileAttr = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

        SHGetFileInfo(path, fileAttr, ref shfi,
            (uint)Marshal.SizeOf(shfi),
            SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES);

        return shfi.szTypeName ?? string.Empty;
    }

    // ¢w¢w P/Invoke ¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x001;
    private const uint SHGFI_TYPENAME = 0x400;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x010;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x010;
}