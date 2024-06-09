# FxEvents: An Advanced Event Subsystem for FiveM

[Join Our Discord Server](https://discord.gg/KKN7kRT2vM)

FxEvents is a robust event handling system for FiveM, allowing secure and efficient communication between client and server. It features encrypted signatures and MsgPack binary serialization to protect against malicious clients. To integrate FxEvents into your project, simply add `FxEvents.Client.dll` or `FxEvents.Server.dll` along with `Newtonsoft.Json.dll` (if using JSON serialization). The MsgPack functionality is built into FiveM, so no additional libraries are required!

Beginning from Version 3.0.0, the library won't need initialization of events as its internal events are SHA-256 generated starting from the resource name itself + a random seed.
This means that initialization is mainly used to register events with [FxEvent] attribute that wouldn't be registered without a mandatory call from the requesting script.


## Usage Examples

### Initialization

```csharp
public class Main : BaseScript
{
 public Main()
 {
    // Initialize the FxEvents library
    EventHub.Initialize();
 }
}
```

### Mounting an Event

- Events can be mounted like standard events. Here’s an example of an event mounted in-line:

```csharp
EventHub.Mount("eventName", new Action<ISource, type1, type2>(([FromSource] source, val1, val2) =>    
{
  // Code to be executed inside the event.
  // ISource is an optional class that handles clients triggering the event. It is similar to the "[FromSource] Player player" parameter but can be customized.
  // On the client-side, the same principle applies without the ClientId parameter.
}));
```

- Events can also be mounted using the attribute [FxEvent("EventName")] or by `EventHub.Events["EventName"] += new Action / new Func`. 
⚠️ Note: For callbacks, only one method per attribute can be registered.

- Starting from version 3.0.0, events are handled similarly to Mono V2 (thanks to @Thorium for ongoing support). For example:

```csharp
[FxEvent("myEvent")]
public static async void GimmeAll(int a, string b)
    => Logger.Info($"GimmeAll1 {a} {b}");

[FxEvent("myEvent")]
public static async void GimmeAll(int a) 
    => Logger.Info($"GimmeAll1 {a}");

[FxEvent("myEvent")]
public static async void GimmeAll(string a, int b)
    => Logger.Info($"GimmeAll2 {a} {b}");

[FxEvent("myEvent")]
public static async void GimmeAll(string a, int b, string c = "Hey")
    => Logger.Info($"GimmeAll3 {a} {b} {c}");

[FxEvent("myEvent")]
public static async void GimmeAll(int a, string b, string c = "Oh", int d = 678)
    => Logger.Info($"GimmeAll4 {a} {b} {c} {d}");

[FxEvent("myEvent")]
public static async void GimmeAll(int a, PlayerClient b, string c = "Oh", int d = 678)
    => Logger.Info($"GimmeAll5 {a} {b.Player.Name} {c} {d}");

// Trigger the event
EventHub.Send("myEvent", 1234, 1);
```

Outputs:
![image](https://github.com/manups4e/fx-events/assets/4005518/4e42a6b8-e3eb-4337-99a0-22be5b5211b6)

⚠️ Attributed methods MUST be static.

### Triggering an Event

FxEvents currently supports client-server communication only. Future updates will include support for same-side events.

```csharp
// Client-side
EventHub.Send("eventName", params);

// Server-side
EventHub.Send(Player, "eventName", params);
EventHub.Send(List<Player>, "eventName", params);
EventHub.Send(ISource, "eventName", params);
EventHub.Send(List<ISource>, "eventName", params);
EventHub.Send("eventName", params); // For all connected players
```

### Triggering a Callback

#### Mounting it

```csharp
EventHub.Mount("eventName", new Func<ISource, type1, type2, Task<returnType>>(async ([FromSource] source, val1, val2) =>    
{
  // Code to be executed inside the event.
  // ISource is an optional class that handles clients triggering the event. It is similar to the "[FromSource] Player player" parameter but can be customized.
  // On the client-side, the same principle applies without the ISource parameter.
  return val3;
}));
```

- Callbacks can also be mounted using the [FxEvent("EventName")] attribute ⚠️ Only one per endpoint. Example:

```csharp
[FxEvent("myEvent")]
public static string GimmeAll(int a, string b)
    => "This is a test";
```

#### Calling it

```csharp
// Client-side
type param = await EventHub.Get<type>("eventName", params);

// Server-side
type param = await EventHub.Get<type>(ClientId, "eventName", params);
type param = await EventHub.Get<type>(Player, "eventName", params);
```

Callbacks can also be triggered server-side when the server needs information from specific clients.

The library includes additional features for customization and debugging serialization:

### ToJson()
![image](https://user-images.githubusercontent.com/4005518/188593550-48891947-fb41-4ec1-894c-b429ca890361.png)

⚠️ Requires Newtonsoft.Json
```csharp
string text = param.ToJson();
```

### FromJson()
⚠️ Requires Newtonsoft.Json
```csharp
type value = jsonText.FromJson<type>();
```

### ToBytes()
![image](https://user-images.githubusercontent.com/4005518/188594841-3ea787d0-37f3-4b23-9ff7-cdb999d0d101.png)
```csharp
byte[] bytes = param.ToBytes();
```

### FromBytes()
```csharp
type value = bytes.FromBytes<type>();
```

### EncryptObject(string passkey)
- Binary serialization is performed internally.
```csharp
byte[] bytes = param.EncryptObject("passkey");
```

### DecryptObject(string passkey)
- Binary deserialization is performed internally.
```csharp
T object = bytes.DecryptObject<T>("passkey");
```

### GenerateHash(string input)
- Generate the Sha-256 hash of the given input string.
```csharp
byte[] hash = Encryption.GenerateHash(string input)
```

### BytesToString
```csharp
byte[] bytes = param.ToBytes();
string txt = bytes.BytesToString();
```

### StringToBytes
```csharp
byte[] bytes = txt.StringToBytes();
type value = bytes.FromBytes<type>();
```
