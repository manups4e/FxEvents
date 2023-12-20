namespace Logger
{
    public interface ILogger
    {

        /// <summary>
        /// Sends an green info message in console
        /// </summary>
        /// <param name="text">Text of the message</param>
        void Info(string text);

        /// <summary>
        /// Sends a purple debug message in console. (it checks for 'fxevents_debug_mode' in the fxmanifest)
        /// </summary>
        /// <param name="text">Text of the message</param>
        void Debug(string text);

        /// <summary>
        /// Sends a yellow Warning message
        /// </summary>
        /// <param name="text">Text of the message</param>
        void Warning(string text);

        /// <summary>
        /// Sends a red Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        void Error(string text);

        /// <summary>
        /// Sends a dark red Fatal Error message
        /// </summary>
        /// <param name="text">Text of the message</param>
        void Fatal(string text);
    }
}