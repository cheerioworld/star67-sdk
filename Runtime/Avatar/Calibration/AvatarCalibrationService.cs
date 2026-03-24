using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Star67.Avatar.Calibration
{
    public sealed class AvatarCalibrationService : IAvatarCalibrationService
    {
        public event Action<AvatarCalibrationState> AvatarCalibrated;
        private readonly IAvatarCalibrationStep[] calibrationSteps;
        private readonly IAvatarCalibrationPoseGuard[] poseGuards;

        [Preserve]
        public AvatarCalibrationService()
            : this(
                new IAvatarCalibrationStep[]
                {
                    new HumanoidReferencePoseCalibrationStep(),
                    new EyeHeightCalibrationStep()
                },
                Array.Empty<IAvatarCalibrationPoseGuard>())
        {
        }

        public AvatarCalibrationService(IEnumerable<IAvatarCalibrationStep> calibrationSteps, IEnumerable<IAvatarCalibrationPoseGuard> poseGuards = null)
        {
            this.calibrationSteps = calibrationSteps == null
                ? Array.Empty<IAvatarCalibrationStep>()
                : calibrationSteps
                    .Where(step => step != null)
                    .OrderBy(step => step.Order)
                    .ToArray();

            this.poseGuards = poseGuards == null
                ? Array.Empty<IAvatarCalibrationPoseGuard>()
                : poseGuards
                    .Where(guard => guard != null)
                    .ToArray();
        }
        
        public AvatarCalibrationState Calibrate(IAvatar avatar)
        {
            var state = new AvatarCalibrationState();
            if (avatar?.Rig?.Root == null)
            {
                AvatarCalibrated?.Invoke(state);
                return state;
            }

            Transform root = avatar.Rig.Root;
            Animator animator = avatar.Rig.Animator;
            if (animator == null || animator.avatar == null || !animator.isHuman)
            {
                Debug.LogWarning($"AvatarCalibrationService: Avatar '{root.name}' does not expose a humanoid Animator. Skipping calibration.");
                AvatarCalibrated?.Invoke(state);
                return state;
            }

            var guardScopes = new List<IDisposable>(poseGuards.Length);
            try
            {
                for (int i = 0; i < poseGuards.Length; i++)
                {
                    IAvatarCalibrationPoseGuard poseGuard = poseGuards[i];
                    if (!poseGuard.CanGuard(avatar))
                    {
                        continue;
                    }

                    IDisposable scope = poseGuard.Enter(avatar);
                    if (scope != null)
                    {
                        guardScopes.Add(scope);
                    }
                }

                if (!HumanoidTPoseScope.TryCreate(avatar.Rig, out HumanoidTPoseScope tPoseScope))
                {
                    Debug.LogWarning($"AvatarCalibrationService: Could not resolve a humanoid T-pose for avatar '{root.name}'. Skipping calibration.");
                    AvatarCalibrated?.Invoke(state);
                    return state;
                }

                using (tPoseScope)
                {
                    var context = new AvatarCalibrationContext(avatar, state);

                    for (int i = 0; i < calibrationSteps.Length; i++)
                    {
                        IAvatarCalibrationStep step = calibrationSteps[i];
                        if (!step.CanCalibrate(avatar))
                        {
                            continue;
                        }

                        step.Calibrate(context);
                    }

                    state.IsCalibrated = state.ReferencePose.HasAnyPose;
                    if (!state.IsCalibrated)
                    {
                        Debug.LogWarning($"AvatarCalibrationService: Calibration did not capture any reference pose data for avatar '{root.name}'.");
                        AvatarCalibrated?.Invoke(state);
                        return state;
                    }

                    AvatarCalibrated?.Invoke(state);
                    return state;
                }
            }
            finally
            {
                for (int i = guardScopes.Count - 1; i >= 0; i--)
                {
                    guardScopes[i]?.Dispose();
                }
            }
        }

        public Task<AvatarCalibrationState> CalibrateAsync(IAvatar avatar, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Calibrate(avatar));
        }
    }
}
