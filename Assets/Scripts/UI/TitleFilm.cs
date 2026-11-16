using UnityEngine;
using UnityEngine.Video;

namespace SurvivalChaos
{
    /// <summary>
    /// Plays the title film behind the menu: the logo's power-on once, then
    /// the rest round and round.
    ///
    /// TitleScreen.mp4 opens on the empty sky, and the logo flickers on over
    /// its first half second. It used to fade the logo out at its end, and the
    /// VideoPlayer's Loop took it back to the start, so the menu put the logo
    /// out and lit it again every 15 s, behind the buttons as well. The clip
    /// was cut again on 16 November 2026: its last six frames blend into the
    /// six before <see cref="loopFrame"/>, where a keyframe stands, so a few
    /// frames from its end it jumps to the matching frame there and goes on,
    /// and the power-on plays once.
    ///
    /// It can be held on its first frame (<see cref="HeldBack"/>): the title
    /// splash holds it under the opening card and the fade, so the power-on
    /// is the logo's reveal rather than something that happened in the dark.
    ///
    /// The VideoPlayer's own Loop stays on. Should the jump be missed, the
    /// film starts again from the top, power-on and all, rather than stopping.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [DisallowMultipleComponent]
    public sealed class TitleFilm : MonoBehaviour
    {
        [Tooltip("The frame the film goes on from after its last. TitleScreen.mp4's last frames blend into the ones just before it.")]
        [SerializeField] private int loopFrame = 81;

        /// <summary>
        /// While true the film waits on its first frame, the sky before the
        /// logo lights. The title splash asks for it before the scene's first
        /// frame, as it does <see cref="MusicSource.HeldBack"/>.
        /// </summary>
        public static bool HeldBack { get; set; }

        /// <summary>
        /// How many frames before its last the film makes its jump, so a slow
        /// frame cannot carry it past the end and into the VideoPlayer's own
        /// Loop, power-on and all.
        /// </summary>
        public const int JumpBeforeEnd = 3;

        /// <summary>Frames a seek takes to land, at the film's rate: the jump aims that far on.</summary>
        public const int SeekLead = 2;

        /// <summary>Seconds a jump may take to land before it is made again.</summary>
        private const float SeekPatience = 1f;

        private VideoPlayer player;
        private bool holding;
        private bool jumping;
        private float jumpStartedAt;
        private long previousFrame = -1;

        /// <summary>The frame the film goes on from after its last.</summary>
        public int LoopFrame => loopFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHold()
        {
            HeldBack = false;
        }

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            // Pause before the film is ready readies it and keeps it on its
            // first frame. Play On Awake has only begun to read it by now.
            if (HeldBack)
            {
                if (!holding)
                {
                    holding = true;
                    player.Pause();
                }
                return;
            }

            if (holding)
            {
                holding = false;
                player.Play();
            }

            if (!player.isPlaying)
            {
                return;
            }

            long frame = player.frame;
            long lastFrame = (long)player.frameCount - 1;

            // A jump has landed once the film shows a frame from before the end
            // again. The seek's own completion is no sign: frame still reads
            // the old one for a moment after it, and seeing the end there,
            // this jumped again, every frame, and the film stood still on
            // frame 374 (16 November 2026).
            if (jumping)
            {
                if (Landed(frame, lastFrame) || Time.unscaledTime - jumpStartedAt > SeekPatience)
                {
                    jumping = false;
                    previousFrame = frame;
                }
                return;
            }

            if (LoopDue(frame, previousFrame, lastFrame, loopFrame))
            {
                long target = LoopTarget(frame, lastFrame, loopFrame);
                jumping = true;
                jumpStartedAt = Time.unscaledTime;
                player.frame = target;
                frame = target;
            }

            previousFrame = frame;
        }

        /// <summary>Whether a jump has landed: the film is back before the frames it jumps from.</summary>
        public static bool Landed(long frame, long lastFrame)
        {
            return frame >= 0 && frame < lastFrame - JumpBeforeEnd;
        }

        /// <summary>
        /// Whether the film should go back past its power-on: in its last
        /// <see cref="JumpBeforeEnd"/> frames and the last itself, or once its
        /// own Loop has taken it back to the start because the jump was missed.
        /// Never for a clip too short to have the loop frame in it, which then
        /// loops whole.
        /// </summary>
        public static bool LoopDue(long frame, long previousFrame, long lastFrame, int loopFrame)
        {
            if (loopFrame <= JumpBeforeEnd || lastFrame <= loopFrame + JumpBeforeEnd)
            {
                return false;
            }

            bool nearTheEnd = frame >= lastFrame - JumpBeforeEnd;
            bool wrapped = frame < loopFrame && previousFrame > loopFrame;
            return nearTheEnd || wrapped;
        }

        /// <summary>
        /// Where the jump from <paramref name="frame"/> lands: the frame that
        /// follows it in the loop, where frames before the last stand for the
        /// ones as far before <paramref name="loopFrame"/>, plus
        /// <see cref="SeekLead"/> for the time the seek takes. From the start
        /// of the clip, after a missed jump, it is the loop frame itself.
        /// </summary>
        public static long LoopTarget(long frame, long lastFrame, int loopFrame)
        {
            if (frame < loopFrame)
            {
                return loopFrame;
            }

            return loopFrame - (lastFrame - frame) + SeekLead;
        }
    }
}
