# FxEvents an advanced event subsystem for FiveM

With FxEvents you can send and get values between client and server using an advanced event handling process. 
Signatures are encrypted per each client and using the provided MsgPack binary serialization you can hide contents from malicious clients!
To work you only need to add `FXEvents.Client.dll` or `FXEvents.Server.dll` and `Newtonsoft.Json.dll` (In case of json serialization) to your resource.
No need of any external library for MsgPack, the event system uses the internal MsgPack dll provided with fivem itself!!

[Discord Server Invite](https://discord.gg/KKN7kRT2vM)

Usage examples:

## Initialization
- Encryption key **CANNOT** remain empty or null, you can generate encryption keys, passphrases, passwords online or use the provided serverside command `generagekey` to let the library generate a random passphrase both literal and encrtypted to be copied and stored in a safe place. __PLEASE NOTE__: FXEvents won't store nor save any passkey anywhere for security reasons, do not lose the key or your data won't be recovered.
```c#
public class Main : BaseScript
{
 public Main()
 {
  // The Event Dispatcher can now be initialized with your own inbound, outbound, and signatures.
  // This allows you to use FxEvents in more than one resource on the server without having signature collisions.
  EventDispatcher.Initalize("inbound", "outbound", "signature", "encryption key");
 }
}
```
 
## To mount an event:
- Events can be mounted like normal events, this example is made to show an event mounted in-line.
```c#
EventDispatcher.Mount("eventName", new Action<ISource, type1, type2>(([FromSource] source, val1, val2) =>    
{
  // code to be used inside the event.
  // ISource is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
  // Clientside is the same thing without the ClientId parameter
}));
```

## To trigger an event
The library only works in client <--> server communication.. for the moment same side events are not working but the feature will be added in the future!
```c#
// clientside
EventDispatcher.Send("eventName", params);

// serverside
EventDispatcher.Send(Player, "eventName", params);
EventDispatcher.Send(List<Player>, "eventName", params);
EventDispatcher.Send(ISource, "eventName", params);
EventDispatcher.Send(List<ISource>, "eventName", params);
```

## To trigger a callback
### Mounting it
```c#
EventDispatcher.Mount("eventName", new Func<ISource, type1, type2, Task<returnType>>(async ([FromSource] source, val1, val2) =>    
{
  // code to be used inside the event.
  // ISource is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
  // Clientside is the same thing without the ISource parameter
  return val3
}));
```
### Calling it
```c#
// clientside
type param = await EventDispatcher.Get<type>("eventName", params);

// serverside
type param = await EventDispatcher.Get<type>(ClientId, "eventName", params);
type param = await EventDispatcher.Get<type>(Player, "eventName", params);
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
