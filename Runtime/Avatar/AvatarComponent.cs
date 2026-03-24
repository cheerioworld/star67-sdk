using System;

namespace Star67.Avatar
{
    /// <summary>
    /// Represents a component that can be added to a <see cref="IAvatar"/> instance through its exposed <see cref="AvatarComponentManager"/>.
    /// A avatar component adds extra functionality to the avatar.
    /// <br/><br/>
    /// It's important that any implementation is prepared to Add/Remove the component across multiple avatar instances. This requirement
    /// is necessary for the correct function of the avatar cloning features.
    /// </summary>
    public abstract class AvatarComponent : IAvatarComponent
    {
        /// <summary>
        /// The name of this component instance.
        /// </summary>
        public abstract string Name { get; }
        
        /// <summary>
        /// The avatar that this component is added to.
        /// </summary>
        public IAvatar Avatar { get; private set; }
        
        public event Action Added;
        public event Action Removed;
        
        /// <summary>
        /// If enabled, this component won't be serialized by the <see cref="AvatarComponentManager"/>.
        /// </summary>
        // public bool ShouldSkipSerialization;

        public bool TryAdd(IAvatar avatar, bool notify)
        {
            Avatar = avatar;

            try
            {
                if (TryInitialize())
                {
                    if (notify)
                    {
                        Added?.Invoke();
                    }

                    return true; 
                }
            }
            catch (Exception)
            {
                Avatar = null;
                throw;
            }
            
            Avatar = null;
            return false;
        }
        
        public void Remove(bool notify)
        {
            try
            {
                OnRemoved();
            }
            finally
            {
                Avatar = null;
                if (notify)
                {
                    Removed?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// Creates a copy of this component with its current state that is ready to be added to a new avatar.
        /// </summary>
        // public abstract AvatarComponent Copy();
        
        /// <summary>
        /// Called when being added to a avatar, must return true if the initialization was correct.
        /// </summary>
        /// <returns>Whether the initialization succeeded. Return false to avoid the component from being added to the avatar.</returns>
        protected abstract bool TryInitialize();
        
        /// <summary>
        /// Called when being removed from a avatar, it should dispose all resources and leave the avatar in its original state
        /// </summary>
        protected abstract void OnRemoved();
    }
}