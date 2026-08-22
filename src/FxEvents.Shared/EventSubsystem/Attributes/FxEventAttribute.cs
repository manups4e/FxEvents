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

    
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class FxNetEventAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class FxLocalEventAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }}
