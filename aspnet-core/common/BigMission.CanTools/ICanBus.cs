using System;
using System.Threading.Tasks;

namespace BigMission.CanTools
{
    /// <summary>
    /// Represents a general CAN Bus interface driver for sending and receiving messages.
    /// </summary>
    public interface ICanBus
    {
        bool IsOpen { get; }

        /// <summary>
        /// Connects to CAN bus and starts listening.
        /// </summary>
        /// <param name="driverInterface">e.g. can0 or COM3</param>
        /// <param name="speed"></param>
        /// <returns></returns>
        int Open(string driverInterface, CanSpeed speed);

        /// <summary>
        /// Triggered when there is a new message received on the CAN Bus.
        /// </summary>
        event Action<CanMessage> Received;

        /// <summary>
        /// Send a message on the CAN bus.
        /// </summary>
        /// <param name="message"></param>
        Task SendAsync(CanMessage message);

        void Close();
    }
}
