using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PokaAntiAFK_WinForms
{
    // VRChat OSC button: padded address, padded ",i" type tag, big-endian int32.
    internal sealed class OscInput : IDisposable
    {
        private readonly UdpClient client = new UdpClient(AddressFamily.InterNetwork);
        private readonly IPEndPoint endpoint;
        public OscInput(int port) { endpoint = new IPEndPoint(IPAddress.Loopback, port); }
        public void Forward(bool pressed)
        {
            var address = Encoding.ASCII.GetBytes("/input/MoveForward");
            int padded = (address.Length + 4) & ~3;
            var packet = new byte[padded + 8];
            Array.Copy(address, packet, address.Length);
            packet[padded] = (byte)',';
            packet[padded + 1] = (byte)'i';
            packet[packet.Length - 1] = pressed ? (byte)1 : (byte)0;
            client.Send(packet, packet.Length, endpoint);
        }
        public void Dispose() { client.Dispose(); }
    }
}
