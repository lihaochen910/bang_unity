﻿using System;
using System.Collections.Generic;
using Bang.Components;
using Bang.Contexts;
using Bang.Diagnostics;
using Bang.Systems;
using System.Diagnostics;

namespace Bang
{

// This file contains the code responsible for debug information used when creating a world.
public partial class World
{
    private bool _initializedDiagnostics = false;

    /// <summary>
    /// This is the stopwatch used per systems when monitoring performance. Only used if <see cref="DIAGNOSTICS_MODE"/> is set.
    /// </summary>
    protected readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// This is the stopwatch used on all systems when monitoring performance. Only used if <see cref="DIAGNOSTICS_MODE"/> is set.
    /// </summary>
    protected readonly Stopwatch _overallStopwatch = Stopwatch.StartNew();
    
    public readonly Dictionary<int, SmoothCounter> EarlyStartCounters = new();

    /// <summary>
    /// This has the duration of each start system (id) to its corresponding time (in ms).
    /// See <see cref="IdToSystem"/> on how to fetch the actual system.
    /// </summary>
    public readonly Dictionary<int, SmoothCounter> StartCounters = new();

    /// <summary>
    /// This has the duration of each update system (id) to its corresponding time (in ms).
    /// See <see cref="IdToSystem"/> on how to fetch the actual system.
    /// </summary>
    public readonly Dictionary<int, SmoothCounter> UpdateCounters = new();
    
    public readonly Dictionary<int, SmoothCounter> LateUpdateCounters = new();

    /// <summary>
    /// This has the duration of each fixed update system (id) to its corresponding time (in ms).
    /// See <see cref="IdToSystem"/> on how to fetch the actual system.
    /// </summary>
    public readonly Dictionary<int, SmoothCounter> FixedUpdateCounters = new();

    /// <summary>
    /// This has the duration of each reactive system (id) to its corresponding time (in ms).
    /// See <see cref="IdToSystem"/> on how to fetch the actual system.
    /// </summary>
    public readonly Dictionary<int, SmoothCounter> ReactiveCounters = new();

    /// <summary>
    /// Initialize the performance counters according to the systems present in the world.
    /// </summary>
    protected void InitializeDiagnosticsCounters()
    {
        Debug.Assert(DIAGNOSTICS_MODE,
            "Why are we initializing diagnostics out of diagnostic mode?");

        if (_initializedDiagnostics)
        {
            // Already initialized.
            return;
        }

        _initializedDiagnostics = true;

        foreach (var (systemId, system) in IdToSystem)
        {
            if (system is IStartupSystem)
            {
                EarlyStartCounters[systemId] = new();
            }
            
            if (system is IStartupSystem)
            {
                StartCounters[systemId] = new();
            }

            if (system is IUpdateSystem)
            {
                UpdateCounters[systemId] = new();
            }
            
            if (system is ILateUpdateSystem)
            {
                LateUpdateCounters[systemId] = new();
            }

            if (system is IFixedUpdateSystem)
            {
                FixedUpdateCounters[systemId] = new();
            }

            if (system is IReactiveSystem)
            {
                ReactiveCounters[systemId] = new();
            }

            InitializeDiagnosticsForSystem(systemId, system);
        }
    }

    private void UpdateDiagnosticsOnDeactivateSystem(int id)
    {
        if (EarlyStartCounters.TryGetValue(id, out var value)) value.Clear();
        if (StartCounters.TryGetValue(id, out value)) value.Clear();
        if (UpdateCounters.TryGetValue(id, out value)) value.Clear();
        if (LateUpdateCounters.TryGetValue(id, out value)) value.Clear();
        if (FixedUpdateCounters.TryGetValue(id, out value)) value.Clear();
        if (ReactiveCounters.TryGetValue(id, out value)) value.Clear();

        ClearDiagnosticsCountersForSystem(id);
    }

    /// <summary>
    /// Implemented by custom world in order to clear diagnostic information about the world.
    /// </summary>
    /// <param name="systemId"></param>
    protected virtual void ClearDiagnosticsCountersForSystem(int systemId) { }

    /// <summary>
    /// Implemented by custom world in order to express diagnostic information about the world.
    /// </summary>
    protected virtual void InitializeDiagnosticsForSystem(int systemId, ISystem system) { }

    private static void CheckSystemsRequirements(IList<(ISystem system, bool isActive)> systems)
    {
        // First, list all the systems in the world according to their type, and map
        // to the order in which they appear.
        Dictionary<Type, int> systemTypes = new();
        for (int i = 0; i < systems.Count; i++)
        {
            Type t = systems[i].system.GetType();

            Assert.Verify(!systemTypes.ContainsKey(t),
                $"Why are we adding {t.Name} twice in the world!?");

            systemTypes.Add(t, i);
        }

        foreach (var (t, index) in systemTypes)
        {
            if (Attribute.GetCustomAttribute(t, typeof(RequiresAttribute)) is RequiresAttribute requires)
            {
                foreach (Type requiredSystem in requires.Types)
                {
                    Assert.Verify(typeof(ISystem).IsAssignableFrom(requiredSystem),
                        "Why does the system requires a type that is not a system?");

                    if (systemTypes.TryGetValue(requiredSystem, out int order))
                    {
                        Assert.Verify(index > order,
                            $"Required system: {requiredSystem.Name} does not precede: {t.Name}.");
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Missing {requiredSystem.Name} required by {t.Name} in the world!");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check whether a context is unique.
    /// </summary>
    /// <param name="id">Value of <see cref="Context.Id"/>.</param>
    internal bool IsUniqueContext(int id)
    {
        return _cacheUniqueContexts.ContainsKey(id);
    }
    
    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnAddedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnAddedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnRemovedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnRemovedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnModifiedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnModifiedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnActivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnActivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnDeactivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnDeactivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnSystemActivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnSystemActivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnSystemDeactivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnSystemDeactivatedCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnBeforeRemovingCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnBeforeRemovingCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnBeforeModifyingCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnBeforeModifyingCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeOnMessageCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterOnMessageCall(int systemId) { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsBeforeNotifyReactiveSystemsCall() { }

    [Conditional("DEBUG")]
    protected virtual void DiagnosticsAfterNotifyReactiveSystemsCall() { }

}

}