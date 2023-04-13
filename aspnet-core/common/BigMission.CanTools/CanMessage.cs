﻿using System;

namespace BigMission.CanTools
{
    public enum IdLength { _11bit, _29bit }

    public class CanMessage
    {
        public uint CanId { get; set; }
        public IdLength IdLength { get; set; }
        public byte[] Data { get; set; }
        public int DataLength { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
