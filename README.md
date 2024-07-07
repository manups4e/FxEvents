# FxEvents: An Advanced Event Subsystem for FiveM

FxEvents provides a robust and secure framework for event handling in FiveM, equipped with powerful serialization, encryption, and anti-tampering features. With its easy integration and extensive functionality, FxEvents is a valuable tool for developers seeking efficient and secure client-server communication in their FiveM projects. It incorporates encrypted signatures and MsgPack binary serialization to safeguard against malicious client actions. To integrate FxEvents into your project, simply include `FxEvents.Client.dll` or `FxEvents.Server.dll` along with `Newtonsoft.Json.dll` (if you opt for JSON serialization). The MsgPack functionality is inherently supported by FiveM, so no additional libraries are required!

Starting from Version 3.0.0, the library eliminates the need for manual event initialization, as internal events are now SHA-256 generated based on the resource name combined with a random seed. This update streamlines the process, with initialization primarily used to register events marked with the `[FxEvent]` attribute that would otherwise require a mandatory call from the requesting script.

For more details and support, [join our Discord server](https://discord.gg/KKN7kRT2vM).

---

**Support**

If you like my work, please consider supporting me via PayPal. You can [buy me a coffee or donut, some banana, a shirt, BMW i4, Taycan, Tesla, the stars, or whatever you want here](https://ko-fi.com/manups4e).

---

## Usage Examples:

### Initialization

To initialize the FxEvents library, include the following in your script:

```csharp
public class Main : BaseScript
{
    public Main()
    {
        // Initialize the FxEvents library. Call this once at script start to enable EventHub usage anywhere.
        EventHub.Initialize();
    }
}
```

---

### Mounting an Event

Events can be mounted similarly to standard events. Below is an example of mounting an event in both lambda style and classic method:

- The `FromSource` attribute enables you to retrieve the source as in standard events, allowing requests for Player, the ISource inheriting class, source as Int32 or as String.

```csharp
EventHub.Mount("eventName", Binding.All, new Action<ISource, type1, type2>(([FromSource] source, val1, val2) =>    
{
    // Code to execute inside the event.
    // ISource is an optional class handling clients triggering the event, similar to the "[FromSource] Player player" parameter but customizable.
    // On the client-side, the same principle applies without the ClientId parameter.
}));

EventHub.Mount("eventName", Binding.All, new Action<ISource, type1, type2>(MyMethod));
private void MyMethod(([FromSource] ISource source, type1 val1, type2 val2)
{
    // Code to execute inside the event.
    // ISource is an optional class handling clients triggering the event, similar to the "[FromSource] Player player" parameter but customizable.
    // On the client-side, the same principle applies without the ClientId parameter.
}
```

- **Q: Must the returning method mounted with `Func` be `async Task`?**
- **A: Not necessarily. If you don't need it to be a task, return the required type. The `Get` method is awaitable because it waits for a response from the other side.**

- Events can also be mounted using the `[FxEvent("EventName")]` attribute or by `EventHub.Events["EventName"] += new Action / new Func`.

⚠️ Note: Only one method per attribute can be registered for callbacks.

---

### Event Handling Enhancements

From version 3.0.0, events are managed similarly to Mono V2 (thanks to @Thorium for ongoing support), allowing for type conversions across sides. Events are no longer bound to specific parameters, enabling casting from one type to another. ⚠️ Generic objects (object or dynamic) are not allowed due to MsgPack restrictions.

Example:

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

Events can be triggered from both client and server sides:

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

#### Mounting a Callback

```csharp
EventHub.Mount("eventName", Binding.All, new Func<ISource, type1, type2, Task<returnType>>(async ([FromSource] source, val1, val2) =>    
{
    // Code to execute inside the event.
    // ISource is an optional class handling clients triggering the event, similar to the "[FromSource] Player player" parameter but customizable.
    // On the client-side, the same principle applies without the ISource parameter.
    return val3;
}));
```

Callbacks can also be mounted using the `[FxEvent("EventName")]` attribute. ⚠️ Only one per endpoint. Example:

```csharp
[FxEvent("myEvent")]
public static string GimmeAll(int a, string b)
    => "This is a test";
```

#### Calling a Callback

```csharp
// Client-side
type param = await EventHub.Get<type>("eventName", params);

// Server-side
type param = await EventHub.Get<type>(ClientId, "eventName", params);
type param = await EventHub.Get<type>(Player, "eventName", params);
```

Callbacks can also be triggered server-side when the server needs information from specific clients.

Both sides have a new `SendLocal` and `GetLocal` (method requires binding as All or Local) to trigger local events. FxEvents is required in both scripts to work.

---

### Native ValueTuple Support

Starting from version 3.0.0, FxEvents offers native ValueTuple support for client-side (server-side using .Net Standard 2.0 already supports it natively). This provides an alternative to non-fixable Tuples that can't be included in collections or as members/fields inside classes and structs. ValueTuple is dynamic, easy to use, supports the latest C# versions, and being natively supported means no need for external NuGet packages or imported libraries to use it with FxEvents and FiveM.

---

### FiveM Types Support

FxEvents supports FiveM internal types, allowing you to send Vectors, Quaternions, Matrices, and Entities.

- **Player support:** You can send a Player object (or an int32/string) and receive a Player object on the other side.
- **Entities support:** You can send a Ped, Vehicle, Prop, or Entity type as long as they're networked. FxEvents handles them using their NetworkID.
- **Vectors and Math structs:** These are handled as float arrays, making them lightweight and fast.

---

### Event Binding

Starting from 3.0.0, all events are handled like in Mono V2 update (thanks @thorium) and require a binding to be specified when mounted. There are 4 types of binding:

- **None:** The event will be ignored and never triggered.
- **Local:** The event is triggered ONLY when called using `SendLocal` / `GetLocal`.
- **Remote:** The event will trigger only for Client/Server events (classic FxEvents way).
- **All:** The event will trigger both with same-side or client/server calls.

This allows better handling of communications between sides. Legacy EventDispatcher automatically mounts as Remote and `[FxEvent]` attribute binds automatically to All if no binding is specified.

---

### Anti-Tamper

In version 3.0.0, a basic Anti-Tamper system is introduced, which uses a server event (`EventHandlers[fxevents:tamperingprotection] += anyMethodYouWant`) with parameters [source, endpoint, TamperType]:

- **source:** The player handle that triggered the Anti-Tamper.
- **endpoint:** The event that has been flagged as tampered.
- **TamperType:** The type of tamper applied. It can be:
 - **TamperType:** The type of tamper applied. It can be:
  - **REPEATED_MESSAGE_ID:** Indicates someone tried to send the same event or a new event with a used ID.
  - **EDITED_ENCRYPTED_DATA:** Indicates someone altered the encrypted data trying to change values, making it impossible to decrypt.
  - **REQUESTED_NEW_PUBLIC_KEY:** Indicates a client tried to request a new ID to change the encryption, potentially attempting to edit the encryption.

---

### Additional Features for Customization and Debugging

FxEvents includes several additional features for customization and debugging, especially regarding serialization:

#### ToJson()

Requires Newtonsoft.Json:
```csharp
string text = param.ToJson();
```

#### FromJson()

Requires Newtonsoft.Json:
```csharp
type value = jsonText.FromJson<type>();
```

#### ToBytes()

```csharp
byte[] bytes = param.ToBytes();
```

#### FromBytes()

```csharp
type value = bytes.FromBytes<type>();
```

#### EncryptObject(string passkey)

Binary serialization is performed internally. ⚠️ Same key for encryption and decryption:
```csharp
byte[] bytes = param.EncryptObject("passkey");
```

#### DecryptObject(string passkey)

Binary deserialization is performed internally. ⚠️ Same key for encryption and decryption:
```csharp
T object = bytes.DecryptObject<T>("passkey");
```

#### GenerateHash(string input)

Generates the SHA-256 hash of the given input string:
```csharp
byte[] hash = Encryption.GenerateHash(string input)
```

#### BytesToString

Converts a byte array to a string:
```csharp
byte[] bytes = param.ToBytes();
string txt = bytes.BytesToString();
```

#### StringToBytes

Converts a string back to a byte array:
```csharp
byte[] bytes = txt.StringToBytes();
type value = bytes.FromBytes<type>();
```
