﻿using Bang.Components;
using Bang.Entities;
using Bang.Systems;
using Bang.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;


namespace Bang.Contexts
{
    /// <summary>
    /// Context is the pool of entities accessed by each system that defined it.
    /// </summary>
    #if DEBUG
    [DebuggerDisplay("{_contextDebugInfo}")]
    #endif
    public class Context : Observer, IDisposable
    {
        /// <summary>
        /// List of entities that will be fed to the system of this context.
        /// </summary>
        private readonly Dictionary<int, Entity> _entities = new();

        /// <summary>
        /// List of entities that are tracked, yet deactivated.
        /// </summary>
        private readonly HashSet<int> _deactivatedEntities = new();

        /// <summary>
        /// Cached value of the immutable set of entities.
        /// </summary>
        private ImmutableArray<Entity>? _cachedEntities = null;

        /// <summary>
        /// Track the target components and what kind of filter should be performed for each.
        /// </summary>
        private readonly ImmutableDictionary<ContextAccessorFilter, ImmutableArray<int>> _targetComponentsIndex;

        /// <summary>
        /// Track the kind of operation the system will perform for each of the components.
        /// This is saved as a hash set since we will be using this to check if a certain component is set.
        /// </summary>
        private readonly ImmutableDictionary<ContextAccessorKind, ImmutableHashSet<int>> _componentsOperationKind;

        internal ImmutableHashSet<int> ReadComponents => _componentsOperationKind[ContextAccessorKind.Read];

        internal ImmutableHashSet<int> WriteComponents => _componentsOperationKind[ContextAccessorKind.Write];

        /// <summary>
        /// This will be fired when a component is added to an entity present in the system.
        /// </summary>
        internal event Action<Entity, int>? OnComponentAddedForEntityInContext;

        /// <summary>
        /// This will be fired when a component is removed from an entity present in the system.
        /// </summary>
        internal event Action<Entity, int, bool>? OnComponentRemovedForEntityInContext;
        
        /// <summary>
        /// This will be fired when a component before remove from an entity present in the system.
        /// </summary>
        internal event Action<Entity, int, bool>? OnComponentBeforeRemovingForEntityInContext;

        /// <summary>
        /// This will be fired when a component is modified from an entity present in the system.
        /// </summary>
        internal event Action<Entity, int>? OnComponentModifiedForEntityInContext;
        
        /// <summary>
        /// This will be fired when a component is before modify from an entity present in the system.
        /// </summary>
        internal event Action<Entity, int>? OnComponentBeforeModifyingForEntityInContext;

        /// <summary>
        /// This will be fired when an entity (which was previously disabled) gets enabled.
        /// </summary>
        internal event Action<Entity>? OnActivateEntityInContext;

        /// <summary>
        /// This will be fired when an entity (which was previously enabled) gets disabled.
        /// </summary>
        internal event Action<Entity>? OnDeactivateEntityInContext;

        /// <summary>
        /// This will be fired when a message gets added in an entity present in the system.
        /// </summary>
        internal event Action<Entity, int, IMessage>? OnMessageSentForEntityInContext;

        private readonly int _id;

        internal override int Id => _id;

        /// <summary>
        /// Returns whether this context does not have any filter and grab all entities instead.
        /// </summary>
        private bool IsNoFilter => _targetComponentsIndex.ContainsKey(ContextAccessorFilter.None);

        /// <summary>
        /// Entities that are currently active in the context.
        /// </summary>
        public override ImmutableArray<Entity> Entities
        {
            get
            {
                if (_cachedEntities is null)
                {
                    _cachedEntities = _entities.Values.ToImmutableArray();
                }

                return _cachedEntities.Value;
            }
        }

        /// <summary>
        /// Get the single entity present in the context.
        /// This assumes that the context targets a unique component.
        /// TODO: Add flag that checks for unique components within this context.
        /// </summary>
        public Entity Entity
        {
            get
            {
                Debug.Assert(_entities.Count != 0, "Getting an entity without one available. This will crash!");
                return _entities.First().Value;
            }
        }

