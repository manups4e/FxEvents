﻿using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization.Implementations.MsgPackResolvers
{
    internal class Matrix3x3Resolver : MessagePackSerializer<Matrix3x3>
    {
        public Matrix3x3Resolver(MsgPack.Serialization.SerializationContext ownerContext) : base(ownerContext)
        {
        }

        protected override void PackToCore(Packer packer, Matrix3x3 objectTree)
        {
            float[] array = objectTree.ToArray();
            packer.PackArray(array, OwnerContext);
        }

        protected override Matrix3x3 UnpackFromCore(Unpacker unpacker)
        {
            float[] values = new float[9];
            for (int i = 0; i < 9; i++)
            {
                if (!unpacker.Read())
                {
                    throw SerializationExceptions.NewMissingItem(i);
                }

                float item;
                if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
                {
                    item = OwnerContext.GetSerializer<float>().UnpackFrom(unpacker);
                }
                else
                {
                    using Unpacker subtreeUnpacker = unpacker.ReadSubtree();
                    item = OwnerContext.GetSerializer<float>().UnpackFrom(subtreeUnpacker);
                }

                values[i] = item;
            }
            return new Matrix3x3(values);

        }

    }
}
