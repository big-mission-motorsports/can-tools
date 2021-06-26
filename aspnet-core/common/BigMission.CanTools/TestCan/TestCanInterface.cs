﻿using BigMission.CanTools.PiCan;
using NLog;
using System;

namespace BigMission.CanTools.TestCan
{
    public class TestCanInterface : ICanBus
    {
        public event Action<CanMessage> Received;
        private readonly PiCanMessageParser canParser;
        private ILogger Logger { get; }

        public TestCanInterface(ILogger logger)
        {
            Logger = logger;
            canParser = new PiCanMessageParser(Logger);
        }

        public void Close()
        {
            Logger.Info("Closed test can interface");
        }

        public int Open(string driverInterface, CanSpeed speed)
        {
            Logger.Info("Opened test can interface");
            return 0;
        }

        public void Send(CanMessage message)
        {
            Logger.Info("Sent test can interface");
        }


        //  can0  001   [8]  94 00 4F 00 00 00 4E 00
        //  can0  001   [6]  94 00 4F 00 00 00 4E
        //  can0  00000001   [8]  94 00 4F 00 00 00 4E 00
        public void SimulateRx(string piCanMessage)
        {
            Logger.Debug("Sending test can message: " + piCanMessage);
            var cm = canParser.Process(piCanMessage);
            Received?.Invoke(cm);
        }
    }
}