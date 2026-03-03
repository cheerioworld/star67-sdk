using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Star67.Sdk.Tests
{
    public class BasisFaceBlendshapeFrameDriverTests
    {
        private readonly List<GameObject> _gameObjects = new();
        private readonly List<Mesh> _meshes = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _gameObjects.Count; i++)
            {
                Object.DestroyImmediate(_gameObjects[i]);
            }

            _gameObjects.Clear();

            for (int i = 0; i < _meshes.Count; i++)
            {
                Object.DestroyImmediate(_meshes[i]);
            }

            _meshes.Clear();
        }

        [Test]
        public void ApplyFrame_UsesExactBlendshapeNames()
        {
            SkinnedMeshRenderer renderer = CreateRenderer("EyeBlinkLeft");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { renderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.5f
                }
            });

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void ApplyFrame_ResolvesCommonAliasNames()
        {
            SkinnedMeshRenderer renderer = CreateRenderer("blendShape.eyeBlink_L", "mouthSmile_R");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { renderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.25f
                },
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.MouthSmileRight,
                    weight = 0.75f
                }
            });

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(25f).Within(0.001f));
            Assert.That(renderer.GetBlendShapeWeight(1), Is.EqualTo(75f).Within(0.001f));
        }

        [Test]
        public void ApplyFrame_DrivesAllMatchingRenderers()
        {
            SkinnedMeshRenderer firstRenderer = CreateRenderer("JawOpen");
            SkinnedMeshRenderer secondRenderer = CreateRenderer("JawOpen");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { firstRenderer, secondRenderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.JawOpen,
                    weight = 0.6f
                }
            });

            Assert.That(firstRenderer.GetBlendShapeWeight(0), Is.EqualTo(60f).Within(0.001f));
            Assert.That(secondRenderer.GetBlendShapeWeight(0), Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void ApplyFrame_LeavesOmittedChannelsUnchanged()
        {
            SkinnedMeshRenderer renderer = CreateRenderer("EyeBlinkLeft", "JawOpen");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { renderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.4f
                }
            });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.JawOpen,
                    weight = 0.8f
                }
            });

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(40f).Within(0.001f));
            Assert.That(renderer.GetBlendShapeWeight(1), Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void ApplyFrame_IgnoresUnmatchedChannels()
        {
            SkinnedMeshRenderer renderer = CreateRenderer("EyeBlinkLeft");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { renderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.3f
                }
            });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.JawOpen,
                    weight = 1f
                }
            });

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void UpdateTarget_RebuildsTheBlendshapeMap()
        {
            SkinnedMeshRenderer oldRenderer = CreateRenderer("EyeBlinkLeft");
            SkinnedMeshRenderer newRenderer = CreateRenderer("JawOpen");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { oldRenderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.5f
                }
            });

            driver.UpdateTarget(new[] { newRenderer });
            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.2f
                },
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.JawOpen,
                    weight = 0.25f
                }
            });

            Assert.That(oldRenderer.GetBlendShapeWeight(0), Is.EqualTo(50f).Within(0.001f));
            Assert.That(newRenderer.GetBlendShapeWeight(0), Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ApplyFrame_LastDuplicateEntryWins()
        {
            SkinnedMeshRenderer renderer = CreateRenderer("EyeBlinkLeft");
            var driver = new BasisFaceBlendshapeFrameDriver(new[] { renderer });

            driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.1f
                },
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.EyeBlinkLeft,
                    weight = 0.9f
                }
            });

            Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void Driver_IsNullSafeForMissingTargetsAndFrames()
        {
            var emptyTargetObject = new GameObject("EmptyTarget");
            _gameObjects.Add(emptyTargetObject);

            SkinnedMeshRenderer rendererWithoutMesh = emptyTargetObject.AddComponent<SkinnedMeshRenderer>();
            var driver = new BasisFaceBlendshapeFrameDriver(new SkinnedMeshRenderer[] { null, rendererWithoutMesh });

            Assert.DoesNotThrow(() => driver.ApplyFrame(null));
            Assert.DoesNotThrow(() => driver.ApplyFrame(System.Array.Empty<FaceBlendshape>()));
            Assert.DoesNotThrow(() => driver.UpdateTarget(null));
            Assert.DoesNotThrow(() => driver.ApplyFrame(new[]
            {
                new FaceBlendshape
                {
                    location = FaceBlendshapeLocation.JawOpen,
                    weight = 0.4f
                }
            }));
        }

        private SkinnedMeshRenderer CreateRenderer(params string[] blendShapeNames)
        {
            var gameObject = new GameObject("BlendshapeRenderer");
            _gameObjects.Add(gameObject);

            SkinnedMeshRenderer renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            return renderer;
        }

        private Mesh CreateMesh(params string[] blendShapeNames)
        {
            var mesh = new Mesh
            {
                name = "BlendshapeTestMesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                },
                triangles = new[] { 0, 1, 2 }
            };

            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

            for (int i = 0; i < blendShapeNames.Length; i++)
            {
                mesh.AddBlendShapeFrame(blendShapeNames[i], 100f, deltaVertices, deltaNormals, deltaTangents);
            }

            _meshes.Add(mesh);
            return mesh;
        }
    }
}
