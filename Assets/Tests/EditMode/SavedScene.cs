using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Reads objects out of a scene as it is saved on disk, without opening it.
    ///
    /// Edit mode tests run in a scene of their own, so the Game scene's objects
    /// are not there to find, and opening it from a test would load its lighting
    /// and probe data and wake everything in it that runs in edit mode. The
    /// saved file holds the same values as text. This reads the few kinds of
    /// field the tests need, by name; it is not a YAML parser.
    /// </summary>
    internal sealed class SavedScene
    {
        private static readonly Regex Document = new Regex(
            @"--- !u!\d+ &(-?\d+)(?: stripped)?\r?\n(\w+):\r?\n(.*?)(?=\r?\n--- !u!|\z)",
            RegexOptions.Singleline);

        private const string Number = @"(-?[\d.]+(?:[eE][-+]?\d+)?)";

        private readonly Dictionary<string, (string Type, string Body)> objects =
            new Dictionary<string, (string, string)>();

        public static SavedScene Load(string path)
        {
            SavedScene scene = new SavedScene();

            foreach (Match match in Document.Matches(File.ReadAllText(path)))
            {
                scene.objects[match.Groups[1].Value] = (match.Groups[2].Value, match.Groups[3].Value);
            }

            return scene;
        }

        /// <summary>The first GameObject called <paramref name="name"/>, or null.</summary>
        public string GameObjectNamed(string name)
        {
            foreach (KeyValuePair<string, (string Type, string Body)> entry in objects)
            {
                if (entry.Value.Type == "GameObject" &&
                    Regex.IsMatch(entry.Value.Body, @"\n  m_Name: " + Regex.Escape(name) + @"\r?\n"))
                {
                    return entry.Key;
                }
            }

            return null;
        }

        private IEnumerable<string> Components(string gameObject)
        {
            foreach (Match match in Regex.Matches(objects[gameObject].Body, @"component: \{fileID: (-?\d+)\}"))
            {
                yield return match.Groups[1].Value;
            }
        }

        /// <summary>The first component of a type, such as "BoxCollider", or null.</summary>
        public string Component(string gameObject, string type)
        {
            foreach (string id in Components(gameObject))
            {
                if (objects.TryGetValue(id, out var found) && found.Type == type)
                {
                    return id;
                }
            }

            return null;
        }

        /// <summary>The script on <paramref name="gameObject"/> that has a field called <paramref name="field"/>, or null.</summary>
        public string ScriptWith(string gameObject, string field)
        {
            foreach (string id in Components(gameObject))
            {
                if (objects.TryGetValue(id, out var found) && found.Type == "MonoBehaviour" &&
                    Regex.IsMatch(found.Body, @"\n  " + Regex.Escape(field) + ":"))
                {
                    return id;
                }
            }

            return null;
        }

        private Match Field(string id, string field, string value)
        {
            Match match = Regex.Match(objects[id].Body, @"\n  " + Regex.Escape(field) + ": " + value);
            Assert(match.Success, id + " has no " + field);
            return match;
        }

        public string Reference(string id, string field)
        {
            return Field(id, field, @"\{fileID: (-?\d+)").Groups[1].Value;
        }

        public float Float(string id, string field)
        {
            return Parse(Field(id, field, Number).Groups[1].Value);
        }

        /// <summary>A vector field, with z 0 when it has none (a 2D offset or size).</summary>
        public Vector3 Vector(string id, string field)
        {
            Match match = Field(id, field, @"\{x: " + Number + ", y: " + Number + "(?:, z: " + Number + ")?");
            return new Vector3(
                Parse(match.Groups[1].Value),
                Parse(match.Groups[2].Value),
                match.Groups[3].Success ? Parse(match.Groups[3].Value) : 0f);
        }

        /// <summary>
        /// Where a height <paramref name="localY"/> in a transform's own space
        /// sits in the world, and what a unit of that space measures there. Only
        /// for a chain with no rotation anywhere, which is what the scene's
        /// bounds boxes are; false for anything else.
        /// </summary>
        public bool TryWorldY(string transform, float localY, out float worldY, out float scale)
        {
            worldY = localY;
            scale = 1f;

            for (string id = transform; id != "0"; id = Reference(id, "m_Father"))
            {
                Match rotation = Field(id, "m_LocalRotation", @"\{x: " + Number + ", y: " + Number + ", z: " + Number);

                for (int axis = 1; axis <= 3; axis++)
                {
                    if (Mathf.Abs(Parse(rotation.Groups[axis].Value)) > 1e-6f)
                    {
                        return false;
                    }
                }

                Vector3 position = Vector(id, "m_LocalPosition");
                Vector3 size = Vector(id, "m_LocalScale");
                worldY = position.y + size.y * worldY;
                scale *= size.y;
            }

            return true;
        }

        /// <summary>
        /// The band ApplyBounds on the object called <paramref name="player"/>
        /// holds it in, read the way ApplyBounds reads it: the vertical extent,
        /// in the world, of the 2D box it is handed.
        /// </summary>
        public void Band(string player, out float floor, out float ceiling)
        {
            string ship = GameObjectNamed(player);
            Assert(ship != null, "no " + player + " in the scene");

            string box = Reference(ScriptWith(ship, "playerBounds"), "playerBounds");
            float height = Vector(box, "m_Size").y;
            bool flat = TryWorldY(Component(Reference(box, "m_GameObject"), "Transform"),
                Vector(box, "m_Offset").y, out float middle, out float scale);
            Assert(flat, "the band's box is turned, which this does not read");

            floor = middle - height * scale * 0.5f;
            ceiling = middle + height * scale * 0.5f;
        }

        /// <summary>A transform's scale in the world: its own times every parent's.</summary>
        public Vector3 WorldScale(string transform)
        {
            Vector3 scale = Vector3.one;

            for (string id = transform; id != "0"; id = Reference(id, "m_Father"))
            {
                scale = Vector3.Scale(scale, Vector(id, "m_LocalScale"));
            }

            return scale;
        }

        private static float Parse(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }
    }
}
