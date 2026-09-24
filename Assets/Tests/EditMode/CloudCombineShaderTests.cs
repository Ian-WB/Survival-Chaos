using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The clouds are composited by the project's copy of HDRP's combine
    /// shader, which stops the fog reaching into them past Max Fog Distance.
    ///
    /// The swap is one reference in HDRenderPipelineGlobalSettings, in a
    /// settings class HDRP keeps internal and hides from the Inspector. Anything
    /// that resets the pipeline's resources - an HDRP upgrade, a Reset on the
    /// global settings - puts the package's shader back, and nothing shows it
    /// except the clouds going grey again the next time someone thickens the
    /// fog. Hence the reflection: there is no public way to read it.
    /// </summary>
    public class CloudCombineShaderTests
    {
        private const string ShaderPath = "Assets/Art/Shaders/VolumetricCloudsCombine.shader";

        private const string ResourcesType =
            "UnityEngine.Rendering.HighDefinition.VolumetricCloudsRuntimeResources, " +
            "Unity.RenderPipelines.HighDefinition.Runtime";

        [Test]
        public void TheClouds_AreCombinedByTheProjectShader()
        {
            Type resources = Type.GetType(ResourcesType);
            Assert.IsNotNull(resources,
                "HDRP no longer has VolumetricCloudsRuntimeResources. It has been renamed or moved, " +
                "so find where the combine shader is set now and check the project copy still fits.");

            object settings = typeof(GraphicsSettings)
                .GetMethod(nameof(GraphicsSettings.GetRenderPipelineSettings), Type.EmptyTypes)
                .MakeGenericMethod(resources)
                .Invoke(null, null);

            var inUse = resources.GetProperty("volumetricCloudsCombinePS").GetValue(settings) as Shader;
            var ours = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            // Unity's ==, not AreSame: after the shader is reimported the pipeline
            // keeps its old C# handle, a different object pointing at the same shader.
            Assert.IsNotNull(ours, ShaderPath + " is missing.");
            Assert.IsTrue(inUse == ours,
                "The clouds are combined by " + (inUse != null ? inUse.name : "nothing") + ", so the " +
                "fog is back to burying them. Point VolumetricCloudsRuntimeResources." +
                "volumetricCloudsCombinePS at " + ShaderPath + " again.");
        }

        [Test]
        public void TheProjectShader_Compiles()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            Assert.IsNotNull(shader, ShaderPath + " is missing.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader),
                "The project's cloud combine shader no longer compiles, most likely after an HDRP " +
                "upgrade. Re-copy the package's VolumetricCloudsCombine.shader and reapply " +
                "CloudFogDepth.");
        }
    }
}
