// using System;
// using VContainer.Unity;
// using UnityEngine;
//
// namespace Star67.Core
// {
//
//     public sealed class GameLoop :
//         IStartable,
//         ITickable,
//         ILateTickable,
//         IFixedTickable,
//         IDisposable
//     {
//         readonly ISystemRegistry registry;
//
//         public GameLoop(ISystemRegistry registry)
//         {
//             this.registry = registry;
//         }
//
//         void IStartable.Start()
//         {
//             var systems = registry.Initializables;
//             for (int i = 0; i < systems.Count; i++)
//                 systems[i].Initialize();
//         }
//
//         void ITickable.Tick()
//         {
//             var frame = new FrameContext(
//                 Time.deltaTime,
//                 Time.unscaledDeltaTime,
//                 Time.time,
//                 Time.frameCount);
//
//             var systems = registry.FrameSystems;
//             for (int i = 0; i < systems.Count; i++)
//                 systems[i].Update(in frame);
//         }
//
//         void ILateTickable.LateTick()
//         {
//             var frame = new FrameContext(
//                 Time.deltaTime,
//                 Time.unscaledDeltaTime,
//                 Time.time,
//                 Time.frameCount);
//
//             var systems = registry.LateFrameSystems;
//             for (int i = 0; i < systems.Count; i++)
//                 systems[i].LateUpdate(in frame);
//         }
//
//         void IFixedTickable.FixedTick()
//         {
//             var frame = new FrameContext(
//                 Time.fixedDeltaTime,
//                 Time.fixedUnscaledDeltaTime,
//                 Time.time,
//                 Time.frameCount);
//
//             var systems = registry.FixedFrameSystems;
//             for (int i = 0; i < systems.Count; i++)
//                 systems[i].FixedUpdate(in frame);
//         }
//
//         void IDisposable.Dispose()
//         {
//             var systems = registry.TeardownSystems;
//             for (int i = systems.Count - 1; i >= 0; i--)
//                 systems[i].Shutdown();
//         }
//     }
// }