        /// <summary>
        /// Whether the context has any entity active.
        /// </summary>
        public bool HasAnyEntity => _entities.Count > 0;
        
        #if DEBUG
        private readonly string _contextDebugInfo;
        #endif

        internal Context(World world, ISystem system) : base(world)
        {
            var filters = CreateFilterList(system);
            _targetComponentsIndex = CreateTargetComponents(filters);
            _componentsOperationKind = CreateAccessorKindComponents(filters);

            _id = CalculateId();
            // InitDelegateCache();
            
            #if DEBUG
            _contextDebugInfo = $"c_{system.GetType().Name}";
            #endif
        }

        /// <summary>
        /// Initializes a context that is not necessarily tied to any system.
        /// </summary>
        internal Context(World world, ContextAccessorFilter filter, Span<int> components) : base(world)
        {
            _targetComponentsIndex =
                new Dictionary<ContextAccessorFilter, ImmutableArray<int>> { { filter, components.ToImmutableArray() } }.ToImmutableDictionary();
            _componentsOperationKind = ImmutableDictionary<ContextAccessorKind, ImmutableHashSet<int>>.Empty;

            _id = CalculateId();
            // InitDelegateCache();
        }

        // private Action<Entity, int> _cachedOnEntityComponentAdded;
        // private Action<Entity, int, bool> _cachedOnEntityComponentRemoved;
        // private Action<Entity, int> _cachedOnComponentAddedForEntityInContext;
        // private Action<Entity, int, bool> _cachedOnComponentBeforeRemovingForEntityInContext;
        // private Action<Entity, int, bool> _cachedOnComponentRemovedForEntityInContext;
        // private Action<Entity, int> _cachedOnComponentBeforeModifyingForEntityInContext;
        // private Action<Entity, int> _cachedOnComponentModifiedForEntityInContext;
        // private Action<Entity, int, IMessage> _cachedOnMessageSentForEntityInContext;
        // private Action<Entity> _cachedOnEntityActivated;
        // private Action<Entity> _cachedOnEntityDeactivated;
        //
        // private void InitDelegateCache()
        // {
        //     _cachedOnEntityComponentAdded = OnEntityComponentAdded;
        //     _cachedOnEntityComponentRemoved = OnEntityComponentRemoved;
        //
        //     _cachedOnComponentBeforeRemovingForEntityInContext = OnComponentBeforeRemovingForEntityInContext;
        //     _cachedOnComponentRemovedForEntityInContext = OnComponentRemovedForEntityInContext;
        //     _cachedOnComponentBeforeModifyingForEntityInContext = OnComponentBeforeModifyingForEntityInContext;
        //     _cachedOnComponentModifiedForEntityInContext = OnComponentModifiedForEntityInContext;
        //     
        //     _cachedOnMessageSentForEntityInContext = OnMessageSentForEntityInContext;
        //     _cachedOnEntityActivated = OnEntityActivated;
        //     _cachedOnEntityDeactivated = OnEntityDeactivated;
        //     
        //     _cachedOnComponentAddedForEntityInContext = OnComponentAddedForEntityInContext;
        // }

        /// <summary>
        /// This gets the context unique identifier.
        /// This is important to get it right since it will be reused across different systems.
        /// It assumes that we won't get more than 1000 components declared. If this changes (oh! hello!), maybe we should
        /// reconsider this code.
        /// </summary>
        private int CalculateId()
        {
            List<int> allComponents = new();

            // Dictionaries by themselves do not guarantee any ordering.
            var orderedComponentsFilter = _targetComponentsIndex.OrderBy(kv => kv.Key);
            foreach (var (filter, collection) in orderedComponentsFilter)
            {
                // Add the filter identifier. This is negative so the hash can uniquely identify them.
                allComponents.Add(-(int)filter);

                // Sum one to the value so we are not ignoring 0-indexed components.
                allComponents.AddRange(collection.Sort().Select(c => c + 1));
            }

            return allComponents.GetHashCodeImpl();
        }

