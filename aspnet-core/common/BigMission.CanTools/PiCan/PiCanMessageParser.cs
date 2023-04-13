using NLog;
using System;
using System.Text.RegularExpressions;

namespace BigMission.CanTools.PiCan
{
    /// <summary>
    /// Processes CAN Bus messages from command line output that's
    /// received from pican candump command.
    /// </summary>
    public class PiCanMessageParser
    {
        private ILogger Logger { get; }

        //  can0  001   [8]  94 00 4F 00 00 00 4E 00
        //  can0  001   [6]  94 00 4F 00 00 00 4E
        //  can0  00000001   [8]  94 00 4F 00 00 00 4E 00
        private readonly Regex regex = new Regex(@"\s*can\d\s+(?'id'[\d\w]+)\s+\[(?'len'\d)\]\s+(?'data'[\s\d\w]{2,23})");


        public PiCanMessageParser(ILogger logger)
        {
            Logger = logger;
        }


        public CanMessage Process(string message)
        {
            try
            {
                var timestamp = DateTime.UtcNow;
                var m = regex.Match(message);
                if (m.Success)
                {
                    var idstr = m.Groups["id"].Value;
                    var id = Convert.ToUInt32(idstr, 16);
                    var dataBytes = int.Parse(m.Groups["len"].Value);
                    var dataStr = m.Groups["data"].Value;

                    dataStr = dataStr.Replace(" ", "");
                    //for (int i = dataBytes; i < 8; i++)
                    //{
                    //    dataStr += "00";
                    //}

                    //ulong data = Convert.ToUInt64(dataStr, 16);
                    var data = new byte[dataBytes];
                    for (int i = 0; i < dataBytes; i++)
                    {
                        var s = dataStr[..2];
                        dataStr = dataStr.Remove(0, 2);
                        data[i] = Convert.ToByte(s, 16);
                    }

                    var cm = new CanMessage { CanId = id, Timestamp = timestamp, Data = data, DataLength = dataBytes };
                    if (idstr.Length == 3)
                    {
                        cm.IdLength = IdLength._11bit;
                    }
                    else
                    {
                        cm.IdLength = IdLength._29bit;
                    }

                    return cm;
                }
                else
                {
                    Logger.Trace($"Error parsing: {message}");
                }
            }
            catch(Exception ex)
            {
                Logger.Error(ex, "Processing CAN message failed:{0}", message);
            }
            return null;
        }


    }
}
