using FxEvents;
using System;

namespace Logger
{
    public class Log
    {
        // Colors
        public const string LIGHT_RED = "^1";
        public const string LIGHT_GREEN = "^2";
        public const string YELLOW = "^3";
        public const string DARK_BLUE = "^4";
        public const string LIGHT_BLUE = "^5";
        public const string PURPLE = "^6";
        public const string WHITE = "^7";
        public const string DARK_RED = "^8";
        public const string PINK = "^9";

        public Log() { }

        /// <summary>
        /// Sends an green info message in console
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Info(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [INFO] -- ";
            string colore = LIGHT_GREEN;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// Sends a purple debug message in console. (it checks for 'fxevents_debug_mode' in the fxmanifest)
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Debug(string text)
        {
            if (EventDispatcher.Debug) return;
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [DEBUG] -- ";
            string colore = LIGHT_BLUE;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// Sends a yellow Warning message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Warning(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [WARNING] --";
            string colore = YELLOW;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// Sends a red Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Error(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [ERROR] -- ";
            string colore = LIGHT_RED;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// Sends a dark red Fatal Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Fatal(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [FATAL] -- ";
            string colore = DARK_RED;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }
    }
}