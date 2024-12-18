using System;
using System.Collections.Generic;
using Bang.Systems;
#if NETSTANDARD
using Bang.Util;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Bang
{
    /// <summary>
    /// Reflection helper utility to access the world.
    /// </summary>
    public partial class World
    {
        /// <summary>
        /// Cache the lookup implementation for this game.
        /// </summary>
#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        private static Type? _cachedLookupImplementation = null;

        /// <summary>
        /// Look for an implementation for the lookup table of components.
        /// </summary>
#if NET6_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL2026:System.Reflection.Assembly.GetTypes() can break functionality when trimming application code. Types might be removed.", Justification = "Target assemblies scanned are not trimmed.")]
        [UnconditionalSuppressMessage("AOT", "IL2074:Public constructor might have been removed when scanning the candidate type.", Justification = "Target assemblies scanned are not trimmed.")]
#endif
        public static ComponentsLookup FindLookupImplementation()
        {
            if (_cachedLookupImplementation is null)
            {
                Type lookup = typeof(ComponentsLookup);

                // var isLookup = (Type t) => !t.IsInterface && !t.IsAbstract && lookup.IsAssignableFrom(t);
                bool isLookup(Type t)
                {
                    return !t.IsInterface && !t.IsAbstract && lookup.IsAssignableFrom(t);
                }

                // We might find more than one lookup implementation, when inheriting projects with a generator.
                List<Type> candidateLookupImplementations = new List<Type>();

                Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly s in allAssemblies)
                {
                    Type[] types;
                    try
                    {
                        types = s.GetTypes();
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        continue;
                    }
                    foreach (Type t in types)
                    {
                        if (isLookup(t))
                        {
                            candidateLookupImplementations.Add(t);
                        }
                    }
                }
                
                #if NET6_0_OR_GREATER
                _cachedLookupImplementation = candidateLookupImplementations.MaxBy(NumberOfParentClasses);
                #else
                // UNITY specific
                _cachedLookupImplementation = candidateLookupImplementations.FirstOrDefault(t => t.Name.Equals("Assembly_CSharpComponentsLookup"));
                #endif
            }

            if (_cachedLookupImplementation is not null)
            {
                return (ComponentsLookup)Activator.CreateInstance(_cachedLookupImplementation)!;
            }

            throw new InvalidOperationException("A generator is required to be run before running the game!");

            static int NumberOfParentClasses(Type type)
                => type.BaseType is null ? 0 : 1 + NumberOfParentClasses(type.BaseType);
        }

        /// <summary>
        /// Returns whether a system is eligible to be paused.
        /// This means that:
        ///   - it is an update system;
        ///   - it does not have the DoNotPauseAttribute.
        /// </summary>
        private static bool IsPauseSystem(ISystem s)
        {
            if (Attribute.IsDefined(s.GetType(), typeof(IncludeOnPauseAttribute)))
            {
                return true;
            }

            if (s is IRenderSystem)
            {
                // do not pause render systems.
                return false;
            }

            if (s is not IFixedUpdateSystem && s is not IUpdateSystem && s is not ILateUpdateSystem)
            {
                // only pause update systems.
                return false;
            }

            if (Attribute.IsDefined(s.GetType(), typeof(DoNotPauseAttribute)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether a system is only expect to play when the game is paused.
        /// This is useful when defining systems that still track the game stack, even if paused.
        /// </summary>
        private static bool IsPlayOnPauseSystem(ISystem s)
        {
            return Attribute.IsDefined(s.GetType(), typeof(OnPauseAttribute));
        }
    }
}