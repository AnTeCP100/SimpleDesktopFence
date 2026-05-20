using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace SimpleDesktopFence.Helpers;

/// <summary>
/// Displays the native Windows Shell context menu for one or more file/folder paths.
/// Uses ILCreateFromPathW + SHBindToParent which is simpler and more reliable
/// than parsing through the desktop IShellFolder manually.
/// All selected items must share the same parent folder (always true in this app).
/// </summary>
public static class NativeContextMenu
{
    // ── COM: IContextMenu ─────────────────────────────────────────────────

    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(
            IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFO pici);
        [PreserveSig]
        int GetCommandString(
            UIntPtr idcmd, uint uType, uint[]? reserved,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, uint cchMax);
    }

    // ── COM: IShellFolder ─────────────────────────────────────────────────

    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        void ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
        void EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
        void BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);
        void CompareIDs(nint lParam, IntPtr pidl1, IntPtr pidl2);
        void CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);
        void GetAttributesOf(uint cidl, IntPtr[] apidl, ref uint rgfInOut);
        void GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] apidl,
            ref Guid riid, uint rgfReserved, out IntPtr ppv);
        void GetDisplayNameOf(IntPtr pidl, uint uFlags, out STRRET pName);
        void SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            uint uFlags, out IntPtr ppidlOut);
    }

    // ── Structs ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFO
    {
        public int cbSize;
        public int fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public int dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct STRRET
    {
        [FieldOffset(0)] public uint uType;
        [FieldOffset(4)] public IntPtr pOleStr;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────

    // Creates a full absolute PIDL from a file-system path — much simpler than ParseDisplayName
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    // Splits a full PIDL into (parent folder, last child PIDL).
    // ppidlLast points INSIDE pidl — do NOT free it separately.
    [DllImport("shell32.dll")]
    private static extern int SHBindToParent(
        IntPtr pidl, [In] ref Guid riid,
        out IntPtr ppv, out IntPtr ppidlLast);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(
        IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int SW_SHOWNORMAL = 1;

    private static readonly Guid IID_IContextMenu =
        new("000214E4-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellFolder =
        new("000214E6-0000-0000-C000-000000000046");

    // ── Public API ────────────────────────────────────────────────────────

    public static void Show(Window owner, string[] paths, System.Windows.Point screenPt)
    {
        if (paths.Length == 0) return;

        IntPtr hwnd = new WindowInteropHelper(owner).Handle;

        // Build absolute PIDLs for every selected path
        var absPidls = new IntPtr[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            absPidls[i] = ILCreateFromPathW(paths[i]);

        try
        {
            // Get the parent IShellFolder from the first item's PIDL.
            // SHBindToParent also returns a pointer to the child (last) component
            // of the PIDL — this is a pointer INTO absPidls[0], NOT a new allocation.
            var iidFolder = IID_IShellFolder;
            int hr = SHBindToParent(absPidls[0], ref iidFolder,
                                     out IntPtr pParentFolder, out IntPtr ppidlFirst);
            if (hr != 0 || pParentFolder == IntPtr.Zero) return;

            var parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(pParentFolder);
            Marshal.Release(pParentFolder);

            // Collect child PIDLs for every selected item (pointer into each absPidl)
            var childPidls = new IntPtr[paths.Length];
            childPidls[0] = ppidlFirst;

            for (int i = 1; i < paths.Length; i++)
            {
                var iid2 = IID_IShellFolder;
                SHBindToParent(absPidls[i], ref iid2, out IntPtr pFolder, out childPidls[i]);
                if (pFolder != IntPtr.Zero) Marshal.Release(pFolder);
            }

            // Ask the parent folder for IContextMenu on the selection
            var iidCtx = IID_IContextMenu;
            parentFolder.GetUIObjectOf(hwnd, (uint)childPidls.Length, childPidls,
                                        ref iidCtx, 0, out IntPtr pCtxMenu);

            if (pCtxMenu == IntPtr.Zero)
            {
                Marshal.ReleaseComObject(parentFolder);
                return;
            }

            var ctxMenu = (IContextMenu)Marshal.GetObjectForIUnknown(pCtxMenu);
            Marshal.Release(pCtxMenu);

            IntPtr hMenu = CreatePopupMenu();
            try
            {
                ctxMenu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, 0 /* CMF_NORMAL */);

                uint selected = TrackPopupMenuEx(hMenu,
                    TPM_RETURNCMD | TPM_RIGHTBUTTON,
                    (int)screenPt.X, (int)screenPt.Y,
                    hwnd, IntPtr.Zero);

                if (selected > 0)
                {
                    var invoke = new CMINVOKECOMMANDINFO
                    {
                        cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFO>(),
                        lpVerb = (IntPtr)(selected - 1),   // 0-based command offset
                        nShow = SW_SHOWNORMAL
                    };
                    ctxMenu.InvokeCommand(ref invoke);
                }
            }
            finally
            {
                DestroyMenu(hMenu);
                Marshal.ReleaseComObject(ctxMenu);
                Marshal.ReleaseComObject(parentFolder);
            }
        }
        finally
        {
            // Free the absolute PIDLs we created; child PIDLs point inside these — do NOT free separately
            foreach (var p in absPidls)
                if (p != IntPtr.Zero) ILFree(p);
        }
    }
}
