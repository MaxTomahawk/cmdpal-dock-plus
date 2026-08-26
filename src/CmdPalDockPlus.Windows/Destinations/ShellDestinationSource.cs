using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using CmdPalDockPlus.Core.Profiles;

namespace CmdPalDockPlus.Windows.Destinations;

public sealed class ShellDestinationSource : IAppDestinationSource
{
    private const uint ClsctxInprocServer = 0x1;
    private const uint SigdnNormalDisplay = 0x00000000;
    private const uint SigdnDesktopAbsoluteParsing = 0x80028000;
    private const uint SigdnFileSysPath = 0x80058000;

    private static readonly Guid ClsidApplicationDocumentLists = new("86BEC222-30F2-47E0-9F25-60D11CD75C28");
    private static readonly Guid IidApplicationDocumentLists = new("3C594F9F-9F30-47A1-979A-C9E83D3D0A06");
    private static readonly Guid IidObjectArray = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");
    private static readonly Guid IidShellItem = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
    private static readonly Guid IidShellLinkW = new("000214F9-0000-0000-C000-000000000046");

    public ValueTask<IReadOnlyList<AppDestination>> GetRecentAsync(
        ApplicationMatch application,
        int limit,
        CancellationToken cancellationToken = default)
        => ReadAsync(application, AppDocListType.Recent, DestinationKind.Recent, limit, cancellationToken);

    public ValueTask<IReadOnlyList<AppDestination>> GetFrequentAsync(
        ApplicationMatch application,
        int limit,
        CancellationToken cancellationToken = default)
        => ReadAsync(application, AppDocListType.Frequent, DestinationKind.Frequent, limit, cancellationToken);

    private static ValueTask<IReadOnlyList<AppDestination>> ReadAsync(
        ApplicationMatch application,
        AppDocListType listType,
        DestinationKind kind,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit <= 0 || string.IsNullOrWhiteSpace(application.Aumid))
        {
            return ValueTask.FromResult<IReadOnlyList<AppDestination>>([]);
        }

        var aumid = application.Aumid;
        return new ValueTask<IReadOnlyList<AppDestination>>(
            Task.Run(() => Read(aumid, listType, kind, limit, cancellationToken), cancellationToken));
    }

    private static IReadOnlyList<AppDestination> Read(
        string aumid,
        AppDocListType listType,
        DestinationKind kind,
        int limit,
        CancellationToken cancellationToken)
    {
        IApplicationDocumentLists? lists = null;
        IObjectArray? array = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clsid = ClsidApplicationDocumentLists;
            var iid = IidApplicationDocumentLists;
            var hr = CoCreateInstance(ref clsid, IntPtr.Zero, ClsctxInprocServer, ref iid, out lists);
            if (hr < 0 || lists is null || lists.SetAppID(aumid) < 0)
            {
                return [];
            }

            var arrayIid = IidObjectArray;
            hr = lists.GetList(listType, checked((uint)limit), ref arrayIid, out array);
            if (hr < 0 || array is null || array.GetCount(out var count) < 0)
            {
                return [];
            }

            var found = new List<AppDestination>();
            var take = Math.Min(count, checked((uint)limit));
            for (uint index = 0; index < take; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryReadDestination(array, index, kind, out var destination))
                {
                    found.Add(destination);
                }
            }

            return DestinationDeduplicator.Deduplicate(found, limit);
        }
        catch (COMException)
        {
            return [];
        }
        finally
        {
            ReleaseComObject(array);
            ReleaseComObject(lists);
        }
    }

    private static bool TryReadDestination(IObjectArray array, uint index, DestinationKind kind, out AppDestination destination)
    {
        destination = default!;
        object? item = null;
        try
        {
            var shellItemIid = IidShellItem;
            if (array.GetAt(index, ref shellItemIid, out item) >= 0 && item is IShellItem shellItem)
            {
                return TryReadShellItem(shellItem, kind, out destination);
            }
        }
        finally
        {
            ReleaseComObject(item);
        }

        item = null;
        try
        {
            var shellLinkIid = IidShellLinkW;
            if (array.GetAt(index, ref shellLinkIid, out item) >= 0 && item is IShellLinkW shellLink)
            {
                return TryReadShellLink(shellLink, kind, out destination);
            }
        }
        finally
        {
            ReleaseComObject(item);
        }

        return false;
    }

    private static bool TryReadShellItem(IShellItem item, DestinationKind kind, out AppDestination destination)
    {
        destination = default!;
        var target = ReadDisplayName(item, SigdnFileSysPath);
        if (string.IsNullOrWhiteSpace(target))
        {
            var parsing = ReadDisplayName(item, SigdnDesktopAbsoluteParsing);
            if (Uri.TryCreate(parsing, UriKind.Absolute, out var uri)
                && uri.Scheme is "file" or "http" or "https")
            {
                target = uri.IsFile ? uri.LocalPath : uri.AbsoluteUri;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var display = ReadDisplayName(item, SigdnNormalDisplay);
        if (string.IsNullOrWhiteSpace(display))
        {
            display = Path.GetFileName(target);
        }

        destination = CreateDestination(display ?? target, target, null, kind);
        return true;
    }

    private static bool TryReadShellLink(IShellLinkW link, DestinationKind kind, out AppDestination destination)
    {
        destination = default!;
        var path = new StringBuilder(32768);
        if (link.GetPath(path, path.Capacity, IntPtr.Zero, 0) < 0 || path.Length == 0)
        {
            return false;
        }

        var arguments = new StringBuilder(4096);
        _ = link.GetArguments(arguments, arguments.Capacity);
        var target = path.ToString();
        var display = Path.GetFileName(target);
        destination = CreateDestination(
            string.IsNullOrWhiteSpace(display) ? target : display,
            target,
            arguments.Length == 0 ? null : arguments.ToString(),
            kind);
        return true;
    }

    private static AppDestination CreateDestination(string displayName, string target, string? arguments, DestinationKind kind)
    {
        var identity = $"{kind}\u001f{target}\u001f{arguments}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..20].ToLowerInvariant();
        return new AppDestination($"destination:{hash}", displayName, target, arguments, kind);
    }

    private static string? ReadDisplayName(IShellItem item, uint sigdn)
    {
        var value = IntPtr.Zero;
        try
        {
            return item.GetDisplayName(sigdn, out value) >= 0 && value != IntPtr.Zero
                ? Marshal.PtrToStringUni(value)
                : null;
        }
        finally
        {
            if (value != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(value);
            }
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    private enum AppDocListType
    {
        Recent = 0,
        Frequent = 1,
    }

    [ComImport]
    [Guid("3C594F9F-9F30-47A1-979A-C9E83D3D0A06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationDocumentLists
    {
        [PreserveSig]
        int SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        [PreserveSig]
        int GetList(AppDocListType listType, uint itemsDesired, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IObjectArray? array);
    }

    [ComImport]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? value);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr value);
        [PreserveSig] int GetParent([MarshalAs(UnmanagedType.Interface)] out IShellItem? parent);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.Interface)] IShellItem other, uint hint, out int order);
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
        [PreserveSig] int GetIDList(out IntPtr pidl);
        [PreserveSig] int SetIDList(IntPtr pidl);
        [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        [PreserveSig] int GetHotkey(out short hotkey);
        [PreserveSig] int SetHotkey(short hotkey);
        [PreserveSig] int GetShowCmd(out int showCommand);
        [PreserveSig] int SetShowCmd(int showCommand);
        [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        [PreserveSig] int Resolve(IntPtr hwnd, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid clsid,
        IntPtr outer,
        uint clsContext,
        ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IApplicationDocumentLists? instance);
}