        /// <summary>
        /// Perf: Calculate the context id. This is used to calculate whether it is necessary to create a new context
        /// if there is already an existing one.
        /// </summary>
        internal static int CalculateContextId(ContextAccessorFilter filter, Span<int> components)
        {
            Span<int> allComponents = stackalloc int[components.Length + 1];

            // Add the filter identifier. This is negative so the hash can uniquely identify them.
            allComponents[0] = -(int)filter;
            components.Sort();

            for (int i = 0; i < components.Length; ++i)
            {
                // Sum one to the value so we are not ignoring 0-indexed components.
                allComponents[i + 1] = components[i] + 1;
            }

            return allComponents.GetHashCodeImpl();
        }

        private ImmutableArray<(FilterAttribute, ImmutableArray<int>)> CreateFilterList(ISystem system)
        {
            Func<Type, ImmutableArray<int>> lookup = t =>
            {
                if (!t.IsInterface)
                    return new [] { Lookup.Id(t) }.ToImmutableArray();
                return World.ComponentsLookup.GetAllComponentIndexUnderInterface(t).Select( kv => kv.Item2 ).ToImmutableArray();
            };

            var builder = ImmutableArray.CreateBuilder<(FilterAttribute, ImmutableArray<int>)>();

            // First, grab all the filters of the system.
            FilterAttribute[] filters = (FilterAttribute[])system
                .GetType().GetCustomAttributes(typeof(FilterAttribute), inherit: true);

            // Now, for each filter, populate our set of files.
            foreach (var filter in filters)
            {
                builder.Add((filter, filter.Types.SelectMany(t => lookup(t)).ToImmutableArray()));
            }

            return builder.ToImmutableArray();
        }

        /// <summary>
        /// Create a list of which components we will be watching for when adding a new entity according to a
        /// <see cref="ContextAccessorFilter"/>.
        /// </summary>
        private ImmutableDictionary<ContextAccessorFilter, ImmutableArray<int>> CreateTargetComponents(
            ImmutableArray<(FilterAttribute, ImmutableArray<int>)> filters)
        {
            var builder = ImmutableDictionary.CreateBuilder<ContextAccessorFilter, ImmutableArray<int>>();

            foreach (var (filter, targets) in filters)
            {
                // Keep track of empty contexts.
                if (filter.Filter is ContextAccessorFilter.None)
                {
                    builder[filter.Filter] = ImmutableArray<int>.Empty;
                    continue;
                }

                if (targets.IsDefaultOrEmpty)
                {
                    // No-op, this is so we can watch for the accessor kind.
                    continue;
                }

                // We might have already added components for the filter for another particular kind of target,
                // so check if it has already been added in a previous filter.
                if (!builder.ContainsKey(filter.Filter))
                {
                    builder[filter.Filter] = targets;
                }
                else
                {
                    builder[filter.Filter] = builder[filter.Filter].Union(targets).ToImmutableArray();
                }
            }

            return builder.ToImmutableDictionary();
        }

