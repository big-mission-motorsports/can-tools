using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BigMission.CanTools.PiCan;

/// <summary>
/// Works with Bash shell to command the CAN utility.
/// </summary>
public class ShellCommand : IDisposable
{
    private ILogger Logger { get; }
    private Process process;

    public ShellCommand(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
    }

    public event Action<string> ReceivedOutput;

    public int Run(string cmd, string args = "")
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        Logger.LogInformation(process.StartInfo.FileName + " " + process.StartInfo.Arguments);
        process.Start();

        while (!process.HasExited)
        {
            var resp = process.StandardOutput.ReadLine();
            //Logger.Trace($"RX:{resp}");
            ReceivedOutput?.Invoke(resp);
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Run command and don't wait on the process.
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="args"></param>
    public async Task RunInstAsync(string cmd, string args = "")
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        //Logger.Trace(process.StartInfo.FileName + " " + process.StartInfo.Arguments);
        await Task.Run(() => { 
            try
            { 
                process.Start(); 
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error sending CAN message");
            }
        });
        //await new Task(() => { process.Start(); });
    }

    public void Dispose()
    {
        try
        {
            if (process != null)
            {
                process.Kill();
                process.Dispose();
            }
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
