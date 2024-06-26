using System;

namespace FxEvents.Shared.Attributes
{
    /// <summary>
    /// Indicates that this property should be forcefully added to serialization.
    /// </summary>
    [Obsolete("the current MsgPack version does not consent to serialize non public members/fields")]
    public class ForceAttribute : Attribute
    {
    }
}