        private ImmutableDictionary<ContextAccessorKind, ImmutableHashSet<int>> CreateAccessorKindComponents(
            ImmutableArray<(FilterAttribute, ImmutableArray<int>)> filters)
        {
            var builder = ImmutableDictionary.CreateBuilder<ContextAccessorKind, ImmutableHashSet<int>>();

            // Initialize both fields as empty, if there is none.
            builder[ContextAccessorKind.Read] = ImmutableHashSet<int>.Empty;
            builder[ContextAccessorKind.Write] = ImmutableHashSet<int>.Empty;

            foreach (var (filter, targets) in filters)
            {
                if (targets.IsDefaultOrEmpty || filter.Filter is ContextAccessorFilter.NoneOf)
                {
                    // No-op, this will never be consumed by the system.
                    continue;
                }

                ContextAccessorKind kind = filter.Kind;
                if (kind.HasFlag(ContextAccessorKind.Write))
                {
                    // If this is a read/write, just cache it as a write operation.
                    // Not sure if we can do anything with the information of a read...?
                    kind = ContextAccessorKind.Write;
                }

                // We might have already added components for the filter for another particular kind of target,
                // so check if it has already been added in a previous filter.
                if (builder[kind].IsEmpty)
                {
                    builder[kind] = targets.ToImmutableHashSet();
                }
                else
                {
                    builder[kind] = builder[kind].Union(targets).ToImmutableHashSet();
                }
            }

            return builder.ToImmutableDictionary();
        }

        /// <summary>
        /// Filter an entity for the first time in this context.
        /// This is called when the entity is first created an set into the world.
        /// </summary>
        internal override void FilterEntity(Entity entity)
        {
            if (IsNoFilter)
            {
                // No entities are caught by this context.
                return;
            }

            entity.OnComponentAdded += OnEntityComponentAdded;
            entity.OnComponentRemoved += OnEntityComponentRemoved;
            // entity.OnComponentAdded += _cachedOnEntityComponentAdded;
            // entity.OnComponentRemoved += _cachedOnEntityComponentRemoved;
            
            if (DoesEntityMatch(entity))
            {
                entity.OnComponentBeforeRemoving += OnComponentBeforeRemovingForEntityInContext;
                entity.OnComponentRemoved += OnComponentRemovedForEntityInContext;
                entity.OnComponentBeforeModifying += OnComponentBeforeModifyingForEntityInContext;
                entity.OnComponentModified += OnComponentModifiedForEntityInContext;
                
                entity.OnMessage += OnMessageSentForEntityInContext;
                
                entity.OnEntityActivated += OnEntityActivated;
                entity.OnEntityDeactivated += OnEntityDeactivated;

                // entity.OnComponentBeforeRemoving += _cachedOnComponentBeforeRemovingForEntityInContext;
                // entity.OnComponentRemoved += _cachedOnComponentRemovedForEntityInContext;
                // entity.OnComponentBeforeModifying += _cachedOnComponentBeforeModifyingForEntityInContext;
                // entity.OnComponentModified += _cachedOnComponentModifiedForEntityInContext;
                //
                // entity.OnMessage += _cachedOnMessageSentForEntityInContext;
                //
                // entity.OnEntityActivated += _cachedOnEntityActivated;
                // entity.OnEntityDeactivated += _cachedOnEntityDeactivated;
                
                if (OnComponentAddedForEntityInContext is not null)
                {
                    if (!entity.IsDeactivated)
                    {
                        // TODO: Optimize this? We must notify all the reactive systems
                        // that the entity has been added.
                        foreach (int c in entity.ComponentsIndices)
                        {
                            OnComponentAddedForEntityInContext.Invoke(entity, c);
                        }
                    }

                    entity.OnComponentAdded += OnComponentAddedForEntityInContext;
                    // entity.OnComponentAdded += _cachedOnComponentAddedForEntityInContext;
                }

                if (!entity.IsDeactivated)
                {
                    _entities[entity.EntityId] = entity;
                    _cachedEntities = null;
                }
            }
        }

        /// <summary>
        /// Returns whether the entity matches the filter for this context.
        /// </summary>
        internal bool DoesEntityMatch(Entity e)
        {
            if (_targetComponentsIndex.ContainsKey(ContextAccessorFilter.NoneOf))
            {
                foreach (var c in _targetComponentsIndex[ContextAccessorFilter.NoneOf])
                {
                    if (e.HasComponentOrMessage(c))
                    {
                        return false;
                    }
                }
            }

            if (_targetComponentsIndex.ContainsKey(ContextAccessorFilter.AllOf))
            {
                foreach (var c in _targetComponentsIndex[ContextAccessorFilter.AllOf])
                {
                    if (!e.HasComponentOrMessage(c))
                    {
                        return false;
                    }
                }
            }

            if (_targetComponentsIndex.ContainsKey(ContextAccessorFilter.AnyOf))
            {
                foreach (var c in _targetComponentsIndex[ContextAccessorFilter.AnyOf])
                {
                    if (e.HasComponentOrMessage(c))
                    {
                        return true;
                    }
                }

                return false;
            }

            return true;
        }

