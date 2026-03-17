using System;
using RootMotion;
using Star67.Avatar.Calibration;

namespace Star67.Avatar.Calibration
{
    public sealed class FinalIkAvatarCalibrationPoseGuard : IAvatarCalibrationPoseGuard
    {
        public bool CanGuard(IAvatar avatar)
        {
            return avatar?.Rig?.Root != null
                && avatar.Rig.Root.GetComponentInChildren<SolverManager>(true) != null;
        }

        public IDisposable Enter(IAvatar avatar)
        {
            if (avatar?.Rig?.Root == null)
            {
                return null;
            }

            SolverManager[] solverManagers = avatar.Rig.Root.GetComponentsInChildren<SolverManager>(true);
            return solverManagers.Length == 0 ? null : new Scope(solverManagers);
        }

        private sealed class Scope : IDisposable
        {
            private readonly SolverManager[] solverManagers;
            private readonly bool[] enabledStates;
            private bool disposed;

            public Scope(SolverManager[] solverManagers)
            {
                this.solverManagers = solverManagers;
                enabledStates = new bool[solverManagers.Length];

                for (int i = 0; i < solverManagers.Length; i++)
                {
                    SolverManager solverManager = solverManagers[i];
                    if (solverManager == null)
                    {
                        continue;
                    }

                    enabledStates[i] = solverManager.enabled;
                    solverManager.enabled = false;
                }
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                for (int i = solverManagers.Length - 1; i >= 0; i--)
                {
                    SolverManager solverManager = solverManagers[i];
                    if (solverManager == null)
                    {
                        continue;
                    }

                    solverManager.enabled = enabledStates[i];
                }
            }
        }
    }
}
