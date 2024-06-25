using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    internal class ISourceResolver : MessagePackSerializer<ISource>
    {
        public ISourceResolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, ISource objectTree)
        {
            packer.Pack(objectTree.Handle);
        }

        protected override ISource UnpackFromCore(Unpacker unpacker)
        {
            return (ISource)Activator.CreateInstance(typeof(ISource), unpacker.LastReadData.AsInt32());
        }
    }
}
