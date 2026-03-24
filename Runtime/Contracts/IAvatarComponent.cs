using System;

namespace Star67.Avatar
{
    public interface IAvatarComponent
    {
        /// <summary>
        /// The name of this component instance.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// The avatar that this component is added to.
        /// </summary>
        IAvatar Avatar { get; }
        
        event Action Added;
        event Action Removed;
        
        bool TryAdd(IAvatar avatar, bool notify);

        void Remove(bool notify);

        /// <summary>
        /// If enabled, this component won't be serialized by the <see cref="AvatarComponentManager"/>.
        /// </summary>
        // public bool ShouldSkipSerialization;
        
        /// <summary>
        /// Creates a copy of this component with its current state that is ready to be added to a new avatar.
        /// </summary>
        // public abstract AvatarComponent Copy();
        
        /// <summary>
        /// Called when being added to a avatar, must return true if the initialization was correct.
        /// </summary>
        /// <returns>Whether the initialization succeeded. Return false to avoid the component from being added to the avatar.</returns>
        // protected bool TryInitialize();
        
        /// <summary>
        /// Called when being removed from a avatar, it should dispose all resources and leave the avatar in its original state
        /// </summary>
        // protected void OnRemoved();
    }

    // internal interface IAvatarComponentInternal
    // {
    //     bool TryAdd(IAvatar avatar, bool notify);
    //
    //     void Remove(bool notify);
    // }
    
    
}