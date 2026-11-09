# Docs

The long-form documents about this project, kept here as HTML because HTML is
the only one of the obvious formats that **diffs**. A PDF or a .docx would be a
binary blob rewritten whole on every update, so a commit could tell you the file
changed but never what changed — and this repo has already paid once for
carrying binaries in its history.

| File | What it is |
| --- | --- |
| `rebuild-reference.html` | The whole project: what the rebuild changed and how every system works now. The long one. |
| `rebuild-lessons-codex.html` | Learning the rebuild — an independent study edition by Codex, seventeen chapters that explain the game one decision at a time, from an enemy's health to the evidence behind a fix. Built for comparison with the rebuild reference. |
| `leviathan-dossier.html` | The boss fight on its own — three acts, the muzzle rig, and what measuring it turned up. |
| `open-list.html` | What is still outstanding, as a checklist. |
| `polish-roadmap.html` | Sixteen proposed polish items in three phases, each with a recommended answer. What is accepted moves onto the open list. |
| `polish-roadmap-2.html` | The second pass: sixteen more, numbered 17 to 32 so a number names one item across both. Same rules. |
| `environment-roadmap.html` | Sixteen proposed changes to the island, its sky and its lighting, numbered 33 to 48, each with a recommended answer and an expected frame cost. Same rules. |
| `agent-guide.md` | For an AI agent with shell and file access to this machine, to read before touching anything. Most of its rules exist because a specific thing went wrong once. |
| `playtest-bot.md` | The editor playtest bot: an opt-in baseline pilot for repeated gameplay testing, using normal movement, dash, firing direction and pickups. Not a human simulation, and its success rate is not a difficulty verdict. |

## These are snapshots, not the source

Each of them is also published as a private page on claude.ai, and **the
published page is the live version**. These copies exist so the writing survives
offline, outside an account, and inside the project's own history.

`open-list.html` is the one to be careful with: the published version is
interactive — ticking an item saves a new version of the page — so this file
goes stale the moment anything is ticked. It is a point-in-time snapshot of the
list, not a mirror of it.

## Refreshing them

Ask Claude. The routine is to read the live page, rebuild from that rather than
from these files, republish, and copy the result back here — in that order, so a
stale local copy can never overwrite the live one. Refresh these whenever a page
is republished; a snapshot nobody updates is worse than no snapshot, because it
looks current.

## They are not imported by Unity

Unity only reads `Assets/`, `Packages/` and `ProjectSettings/`. A folder beside
those is invisible to the editor, costs nothing at import, and reaches no build.
