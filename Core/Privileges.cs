using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Reclaim.Core;

public static class Privileges
{
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static void EnableMemoryPrivileges()
    {
        Enable("SeProfileSingleProcessPrivilege");
        Enable("SeIncreaseQuotaPrivilege");
    }

    static bool Enable(string name)
    {
        if (!Native.OpenProcessToken(Native.GetCurrentProcess(), Native.TokenAdjustPrivileges | Native.TokenQuery, out var token))
            return false;

        try
        {
            if (!Native.LookupPrivilegeValue(null, name, out long luid))
                return false;

            var state = new Native.TokenPrivileges { PrivilegeCount = 1, Luid = luid, Attributes = Native.PrivilegeEnabled };
            if (!Native.AdjustTokenPrivileges(token, false, ref state, Marshal.SizeOf<Native.TokenPrivileges>(), IntPtr.Zero, IntPtr.Zero))
                return false;

            return Marshal.GetLastWin32Error() != Native.ErrorNotAllAssigned;
        }
        finally
        {
            Native.CloseHandle(token);
        }
    }
}
