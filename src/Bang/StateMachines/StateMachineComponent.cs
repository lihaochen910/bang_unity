using System;
using Bang.Components;
using Bang.Entities;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Bang.StateMachines
{
    /// <summary>
    /// Implements a state machine component.
    /// </summary>
    public readonly struct StateMachineComponent<T> : IStateMachineComponent, IModifiableComponent where T : StateMachine, new()
    {
        /// <summary>
        /// This will fire a notification whenever the state changes.
        /// </summary>
        public string State => _routine.Name;

        [Serialize]
        private readonly T _routine;

#if NET6_0_OR_GREATER
        /// <summary>
        /// Creates a new <see cref="StateMachineComponent{T}"/>.
        /// </summary>
        public StateMachineComponent() => _routine = new();
#endif

        /// <summary>
        /// Default constructor initialize a brand new routine.
        /// </summary>
#if NET6_0_OR_GREATER
        [JsonConstructor]
#endif
        public StateMachineComponent(T routine) => _routine = routine;

        /// <summary>
        /// Initialize the state machine with the world knowledge. Called before any tick.
        /// </summary>
        public void Initialize(World world, Entity e)
	{
            // if (_routine is null) {
            //     var fieldInfo = GetType().GetField("_routine", BindingFlags.Instance | BindingFlags.NonPublic);
            //     fieldInfo.SetValue(this, new T());
            //     // var myObject = this;
            //     // fieldInfo.SetValueDirect(__makeref(myObject), new T());
            // }
            _routine.Initialize(world, e);
        }

        /// <summary>
        /// Tick a yield operation in the state machine. The next tick will be called according to the returned <see cref="WaitKind"/>.
        /// </summary>
        public bool Tick(float seconds) => _routine.Tick(seconds * 1000);

        /// <summary>
        /// Called right before the component gets destroyed.
        /// </summary>
        public void OnDestroyed() => _routine.OnDestroyed();

        /// <summary>
        /// Subscribe for notifications on this component.
        /// </summary>
        public void Subscribe(Action notification) => _routine.Subscribe(notification);

        /// <summary>
        /// Stop listening to notifications on this component.
        /// </summary>
        public void Unsubscribe(Action notification) => _routine.Unsubscribe(notification);
    }
}