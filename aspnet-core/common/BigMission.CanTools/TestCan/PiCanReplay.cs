using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BigMission.CanTools.TestCan
{
    public class PiCanReplay
    {
        public bool RepeatLoop { get; set; } = true;
        public string DataFile { get; }
        public TimeSpan MessageSpacing { get; set; } = TimeSpan.FromMilliseconds(100);
        public TestCanInterface CanBus { get; }
        public ILogger Logger { get; }

        public PiCanReplay(string dataFile, TestCanInterface canBus, ILoggerFactory loggerFactory)
        {
            DataFile = dataFile;
            CanBus = canBus;
            Logger = loggerFactory.CreateLogger(GetType().Name);
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                try
                {
                    do
                    {
                        string line;
                        var file = new StreamReader(DataFile);
                        while ((line = file.ReadLine()) != null)
                        {
                            CanBus.SimulateRx(line);
                            await Task.Delay(MessageSpacing);
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                        }
                    }
                    while (RepeatLoop && !cancellationToken.IsCancellationRequested);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error replaying CAN file.");
                }
            }, cancellationToken);
        }
    }
}
