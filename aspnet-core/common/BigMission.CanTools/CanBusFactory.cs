﻿using BigMission.CanTools.PiCan;
using BigMission.CanTools.SerialCan;
using NLog;
using System;
using System.Runtime.InteropServices;

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
            // Use pican on linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logger.Trace($"File exists: ${cmd}");
                canBus = new PiCanCanBus(logger, cmd, arg, bitrate);
                canBus.Open(arg, speed);
            }
            // Use serial port on windows
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            else
            {
                throw new NotImplementedException("OS Platform not supported.");
            }

            return canBus;
        }
    }
}
