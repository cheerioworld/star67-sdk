# Star67 avatar SDK agent guide

## Purpose and consumers

This is the independently versioned `com.cheerioworld.star67.sdk` Unity package, normally nested inside the `star67-sdk-dev` repository. It contains reusable avatar preparation, loading, calibration, retargeting, shared tracking representations, recording, and UDP preview transport. Selected Basis components support the custom `.BEE` avatar format.

Star67 is a Chatroulette-style avatar video-call app. Genies and `.BEE` avatars are intended for long-term support. The app transmits rendered avatar video over Agora; SDK UDP tracking primarily sends phone tracking into Editor previews. Creator-facing activation of that tracking server is planned.

Built-avatar upload, storage, cataloging, and distribution currently use `star67-admin-web` through `star67-api` in the company-wide `cheerio-apps` repository. `cheerio-infra` owns infrastructure and ArgoCD GitOps delivery. Public UPM distribution with a sample project for preparing, building, and publishing avatars is a future goal, not a claim that self-service publishing is shipped.

## Code boundaries

- `Runtime/Contracts` defines reusable avatar interfaces. `Runtime/Avatar` handles avatar loading/calibration and related runtime behavior.
- `Runtime/Tracking/Core` owns shared tracking contracts, frames, codec, recording, and transport. `Runtime/Tracking/Unity` connects tracking to Unity and avatar rigs.
- `Editor` contains creator/editor tooling; keep Editor-only code out of runtime assemblies.
- `Basis` and `External` contain supporting/vendor code. The separate upstream Basis checkout in SDK development is mostly reference material; do not refresh vendors as routine setup.
- MediaPipe input processing belongs in the sibling `com.cheerioworld.star67.mediapipe` package. App-specific UI, authentication, matchmaking, and Agora integration belong in the consuming app.
- Tracking inputs should produce normalized semantic animation data and remain independent of avatar-specific rigs. Retargeters adapt that data to avatar conventions.
- Prefer pure C# services and constructor-injected dependencies where practical. Consumers can compose them with VContainer.
- Avoid per-frame allocations, LINQ, reflection, or dynamic dispatch in performance-sensitive tracking and retargeting loops. Prefer structs and precomputed mappings where appropriate.

## Unity workflow

- Use filesystem tools for source and documentation work. Use Unity MCP for live Editor state, scenes, GameObjects, components, assets, packages, console logs, profiler data, and Editor validation.
- If Unity MCP is unavailable when needed or fails to connect, pause Unity work and ask the user to restore the connection before proceeding.
- After script or asset changes, refresh/compile through Unity MCP and inspect relevant Console errors. Run the tests appropriate to the affected behavior. Documentation-only changes do not require an Editor connection or build.
- Do not clear the Console or alter scenes, assets, or project settings unless required by the task. Preserve asset GUIDs and synchronized `.meta` files; isolate Editor-only code from runtime assemblies.

## Development and validation

- `package.json` declares package compatibility/dependencies. The consuming Unity project's `ProjectSettings/ProjectVersion.txt` and package manifest determine the actual Editor/dependency setup.
- In the normal SDK development checkout, `sdk-development` consumes this package for avatar preparation and SDK testing; `hand-tracking-dev` consumes it with MediaPipe for hand integration testing. The main Star67 app also references it locally.
- Run relevant Unity Test Runner tests from `Tests/Editor/Star67.Sdk.Editor.Tests.asmdef` and `Tests/Runtime/Star67.Sdk.Tests.asmdef` in a consuming project. Compile affected consumers after public contract changes.
- Local package edits reach consumers without publishing. Package publication is a separate operation.
- Preserve `.meta` files and asset GUIDs. Changes to the upstream-copy script in SDK development must account for local modifications to the vendored `Basis` directory.

## Cross-repository working agreements

- Follow necessary dependencies across available repositories, including related edits; read the destination repository's guidance first. If a required checkout is unavailable, identify the missing dependency rather than assume it was updated.
- Keep priorities task-specific. Ask when a material tradeoff between the app experience and creator tooling is unresolved.
- Preserve unrelated working-tree changes and respect nested Git repository boundaries. Commit child repositories before updating parent submodule pointers, and make child commits available on their remotes before publishing parent pointers.
- Implementation or validation alone does not authorize publication or deployment. Follow explicit task authorization and the repository's delivery rules.
- Update relevant guidance alongside architectural changes. Clearly separate implemented behavior from planned capabilities, and refer to manifests, workflows, and runbooks for changing operational details.
