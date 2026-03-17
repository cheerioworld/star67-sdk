using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Star67.Avatar.Calibration
{
    public sealed class AvatarCalibrationService : IAvatarCalibrationService
    {
        public event Action<AvatarCalibrationState> AvatarCalibrated;
        private readonly IAvatarCalibrationStep[] calibrationSteps;
        private readonly IAvatarCalibrationPoseGuard[] poseGuards;

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
        
        public Task<AvatarCalibrationRuntime> CalibrateAsync(IAvatar avatar, CancellationToken cancellationToken = default)
        {
            if (avatar?.Rig?.Root == null)
            {
                AvatarCalibrated?.Invoke(null);
                return Task.FromResult<AvatarCalibrationRuntime>(null);
            }

            Transform root = avatar.Rig.Root;
            Animator animator = avatar.Rig.Animator;
            if (animator == null || animator.avatar == null || !animator.isHuman)
            {
                Debug.LogWarning($"AvatarCalibrationService: Avatar '{root.name}' does not expose a humanoid Animator. Skipping calibration.");
                var runtime = root.GetComponent<AvatarCalibrationRuntime>();
                AvatarCalibrated?.Invoke(runtime.State);
                return Task.FromResult(runtime);
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                    var runtime = root.GetComponent<AvatarCalibrationRuntime>();
                    AvatarCalibrated?.Invoke(runtime.State);
                    return Task.FromResult(runtime);
                }

                using (tPoseScope)
                {
                    var state = new AvatarCalibrationState();
                    var context = new AvatarCalibrationContext(avatar, state);

                    for (int i = 0; i < calibrationSteps.Length; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

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
                        var calibrationRuntime = root.GetComponent<AvatarCalibrationRuntime>();
                        AvatarCalibrated?.Invoke(calibrationRuntime.State);
                        return Task.FromResult(calibrationRuntime);
                    }

                    AvatarCalibrationRuntime runtime = root.GetComponent<AvatarCalibrationRuntime>();
                    if (runtime == null)
                    {
                        runtime = root.gameObject.AddComponent<AvatarCalibrationRuntime>();
                    }

                    runtime.SetState(state);
                    AvatarCalibrated?.Invoke(state);
                    return Task.FromResult(runtime);
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
    }
}
