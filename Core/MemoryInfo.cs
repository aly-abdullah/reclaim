using System.Runtime.InteropServices;

namespace Reclaim.Core;

public readonly record struct MemorySnapshot(ulong Total, ulong InUse, ulong Modified, ulong Standby, ulong Free)
{
    public double UsedRatio => Total == 0 ? 0 : (double)InUse / Total;
    public double Fraction(ulong value) => Total == 0 ? 0 : (double)value / Total;
}

public static class MemoryInfo
{
    const int ListFields = 22;
    static readonly ulong PageSize = (ulong)Environment.SystemPageSize;

    public static MemorySnapshot Read()
    {
        var status = new Native.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Native.MemoryStatusEx>() };
        Native.GlobalMemoryStatusEx(ref status);
        ulong total = status.TotalPhys;

        if (!TryReadLists(out ulong free, out ulong modified, out ulong standby))
            return new MemorySnapshot(total, total - status.AvailPhys, 0, 0, status.AvailPhys);

        ulong reclaimable = free + standby + modified;
        ulong inUse = total > reclaimable ? total - reclaimable : 0;
        return new MemorySnapshot(total, inUse, modified, standby, free);
    }

    static unsafe bool TryReadLists(out ulong free, out ulong modified, out ulong standby)
    {
        free = modified = standby = 0;
        nuint* buffer = stackalloc nuint[ListFields];
        int status = Native.NtQuerySystemInformation(Native.SystemMemoryListInformation, (IntPtr)buffer, ListFields * sizeof(nuint), out _);
        if (status != 0)
            return false;

        free = ((ulong)buffer[0] + buffer[1]) * PageSize;
        modified = (ulong)buffer[2] * PageSize;
        ulong standbyPages = 0;
        for (int i = 5; i < 13; i++)
            standbyPages += buffer[i];
        standby = standbyPages * PageSize;
        return true;
    }
}
