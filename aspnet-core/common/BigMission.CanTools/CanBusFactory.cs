using BigMission.CanTools.PiCan;
using BigMission.CanTools.SerialCan;
using NLog;
using System;
using System.IO;

namespace BigMission.CanTools
{
    /// <summary>
    /// Creates a CAN bus interface using host setting such as availability of utility commands and serial ports.
    /// </summary>
    public class CanBusFactory
    {
        public static ICanBus CreateCanBus(string cmd, string arg, string bitrate, ILogger logger)
        {
            var speed = CanUtilities.ParseSpeed(bitrate);
            ICanBus canBus;
            // Try pican
            if (File.Exists(cmd))
            {
                logger.Trace($"File exists: ${cmd}");
                canBus = new PiCanCanBus(logger, cmd, arg, bitrate);
                canBus.Open(arg, speed);
            }
            else // Try serial port
            {
                logger.Trace($"Could not find file: ${cmd}. Trying serial driver...");
                canBus = new CanInterfaceSerial(logger);
                try
                {
                    var result = canBus.Open("COM3", speed);
                    if (result != 0)
                    {
                        logger.Trace($"Serial driver failed. Reverting to pican.");
                        // Revert to the pi can when serial interface isn't working
                        canBus = new PiCanCanBus(logger, cmd, arg, bitrate);
                        canBus.Open(arg, speed);
                    }
                }
                catch (UnauthorizedAccessException uae)
                {
                    logger.Error(uae, "Cannot open COM port.");
                }
            }

            return canBus;
        }
    }
}
