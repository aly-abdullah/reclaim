using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace Reclaim.Core;

public static class StartupManager
{
    public const string StartupArgument = "--startup";
    const string TaskName = "Reclaim Memory Optimizer";

    static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.");

    public static bool IsRegistered(out bool pointsHere)
    {
        pointsHere = false;
        var (code, output) = Run("/Query", "/TN", TaskName, "/XML");
        if (code != 0)
            return false;
        pointsHere = output.Contains(SecurityElement.Escape(ExecutablePath), StringComparison.OrdinalIgnoreCase);
        return true;
    }

    public static void Register()
    {
        string user = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape(ExecutablePath);
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Starts Reclaim in the system tray when you sign in.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{user}</UserId>
                  <Delay>PT5S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                  <Arguments>{StartupArgument}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        string path = Path.Combine(Path.GetTempPath(), $"reclaim-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, xml, Encoding.Unicode);
        try
        {
            var (code, output) = Run("/Create", "/TN", TaskName, "/XML", path, "/F");
            if (code != 0)
                throw new InvalidOperationException(output.Trim());
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static void Unregister()
    {
        var (code, output) = Run("/Delete", "/TN", TaskName, "/F");
        if (code != 0 && IsRegistered(out _))
            throw new InvalidOperationException(output.Trim());
    }

    static (int Code, string Output) Run(params string[] arguments)
    {
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Task Scheduler could not be started.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}
