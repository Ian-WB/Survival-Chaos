# Editor playtest bot

An opt-in baseline pilot for repeated gameplay testing. It uses normal movement,
dash, firing-direction and pickup interactions. It is **not a human simulation**
and its success rate is not a difficulty verdict.

## Use

1. Open `Survival Chaos > Playtest Bot`.
2. Enter Play mode in the gameplay scene, with god mode off.
3. Click **Start in current run**. Watch in the Game view.
4. Click **Stop and export report**, close the bot window, or exit Play mode to
   return control. Normal pause input remains available while it runs.

The tool never starts Play mode itself, skips waves, grants upgrades, changes
health or restarts a run. It refuses god mode and invalidates a run if god mode
is enabled later. A positive nonstandard time scale also ends the session.
Pausing suspends gameplay decisions. Stopping the bot does not pause the game.

Reports are written automatically to `Logs/PlaytestBot/`, outside `Assets`, when
the session ends. They contain decision changes, health/level changes, camera
settings, boss phase diagnostics, total kills and collected upgrade counts. The
summary's `healingObserved` is health gained beyond what maximum-health picks
explain, which in practice is salvage collected.
Health losses include recent candidate decisions, predicted landing risk, input,
dash state and the nearest delayed observations. These are evidence of what the
bot knew, not proof of which object caused the hit. Fatal health loss is recorded
even when the ending stops gameplay updates.
Totals belong to the whole game run; bot elapsed time measures only its session.
Damage attribution is deliberately unknown. No reports or editor tool enter a
standalone build. No prefab or scene installation is needed.

## How it works

The editor window temporarily replaces `GameInput.Source` with an `IGameInput`
implementation. Each frame's inputs are computed once, so different consumers
see the same axes and button events. Keyboard smoothing matches the current
`InputSystemGameInput`: sensitivity/gravity 6, with a zero reset on reversal.
This duplicated keyboard policy must be checked if the game's input changes.

The pilot samples active objects through the gameplay camera's frustum and
physics occlusion checks, then queues those observations. Its default delay is
0.25 seconds, configurable in the window before starting. Sampling is every
0.10 seconds and decisions commit for 0.20 seconds. These are proposed testing
parameters, not measurements of human reaction time.

It compares nine movement choices over a 1.20-second horizon in the arena's
actual angular coordinates and height band. Danger takes priority over pickups
and target alignment. Velocity comes from consecutive visible samples, not
projectile steering state. Enemy shots are identified by their existing tag;
pooled shot histories are reset using their volley generation. Objects absent
from a new observation are forgotten; already queued observations still retain
the configured delay. It does not query offscreen positions to predict motion.
When walking routes are dangerous and dash is ready, it also evaluates dash
routes. Each captures the smoothed heading on the press frame, uses the live
duration and separate orbit/climb boosts, and splits integration at dash expiry.
It accounts for dash invincibility but penalises danger immediately after landing.
An in-progress dash retains its committed heading and remaining time.

Visible prow-pod growth can trigger a height dodge. Crown and keel growth are no
longer treated as laser warnings. This reads the drawn glow's scale, not the boss
attack countdown. Visible lance ribbon segments are additional hazards, sampled
from their drawn mesh and passed through the same observation delay. Repeated
visible hull-emission flashes create an ambiguous possible-ram warning: damage
flashes can look the same, so the report must not call this certain recognition.
The hull is tracked even though it is not an ordinary Enemy component.
So are the two shooting kinds: the Enemy and Enemy 1 prefabs carry `Enemy_1`,
which is a separate class, and until 22 September the pilot looked for `Enemy`
alone, so it saw their rounds once fired and never the ships.
Wreckage plates shed in the second act are tracked as stationary hazards.

