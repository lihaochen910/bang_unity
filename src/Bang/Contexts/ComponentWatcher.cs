using Bang.Entities;
using Bang.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace Bang.Contexts
{
    /// <summary>
    /// A context may have a collection of watchers.
    /// </summary>
    #if DEBUG
    [DebuggerDisplay("{_targetComponentDebugInfo}")]
    #endif
    internal class ComponentWatcher
    {
        public readonly World World;

        internal readonly int Id;

        private readonly int _targetComponent;

        private readonly object _lock = new();
        
        #if DEBUG
        private readonly string _targetComponentDebugInfo;
        #endif

        /// <summary>
        /// Tracks the total of entities to notify.
        /// This will make sure that, even if the same entity has an operation multiple times,
        /// it will only be passed on once per update.
        /// Maps:
        ///  [Notification kind -> [Entity id, Entity]]
        /// </summary>
        private Dictionary<WatcherNotificationKind, Dictionary<int, Entity>>? _entitiesToNotify;

        /// <summary>
        /// Get the entities that will be notified.
        /// This will immediately clear the notification list.
        /// </summary>
        public Dictionary<WatcherNotificationKind, Dictionary<int, Entity>> PopNotifications()
        {
            lock (_lock)
            {
                if (_entitiesToNotify is not null)
                {
                    // We will only filter entities that have not been destroyed or are being passed over to a 
                    // remove watch system.
                    var result = _entitiesToNotify.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value
                            .Select(indexAndEntity => indexAndEntity.Value)
                            .Where(e => !e.IsDestroyed || kv.Key == WatcherNotificationKind.Removed)
                            .ToDictionary(e => e.EntityId, e => e));

                    _entitiesToNotify = null;

                    return result;
                }

                throw new InvalidOperationException("Why are we getting the entities for an empty notification?");
            }
        }

        private void QueueEntityNotification(WatcherNotificationKind kind, Entity entity)
        {
            lock (_lock)
            {
                if (_entitiesToNotify is null)
                {
                    _entitiesToNotify = new();
                }

                if (!_entitiesToNotify.ContainsKey(kind))
                {
                    _entitiesToNotify.Add(kind, new());

                    World.QueueWatcherNotification(Id);
                }

                // Only add each entity once to our notification list.
                if (!_entitiesToNotify[kind].ContainsKey(entity.EntityId))
                {
                    _entitiesToNotify[kind].Add(entity.EntityId, entity);
                }
            }
        }

        /// <summary>
        /// A watcher will target a single component.
        /// </summary>
        internal ComponentWatcher(World world, int contextId, Type targetComponent)
        {
            World = world;

            _targetComponent = world.ComponentsLookup.Id(targetComponent);
            Id = HashExtensions.GetHashCode(contextId, _targetComponent);
            
            #if DEBUG
            _targetComponentDebugInfo = $"cw_{targetComponent.Name}";
            #endif
        }
        
        #if !DEBUG
        internal ComponentWatcher(World world, int contextId, int targetComponentId)
        #else
        internal ComponentWatcher(World world, int contextId, int targetComponentId, Type targetComponent)
        #endif
        {
            World = world;

            _targetComponent = targetComponentId;
            Id = HashExtensions.GetHashCode(contextId, _targetComponent);
            
            #if DEBUG
            _targetComponentDebugInfo = $"cw_{targetComponent.Name}";
            #endif
        }

        internal void SubscribeToContext(Context context)
        {
            context.OnComponentAddedForEntityInContext += OnEntityComponentAdded;
            context.OnComponentRemovedForEntityInContext += OnEntityComponentRemoved;
            context.OnComponentBeforeRemovingForEntityInContext += OnEntityComponentBeforeRemoving;
            context.OnComponentModifiedForEntityInContext += OnEntityComponentReplaced;
            context.OnComponentBeforeModifyingForEntityInContext += OnEntityComponentBeforeReplacing;

            context.OnActivateEntityInContext += OnEntityActivated;
            context.OnDeactivateEntityInContext += OnEntityDeactivated;
        }

        private void OnEntityComponentAdded(Entity e, int index)
        {
            // #if DEBUG
            // var targetComponentType = e.World.ComponentsLookup.IdType(_targetComponent);
            // var inComponentType = e.World.ComponentsLookup.IdType(index);
            // #endif
            if (index != _targetComponent)
            {
                return;
            }

            QueueEntityNotification(WatcherNotificationKind.Added, e);
        }

        private void OnEntityComponentRemoved(Entity e, int index, bool causedByDestroy)
        {
            if (index != _targetComponent)
            {
                return;
            }

            if (e.IsDestroyed)
            {
                // entity has already been notified prior to this call.
                return;
            }

            if (_entitiesToNotify is not null &&
                _entitiesToNotify.TryGetValue(WatcherNotificationKind.Added, out var notificationOnAdded) &&
                notificationOnAdded.ContainsKey(e.EntityId))
            {
                // This was previously added. But now it's removed! So let's clean up this list.
                // We do this here because the order matters. If it was removed then added, we want to keep both.
                notificationOnAdded.Remove(e.EntityId);
            }

            QueueEntityNotification(WatcherNotificationKind.Removed, e);
        }

        private void OnEntityComponentBeforeRemoving(Entity e, int index, bool causedByDestroy)
        {
            if (index != _targetComponent)
            {
                return;
            }

            if (e.IsDestroyed)
            {
                // entity has already been notified prior to this call.
                return;
            }
            
            World.NotifyComponentBeforeRemoving(Id, e, index, causedByDestroy);
        }

        private void OnEntityComponentReplaced(Entity e, int index)
        {
            if (index != _targetComponent)
            {
                return;
            }

            QueueEntityNotification(WatcherNotificationKind.Modified, e);
        }
        
        private void OnEntityComponentBeforeReplacing(Entity e, int index)
        {
            if (index != _targetComponent)
            {
                return;
            }
            
            World.NotifyComponentBeforeReplacing(Id, e, index);
        }

        private void OnEntityActivated(Entity e)
        {
            QueueEntityNotification(WatcherNotificationKind.Enabled, e);
        }

        private void OnEntityDeactivated(Entity e)
        {
            if (_entitiesToNotify is not null &&
                _entitiesToNotify.TryGetValue(WatcherNotificationKind.Added, out var notificationOnAdded) &&
                notificationOnAdded.ContainsKey(e.EntityId))
            {
                // This entity was literally just added this frame. For such scenario, don't trigger Added *or* Deactivated.
                // It was born into anonymity. Leave it that way.
                notificationOnAdded.Remove(e.EntityId);
                return;
            }

            QueueEntityNotification(WatcherNotificationKind.Disabled, e);
        }

        internal void OnFinish()
        {
            lock (_lock)
            {
                _entitiesToNotify = null;
            }
        }
    }
}