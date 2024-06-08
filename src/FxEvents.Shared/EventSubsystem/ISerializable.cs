using FxEvents.Shared.Snowflakes;

namespace FxEvents.Shared.EventSubsystem
{

    public interface IMessage
    {
        Snowflake Id { get; set; }
        string Endpoint { get; set; }
        byte[] Signature { get; set; }
    }
}