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
Callbacks can be called serverside too because it might happen that the server needs info from certain clients and this will help you doing it.

The library comes with some goodies to help with customization and debugging serialization printing.

## ToJson()
![image](https://user-images.githubusercontent.com/4005518/188593550-48891947-fb41-4ec1-894c-b429ca890361.png)
```c#
string text = param.ToJson();
```

## FromJson()
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
