using System;

namespace FxEvents.Shared.Attributes
{
    /// <summary>
    /// Indicates that this property should be forcefully added to serialization.
    /// </summary>
    public class ForceAttribute : Attribute
    {
        public bool Read { get; set; } = true;
        public bool Write { get; set; } = true;
    }
}