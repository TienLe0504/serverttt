using System;
using System.Collections.Generic;
using System.Linq;

namespace NetworkShared.Registries
{
    public class PacketRegistry
    {
        private Dictionary<PacketType, Type> packetTypes = new Dictionary<PacketType, Type>();

        public Dictionary<PacketType, Type> PacketTypes
        {
            get
            {
                if (packetTypes.Count == 0)
                {
                    Initialize();
                }

                return packetTypes;
            }
        }

        private void Initialize()
        {
            var packetType = typeof(INetPacket);
            var packets = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => packetType.IsAssignableFrom(p) && !p.IsInterface);

            foreach (var packet in packets)
            {
                var instance = (INetPacket)Activator.CreateInstance(packet);
                packetTypes.Add(instance.Type, packet);
            }
        }
    }
}
