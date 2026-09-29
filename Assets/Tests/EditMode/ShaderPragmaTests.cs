using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Builds come out with no warnings. The one there was came from TextMesh
    /// Pro's Mobile shaders, which carried #pragma enable_d3d11_debug_symbols:
    /// deprecated, and only ever useful for debugging a shader in a GPU tool.
    /// Removed on 29 Sep 2026. Re-importing TMP's Essential Resources writes
    /// the shaders back as shipped and brings it with them, so this reads the
    /// files rather than trusting that nobody will.
    /// </summary>
    public class ShaderPragmaTests
    {
        private const string Pragma = "#pragma enable_d3d11_debug_symbols";

        [Test]
        public void NoShaderAsksForD3D11DebugSymbols()
        {
            string[] offenders = Directory.GetFiles(Application.dataPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".shader") || path.EndsWith(".cginc") || path.EndsWith(".hlsl"))
                .Where(path => File.ReadLines(path).Any(line => line.Trim() == Pragma))
                .Select(path => path.Substring(Application.dataPath.Length - "Assets".Length).Replace('\\', '/'))
                .ToArray();

            Assert.IsEmpty(offenders,
                "Each of these puts a deprecation warning in every build; delete its " + Pragma + " line:\n" +
                string.Join("\n", offenders));
        }
    }
}
