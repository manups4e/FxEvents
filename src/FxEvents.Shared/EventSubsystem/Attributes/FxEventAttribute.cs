using System;
using System.Collections.Generic;
using System.Text;

namespace FxEvents.Shared
{
    [Flags]
    public enum Binding
    {
        /// <summary>
        /// No one can call this
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Server only accepts server calls, client only client calls
        /// </summary>
        Local = 0x1,

        /// <summary>
        /// Server only accepts client calls, client only server calls
        /// </summary>
        Remote = 0x2,

        /// <summary>
        /// Accept all incoming calls
        /// </summary>
        All = Local | Remote
    }

    /// <summary>
    /// The fxevent attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class FxEventAttribute : Attribute
    {
        public string Name { get; }
        public Binding Binding { get; }
        public FxEventAttribute(string name, Binding binding = Binding.All)
        {
            Name = name;
            Binding = binding;
        }
    }
}
