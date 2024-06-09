# FxEvents an advanced event subsystem for FiveM

With FxEvents you can send and get values between client and server using an advanced event handling process. 
Signatures are encrypted per each client and using the provided MsgPack binary serialization you can hide contents from malicious clients!
To work you only need to add `FxEvents.Client.dll` or `FxEvents.Server.dll` and `Newtonsoft.Json.dll` (In case of json serialization) to your resource.
No need of any external library for MsgPack, the event system uses the internal MsgPack dll provided internally by fivem itself!!

[Discord Server Invite](https://discord.gg/KKN7kRT2vM)

Usage examples:

## Initialization
- Encryption key **CANNOT** remain empty or null, you can generate encryption keys, passphrases, passwords online or use the provided serverside command `generagekey` to let the library generate a random passphrase both literal and encrtypted to be copied and stored in a safe place. __PLEASE NOTE__: FxEvents won't store nor save any passkey anywhere for security reasons, do not lose the key or your data won't be recovered.
```c#
public class Main : BaseScript
{
 public Main()
 {
  // The Event Dispatcher can now be initialized with your own inbound, outbound, and signatures.
  // This allows you to use FxEvents in more than one resource on the server without having signature collisions.
  EventHub.Initalize("inbound", "outbound", "signature");
 }
}
```
 
## To mount an event:
- Events can be mounted like normal events, this example is made to show an event mounted in-line.
```c#
EventHub.Mount("eventName", new Action<ISource, type1, type2>(([FromSource] source, val1, val2) =>    
{
  // code to be used inside the event.
  // ISource is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
  // Clientside is the same thing without the ClientId parameter
}));
```
- Events can also be mounted by using the attribute [FxEvent("EventName")] or by EventHub.Events["EventName"] += new Action / new Func 
⚠️ (Beware that in case of callbacks, only 1 method per attribute can be registered)
- In version 3.0.0 and above, events are handled like in Mono V2 (thanks @Thorium for not abandoning us) for example
```c#
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
outputs
![image](https://github.com/manups4e/fx-events/assets/4005518/4e42a6b8-e3eb-4337-99a0-22be5b5211b6)

⚠️ Attributed methods MUST be static.

## To trigger an event
The library only works in client <--> server communication.. for the moment same side events are not working but the feature will be added in the future!
```c#
// clientside
EventHub.Send("eventName", params);

// serverside
EventHub.Send(Player, "eventName", params);
EventHub.Send(List<Player>, "eventName", params);
EventHub.Send(ISource, "eventName", params);
EventHub.Send(List<ISource>, "eventName", params);
EventHub.Send("eventName", params); // For all Connected Players
```

## To trigger a callback
### Mounting it
```c#
EventHub.Mount("eventName", new Func<ISource, type1, type2, Task<returnType>>(async ([FromSource] source, val1, val2) =>    
{
  // code to be used inside the event.
  // ISource is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
  // Clientside is the same thing without the ISource parameter
  return val3
}));
```

- Callbacks can also be mounted using [FxEvent("EventName")] attribute ⚠️ ONLY 1 PER ENDPOINT. Example
```c#
[FxEvent("myEvent")]
public static string GimmeAll(int a, string b)
    => "this is a test";
```

### Calling it
```c#
// clientside
type param = await EventHub.Get<type>("eventName", params);

// serverside
type param = await EventHub.Get<type>(ClientId, "eventName", params);
type param = await EventHub.Get<type>(Player, "eventName", params);
```
Callbacks can be called serverside too because it might happen that the server needs info from certain clients and this will help you doing it.

The library comes with some goodies to help with customization and debugging serialization printing.

## ToJson() 
![image](https://user-images.githubusercontent.com/4005518/188593550-48891947-fb41-4ec1-894c-b429ca890361.png)

⚠️ You need Newtonsoft.Json to make this work!!
```c#
string text = param.ToJson();
```

## FromJson()
⚠️ You need Newtonsoft.Json to make this work!!
```c#
type value = jsonText.FromJson<type>();
```

## ToBytes()
![image](https://user-images.githubusercontent.com/4005518/188594841-3ea787d0-37f3-4b23-9ff7-cdb999d0d101.png)
```c#
byte[] bytes = param.ToBytes();
```

## FromBytes()
```c#
type value = bytes.FromBytes<type>();
```

# EncryptObject(string passkey)
- Binary serialization performed internally.
```c#
byte[] bytes = param.EncryptObject("passkey");
```

# DecryptObject(string passkey)
- Binary deserialization performed internally.
```c#
T object = bytes.DecryptObject<T>("passkey");
```

## BytesToString
```c#
byte[] bytes = param.ToBytes();
string txt = bytes.BytesToString();
```

## StringToBytes
```c#
byte[] bytes = txt.StringToBytes();
type value = bytes.FromBytes<type>();
```
