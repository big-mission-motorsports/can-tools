using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BigMission.CanTools.PiCan
{
    /// <summary>
    /// CAN support for Raspberry Pi's PiCAN Hat.  This uses the executables that
    /// are provided with the hat by calling them in a command shell.
    /// </summary>
    public class PiCanCanBus : ICanBus
    {
        public event Action<CanMessage> Received;
        private ILogger Logger { get; }
        private ShellCommand shell;
        private readonly string sendCmd;
        private readonly PiCanMessageParser canParser;
        private Thread receiveThread;
        public bool IsOpen { get; private set; }
        public bool SilentOnCanBus { get; set; }

        private readonly ILoggerFactory loggerFactory;
        private readonly string cmd;
        private readonly string arg;
        private readonly string bitrate;


        public PiCanCanBus(ILoggerFactory loggerFactory, string cmd, string arg, string bitrate)
        {
            Logger = loggerFactory.CreateLogger(GetType().Name);
            this.loggerFactory = loggerFactory;
            this.cmd = cmd;
            this.arg = arg;
            this.bitrate = bitrate;
            sendCmd = cmd.Replace("candump", "cansend");
            canParser = new PiCanMessageParser(loggerFactory);
        }


        public int Open(string driverInterface, CanSpeed speed)
        {
            // Run a shell interface to pican tools
            shell = new ShellCommand(loggerFactory);

            Close();
            Thread.Sleep(1000);
            LinkUp();

            Logger.LogDebug($"CAN link started");

            shell.ReceivedOutput += CanShell_ReceivedOutput;
            receiveThread = new Thread(DoReceive) { IsBackground = true };
            receiveThread.Start();
            IsOpen = true;
            Logger.LogDebug($"Completed pican initialization.");
            return 0;
        }

        private void DoReceive()
        {
            try
            {
                Logger.LogDebug($"Starting pican candump...");
                shell.Run(cmd, arg);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error receiving candump output");
            }
        }

        private void CanShell_ReceivedOutput(string obj)
        {
            try
            {
                // Receive command line CAN dump data
                var cm = canParser.Process(obj);
                Logger?.LogTrace($"RX CANID: {cm.CanId:X}");

                Received?.Invoke(cm);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Unable to process or send message");
            }
        }

        public async Task SendAsync(CanMessage message)
        {
            if (SilentOnCanBus) return;
            var canIdStr = CanUtilities.InferCanIdString(message.CanId);
            var dataStr = CanUtilities.ConvertExactString(message.Data);

            var arg = $"{this.arg} {canIdStr}#{dataStr}";
            await shell.RunInstAsync(sendCmd, arg);
            Logger.LogTrace($"TX: {arg}");
        }

        private void LinkUp()
        {
            var cmd = new ShellCommand(loggerFactory);
            try
            {
                Logger.LogDebug("Turning link up");
                var speed = CanUtilities.ParseSpeed(bitrate);
                Logger.LogDebug($"Start up CAN link: {speed}/{bitrate}...");
                cmd.Run("sudo", $"/sbin/ip link set {arg} up type can bitrate {bitrate}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error starting pican link");
            }
        }

        public void Close()
        {
            var cmd = new ShellCommand(loggerFactory);
            try
            {
                Logger.LogDebug("Turning off can link");
                cmd.Run("sudo", $"/sbin/ip link set {arg} down");

                if (receiveThread != null)
                {
                    shell.Dispose();
                    receiveThread.Abort();
                    receiveThread = null;
                }

                IsOpen = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error closing pican link.");
            }
        }
    }
}
