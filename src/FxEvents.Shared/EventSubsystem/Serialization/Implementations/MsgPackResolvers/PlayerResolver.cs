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
            return EventHub.Instance.GetPlayers[unpacker.LastReadData.AsInt32()];
        }
    }
}
