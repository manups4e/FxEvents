using MsgPack;
using MsgPack.Serialization;
using System;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPack.MsgPackResolvers
{
	public class EnumFixer : MessagePackSerializer<Enum>
	{
		public EnumFixer(SerializationContext ownerContext) : base(ownerContext)
		{
		}

		protected override void PackToCore(Packer packer, Enum objectTree)
		{
			packer.Pack(Convert.ToInt64(objectTree));
		}

		protected override Enum UnpackFromCore(Unpacker unpacker)
		{
			var data = unpacker.LastReadData;
			if (unpacker.ReadInt64(out long value))
			{
				return (Enum)Enum.ToObject(typeof(Enum), value);
			}
			return null;
		}
	}

}
