using System.IO;
using System.Runtime.InteropServices;

namespace OrionLauncher.Services;

/// <summary>
/// Wraps the Windows Restart Manager API to check whether a file is in use
/// by any process — including DLLs that have been memory-mapped and whose
/// original file handle has already been closed by the loader.
/// </summary>
internal static class RestartManagerHelper
{
    // ---- P/Invoke declarations ----

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out int pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(int pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        int pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        int dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        out uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]  public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public int TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
    }

    private const int ERROR_MORE_DATA = 234;

    // ---- Public API ----

    /// <summary>
    /// Returns true if any process currently has <paramref name="filePath"/> open
    /// or memory-mapped (e.g. as a loaded DLL).
    /// Returns false if the Restart Manager API is unavailable or errors.
    /// </summary>
    public static bool IsInUseByAnyProcess(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        int session;
        try
        {
            int err = RmStartSession(out session, 0, Guid.NewGuid().ToString());
            if (err != 0) return false;
        }
        catch (DllNotFoundException)
        {
            return false; // rstrtmgr.dll not available (shouldn't happen on Vista+)
        }

        try
        {
            if (RmRegisterResources(session, 1, [filePath], 0, null, 0, null) != 0)
                return false;

            uint procInfoNeeded = 0, procInfo = 0;
            uint rebootReasons;
            int result = RmGetList(session, out procInfoNeeded, ref procInfo, null, out rebootReasons);

            if (result == ERROR_MORE_DATA && procInfoNeeded > 0)
            {
                var infos = new RM_PROCESS_INFO[procInfoNeeded];
                procInfo = procInfoNeeded;
                result = RmGetList(session, out procInfoNeeded, ref procInfo, infos, out rebootReasons);
                return result == 0 && procInfo > 0;
            }

            return result == 0 && procInfoNeeded > 0;
        }
        finally
        {
            RmEndSession(session);
        }
    }
}
