using Bang.Contexts;

namespace Bang.Systems
{
    /// <summary>
    /// A system only called once before the world starts.
    /// </summary>
    public interface IEarlyStartupSystem : ISystem
    {
        /// <summary>
        /// This is called before CreateAllEntities call.
        /// </summary>
        public abstract void EarlyStart(Context context);
    }
}