Enemy rounds fly at about 26 units a second, so by the time the delayed pilot
has seen one twice it has covered 9-12 units. Rounds fired from closer than that
cannot be answered by watching them, so the pilot watches the guns instead:
each shooting enemy projects a line of fire at each gun's height, 45 degrees
(about 14.7 units) round the ring in the direction it faces, and standing in it
is costly. Farther out, the rounds themselves are seen in time. Enemies that
chase the player's height (Enemy 2 closes most of a gap in half a second) are
forecast with the same exponential approach the game runs, rather than holding
the height they were seen at. The reaction delay is caught up first, toward
where the pilot's ship actually went since the sighting, so no candidate route
can change a chase that has already happened; then the chase is stepped along
each route, closing on the route's height at each step, and holds whenever the
chaser is out of range.
In the boss fight the emplacements are the targets: while one is in sight the
hull is not, since it takes no damage until they are gone. The emplacements
stick out of one side of the hull and the hull swallows rounds, so from the
wrong side the pilot goes the long way round the ring to the front instead of
shooting armour. Emplacements outrank everything but a heal when low, and the
current target is kept unless another beats it by a clear margin: each has 80
health, and a pilot drifting between an emplacement and nearby obstacles never
finished one. Reports log each emplacement's health in steps of 10 and when it
falls.
Height is scored on the closest a candidate route comes to the target, with a
little weight on where it ends. Scored on the end alone, a climb held for the
whole horizon overshoots a target in the middle of the band by as much as
staying put misses it, so the pilot sat above the Prow shooting into the Crown.
While an emplacement is the target, reports add an `AIM` line each second:
the pilot's height and angle against the pod's real position and against the
position the pilot saw, which way it is firing, how far in front of the pod's face
it is, and the first thing a round would meet on the way: the ring is walked at
the pilot's height the way a round flies, against the real colliders, and an
emplacement, the hull, a wreck plate or a ship of either kind all count. These
are read from the game for the report only.
With no emplacement in sight the hull takes an emplacement's priority, so
obstacles near the pilot do not pull it off the exposed boss.
The policy pursues salvage ahead of everything below half health, and ahead of
ordinary targets whenever any health is missing; upgrades go shots, attack
speed, maximum health, then movement. The real player must touch a pickup for
its benefit to apply.

The tool restores the previous input provider only while it still owns input.
It also stops on Play-mode exit, window closure, assembly reload, player removal,
run ending or faults. Export failure cannot prevent input restoration.

## Limits

- Camera filtering is not pixel vision: contrast, fog, bloom and audio are not
  perceived. Only geometry represented by physics colliders can occlude rays.
- Renderer bounds approximate threat sizes. They are not exact hitboxes.
- Prediction cannot foresee a torpedo turning or a disc bouncing.
- It can miss a charge first seen after growth stops. Short hull flashes can
  fall between observations; offscreen audio warnings are not perceived.
- Visible beam segments are held at their last observed positions over the
  prediction horizon; expiry and unseen beam geometry are not known.
- Dash prediction is numerical, not a second physics simulation. Trigger timing
  and script update order can differ by a frame. Landing safety is estimated
  against delayed observations, not guaranteed.
- It uses exact self-state and identity labels for visible objects. This is more
  information than a human has even with reaction delay.
- Some project integration reads private fields for movement, glow transforms,
  firing prefabs, gun positions and each enemy's height-chase settings. Missing fields stop the bot rather than invent values.
- No automatic batch runs, learned behaviour or difficulty recommendations.

## Validation performed

Compiled in the project's Unity 6000.6.2f1 editor and verified the loaded type.
Two short, normal-speed sessions ran with god mode off. The first destroyed 17
enemies over about 57 seconds; the second destroyed 4 over about 24 seconds.
Observed input reads were stable within a frame. Sampled observation ages were
0.316 and 0.273 seconds, both above the configured 0.25-second delay.

Manual Stop restored the exact captured input provider. Exiting Play mode while
the bot was active stopped it and restored `InputSystemGameInput`. Starting
outside Play mode was rejected. Reports were exported.

These are integration smoke checks, not a completed survival run or boss test.
Death/victory handling, a long pooling soak and boss success remain unverified.

On 22 September a 7.8-minute run died at level 13 before the boss, taking 43
hits. For 21 of the 37 traceable hits nothing was seen within 1.5 units, and 15
came within 1.5 units of an Enemy 2. The line-of-fire and height-chase
forecasts, and wreckage tracking, were added in answer; they are covered by unit
tests only and have not yet been run in Play mode. The boss has never been
reached: use the debug menu's Skip to boss before starting the bot to test it.

Later the same day full runs did reach the boss, once dying 11 seconds into
Exposed. A review by ChatGPT then found the pilot had never seen the Enemy and
Enemy 1 ships (above), so every run to that point was made half blind, and the
line-of-fire forecast had never run for the guns it was written for: its unit
test checked the arithmetic, not whether anything fed it. The first run after
the fix took 7 hits in its first eight minutes, against 25 for the one blind
run that lived that long, then 36 between eight and ten minutes in, and died 8
seconds into Armoured at level 20.

## Removal

Stop the bot, then delete `Assets/Scripts/Editor/SurvivalPlaytestBot.cs` through
Unity. There are no saved scene components or gameplay edits to undo. This
document and exported reports may be removed independently.
