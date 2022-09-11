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
        /// sends an green info message in console
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Info(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [INFO] -- ";
            string color = LIGHT_GREEN;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// sends a purple debug message in console. (it checks for the fxevents_debug_mode in the fxmanifest, refer to EventDispatcher)
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Debug(string text)
        {
            if (EventDispatcher.Debug) return;
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [DEBUG] -- ";
            string color = LIGHT_BLUE;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a yellow Warning message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Warning(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [WARNING] --";
            string color = YELLOW;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a red errorPrefixor message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void errorPrefixor(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [errorPrefixOR] -- ";
            string color = LIGHT_RED;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a dark red Fatal errorPrefixor message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public async void Fatal(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [FATAL] -- ";
            string color = DARK_RED;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }
    }
}