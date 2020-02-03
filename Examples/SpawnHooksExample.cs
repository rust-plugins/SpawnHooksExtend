using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spawn Hooks Example", "NickRimmer", "1.0.0")]
    [Description("Example of using Spawn Hooks plugin")]
    public class SpawnHooksExample : RustPlugin
    {
        private const string OilBoxTag = "oil_box";

        // return type can be 'void' or 'string'.
        // If method returned string, then this string will be sent to 'OnEntityRemoved(name, tag, ...)'  as 'tag' value
        //
        // private void OnEntitySpawned(string name, BaseEntity entity, Dictionary<string, int> currentTags)
        private void OnEntitySpawned(string name, BaseEntity entity)
        {
            PrintWarning($"'{name}' was spawned {entity.ServerPosition}");
        }

        // private void OnEntityRemoved(string name, string tag, Dictionary<string, uint> currentTags)
        private void OnEntityRemoved(string name, string tag, Dictionary<string, uint> currentTags)
        {
            if(!currentTags.ContainsKey(tag) || currentTags[tag] == 0) PrintWarning($"All '{name}' was removed");
            else PrintWarning($"'{name}' was removed");
        }

        // private void OnTankSpawned(string name, BaseEntity entity, Dictionary<string, uint> currentTags)
        private void OnTankSpawned()
        {
            PrintWarning("Tank was spawned");
        }

        // private void OnTankRemoved(string name, string tag, Dictionary<string, uint> currentTags)
        private void OnTankRemoved()
        {
            PrintWarning("Tank was removed");
        }

        // return string to tagging this entity
        private string OnLockedCrateSpawned(string name, HackableLockedCrate entity)
        {
            if (IsOnOilRig(entity))
            {
                PrintWarning($"Oil box spawned: {entity.ServerPosition}");
                return OilBoxTag;
            }

            PrintWarning($"Hackable box spawned: {entity.ServerPosition}");
            return null;
        }

        // tag will contains value returned from 'OnLockedCrateSpawned' method for removed Entity
        private void OnLockedCrateRemoved(string name, string tag, Dictionary<string, uint> currentTags)
        {
            if (tag == OilBoxTag)
            {
                if (!currentTags.ContainsKey(tag) || currentTags[tag] == 0) PrintWarning("All Oil boxes disappeared");
                else PrintWarning("Oil box disappeared");
            }
            else
                PrintWarning("Hackable box disappeared");
        }

        private static bool IsOnOilRig(BaseEntity entity)
        {
            if (entity.IsDestroyed) return false;
            
            var radius = 20;
            var nearObjects = GetNearObjects(entity.ServerPosition, radius);
            var result = nearObjects.Any(x =>
                x.StartsWith("oilrig_", true, CultureInfo.InvariantCulture) ||
                x.Equals("Oil Barrel Spawner", StringComparison.InvariantCultureIgnoreCase));

            return result;
        }

        private static IEnumerable<string> GetNearObjects(Vector3 target, float radius)
        {
            var hitColliders = Physics.OverlapSphere(target, radius);
            return hitColliders.Select(x => x.gameObject.name);
        }
    }
}