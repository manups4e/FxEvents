using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Serialization
{

    internal enum MsgPackCode : byte
    {
        FixIntPositiveMin = 0,
        FixIntPositiveMax = 127,
        NilValue = 192,
        TrueValue = 195,
        FalseValue = 194,
        SignedInt8 = 208,
        UnsignedInt8 = 204,
        SignedInt16 = 209,
        UnsignedInt16 = 205,
        SignedInt32 = 210,
        UnsignedInt32 = 206,
        SignedInt64 = 211,
        UnsignedInt64 = 207,
        Real32 = 202,
        Real64 = 203,
        MinimumFixedArray = 144,
        MaximumFixedArray = 159,
        Array16 = 220,
        Array32 = 221,
        MinimumFixedMap = 128,
        MaximumFixedMap = 143,
        Map16 = 222,
        Map32 = 223,
        MinimumFixedRaw = 160,
        MaximumFixedRaw = 191,
        MaximumFixedString = 175,
        Str8 = 217,
        Raw16 = 218,
        Raw32 = 219,
        Bin8 = 196,
        Bin16 = 197,
        Bin32 = 198,
        Ext8 = 199,
        Ext16 = 200,
        Ext32 = 201,
        FixExt1 = 212,
        FixExt2 = 213,
        FixExt4 = 214,
        FixExt8 = 215,
        FixExt16 = 216,
        FixIntNegativeMin = 0xE0,
        FixIntNegativeMax = 0xFF
    }
}