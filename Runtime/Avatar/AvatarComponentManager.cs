using System;
using System.Collections.Generic;
using UnityEngine;

namespace Star67.Avatar
{
    public class AvatarComponentManager
    {
        public IReadOnlyList<IAvatarComponent> All => _components;
        public int Count => _components.Count;

        public event Action<IAvatarComponent> ComponentAdded;
        public event Action<IAvatarComponent> ComponentRemoved;

        // dependencies
        private readonly IAvatar _avatar;

        // state
        private readonly List<IAvatarComponent> _components;

        // helpers
        private readonly List<IAvatarComponent> _tmpComponents;

        public AvatarComponentManager(IAvatar avatar)
        {
            _avatar = avatar;

            _components = new List<IAvatarComponent>();

            _tmpComponents = new List<IAvatarComponent>();
        }

        public bool TryGet<T>(out T result)
            where T : IAvatarComponent
        {
            foreach (IAvatarComponent component in _components)
            {
                if (component is not T tComponent)
                {
                    continue;
                }

                result = tComponent;
                return true;
            }

            result = default;
            return false;
        }

        public List<T> GetAll<T>()
            where T : IAvatarComponent
        {
            var results = new List<T>(_components.Count);
            GetAll<T>(results);
            return results;
        }

        public void GetAll<T>(ICollection<T> results)
            where T : IAvatarComponent
        {
            if (results is null)
            {
                return;
            }

            foreach (IAvatarComponent component in _components)
            {
                if (component is T tComponent)
                {
                    results.Add(tComponent);
                }
            }
        }

        public bool Add<T>(bool notify = true)
            where T : IAvatarComponent, new()
        {
            return Add(new T(), notify);
        }

        public bool Add(IAvatarComponent component, bool notify = true)
        {
            if (component is null)
            {
                return false;
            }

            if (component.Avatar is not null)
            {
                Debug.LogError(
                    $"[{nameof(AvatarComponentManager)}] cannot add component {component.Name} ({component.GetType().Name}) because it is already added to another avatar: {component.Avatar.Rig.Root?.name}");
                return false;
            }

            if (component.Avatar == _avatar)
            {
                Debug.LogWarning(
                    $"[{nameof(AvatarComponentManager)}] component {component.Name} ({component.GetType().Name}) is already added to this avatar: {_avatar.Rig.Root?.name}");
                return false;
            }
            
            try
            {
                if (!component.TryAdd(_avatar, notify))
                {
                    Debug.LogWarning(
                        $"[{nameof(AvatarComponentManager)}] component {component.Name} ({component.GetType().Name}) will not be added because it failed to initialize");
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[{nameof(AvatarComponentManager)}] exception thrown when adding the component: {component.Name} ({component.GetType().Name})\n{exception}");
                return false;
            }

            _components.Add(component);

            if (notify)
            {
                ComponentAdded?.Invoke(component);
            }

            return true;
        }

        public void Remove(IAvatarComponent component, bool notify = true)
        {
            if (component is null || component.Avatar != _avatar)
            {
                return;
            }

            int index = _components.IndexOf(component);
            if (index < 0 || index >= _components.Count)
            {
                return;
            }

            _components.RemoveAt(index);
            SafeRemove(component, notify);

            if (notify)
            {
                ComponentRemoved?.Invoke(component);
            }
        }

        /// <summary>
        /// Will remove all components of the given type.
        /// </summary>
        public void Remove<T>(bool notify = true)
            where T : IAvatarComponent
        {
            // gather all components that match the type in the tmp components list
            _tmpComponents.Clear();
            foreach (IAvatarComponent component in _components)
            {
                if (component is T)
                {
                    _tmpComponents.Add(component);
                }
            }

            // remove matched components
            foreach (IAvatarComponent component in _tmpComponents)
            {
                Remove(component);
            }

            _tmpComponents.Clear();
        }

        public bool Add<T>(IEnumerable<T> components, bool notify = true)
            where T : IAvatarComponent
        {
            if (components is null)
            {
                return true;
            }

            bool allWereAdded = true;
            foreach (T component in components)
            {
                if (component is not null && !Add(component, notify))
                {
                    allWereAdded = false;
                }
            }

            return allWereAdded;
        }

        public void Remove<T>(IEnumerable<T> components, bool notify = true)
            where T : IAvatarComponent
        {
            foreach (T component in components)
            {
                Remove(component, notify);
            }
        }

        public void RemoveAll(bool notify = true)
        {
            // gather all components in the tmp list
            _tmpComponents.Clear();
            _tmpComponents.AddRange(_components);

            // remove matched components
            foreach (IAvatarComponent component in _tmpComponents)
            {
                Remove(component, notify);
            }

            _tmpComponents.Clear();
        }

        // public List<JToken> SerializeAll()
        // {
        //     var tokens = new List<JToken>(_components.Count);
        //     SerializeAll(tokens);
        //     return tokens;
        // }

        // public void SerializeAll(ICollection<JToken> results)
        // {
        //     foreach (AvatarComponent component in _components)
        //     {
        //         if (!component.ShouldSkipSerialization && SerializerAs<AvatarComponent>.TrySerialize(component, out JToken token))
        //         {
        //             results.Add(token);
        //         }
        //     }
        // }

        // public void DeserializeAndAdd(JToken token)
        // {
        //     if (SerializerAs<AvatarComponent>.TryDeserialize(token, out AvatarComponent component))
        //     {
        //         Add(component);
        //     }
        // }

        // public void DeserializeAndAdd(IEnumerable<JToken> tokens)
        // {
        //     foreach (JToken token in tokens)
        //     {
        //         DeserializeAndAdd(token);
        //     }
        // }

        private static void SafeRemove(IAvatarComponent component, bool notify)
        {
            try
            {
                component.Remove(notify);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[{nameof(AvatarComponentManager)}] exception thrown when removing the component: {component.Name} ({component.GetType().Name})\n{exception}");
            }
        }
    }
}