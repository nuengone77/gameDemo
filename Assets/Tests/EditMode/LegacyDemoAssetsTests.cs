using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace MagicGame.Tests
{
    public class LegacyDemoAssetsTests
    {
        const string Hovl = "Assets/Hovl Studio/Magic effects pack/";
        const string Matthew = "Assets/Matthew Guz/";

        static readonly object[] Profiles =
        {
            new object[] { Hovl + "Settings/PP low.asset", "70629691e8eede744b1c85c9ddcc4bc4", 5f },
            new object[] { Matthew + "Spell Area of Effect FREE/Scene Demo/Scene Demo AoE_Profiles/PP Volumen Profile.asset", "8e19f9cae612f5e4b8237fe5e8e6e747", 5f },
            new object[] { Matthew + "Status and Auras FREE/Scene Demo/Scene Demo Auras_Profiles/PP Volumen Profile.asset", "9fca554738237be4984f074c3c5f85eb", 5f },
            new object[] { Matthew + "Status and Auras FREE/Scene Demo/Scene Demo Auras_Profiles/PP Volumen Profile 1.asset", "aa9ed14a36536ec4eb0e653395442650", 5f },
            new object[] { Matthew + "Status and Auras FREE/Scene Demo/Scene Demo Auras_Profiles/PP Volumen Profile 2.asset", "ca6eb9040e1cd0d47add45864a88f7f7", 4.96f }
        };

        static List<Object> Components(Object profile)
        {
            var property = new SerializedObject(profile).FindProperty("components");
            var result = new List<Object>();
            for (int i = 0; i < property.arraySize; i++)
            {
                var component = property.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(component, Is.Not.Null, "A profile must not contain missing overrides");
                result.Add(component);
            }
            return result;
        }

        [TestCaseSource(nameof(Profiles))]
        public void DemoProfileRetainsGuidRootIdAndBloom(string path, string expectedGuid, float intensity)
        {
            var profile = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.VolumeProfile"));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(profile, out string guid, out long id), Is.True);
            Assert.That(guid, Is.EqualTo(expectedGuid));
            Assert.That(id, Is.EqualTo(11400000));
            var bloom = Components(profile).Single(c => c.GetType().Name == "Bloom");
            var serialized = new SerializedObject(bloom);
            Assert.That(serialized.FindProperty("intensity.m_OverrideState").boolValue, Is.True);
            Assert.That(serialized.FindProperty("intensity.m_Value").floatValue, Is.EqualTo(intensity).Within(.001f));
        }

        [Test]
        public void HovlProfileKeepsGradingAndEnabledCurves()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath(Hovl + "Settings/PP low.asset");
            var components = Components(profile);
            var adjustments = new SerializedObject(components.Single(c => c.GetType().Name == "ColorAdjustments"));
            Assert.That(adjustments.FindProperty("postExposure.m_Value").floatValue, Is.EqualTo(.6f).Within(.001f));
            Assert.That(adjustments.FindProperty("saturation.m_Value").floatValue, Is.EqualTo(24));
            Assert.That(adjustments.FindProperty("contrast.m_Value").floatValue, Is.EqualTo(3));
            var mixer = new SerializedObject(components.Single(c => c.GetType().Name == "ChannelMixer"));
            foreach (var channel in new[] { "redOutRedIn", "greenOutGreenIn", "blueOutBlueIn" })
                Assert.That(mixer.FindProperty(channel + ".m_Value").floatValue, Is.EqualTo(110));
            var curves = new SerializedObject(components.Single(c => c.GetType().Name == "ColorCurves"));
            foreach (var channel in new[] { "master", "red" })
            {
                Assert.That(curves.FindProperty(channel + ".m_OverrideState").boolValue, Is.True);
                var curve = curves.FindProperty(channel + ".m_Value.m_Curve").animationCurveValue;
                Assert.That(curve.length, Is.EqualTo(2));
                Assert.That(curve.Evaluate(.5f), Is.EqualTo(.5f).Within(.001f));
            }
        }

        [TestCase(Hovl + "Demo scene/Magic effects demo.unity")]
        [TestCase(Hovl + "Demo scene/Magic effects second demo.unity")]
        [TestCase(Matthew + "Spell Area of Effect FREE/Scene Demo/Scene Demo AoE FREE.unity")]
        [TestCase(Matthew + "Status and Auras FREE/Scene Demo/Scene Demo Auras FREE.unity")]
        public void DemoSceneHasValidPostProcessingWithoutMissingScripts(string path)
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                // The vendor's FREE Auras demo also references 32 absent prefab assets.
                // Preserve these references and accept only those known import errors;
                // missing scripts and any other unexpected errors must still fail.
                var missing = Regex.Matches(File.ReadAllText(path), @"m_SourcePrefab: \{fileID: -?\d+, guid: ([a-f0-9]+),")
                    .Cast<Match>().Select(m => m.Groups[1].Value)
                    .Where(guid => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))).Distinct().ToArray();
                if (missing.Length > 0)
                {
                    Assert.That(path, Is.EqualTo(Matthew + "Status and Auras FREE/Scene Demo/Scene Demo Auras FREE.unity"));
                    Assert.That(missing.Length, Is.EqualTo(32), "Only the original vendor demo prefab gaps are expected");
                    LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: '" + Regex.Escape(path) + "'"));
                    var prefabError = new Regex(@"^Prefab instance problem\. Missing Prefab Asset: .*guid: (" + string.Join("|", missing) + @")\)");
                    for (int i = 0; i < missing.Length; i++) LogAssert.Expect(LogType.Error, prefabError);
                }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            try
            {
                var objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                foreach (var go in objects)
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go), Is.Zero, go.name);
                var behaviours = objects.SelectMany(go => go.GetComponents<MonoBehaviour>()).Where(c => c != null).ToArray();
                var volume = behaviours.Single(c => c.GetType().FullName == "UnityEngine.Rendering.Volume");
                var profile = new SerializedObject(volume).FindProperty("sharedProfile").objectReferenceValue;
                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.GetType().FullName, Is.EqualTo("UnityEngine.Rendering.VolumeProfile"));
                var camera = behaviours.Single(c => c.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                var data = new SerializedObject(camera);
                Assert.That(data.FindProperty("m_RenderPostProcessing").boolValue, Is.True);
                int mask = data.FindProperty("m_VolumeLayerMask").intValue;
                Assert.That(mask & (1 << volume.gameObject.layer), Is.Not.Zero);
                Assert.That(camera.GetComponent<Camera>(), Is.Not.Null);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
            }
        }

        [TestCase("CameraOrbit", "54a60ea4e1f9da84097e5c4cd5bcdabd")]
        [TestCase("RotationParticle", "f88e27b7616b28841b2957acbec3d80b")]
        public void DemoScriptBindsToItsClassWithOriginalGuid(string name, string guid)
        {
            string path = Matthew + "Spell Area of Effect FREE/Scripts/" + name + ".cs";
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass().FullName, Is.EqualTo("MatthewAssets." + name));
        }
    }
}
