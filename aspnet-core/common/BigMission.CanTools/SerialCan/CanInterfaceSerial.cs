using NLog;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace BigMission.CanTools.SerialCan
{
    /// <summary>
    /// Conencts with USB CAN bus interface with LAWICEL CAN232 Windows drivers.
    /// To enable driver support from CANUSB, change the USB Serial Driver 
    /// in Windows Device Manager to "Load VCP" on Advanced tab.
    /// </summary>
    public class CanInterfaceSerial : ICanBus
    {
        private string CommPort { get; }
        private const int COM_SPEED = 57600;
        private SerialPort serialPort;
        public event Action<CanMessage> Received;
        private ILogger Logger { get; }
        private readonly StringBuilder responseBuffer = new StringBuilder();
        public bool IsOpen { get; private set; }


        public CanInterfaceSerial(ILogger logger)
        {
            Logger = logger;
        }


        public int Open(string driverInterface, CanSpeed speed)
        {
            serialPort = new SerialPort(driverInterface, COM_SPEED);
            try
            {
                serialPort.Open();
            }
            catch (System.IO.IOException ex)
            {
                Logger.Warn(ex, "Unable to connect to CAN over COMM port");
                return -1;
            }

            if (serialPort.IsOpen)
            {
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Write($"S{(int)speed}\r");
                serialPort.Write("O\r");
                IsOpen = true;
                return 0;
            }
            else
            {
                return -1;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var data = serialPort.ReadExisting();
            if (Received != null)
            {
                //Logger.Trace("RX:" + data);
                responseBuffer.Append(data);

                var buff = responseBuffer.ToString();
                var lines = buff.Split('\r').ToList();
                responseBuffer.Clear();

                if (lines.Count > 1)
                {
                    var last = lines.Last();

                    // Check for incomplete content and retain for next response
                    if (!string.IsNullOrEmpty(last))
                    {
                        responseBuffer.Append(last);
                    }
                    lines.RemoveAt(lines.Count - 1);
                }

                foreach (var line in lines)
                {
                    var cm = Parse(line);
                    if (Received != null && cm != null)
                    {
                        Received(cm);
                    }
                }
            }
        }

        private static CanMessage Parse(string line)
        {
            // Make sure there is sufficent content to be valid before bothering
            if (line.Length < 6)
                return null;

            var cm = new CanMessage { Timestamp = DateTime.UtcNow };
            var l = line;

            // Get the message ID
            if (line[0] == 'T')
            {
                if (line.Length < 9)
                    return null;

                cm.IdLength = IdLength._29bit;
                l = l[1..];
                cm.CanId = uint.Parse(l[0..8], System.Globalization.NumberStyles.HexNumber);
                l = l[8..];
            }
            else if (line[0] == 't')
            {
                cm.IdLength = IdLength._11bit;
                l = l[1..];
                cm.CanId = uint.Parse(l[0..3], System.Globalization.NumberStyles.HexNumber);
                l = l[3..];
            }
            else
            {
                // Throw away invalid messages
                return null;
            }

            // Data length
            if (l.Length < 1)
                return null;

            cm.DataLength = int.Parse(l[0].ToString());
            l = l[1..];

            // Data
            var bytes = new List<byte>();
            for (int i = 0; i < cm.DataLength; i++)
            {
                if (l.Length < 2)
                    return null;

                var b = byte.Parse(l[0..2], System.Globalization.NumberStyles.HexNumber);
                bytes.Add(b);
                l = l[2..];
            }

            for (int i = bytes.Count; i < 8; i++)
            {
                bytes.Add(0);
            }

            // Preserve byte order
            bytes.Reverse();

            cm.Data = BitConverter.ToUInt64(bytes.ToArray());
            return cm;
        }

        public void Close()
        {
            if (serialPort.IsOpen)
            {
                serialPort.Write("C\r");
                serialPort.Close();
                serialPort.Dispose();
                IsOpen = false;
            }
        }

        public Task SendAsync(CanMessage cm)
        {
            var idstr = cm.CanId.ToString("X");
            var idTotalLen = 3;
            var messagePrefix = "t";
            if (cm.IdLength == IdLength._29bit)
            {
                idTotalLen = 8;
                messagePrefix = "T";
            }

            // Append any needed leading zeros
            while (idstr.Length < idTotalLen)
            {
                idstr = "0" + idstr;
            }

            var dataStr = CanUtilities.ConvertExactString(cm.Data, cm.DataLength);

            var message = $"{messagePrefix}{idstr}{cm.DataLength}{dataStr}\r";
            if (serialPort.IsOpen)
            {
                Logger.Trace("TX:" + message);
                serialPort.Write(message);
            }

            return Task.CompletedTask;
        }

    }
}
