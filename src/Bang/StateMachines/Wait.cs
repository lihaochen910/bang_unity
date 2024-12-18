using System;
using System.Collections.Generic;
using Bang.Components;
using Bang.Entities;

namespace Bang.StateMachines
{
    /// <summary>
    /// A message fired to communicate the current state of the state machine.
    /// </summary>
    public record Wait
    {
        /// <summary>
        /// When should the state machine be called again.
        /// </summary>
        public readonly WaitKind Kind;

        /// <summary>
        /// Integer value, if kind is <see cref="WaitKind.Ms"/> or <see cref="WaitKind.Frames"/>.
        /// </summary>
        public int? Value;

        /// <summary>
        /// Used for <see cref="WaitKind.Message"/>.
        /// </summary>
        public Type? Component;

        /// <summary>
        /// Used for <see cref="WaitKind.Message"/> when waiting on another entity that is not the owner of the state machine.
        /// </summary>
        public Entity? Target;

        /// <summary>
        /// Used for <see cref="WaitKind.Routine"/>.
        /// </summary>
        public IEnumerator<Wait>? Routine;

        /// <summary>
        /// No longer execute the state machine.
        /// </summary>
        public static readonly Wait Stop = new();
        
        /// <summary>
        /// Wait until the next frame.
        /// </summary>
        public static readonly Wait NextFrame = new(WaitKind.Frames, 0);

        /// <summary>
        /// Wait for <paramref name="ms"/>.
        /// </summary>
        public static Wait ForMs(int ms) => FetchWaitForMs(ms);

        /// <summary>
        /// Wait for <paramref name="seconds"/>.
        /// </summary>
        public static Wait ForSeconds(float seconds) => FetchWaitForSeconds(seconds);

        /// <summary>
        /// Wait until message of type <typeparamref name="T"/> is fired.
        /// </summary>
        public static Wait ForMessage<T>() where T : IMessage => FetchWaitForMessage(typeof(T));

        /// <summary>
        /// Wait until message of type <typeparamref name="T"/> is fired from <paramref name="target"/>.
        /// </summary>
        public static Wait ForMessage<T>(Entity target) where T : IMessage => new(typeof(T), target);

        /// <summary>
        /// Wait until <paramref name="frames"/> have occurred.
        /// </summary>
        public static Wait ForFrames(int frames) => FetchWaitForFrames(frames);

        /// <summary>
        /// Wait until the next frame.
        /// </summary>
        // public static Wait NextFrame => new(WaitKind.Frames, 0);

        /// <summary>
        /// Wait until <paramref name="routine"/> finishes.
        /// </summary>
        public static Wait ForRoutine(IEnumerator<Wait> routine) => new(routine);

        private Wait() => Kind = WaitKind.Stop;
        private Wait(WaitKind kind, int value) => (Kind, Value) = (kind, value);
        private Wait(Type messageType) => (Kind, Component) = (WaitKind.Message, messageType);
        private Wait(Type messageType, Entity target) => (Kind, Component, Target) = (WaitKind.Message, messageType, target);
        private Wait(IEnumerator<Wait> routine) => (Kind, Routine) = (WaitKind.Routine, routine);


        #region Cached Wait

        private static Dictionary<Type, Wait> CachedWaitForMessage = new();
        private static Dictionary<int, Wait> CachedWaitForMs = new();
        private static Dictionary<int, Wait> CachedWaitForFrames = new();


        private static Wait FetchWaitForMessage(Type messageType)
        {
            if (!CachedWaitForMessage.ContainsKey(messageType) )
            {
                CachedWaitForMessage.Add(messageType, new Wait(messageType));
            }

            return CachedWaitForMessage[messageType];
        }
        
        private static Wait FetchWaitForMs(int ms)
        {
            if (!CachedWaitForMs.ContainsKey(ms))
            {
                CachedWaitForMs.Add(ms, new Wait(WaitKind.Ms, ms));
            }

            return CachedWaitForMs[ms];
        }
        
        private static Wait FetchWaitForSeconds(float seconds)
        {
            return FetchWaitForMs((int)(seconds * 1000));
        }
        
        private static Wait FetchWaitForFrames(int frames)
        {
            if (!CachedWaitForFrames.ContainsKey(frames))
            {
                CachedWaitForFrames.Add(frames, new Wait(WaitKind.Frames, frames));
            }

            return CachedWaitForFrames[frames];
        }

        #endregion


    }
}