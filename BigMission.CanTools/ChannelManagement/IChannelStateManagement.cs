using BigMission.Drivesync.Shared;
using System.Collections.Generic;

namespace BigMission.CanTools.ChannelManagement;

public interface IChannelStateManagement
{
    ChannelStatusDto[] ClaimAllChannel();
    ChannelStatusDto[] ClaimDirtyChannels();
    Dictionary<int, ChannelStatusDto> GetChannelLookupPassive();
    void UpdateChannelValues(ChannelStatusDto[] values);
}