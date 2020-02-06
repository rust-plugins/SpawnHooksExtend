using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Spawn Hooks", "NickRimmer", "1.0.0")]
    [Description("Provided additional spawn hooks")]
    public class SpawnHooksExtend : RustPlugin
    {
        private const string DefaultAddedHookName = "OnEntitySpawned";
        private const string DefaultRemovedHookName = "OnEntityRemoved";
        private const string DefaultGroupRemovedHookName = "OnGroupEntityRemoved";
        
        private readonly List<EntityTracker> _trackers = new List<EntityTracker>();
        private PluginConfiguration _config;
        private bool _initialized;
        private bool _debug;

        #region init

        private void Init()
        {
            _config = Config.ReadObject<PluginConfiguration>();
        }

        private void OnServerInitialized(bool serverInitialized)
        {
            if (_config.CheckSpawnsOnInit) NextTick(SpawnHooksReload);
            else _initialized = true;
        }

        [HookMethod("SpawnHooksReload")]
        private void SpawnHooksReload()
        {
            Puts("Spawns looking...");
            Reset();
            LoadSpawns();

            _initialized = true;
        }

        private void Reset()
        {
            foreach (var tracker in _trackers.ToList())
                tracker.Stop();

            // not necessary, but to be sure (;
            _trackers.Clear();
        }

        private void LoadSpawns()
        {
            var entities = UnityEngine.Object
                .FindObjectsOfType<BaseEntity>()
                .Where(IsEntitySpawned);

            foreach (var entity in entities)
            {
                var prop = FindProp(entity);
                if(prop != null) AddToTracking(entity, prop);
            }
        }

        #endregion

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!_initialized) return;
            if (!IsEntitySpawned(entity)) return;

            if(_debug) LogEntity("spawned", entity);

            var prop = FindProp(entity);
            if(prop != null) AddToTracking(entity, prop);
        }

        private void AddToTracking(BaseEntity entity, EntityProperty prop)
        {
            LogEntity("added", entity);
            var tag = CallAddedHook(entity, prop) as string ?? prop.Name;
            var tracker = new EntityTracker(
                entity, 
                prop.UpdateIntervalSeconds, 
                timer, 
                tag, 
                (t) => RemoveFromTracking(prop, t));

            _trackers.Add(tracker);
        }

        private void RemoveFromTracking(EntityProperty prop, EntityTracker tracker)
        {
            if (_trackers.Remove(tracker))
            {
                CallRemovedHook(prop, tracker);
                LogEntity("removed", tracker.Entity);
            }
        }

        #region Call added/removed hooks

        private Dictionary<string, uint> GetCurrentTags()
        {
            return _trackers
                .Select(x => x.Tag)
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => (uint) x.Count());
        }

        private string CallAddedHook(BaseEntity entity, EntityProperty prop)
        {
            var hookName = string.IsNullOrEmpty(prop.CustomAddedHookName)
                ? DefaultAddedHookName
                : prop.CustomAddedHookName;

            var tags = GetCurrentTags();
            return Interface.uMod.CallHook(hookName, prop.Name, entity, tags) as string;
        }

        private void CallRemovedHook(EntityProperty prop, EntityTracker tracker)
        {
            var tags = GetCurrentTags();
            var theLastOne = !tags.ContainsKey(tracker.Tag) || tags[tracker.Tag] <= 0;
            if (theLastOne)
            {
                var groupHookName = string.IsNullOrEmpty(prop.CustomGroupRemovedHookName)
                    ? DefaultGroupRemovedHookName
                    : prop.CustomGroupRemovedHookName;

                Interface.uMod.CallHook(groupHookName, prop.Name, tracker.Tag);
            }

            var hookName = string.IsNullOrEmpty(prop.CustomRemovedHookName)
                ? DefaultRemovedHookName
                : prop.CustomRemovedHookName;

            Interface.uMod.CallHook(hookName, prop.Name, tracker.Tag, tags);
        }

        #endregion

        #region Console commands

        [ConsoleCommand("spawns")]
        private void CmdHelp(ConsoleSystem.Arg arg)
        {
            if (arg.Player()?.IsAdmin == false) return;
            var commands = new[]
            {
                "spawns.find [name] - search spawned entity",
                $"spawns.debug - toggle debug mode ({(_debug ? "Enabled" : "Disabled")})"
            };

            arg.ReplyWith(string.Join("\n", commands));
        }

        [ConsoleCommand("spawns.find")]
        private void CmdFind(ConsoleSystem.Arg arg)
        {
            if (arg.Player()?.IsAdmin == false) return;
            if (!arg.Args.Any())
            {
                CmdHelp(arg);
                return;
            }

            Func<BaseEntity, string> BuildEntityString = (entity) =>
            {
                var info = new List<string>
                {
                    entity.GetType().Name
                };

                info.Add($"Id: {entity.GetInstanceID()}");
                info.Add($"Prefab: {entity.PrefabName}");
                info.Add($"Position: {entity.ServerPosition}");
                info.Add($"Traits: {string.Join(", ", entity.Traits)}");
                info.Add($"Flags: {string.Join(", ", entity.flags)}");
                info.Add($"Tag: {entity.tag}");

                info.Add(string.Empty);
                return string.Join("\n  ", info);
            };

            var s = arg.Args[0].ToLower();
            var entities = UnityEngine.GameObject
                .FindObjectsOfType<BaseEntity>();

            var result = new List<BaseEntity>();
            
            result.AddRange(entities
                .Where(x=>x.GetType().Name.ToLower().StartsWith(s)));
            
            result.AddRange(entities
                .Where(x=>x.PrefabName.ToLower().StartsWith(s)));

            result.AddRange(entities
                .Where(x=>x.GetType().Name.ToLower().Contains(s)));

            result.AddRange(entities
                .Where(x=>x.PrefabName.ToLower().Contains(s)));

            result = result
                .GroupBy(x => x.ServerPosition)
                .Select(x => x.First())
                .ToList();

            arg.ReplyWith(string.Join("\n", result.Select(x => BuildEntityString(x))));

            foreach (var entity in result)
                LogEntity("find", entity);
        }

        [ConsoleCommand("spawns.debug")]
        private void CmdDebug(ConsoleSystem.Arg arg)
        {
            if (arg.Player()?.IsAdmin == false) return;
            _debug = !_debug;
            arg.ReplyWith(_debug ? "Debug enabled" : "Debug disabled");
        }

        #endregion

        #region Boring things
        
        protected override void LoadDefaultConfig() => Config.WriteObject(new PluginConfiguration(), true);

        private void LogEntity(string operation, BaseEntity entity)
        {
            var message = $"{operation} | {entity.GetType().Name} / {entity.PrefabName} {(IsEntitySpawned(entity) ? entity.ServerPosition.ToString() : string.Empty)}";
            var messageWithTimestamp = $"{DateTime.Now.ToString("HH:mm:ss")} | {message}";

            if (_config.LogToConsole) Puts(message);
            if (_config.LogToBroadcast) Server.Broadcast(messageWithTimestamp);
            if (_config.LogToFile) LogToFile("common", messageWithTimestamp, this);
        }

        private EntityProperty FindProp(BaseEntity entity)
        {
            Func<EntityProperty, bool> EqualByClass = (prop) =>
                prop.Name.Equals(entity.GetType().Name, StringComparison.InvariantCultureIgnoreCase);

            Func<EntityProperty, bool> EqualByPrefab = (prop) =>
                prop.Name.EndsWith(".prefab") && prop.Name.Equals(entity.PrefabName, StringComparison.InvariantCultureIgnoreCase);

            return _config
                .EntityProps
                .FirstOrDefault(prop => EqualByClass(prop) || EqualByPrefab(prop));
        }

        #endregion

        public static bool IsEntitySpawned(BaseEntity entity) =>
            entity.IsValid() && 
            entity.gameObject.activeInHierarchy &&
            !entity.IsDestroyed;
    
        #region SpawnHooksExtend.Models

        private class EntityProperty
        {
            [JsonProperty("Entity name")]
            public string Name { get; set; }
    
            [JsonProperty("Update interval")]
            public byte UpdateIntervalSeconds { get; set; }
    
            [JsonProperty("Custom added hook")]
            public string CustomAddedHookName { get; set; }
    
            [JsonProperty("Custom removed hook")]
            public string CustomRemovedHookName { get; set; }
    
            [JsonProperty("Custom group removed hook")]
            public string CustomGroupRemovedHookName { get; set; }
        }
    
        private class EntityTracker
        {
            public BaseEntity Entity { get; }
            public string Tag { get; }
    
            private readonly PluginTimers _timerPlugin;
            private readonly Action<EntityTracker> _onDeactivate;
            private readonly byte _updateIntervalSeconds;
            private Timer _innerTimer;
    
            public EntityTracker(BaseEntity entity, byte updateIntervalSeconds, PluginTimers timerPlugin, string tag, Action<EntityTracker> onDeactivate)
            {
                if (entity == null) throw new ArgumentNullException(nameof(entity));
                if (onDeactivate == null) throw new ArgumentNullException(nameof(onDeactivate));
                if (timerPlugin == null) throw new ArgumentNullException(nameof(timerPlugin));
                if (updateIntervalSeconds == 0) throw new ArgumentNullException(nameof(updateIntervalSeconds));
                if (tag == null) throw new ArgumentNullException(nameof(tag));
    
                Entity = entity;
                Tag = tag;
    
                _updateIntervalSeconds = updateIntervalSeconds;
                _timerPlugin = timerPlugin;
                _onDeactivate = onDeactivate;
    
                CheckActive();
            }
    
            private void CheckActive()
            {
                if (!Plugins.SpawnHooksExtend.IsEntitySpawned(Entity))
                {
                    _onDeactivate.Invoke(this);
                    return;
                }
    
                _innerTimer = _timerPlugin.Once(_updateIntervalSeconds, CheckActive);
            }
    
            public void Stop()
            {
                _onDeactivate.Invoke(this);
    
                _innerTimer?.Destroy();
                _innerTimer = null;
            }
        }
    
        private class PluginConfiguration
        {
            [JsonProperty("Log to file")]
            public bool LogToFile { get; set; }
    
            [JsonProperty("Log to global chat")]
            public bool LogToBroadcast { get; set; }
    
            [JsonProperty("Log to console")]
            public bool LogToConsole { get; set; }
    
            [JsonProperty("Check spawns after initialized")]
            public bool CheckSpawnsOnInit { get; set; } = true;
    
            [JsonProperty("List of entities")]
            public EntityProperty[] EntityProps { get; set; } = new[]
            {
                new EntityProperty {Name = nameof(BradleyAPC), UpdateIntervalSeconds = 15},
                new EntityProperty {Name = nameof(CargoPlane), UpdateIntervalSeconds = 30},
                new EntityProperty {Name = nameof(CargoShip), UpdateIntervalSeconds = 60},
                new EntityProperty {Name = nameof(CH47Helicopter), UpdateIntervalSeconds = 30},
                new EntityProperty {Name = nameof(BaseHelicopter), UpdateIntervalSeconds = 30},
            };
        }
    
        #endregion
    }
}
