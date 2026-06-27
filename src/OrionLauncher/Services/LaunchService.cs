using System.Diagnostics;
using System.IO;

namespace OrionLauncher.Services;

/// <summary>
/// Three manual operations — no process detection, no polling.
/// The user controls the full inject / launch / remove lifecycle.
/// </summary>
public class LaunchService
{
    private readonly PakService _pak;
    private readonly EacService _eac;

    public event Action<string>? StatusMessage;

    public LaunchService(PakService pak, EacService eac)
    {
        _pak = pak;
        _eac = eac;
    }

    /// <summary>
    /// Deploys mod files, corrupts EAC, copies bypass shims.
    /// Throws on failure so the caller can surface the error.
    /// </summary>
    public void Inject(string gameDir)
    {
        StatusMessage?.Invoke("Disabling EAC...");
        _eac.DisableEac(gameDir);

        StatusMessage?.Invoke("Deploying bypass shims...");
        _pak.DeployShims(gameDir);

        StatusMessage?.Invoke("Injected — ready to launch.");
    }

    /// <summary>Starts The Isle through Steam — fire-and-forget.</summary>
    public void Launch(string gameDir)
    {
        Process.Start(new ProcessStartInfo("steam://rungameid/376210")
        {
            UseShellExecute = true
        });
        StatusMessage?.Invoke("Game launched.");
    }

    /// <summary>
    /// Restores EAC, removes bypass shims, and wipes mod files from LogicMods.
    /// Safe to call even if Inject was never run.
    /// </summary>
    public void Remove(string gameDir)
    {
        StatusMessage?.Invoke("Removing mod files...");
        _pak.WipeDeployedMods(gameDir);

        StatusMessage?.Invoke("Removing shims...");
        _pak.RemoveShims(gameDir);

        StatusMessage?.Invoke("Restoring EAC...");
        _eac.RestoreEac(gameDir);

        StatusMessage?.Invoke("Game restored to vanilla.");
    }
}
