using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared.EventSubsystem.Attributes
{
    /// <summary>
    /// The fxevent attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class FxEventAttribute : Attribute
    {
        public string Name;
        public FxEventAttribute(string name) => Name = name;
    }
}
