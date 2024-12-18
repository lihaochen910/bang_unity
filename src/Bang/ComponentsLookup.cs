using System;
using System.Collections.Generic;
using Bang.Components;
using Bang.Entities;
using Bang.Interactions;
using Bang.StateMachines;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Bang
{
    /// <summary>
    /// Implemented by generators in order to provide a mapping of all the types to their respective id.
    /// </summary>
    public abstract class ComponentsLookup
    {
        /// <summary>
        /// Tracks the last id this particular implementation is tracking plus one.
        /// </summary>
        public const int NextLookupId = 3;

        /// <summary>
        /// Maps all the components to their unique id.
        /// </summary>
        protected ImmutableDictionary<Type, int> ComponentsIndex { get; init; } = new Dictionary<Type, int>
        {
            { typeof(IStateMachineComponent), BangComponentTypes.StateMachine },
            { typeof(IInteractiveComponent), BangComponentTypes.Interactive },
            { typeof(ITransformComponent), BangComponentTypes.Transform }
        }.ToImmutableDictionary();

        /// <summary>
        /// Maps all the messages to their unique id.
        /// </summary>
        protected ImmutableDictionary<Type, int> MessagesIndex { get; init; } = new Dictionary<Type, int>().ToImmutableDictionary();

        /// <summary>
        /// List of all the unique id of the components that inherit from <see cref="IParentRelativeComponent"/>.
        /// </summary>
        public ImmutableHashSet<int> RelativeComponents { get; protected init; } =
            ImmutableHashSet.Create(BangComponentTypes.Transform);

        /// <summary>
        /// Tracks components and messages without a generator. This query will have a lower performance.
        /// </summary>
        private readonly Dictionary<Type, int> _untrackedIndices = new();

        /// <summary>
        /// Tracks relative components without a generator. This query will have a lower performance.
        /// </summary>
        private readonly HashSet<int> _untrackedRelativeComponents = new();

        private int? _nextUntrackedIndex;
        
        #if DEBUG
        private Lazy<ImmutableDictionary<int, Type>> _componentsIndexForReverseLookup;
        private Lazy<ImmutableDictionary<int, Type>> _messagesIndexForReverseLookup;
        
        private ImmutableDictionary<int, Type> CreateReverseLookupComponentsIndex() {
            var dict = new Dictionary<int, Type>();
            foreach (var keyValuePair in ComponentsIndex)
            {
                dict[keyValuePair.Value] = keyValuePair.Key;
            }
            return dict.ToImmutableDictionary();
        }
        
        private ImmutableDictionary<int, Type> CreateReverseLookupMessagesIndex() {
            var dict = new Dictionary<int, Type>();
            foreach (var keyValuePair in MessagesIndex)
            {
                dict[keyValuePair.Value] = keyValuePair.Key;
            }
            return dict.ToImmutableDictionary();
        }
        #endif
        
        /// <summary>
        /// Get the id for <paramref name="t"/> component type.
        /// </summary>
        /// <param name="t">Type.</param>
        public int Id(Type t)
        {
            Debug.Assert(typeof(IComponent).IsAssignableFrom(t) || typeof(IMessage).IsAssignableFrom(t),
                "Why are we receiving a type that is not an IComponent?");

            int index;

            if (typeof(IMessage).IsAssignableFrom(t) && MessagesIndex.TryGetValue(t, out index))
            {
                return index;
            }

            if (ComponentsIndex.TryGetValue(t, out index))
            {
                return index;
            }

            if (_untrackedIndices.TryGetValue(t, out index))
            {
                return index;
            }

            return AddUntrackedIndexForComponentOrMessage(t);
        }
        
        #if DEBUG
        public Type IdType(int componentId)
        {
            var retry = false;
Retry:
            if (_componentsIndexForReverseLookup is null)
            {
                _componentsIndexForReverseLookup = new Lazy<ImmutableDictionary<int, Type>>(CreateReverseLookupComponentsIndex);
            }
            
            if (_messagesIndexForReverseLookup is null)
            {
                _messagesIndexForReverseLookup = new Lazy<ImmutableDictionary<int, Type>>(CreateReverseLookupMessagesIndex);
            }
            
            if (_messagesIndexForReverseLookup.Value.TryGetValue(componentId, out var t))
            {
                return t;
            }

            if (_componentsIndexForReverseLookup.Value.TryGetValue(componentId, out t))
            {
                return t;
            }

            if (!retry)
            {
                _componentsIndexForReverseLookup = null;
                _messagesIndexForReverseLookup = null;
                retry = true;
                goto Retry;
            }

            return null;
        }
        #endif

        /// <summary>
        /// Returns whether a <paramref name="id"/> is relative to its parent.
        /// </summary>
        public bool IsRelative(int id)
        {
            return RelativeComponents.Contains(id);
        }

        internal int TotalIndices => ComponentsIndex.Count + MessagesIndex.Count + _untrackedIndices.Count;

        private int AddUntrackedIndexForComponentOrMessage(Type t)
        {
            int? id = null;

            if (!t.IsInterface)
            {
                if (typeof(IStateMachineComponent).IsAssignableFrom(t))
                {
                    id = Id(typeof(IStateMachineComponent));
                }
                else if (typeof(IInteractiveComponent).IsAssignableFrom(t))
                {
                    id = Id(typeof(IInteractiveComponent));
                }
            }
            else
            {
                Debug.Assert(t != typeof(IComponent), "Why are we doing a lookup for an IComponent itself?");
            }

            if (id is null)
            {
                _nextUntrackedIndex ??= ComponentsIndex.Count + MessagesIndex.Count;

                id = _nextUntrackedIndex++;
            }

            _untrackedIndices.Add(t, id.Value);

            if (typeof(IParentRelativeComponent).IsAssignableFrom(t))
            {
                _untrackedRelativeComponents.Add(id.Value);
            }

            return id.Value;
        }

        public IEnumerable<(Type, int)> GetAllComponentIndexUnderInterface(Type t)
        {
            foreach (var kv in ComponentsIndex)
            {
                if (t.IsAssignableFrom(kv.Key) && t != kv.Key)
                {
                    yield return (kv.Key, kv.Value);
                }
            }
        }
    }
}