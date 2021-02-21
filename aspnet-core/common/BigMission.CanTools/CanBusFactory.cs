using BigMission.CanTools.PiCan;
using BigMission.CanTools.SerialCan;
using BigMission.DeviceApp.Shared;
using NLog;
using System.IO;

namespace BigMission.CanTools
{
    /// <summary>
    /// Creates a CAN bus interface using host setting such as availability of utility commands and serial ports.
    /// </summary>
    public class CanBusFactory
    {
        public static ICanBus CreateCanBus(CanAppConfigDto config, ILogger logger)
        {
            var speed = CanUtilities.ParseSpeed(config.CanBitrate);
            ICanBus canBus;
            // Try pican
            if (File.Exists(config.CanCmd))
            {
                logger.Trace($"File exists: ${config.CanCmd}");
                canBus = new PiCanCanBus(logger, config);
                canBus.Open(config.CanArg, speed);
            }
            else // Try serial port
            {
                logger.Trace($"Could not find file: ${config.CanCmd}. Trying serial driver...");
                canBus = new CanInterfaceSerial(logger);
                var result = canBus.Open("COM3", speed);
                if (result != 0)
                {
                    logger.Trace($"Serial driver failed. Reverting to pican.");
                    // Revert to the pi can when serial interface isn't working
                    canBus = new PiCanCanBus(logger, config);
                    canBus.Open(config.CanArg, speed);
                }
            }

            return canBus;
        }
    }
}
