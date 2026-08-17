using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssetSplitterUI.Services;

/// <summary>
/// Registers child processes with a Windows Job Object so they are automatically killed
/// when this parent process exits (including unexpected termination via Task Manager).
/// Uses <c>JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE</c> so the OS handles cleanup.
/// Requires Windows 8 or later; silently disabled on earlier versions and non-Windows platforms.
/// </summary>
/// <remarks>
/// References:
/// https://stackoverflow.com/a/4657392/386091
/// https://stackoverflow.com/a/37034966/386091
/// </remarks>
public static partial class ChildProcessTracker
{
    private static readonly IntPtr s_jobHandle;
    private static readonly bool s_isSupported;

    static ChildProcessTracker()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            Environment.OSVersion.Version < new Version(6, 2))
        {
            return;
        }

        string jobName = $"AssetSplitterUI_ChildTracker_{Process.GetCurrentProcess().Id}";
        s_jobHandle = CreateJobObject(IntPtr.Zero, jobName);

        if (s_jobHandle == IntPtr.Zero)
        {
            return;
        }

        var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOBOBJECTLIMIT.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        IntPtr infoPtr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(extendedInfo, infoPtr, false);

            if (!SetInformationJobObject(s_jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
            {
                CloseHandle(s_jobHandle);
                s_jobHandle = IntPtr.Zero;
                return;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }

        s_isSupported = true;
    }

    /// <summary>
    /// Assigns <paramref name="process"/> to the job object so it is killed when the parent exits.
    /// Safe to call even if the child has already exited.
    /// </summary>
    public static void AddProcess(Process process)
    {
        if (!s_isSupported || s_jobHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AssignProcessToJobObject(s_jobHandle, process.Handle);
        }
        catch
        {
            // Process may have already exited; ignore.
        }
    }

    #region Native API

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateJobObject(IntPtr lpJobAttributes, string name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(
      IntPtr job, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    private enum JobObjectInfoType
    {
        AssociateCompletionPortInformation = 7,
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        EndOfJobTimeInformation = 6,
        ExtendedLimitInformation = 9,
        SecurityLimitInformation = 5,
        GroupInformation = 11
    }

    [Flags]
    private enum JOBOBJECTLIMIT : uint
    {
        JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public JOBOBJECTLIMIT LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    #endregion
}
