using FxEvents;
using System;

namespace Logger
{
    public class Log : ILogger
    {
        public Log() { }

        /// <summary>
        /// Sends an green info message in console
        /// </summary>
        /// <param name="text">Text of the message</param>
        public void Info(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [INFO] -- ";
            string color = LoggerColors.LIGHT_GREEN;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a purple debug message in console. (it checks for 'fxevents_debug_mode' in the fxmanifest)
        /// </summary>
        /// <param name="text">Text of the message</param>
        public void Debug(string text)
        {
            if (!EventDispatcher.Debug) return;
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [DEBUG] -- ";
            string color = LoggerColors.LIGHT_BLUE;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a yellow Warning message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public void Warning(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [WARNING] --";
            string color = LoggerColors.YELLOW;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a red Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public void Error(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [ERROR] -- ";
            string color = LoggerColors.LIGHT_RED;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }

        /// <summary>
        /// Sends a dark red Fatal Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        public void Fatal(string text)
        {
            string timestamp = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string errorPrefix = "-- [FATAL] -- ";
            string color = LoggerColors.DARK_RED;
            CitizenFX.Core.Debug.WriteLine($"{color}{timestamp} {errorPrefix} {text}.^7");
        }
    }
}