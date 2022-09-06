# FxEvents an advanced event subsystem for FiveM

With FxEvents you can send and get values between client and server using an advanced event handling process. 
Signatures are encrypted per each client and using the proviced binary serialization you can hide contents from malicious clients!
To work you'll have to implement the Serialization Generator to be found in https://github.com/manups4e/fx-events/tree/main/src/generator into your project.

Usage examples:
 
## To mount an event:
```c#
EventDispatcher.Mount("eventName", new Action<ClientId, type1, type2>((source, val1, val2) =>    
{
  // code to be used inside the event.
  // ClientId is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
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
EventDispatcher.Send(ClientId, "eventName", params);
EventDispatcher.Send(List<ClientId>, "eventName", params);
```

## To trigger a callback
### Mounting it
```c#
EventDispatcher.Mount("eventName", new Func<ClientId, type1, type2, Task<returnType>>(async (source, val1, val2) =>    
{
  // code to be used inside the event.
  // ClientId is the optional insider class that handles clients triggering the event.. is like the "[FromSource] Player player" parameter but can be derived and handled as you want!!
  // Clientside is the same thing without the ClientId parameter
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

The library comes with some goodies like the extensions "ToJson()", "FromJson()", "ToBytes()", "FromBytes()", "BytesToString()", "StringToBytes()" to help with custom and debugging serialization printing.
