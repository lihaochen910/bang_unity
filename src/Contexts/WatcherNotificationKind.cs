﻿namespace Bang.Contexts
{
    /// <summary>
    /// When a system is watching for a component, this is the kind of notification currently fired.
    /// </summary>
    public enum WatcherNotificationKind
    {
        /// <summary>
        /// Component has been added. It is not called if the entity is dead.
        /// </summary>
        Added,

        /// <summary>
        /// Component was removed.
        /// </summary>
        Removed,

        /// <summary>
        /// Component was modified. It is not called if the entity is dead.
        /// </summary>
        Modified
    }
}