        internal override void OnEntityComponentAdded(Entity e, int index)
        {
            if (e.IsDestroyed)
            {
                return;
            }

            OnEntityModified(e, index);
        }

        internal override void OnEntityComponentRemoved(Entity e, int index, bool causedByDestroy)
        {
            if (e.IsDestroyed)
            {
                if (!IsWatchingEntity(e.EntityId))
                {
                    return;
                }

                if (!DoesEntityMatch(e))
                {
                    // The entity was just destroyed, don't bother filtering it.
                    // Destroy it immediately.
                    StopWatchingEntity(e, index, causedByDestroy: true);
                }

                return;
            }

            OnEntityModified(e, index);
        }

        internal override void OnEntityComponentBeforeRemove(Entity e, int index, bool causedByDestroy)
        {
            
        }

        internal void OnEntityActivated(Entity e)
        {
            if (!_entities.ContainsKey(e.EntityId))
            {
                _entities.Add(e.EntityId, e);
                _cachedEntities = null;

                OnActivateEntityInContext?.Invoke(e);

                _deactivatedEntities.Remove(e.EntityId);
            }
        }

        internal void OnEntityDeactivated(Entity e)
        {
            if (_entities.ContainsKey(e.EntityId))
            {
                _entities.Remove(e.EntityId);
                _cachedEntities = null;

                OnDeactivateEntityInContext?.Invoke(e);

                _deactivatedEntities.Add(e.EntityId);
            }
        }

        private void OnEntityModified(Entity e, int index)
        {
            bool isFiltered = DoesEntityMatch(e);
            bool isWatchingEntity = IsWatchingEntity(e.EntityId);

            if (!isWatchingEntity && isFiltered)
            {
                StartWatchingEntity(e, index);
            }
            else if (isWatchingEntity && !isFiltered)
            {
                StopWatchingEntity(e, index, causedByDestroy: false);
            }
        }

        private bool IsWatchingEntity(int entityId) =>
            _entities.ContainsKey(entityId) || _deactivatedEntities.Contains(entityId);

        /// <summary>
        /// Tries to get a unique entity, if none is available, returns null
        /// </summary>
        /// <returns></returns>
        public Entity? TryGetUniqueEntity()
        {
            if (_entities.Count == 1)
            {
                return _entities.First().Value;
            }
            else
            {
                return null;
            }
        }

        private void StartWatchingEntity(Entity e, int index)
        {
            // Add any watchers from now on.
            e.OnComponentAdded += OnComponentAddedForEntityInContext;
            e.OnComponentRemoved += OnComponentRemovedForEntityInContext;
            e.OnComponentBeforeRemoving += OnComponentBeforeRemovingForEntityInContext;
            e.OnComponentModified += OnComponentModifiedForEntityInContext;
            e.OnComponentBeforeModifying += OnComponentBeforeModifyingForEntityInContext;
            
            e.OnMessage += OnMessageSentForEntityInContext;
            
            e.OnEntityActivated += OnEntityActivated;
            e.OnEntityDeactivated += OnEntityDeactivated;
            // e.OnComponentAdded += _cachedOnComponentAddedForEntityInContext;
            // e.OnComponentRemoved += _cachedOnComponentRemovedForEntityInContext;
            // e.OnComponentBeforeRemoving += _cachedOnComponentBeforeRemovingForEntityInContext;
            // e.OnComponentModified += _cachedOnComponentModifiedForEntityInContext;
            // e.OnComponentBeforeModifying += _cachedOnComponentBeforeModifyingForEntityInContext;
            //
            // e.OnMessage += _cachedOnMessageSentForEntityInContext;
            //
            // e.OnEntityActivated += _cachedOnEntityActivated;
            // e.OnEntityDeactivated += _cachedOnEntityDeactivated;
            
            if (!e.IsDeactivated)
            {
                // Notify immediately of the new added component.
                OnComponentAddedForEntityInContext?.Invoke(e, index);

                // This checks whether we are adding a component on an unique component.
                if (World.DIAGNOSTICS_MODE && World.IsUniqueContext(Id))
                {
                    Debug.WriteLineIf(_entities.Count != 0, $"Adding unique component of id {index} twice.");
                }

                _entities.Add(e.EntityId, e);
                _cachedEntities = null;
            }
            else
            {
                _deactivatedEntities.Add(e.EntityId);
            }
        }

