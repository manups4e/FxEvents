using System;

namespace FxEvents.Shared.Attributes
{
    /// <summary>
    /// Indicates that this property should be disregarded from serialization.
    /// </summary>
    [Obsolete("the current MsgPack version does not consent to ignore non public members/fields")]
    public class IgnoreAttribute : Attribute
    {
    }
}