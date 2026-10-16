using FxEvents.Shared.TypeExtensions;
using MsgPack;
using MsgPack.Serialization;
using System;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    public class PlayerResolver : MessagePackSerializer<Player>
    {
        public PlayerResolver(SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Player objectTree)
        {
#if CLIENT
            packer.Pack(objectTree.ServerId);
#elif SERVER
            packer.Pack(int.Parse(objectTree.Handle));
#endif
        }

        protected override Player UnpackFromCore(Unpacker unpacker)
        {
            var data = unpacker.LastReadData;
            int handle = data.UnderlyingType == typeof(string) 
                ? (int.TryParse(data.AsString(), out int parsed) ? parsed : 0)
                : data.AsInt32();
            return EventHub.Instance.GetPlayers[handle];
        }
    }
}