        private void StopWatchingEntity(Entity e, int index, bool causedByDestroy)
        {
            // Remove any watchers.
            e.OnComponentAdded -= OnComponentAddedForEntityInContext;
            e.OnComponentRemoved -= OnComponentRemovedForEntityInContext;
            e.OnComponentBeforeRemoving -= OnComponentBeforeRemovingForEntityInContext;
            e.OnComponentModified -= OnComponentModifiedForEntityInContext;
            e.OnComponentBeforeModifying -= OnComponentBeforeModifyingForEntityInContext;
            
            e.OnMessage -= OnMessageSentForEntityInContext;
            
            e.OnEntityActivated -= OnEntityActivated;
            e.OnEntityDeactivated -= OnEntityDeactivated;
            
            // e.OnComponentAdded -= _cachedOnComponentAddedForEntityInContext;
            // e.OnComponentRemoved -= _cachedOnComponentRemovedForEntityInContext;
            // e.OnComponentBeforeRemoving -= _cachedOnComponentBeforeRemovingForEntityInContext;
            // e.OnComponentModified -= _cachedOnComponentModifiedForEntityInContext;
            // e.OnComponentBeforeModifying -= _cachedOnComponentBeforeModifyingForEntityInContext;
            //
            // e.OnMessage -= _cachedOnMessageSentForEntityInContext;
            //
            // e.OnEntityActivated -= _cachedOnEntityActivated;
            // e.OnEntityDeactivated -= _cachedOnEntityDeactivated;
            
            if (!e.IsDeactivated)
            {
                // Notify immediately of the removed component.
                OnComponentRemovedForEntityInContext?.Invoke(e, index, causedByDestroy);
            }
            else
            {
                Debug.Assert(!_entities.ContainsKey(e.EntityId),
                    "Why is a deactivate entity is in the collection?");

                _deactivatedEntities.Remove(e.EntityId);
            }

            _entities.Remove(e.EntityId);
            _cachedEntities = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            OnComponentAddedForEntityInContext = null;
            OnComponentModifiedForEntityInContext = null;
            OnComponentRemovedForEntityInContext = null;

            OnActivateEntityInContext = null;
            OnDeactivateEntityInContext = null;
            OnMessageSentForEntityInContext = null;

            // _cachedOnEntityComponentAdded = null;
            // _cachedOnEntityComponentRemoved = null;
            // _cachedOnComponentAddedForEntityInContext = null;
            // _cachedOnComponentBeforeRemovingForEntityInContext = null;
            // _cachedOnComponentRemovedForEntityInContext = null;
            // _cachedOnComponentBeforeModifyingForEntityInContext = null;
            // _cachedOnComponentModifiedForEntityInContext = null;
            // _cachedOnMessageSentForEntityInContext = null;
            // _cachedOnEntityActivated = null;
            // _cachedOnEntityDeactivated = null;

            _entities.Clear();
        }
    }
}