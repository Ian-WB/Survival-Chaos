# Docs

Three long-form documents about this project, kept here as HTML because HTML is
the only one of the obvious formats that **diffs**. A PDF or a .docx would be a
binary blob rewritten whole on every update, so a commit could tell you the file
changed but never what changed — and this repo has already paid once for
carrying binaries in its history.

| File | What it is |
| --- | --- |
| `rebuild-reference.html` | The whole project: what the rebuild changed and how every system works now. The long one. |
| `leviathan-dossier.html` | The boss fight on its own — three acts, the muzzle rig, and what measuring it turned up. |
| `open-list.html` | What is still outstanding, as a checklist. |

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
