Plugin provided additional spawn hooks for configured objects.  
By default you will have two additional hooks `OnEntitySpawned` and `OnEntityRemoved`, but you can define your own for each object.

## Configuration
```json
{
  "Check spawns after initialized": true,
  "List of entities": [
    {
      "Custom added hook": "OnTankSpawned",
      "Custom removed hook": "OnTankRemoved",
      "Entity name": "BradleyAPC",
      "Update interval": 15
    },
    {
      "Entity name": "CargoPlane",
      "Update interval": 30
    },
    {
      "Entity name": "CargoShip",
      "Update interval": 60
    },
    {
      "Entity name": "CH47Helicopter",
      "Update interval": 30
    },
    {
      "Entity name": "BaseHelicopter",
      "Update interval": 30
    }
  ],
  "Log to file": false,
  "Log to console": false,
  "Log to global chat": false
}
```

'Check spawns after initialized' - if configured as `false`, then plugin will inform you only about new objects. 
'Custom added/removed hook' - your own hooks for particular object  
'Entity name' - can be class name or full prefab name (eg. `assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab`)  
'Update interval' - interval to check if object still exists

## New hooks
```c#
private void OnEntitySpawned(string name, BaseEntity entity)
{
    PrintWarning($"'{name}' was spawned {entity.ServerPosition}");
}

private void OnEntityRemoved(string name)
{
    PrintWarning($"'{name}' was removed");
}
```
Or you can specify tag value (method should return some string, otherwise will be used configured name).  
Then you can see if all objects with same tag was removed.
```c#
private string OnEntitySpawned(string name, BaseEntity entity)
{
    PrintWarning($"'{name}' was spawned {entity.ServerPosition}");
    
    // check if this is some interesting entity
    if(name == "LootContainer" && EntityInSomeArea(entity)) return "SomeSpecialLootContainer";
    return null;
}

private void OnEntityRemoved(string name, string tag, bool lastWithTag)
{
    if(lastWithTag && tag == "SomeSpecialLootContainer") PrintWarning($"All special loot boxes was removed");
    else PrintWarning($"'{name}' was removed");
}
```

## Console commands
Plugin provided a few useful commands only for admins
- `spawns` - will show help for commands
- `spawns.find <class name or prefab>` - will provide all spawned objects with specified name (be careful, it can be long operation)
- `spawns.debug` - will toggle debug mode. When debug is on, plugin will log all new spawned objects (can be useful to find object name or prefab on spawn)