using BigMission.Drivesync.Shared;
using System.Collections.Generic;
using System.Linq;

namespace BigMission.CanTools.ChannelManagement;

/// <summary>
/// Keeps track of changes to channel values.  This is used
/// for change tracking and sending only changed values.
/// </summary>
public class ChannelStateManagement : IChannelStateManagement
{
    private readonly Dictionary<int, ChannelStatusDto> channels = [];
    private readonly HashSet<int> dirtyChannels = [];

    public void UpdateChannelValues(ChannelStatusDto[] values)
    {
        lock (this)
        {
            foreach (var newVal in values)
            {
                var exists = channels.TryGetValue(newVal.ChannelId, out ChannelStatusDto oldVal);
                if (!exists || (exists && oldVal.Value != newVal.Value))
                {
                    channels[newVal.ChannelId] = newVal;
                    dirtyChannels.Add(newVal.ChannelId);
                }
            }
        }
    }

    public ChannelStatusDto[] ClaimDirtyChannels()
    {
        var cvl = new List<ChannelStatusDto>();
        lock (this)
        {
            foreach (var chid in dirtyChannels)
            {
                if (channels.TryGetValue(chid, out ChannelStatusDto cv))
                {
                    cvl.Add(cv);
                }
            }
            dirtyChannels.Clear();
        }
        return [.. cvl];
    }

    public ChannelStatusDto[] ClaimAllChannel()
    {
        ChannelStatusDto[] values;
        lock (this)
        {
            values = [.. channels.Values];
            dirtyChannels.Clear();
        }
        return values;
    }

    public Dictionary<int, ChannelStatusDto> GetChannelLookupPassive()
    {
        Dictionary<int, ChannelStatusDto> values;
        lock (this)
        {
            values = new Dictionary<int, ChannelStatusDto>(channels);
        }
        return values;
    }
}
