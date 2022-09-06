namespace FxEvents.Shared.Diagnostics
{
    public interface IEventLogger
    {
        void Debug(params object[] values);
        void Info(params object[] values);
    }
}