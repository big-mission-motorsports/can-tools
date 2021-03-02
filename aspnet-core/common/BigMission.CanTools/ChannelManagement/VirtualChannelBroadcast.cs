using BigMission.DeviceApp.Shared;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BigMission.CanTools.ChannelManagement
{
    /// <summary>
    /// Tracks the channels that will be sent on the CAN bus and sends on specificed channel frequency.
    /// </summary>
    public class VirtualChannelBroadcast
    {
        private ILogger Logger { get; }
        private readonly Dictionary<int, ChannelInstance> channels = new Dictionary<int, ChannelInstance>();
        private readonly Dictionary<uint, CanInstance> canValues = new Dictionary<uint, CanInstance>();
        private Timer broadcastTimer;
        private const int CHANNEL_TIMEOUT = 10000;
        private Timer timeoutTimer;

        private ICanBus canBus;
        private object canBusLock = new object();
        public ICanBus CanBus
        {
            get
            {
                lock (canBusLock)
                {
                    //Logger.Trace($"Get CanBus for virutal: {canBus}");
                    return canBus;
                }
            }
            set
            {
                lock (canBusLock)
                {
                    //Logger.Trace("Set CanBus for virutal.");
                    canBus = value;
                }
            }
        }


        public VirtualChannelBroadcast(ILogger logger)
        {
            Logger = logger;
        }


        public void SetVirtualChannelMappings(ChannelMappingDto[] channelMappings)
        {
            var channelIds = channelMappings.Select(c => c.Id);
            lock (channels)
            {
                var existing = channels.Where(c => channelIds.Contains(c.Key)).Select(c => c.Value).ToArray();
                channels.Clear();
                foreach (var cm in channelMappings)
                {
                    var ci = existing.FirstOrDefault(c => c.Mapping.Id == cm.Id);
                    if (ci != null)
                    {
                        ci.Mapping = cm;
                    }
                    else
                    {
                        ci = new ChannelInstance { Mapping = cm };
                    }

                    channels[cm.Id] = ci;
                }
            }
        }

        public void UpdateValues(ChannelDataSetDto dataSet)
        {
            var chInstances = new List<ChannelInstance>();
            lock (channels)
            {
                foreach (var d in dataSet.Data)
                {
                    if (channels.TryGetValue(d.ChannelId, out ChannelInstance ci))
                    {
                        ci.Status = d;
                        chInstances.Add(ci);
                    }
                }
            }

            lock (canValues)
            {
                foreach (var ci in chInstances)
                {
                    if (!canValues.TryGetValue(ci.Mapping.CanId, out CanInstance canData))
                    {
                        canData = new CanInstance();
                        canValues[ci.Mapping.CanId] = canData;
                    }
                    canData.Data = CanUtilities.Encode(canData.Data, ci.Status.Value, ci.Mapping);
                    canData.UpdateImmediate();
                }
            }
        }

        public void Broadcast()
        {
            if (broadcastTimer != null) { throw new InvalidOperationException(); }

            broadcastTimer = new Timer(BroadcastCallback, null, 500, 100);
            timeoutTimer = new Timer(TimeoutCallback, null, 3000, CHANNEL_TIMEOUT);
        }

        private void BroadcastCallback(object o)
        {
            try
            {
                if (Monitor.TryEnter(broadcastTimer))
                {
                    try
                    {
                        var can = CanBus;
                        if (can == null)
                        {
                            return;
                        }

                        // Get value payload
                        KeyValuePair<uint, CanInstance>[] channelPayloads;
                        lock (canValues)
                        {
                            var now = DateTime.UtcNow;
                            channelPayloads = canValues.Where(cv => now > cv.Value.NextBroadcast).ToArray();
                        }

                        // Send
                        try
                        {
                            foreach (var cv in channelPayloads)
                            {
                                var canIdStr = CanUtilities.InferCanIdString(cv.Key);
                                var dataStr = CanUtilities.GetDataString(cv.Value.Data);
                                var cm = new CanMessage { CanId = cv.Key, Data = cv.Value.Data, IdLength = cv.Value.CanIdLength, DataLength = cv.Value.DataLength };
                                can.Send(cm);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "Error sending can broadcast");
                        }

                        lock (channels)
                        {
                            foreach (var ci in channelPayloads)
                            {
                                var channelMappings = channels.Values
                                    .Where(c => c.Mapping.CanId == ci.Key)
                                    .Select(c => c.Mapping)
                                    .ToArray();
                                ci.Value.UpdateNext(channelMappings);
                            }
                        }
                    }
                    finally
                    {
                        Monitor.Exit(broadcastTimer);
                    }
                }
                else
                {
                    Logger.Debug("Can broadcast timer skipped");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error sending can broadcast.");
            }
        }

        private void TimeoutCallback(object o)
        {
            try
            {
                var timeoutChannels = new List<ChannelStatusDto>();
                var now = DateTime.UtcNow;
                var ts = TimeSpan.FromMilliseconds(CHANNEL_TIMEOUT);
                lock (channels)
                {
                    foreach(var ch in channels)
                    {
                        if ((now - ch.Value.Status?.Timestamp) > ts)
                        {
                            ch.Value.Status.Value = 0;
                            timeoutChannels.Add(ch.Value.Status);
                            Logger.Trace($"Timeout on channel {ch.Value.Mapping.ChannelName}. Reset to 0.");
                        }
                    }
                }

                if (timeoutChannels.Any())
                {
                    var ds = new ChannelDataSetDto { Data = timeoutChannels.ToArray() };
                    UpdateValues(ds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error checking for channel timeouts.");
            }
        }

        public void TearDown()
        {
            if (broadcastTimer != null)
            {
                broadcastTimer.Dispose();
            }
        }

        private class ChannelInstance
        {
            public ChannelMappingDto Mapping { get; set; }
            public ChannelStatusDto Status { get; set; }
        }

        private class CanInstance
        {
            public IdLength CanIdLength { get; private set; }
            public ulong Data { get; set; }
            public DateTime NextBroadcast { get; set; }
            public int DataLength { get; private set; }

            public void UpdateNext(ChannelMappingDto[] channelMappings)
            {
                if (channelMappings.First().CanId > CanUtilities.MAX_11_BIT_ID)
                {
                    CanIdLength = IdLength._29bit;
                }
                else
                {
                    CanIdLength = IdLength._11bit;
                }

                int freqMs = channelMappings.Min(m => m.VirtualFrequencyMs);
                if (freqMs < 100)
                {
                    freqMs = 100;
                }
                NextBroadcast = DateTime.UtcNow + TimeSpan.FromMilliseconds(freqMs);

                // Set the data length
                foreach(var cm in channelMappings)
                {
                    var l = cm.Offset + cm.Length;
                    if (l > DataLength)
                    {
                        DataLength = l;
                    }
                }
            }

            public void UpdateImmediate()
            {
                NextBroadcast = DateTime.UtcNow;
            }
        }
    }
}
