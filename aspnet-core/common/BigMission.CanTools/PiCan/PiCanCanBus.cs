using NLog;
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

        private readonly string cmd;
        private readonly string arg;
        private readonly string bitrate;


        public PiCanCanBus(ILogger logger, string cmd, string arg, string bitrate)
        {
            Logger = logger;
            this.cmd = cmd;
            this.arg = arg;
            this.bitrate = bitrate;
            sendCmd = cmd.Replace("candump", "cansend");
            canParser = new PiCanMessageParser(Logger);
        }


        public int Open(string driverInterface, CanSpeed speed)
        {
            // Run a shell interface to pican tools
            shell = new ShellCommand(Logger);

            Close();
            Thread.Sleep(1000);
            LinkUp();

            Logger.Debug($"CAN link started");

            shell.ReceivedOutput += CanShell_ReceivedOutput;
            receiveThread = new Thread(DoReceive) { IsBackground = true };
            receiveThread.Start();
            IsOpen = true;
            Logger.Debug($"Completed pican initialization.");
            return 0;
        }

        private void DoReceive()
        {
            try
            {
                Logger.Debug($"Starting pican candump...");
                shell.Run(cmd, arg);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error receiving candump output");
            }
        }

        private void CanShell_ReceivedOutput(string obj)
        {
            try
            {
                // Receive command line CAN dump data
                var cm = canParser.Process(obj);
                Logger?.Trace($"RX CANID: {cm.CanId:X}");

                Received?.Invoke(cm);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Unable to process or send message");
            }
        }

        public async Task SendAsync(CanMessage message)
        {
            if (SilentOnCanBus) return;
            var canIdStr = CanUtilities.InferCanIdString(message.CanId);
            var dataStr = CanUtilities.ConvertExactString(message.Data);

            var arg = $"{this.arg} {canIdStr}#{dataStr}";
            await shell.RunInstAsync(sendCmd, arg);
            Logger.Trace($"TX: {arg}");
        }

        private void LinkUp()
        {
            var cmd = new ShellCommand(Logger);
            try
            {
                Logger.Debug("Turning link up");
                var speed = CanUtilities.ParseSpeed(bitrate);
                Logger.Debug($"Start up CAN link: {speed}/{bitrate}...");
                cmd.Run("sudo", $"/sbin/ip link set {arg} up type can bitrate {bitrate}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error starting pican link");
            }
        }

        public void Close()
        {
            var cmd = new ShellCommand(Logger);
            try
            {
                Logger.Debug("Turning off can link");
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
                Logger.Error(ex, "Error closing pican link.");
            }
        }
    }
}
