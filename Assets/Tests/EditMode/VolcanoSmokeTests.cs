using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// The volcano's smoke: a fog volume in the Game scene, drawn by a
    /// material whose shader is one function in an HLSL file. All made by
    /// VolcanoSmokeBuilder, in the editor assembly, which this one does not
    /// see - so these read what it made rather than call it.
    /// </summary>
    public class VolcanoSmokeTests
    {
        private const string MaterialPath = "Assets/Art/Materials/VFX/VolcanoSmoke.mat";
        private const string ShaderPath = "Assets/Art/Shaders/VolcanoSmoke.shadergraph";
        private const string FunctionPath = "Assets/Art/Shaders/VolcanoSmoke.hlsl";

        private static string ProjectFolder => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// A graph that fails to compile still imports, as Unity's error
        /// shader, and fog drawn with that is no smoke and no message.
        /// </summary>
        [Test]
        public void TheMaterial_IsOnItsOwnShader_AndItCompiles()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsNotNull(material, MaterialPath + " is missing: run Survival Chaos > Environment > Build Volcano Smoke");

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, ShaderPath);
            Assert.AreSame(shader, material.shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "the smoke's shader has errors");
        }

        /// <summary>
        /// The graph finds its function by the HLSL file's GUID, which is
        /// written into the graph by hand: a file re-created under a new GUID
        /// would leave the graph pointing at nothing.
        /// </summary>
        [Test]
        public void TheGraph_PointsAtItsFunction()
        {
            string graph = File.ReadAllText(Path.Combine(ProjectFolder, ShaderPath));
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(FunctionPath), graph);
            StringAssert.Contains("void VolcanoSmoke_float(", File.ReadAllText(Path.Combine(ProjectFolder, FunctionPath)));
        }

        /// <summary>The saved Game scene has the smoke, wearing the material.</summary>
        [Test]
        public void TheGameScene_HasTheSmoke()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectFolder, "Assets/Scenes/Game.unity"));

            StringAssert.Contains("m_Name: Volcano Smoke", scene);
            StringAssert.Contains(AssetDatabase.AssetPathToGUID(MaterialPath), scene);
        }
    }
}
