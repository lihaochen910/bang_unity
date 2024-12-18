using Bang.Contexts;

namespace Bang.Systems
{
    /// <summary>
    /// A system that consists of a single late_update call.
    /// </summary>
    public interface ILateUpdateSystem : ISystem
    {
        /// <summary>
        /// LateUpdate method. Called after Update method.
        /// </summary>
        public abstract void LateUpdate(Context context);
    }
}