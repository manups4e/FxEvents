using CitizenFX.Core.Native;
using System;
using System.IO;
using System.Threading.Tasks;

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
        /// <param name="text">Testo del messaggio</param>
        public async void Info(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [INFO] -- ";
            string colore = LIGHT_GREEN;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// sends a purple debug message in console. (it checks for the DEBUG convar on server cfg)
        /// </summary>
        /// <param name="text">Testo del messaggio</param>
        public async void Debug(string text)
        {
            if (API.GetConvarInt("DEBUG", 0) == 0) return;
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [DEBUG] -- ";
            string colore = LIGHT_BLUE;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }

        /// <summary>
        /// Sends a yellow Warning message
        /// </summary>
        /// <param name="text">Testo del messaggio</param>
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
        /// <param name="text">Testo del messaggio</param>
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
        /// <param name="text">Testo del messaggio</param>
        public async void Fatal(string text)
        {
            string incipit = $"{DateTime.Now:dd/MM/yyyy, HH:mm}";
            string err = "-- [FATAL] -- ";
            string colore = DARK_RED;
            CitizenFX.Core.Debug.WriteLine($"{colore}{incipit} {err} {text}.^7");
        }
    }
}