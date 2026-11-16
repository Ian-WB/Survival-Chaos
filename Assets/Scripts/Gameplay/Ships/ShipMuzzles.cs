using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The ship's guns: where each round leaves from, and the glow at their
    /// mouths.
    ///
    /// Rounds leave from the guns the ship is wearing, not from a point
    /// inside the hull. The mouths heat up towards each volley, flash on the
    /// shot, and the rounds then open out into the pattern they always made
    /// (see <see cref="ShootScript.Fan"/>).
    ///
    /// Every position is in the model's own space, nose along +X, and
    /// matches the barrels <c>ShipPartsBuilder</c> draws for Hydra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipMuzzles : MonoBehaviour
    {
        /// <summary>Longest the glow builds before a shot.</summary>
        public const float LeadSeconds = 0.22f;

        /// <summary>The glow never takes more than this share of the firing interval.</summary>
        public const float LeadShare = 0.6f;

        /// <summary>How long the shot's flash lasts.</summary>
        public const float FlashSeconds = 0.09f;

        /// <summary>How long a round takes to reach its place in the pattern.</summary>
        public const float FanSeconds = 0.09f;

        public const float HeatSize = 0.05f;
        public const float HeatBrightness = 2.5f;
        public const float FlashSize = 0.12f;
        public const float FlashBrightness = 4f;

        private const int Nose = 0;
        private const int Under = 1;
        private const int Pod = 2;
        private const int Roof = 3;

        // Where rounds leave each row from, on the ship's middle plane.
        private static readonly Vector3[] RowPoints =
        {
            new Vector3(0.2f, -0.02f, 0f),
            new Vector3(0.243f, -0.115f, 0f),
            new Vector3(0.218f, 0.03f, 0f),
            new Vector3(0.203f, 0.163f, 0f),
        };

        // How far to each side of the middle a row's barrels sit. Zero is one mouth.
        private static readonly float[] RowSpread = { 0f, 0.038f, 0.16f, 0.024f };

        // The rows firing at each shot stage, lowest first.
        private static readonly int[][] StageRows =
        {
            new[] { Nose },
            new[] { Under },
            new[] { Under, Pod },
            new[] { Under, Pod, Roof },
        };

        private Renderer[] flares;
        private int[] flareRows;
        private MaterialPropertyBlock block;
        private float heat;
        private float firedAt = float.NegativeInfinity;

        /// <summary>The shot stage the guns are set to, 0 to 3.</summary>
        public int Stage { get; private set; }

        /// <summary>How many rows of guns fire at this stage.</summary>
        public int Rows => RowCount(Stage);

        private static int ClampStage(int stage)
        {
            return Mathf.Clamp(stage, 0, StageRows.Length - 1);
        }

        /// <summary>How many rows of guns fire at a shot stage.</summary>
        public static int RowCount(int stage)
        {
            return StageRows[ClampStage(stage)].Length;
        }

        /// <summary>Where rounds leave a row from, in the model's space.</summary>
        public static Vector3 Row(int stage, int row)
        {
            int[] rows = StageRows[ClampStage(stage)];
            return RowPoints[rows[Mathf.Clamp(row, 0, rows.Length - 1)]];
        }

        /// <summary>
        /// The row a round leaves from, by its rank in the pattern from the
        /// lowest: low rounds from low guns, so no two paths cross.
        /// </summary>
        public static int RowFor(int rank, int rounds, int rows)
        {
            if (rounds <= 0 || rows <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(rank * rows / rounds, 0, rows - 1);
        }

        /// <summary>How many of a pattern's rounds sit below the one at <paramref name="index"/>.</summary>
        public static int Rank(float[] offsets, int index)
        {
            int below = 0;

            for (int i = 0; i < offsets.Length; i++)
            {
                if (offsets[i] < offsets[index])
                {
                    below++;
                }
            }

            return below;
        }

        /// <summary>How long before a shot the guns start to glow, at a firing interval.</summary>
        public static float Lead(float interval)
        {
            return Mathf.Min(LeadSeconds, Mathf.Max(0f, interval) * LeadShare);
        }

        /// <summary>How hot the guns are, 0 to 1: slow at first, hottest on the shot.</summary>
        public static float Heat(float untilNext, float lead)
        {
            if (lead <= 0f || untilNext >= lead)
            {
                return 0f;
            }

            float t = Mathf.Clamp01(1f - untilNext / lead);
            return t * t;
        }

        /// <summary>A fanning round's height: fast off the muzzle, settling onto its row.</summary>
        public static float FanHeight(float from, float to, float elapsed, float seconds)
        {
            if (seconds <= 0f || elapsed >= seconds)
            {
                return to;
            }

            if (elapsed <= 0f)
            {
                return from;
            }

            float left = 1f - elapsed / seconds;
            return Mathf.Lerp(from, to, 1f - left * left * left);
        }

        /// <summary>Builds a glow at every barrel mouth, dark. Once; nothing without a mesh or a material.</summary>
        public void Build(Mesh glowMesh, Material material)
        {
            if (flares != null || glowMesh == null || material == null)
            {
                return;
            }

            var built = new System.Collections.Generic.List<Renderer>();
            var rows = new System.Collections.Generic.List<int>();

            for (int row = 0; row < RowPoints.Length; row++)
            {
                int mouths = RowSpread[row] > 0f ? 2 : 1;

                for (int i = 0; i < mouths; i++)
                {
                    Vector3 mouth = RowPoints[row];
                    mouth.z = i == 0 ? RowSpread[row] : -RowSpread[row];

                    Renderer flare = ShipFlare.Build(
                        transform, "Muzzle " + row + " " + i, glowMesh, material, mouth, Quaternion.identity);

                    if (flare != null)
                    {
                        built.Add(flare);
                        rows.Add(row);
                    }
                }
            }

            flares = built.ToArray();
            flareRows = rows.ToArray();
            block = new MaterialPropertyBlock();
        }

        /// <summary>Sets the shot stage, and puts out the guns that stage does not fire.</summary>
        public void SetStage(int stage)
        {
            Stage = ClampStage(stage);

            if (flares == null)
            {
                return;
            }

            for (int i = 0; i < flares.Length; i++)
            {
                if (!InUse(flareRows[i]))
                {
                    ShipFlare.Set(flares[i], block, Vector3.zero, 0f);
                }
            }
        }

        /// <summary>Where rounds leave a row of this stage from, in the world.</summary>
        public Vector3 WorldPoint(int row)
        {
            return transform.TransformPoint(Row(Stage, row));
        }

        /// <summary>Told every frame how long until the next volley. Infinity is a gun that is not firing.</summary>
        public void Charge(float untilNext, float interval)
        {
            heat = Heat(untilNext, Lead(interval));
        }

        /// <summary>The volley has just left: flash.</summary>
        public void Fire()
        {
            firedAt = Time.time;
        }

        private bool InUse(int row)
        {
            int[] rows = StageRows[Stage];

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == row)
                {
                    return true;
                }
            }

            return false;
        }

        private void LateUpdate()
        {
            if (flares == null || PauseMenu.GameIsPaused)
            {
                return;
            }

            float flash = 1f - Mathf.Clamp01((Time.time - firedAt) / FlashSeconds);
            float burst = flash * flash;

            float size = Mathf.Max(HeatSize * (0.4f + 0.6f * heat), FlashSize * burst);
            float brightness = Mathf.Max(HeatBrightness * heat, FlashBrightness * burst);

            for (int i = 0; i < flares.Length; i++)
            {
                if (InUse(flareRows[i]))
                {
                    ShipFlare.Set(flares[i], block, new Vector3(size, size, size), brightness);
                }
            }
        }
    }
}
