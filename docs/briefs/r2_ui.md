# Round 2 package: UI

Clone: `~/CretaceaUnityClones/ui`. Branch `fix/ui`.
You own: `Assets/Scripts/UI/*.cs`, `Assets/Resources/UI/**`, `Assets/Editor/CretaceaFontAssets.cs`.

Findings to fix (read `docs/review1/ui.md`, all of it is yours; the UI scored 6/10, the best segment):
1. **Major — journal, Documents tab**: the right "paper" card is one flat beige fill, the largest and brightest thing
   on screen; the five rows read like debug output (`1. [UNCOLLECTED - Security Office Desk]`). Use the reader's paper
   treatment for collected pages and a dark outlined slot for missing ones; two-line rows with an icon, "NOT FOUND" and
   a location hint; no brackets in UI copy.
2. **Major — the tablet spoils the story**: before the manual is read it already shows the route to E-0, Substation 3
   and the full six-step Circuit B sequence. Reveal entries as the story flags are set.
3. **Major — a new player cannot tell where to go**: the objective says what, not where; the map has no Records
   Annex; markers follow the route by world X only. Fix the objective copy (short, with the place), the map markers
   and labels, the "you are here" dot.
4. The lead fixed one bug after the review: a three-line objective ran into the journal hint (the banner now grows
   with its text): re-check with the longest objective.
5. The intro review (`docs/review1/intro.md`) found the document reader still open over the whole opening cinematic:
   that was the capture script, but the game must also close any open reader / journal / pause panel when a chapter
   or cutscene starts. Make that robust.
6. All minors in `docs/review1/ui.md`.
