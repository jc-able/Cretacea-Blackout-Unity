export const meta = {
  name: 'cretacea-look-round4',
  description: 'Round 4: six focused packages fix the major findings of critic review 3 (trees and grass, Tyrant Valley at night, Visitor Center rooms, Visitor Center light, outdoor zones, interface and intro polish), each in its own clone with a hidden editor, re-reviewed by a critic, then fixed',
  phases: [
    { title: 'Build', detail: 'one engineer per package in its own clone + hidden editor', model: 'opus' },
    { title: 'Critique', detail: 'independent art-director review of captures and diff', model: 'opus' },
    { title: 'Fix', detail: 'same clone: fix the critic findings, re-capture, commit', model: 'opus' },
  ],
}

const ROOT = '~/CretaceaUnityClones'
const OWNER = '~/CretaceaBlackoutUnity'
const REFS = '~/CretaceaUnityClones/_refs'
const BASE = '3987803'

const PACKAGES = [
  { key: 'island', clone: 'terrain', title: 'the island: tree impostors from eye level, the valley woodland band, tall grass, the ending tree belt, the pond, rock' },
  { key: 'valley', clone: 'creatures', title: 'Tyrant Valley at night: the T. rex reveal, something bright between lightning strikes, a wet road' },
  { key: 'rooms', clone: 'zone1', title: 'Visitor Center rooms: dressing of the bare halls, placeholder props, the first gameplay frame' },
  { key: 'lights', clone: 'kit', title: 'Visitor Center light and fixtures: lobby colour, emergency heads, battery units, the loading dock, plaza lamps' },
  { key: 'zone23', clone: 'zone23', title: 'service path walls, the E-0 gate, the warehouse and wall in the ending shot, substation and coast road details' },
  { key: 'polish', clone: 'ui', title: 'interface and cinematic polish: controls page, HUD chip, tablet, captions, intro staging' },
]

const RULES = `HARD RULES (also in the Look Bible):
- Your clone is ${ROOT}/<KEY>. Every unity command must target it (export UNITY_PROJECT_PATH to the clone in EVERY shell command that calls unity or a Tools/lookdev script; Tools/lookdev/ucli.sh falls back to the owner's project if you forget). NEVER run anything against ${OWNER} (the owner's live editor) and never write outside your clone. Read-only references outside the clone that you may open: ${REFS}/review3/** (the 113 captures the review 3 findings name) and ${REFS}/final/** (the same views after the lead's quick fixes: your starting point).
- Start and stop your hidden editor ONLY with: bash <clone>/Tools/lookdev/clone_editor.sh start|stop <clone>. Stop a process only by a PID you started. Never pkill/killall. Never launch the built game (Builds/*.app).
- Stay inside the files your brief says you own. Keep public signatures stable. No new packages, no downloads, no paid generation.
- Never edit a script on disk while Play mode is running in your editor: stop Play first.
- Images: read at most ~25 in total, never wider than 1280 px (sips -Z 1280 to a scratch copy inside your clone if wider), one or two per Read call. Commands that may exceed ~90 s: run in the background and poll.
- Your FIRST action must be a trivial tool call (ls of your clone). Work in small steps with a capture after each; never compose a whole large file in one thought.
- Zero compile errors, zero new warnings, zero new console errors at hand-in.`

const BUILD_SCHEMA = {
  type: 'object',
  properties: {
    branch: { type: 'string' },
    commit: { type: 'string', description: 'git commit hash on the branch, or "none"' },
    summary: { type: 'string', description: 'what changed, max 1500 chars' },
    files_changed: { type: 'array', items: { type: 'string' } },
    captures_dir: { type: 'string' },
    captures: { type: 'array', items: { type: 'object', properties: { file: { type: 'string' }, shows: { type: 'string' } }, required: ['file', 'shows'] } },
    performance: { type: 'string', description: 'measured numbers if relevant, else ""' },
    requests_for_lead: { type: 'array', items: { type: 'string' } },
    unverified: { type: 'array', items: { type: 'string' } },
    editor_stopped: { type: 'boolean' },
  },
  required: ['branch', 'commit', 'summary', 'files_changed', 'captures_dir', 'captures', 'requests_for_lead', 'unverified', 'editor_stopped'],
}

const CRITIC_SCHEMA = {
  type: 'object',
  properties: {
    goals: { type: 'array', items: { type: 'object', properties: { goal: { type: 'string' }, verdict: { type: 'string', enum: ['met', 'partly', 'not_met', 'not_shown'] }, evidence: { type: 'string' } }, required: ['goal', 'verdict', 'evidence'] } },
    findings: { type: 'array', description: 'at most 10, most important first, each field under 450 chars', items: { type: 'object', properties: { title: { type: 'string' }, severity: { type: 'string', enum: ['high', 'medium', 'low'] }, evidence: { type: 'string' }, fix: { type: 'string' } }, required: ['title', 'severity', 'evidence', 'fix'] } },
    ownership_violations: { type: 'array', items: { type: 'string' } },
    overall: { type: 'string', description: 'max 700 chars: is this package at "amazing" yet, and what stands between it and that' },
  },
  required: ['goals', 'findings', 'ownership_violations', 'overall'],
}

function buildPrompt(p) {
  const clone = `${ROOT}/${p.clone}`
  return `You are one of six engineers working in parallel on round 4 of the look pass of "Cretacea: Blackout", a first-person dinosaur-park horror game in Unity 6 (6000.6.9f1) HDRP 17.6 on an Apple M5 Max. The owner said the first Unity build "doesn't look right, the colors are off" and wants it to "look amazing ... perfect". Three rounds of look work are merged and the game passes its whole story test. YOUR PACKAGE: ${p.title}.

Your clone: ${clone}   Your branch: r4/${p.key}   Base commit: ${BASE}

THIS IS ROUND 4. Twelve independent critics reviewed the merged game (review 3) and scored it 6 to 8 out of 10, with no blockers left and 23 majors. Your task is to fix the findings listed in your round 4 brief: verify each one in a capture first, then prove each fix with a same-view before/after pair. The branch r4/${p.key} is already checked out in your clone at the base commit; do not create another branch.

TAKE-OVER NOTE: another engineer started this exact package about 20 minutes ago and was cut off by a tool restart. Their work is still in your clone: uncommitted edits in the working tree (run git status and git diff first; ignore Assets/Scenes, regenerated .mat/.asset files and .clone_editor.pid), helper scripts and notes under Tools/agent/, captures under Screenshots/. Their hidden editor is probably still running: run 'bash ${clone}/Tools/lookdev/clone_editor.sh start ${clone}' (it says 'already running' and re-applies the setup), then make sure it is NOT in Play mode ('unity command editor.playmode stop') and that the code compiles ('unity recompile "$UNITY_PROJECT_PATH" --format json') before anything else. Read their diff critically: keep and finish what is sound, revert what is half-done or wrong. If the scenes were already built in this clone you can skip straight to a rebuild after your first change.

Read, in this order, inside your clone: docs/LOOK_BIBLE.md, docs/briefs/_common.md, docs/briefs/r2_common.md, docs/briefs/r3_common.md, docs/briefs/r4_common.md, docs/briefs/r4_${p.key}.md, then the review files your brief names (docs/review3/*.md and *_check.md). They contain the acceptance criteria, measured units, material and lamp APIs, the exact shell commands, your file ownership, your goals in priority order and the hand-in format. Then do the whole package, goal by goal, majors first, verifying each with captures you actually look at.

${RULES.replace('<KEY>', p.clone)}

Work until every goal in your brief is done or you can state precisely why one cannot be done here. Quality bar: a player or a critic looking at your captures must not be able to tell this area was generated from boxes. Time box: aim to hand in within about 70 minutes of work; a finished, verified subset beats a broken whole. When finished: follow the hand-in steps in docs/briefs/_common.md and r4_common.md exactly (final rebuild with NavMesh, Play-mode smoke test, route_test.py 45/45 and verify_playmode_doors where the brief asks, capture set + INDEX.md in Screenshots/handin_r4, docs/handin/r4_${p.key}.md, stop your editor, commit on your branch without the generated scenes), then return the structured result.`
}

function criticPrompt(p, build) {
  const clone = `${ROOT}/${p.clone}`
  return `You are an art director and technical reviewer for a first-person horror game ("Cretacea: Blackout", Unity 6 HDRP). An engineer just handed in the round 4 package "${p.title}" on branch r4/${p.key} in the project clone ${clone} (base commit ${BASE}). The owner's standard: "make this game look amazing ... it needs to be perfect". Your job is to find what is still wrong. You are read-only: do not modify any file, do not start Unity, do not run the unity CLI, do not launch the game, do not kill anything.

Read first (in the clone): docs/LOOK_BIBLE.md sections 2 to 5 and 8, and docs/briefs/r4_common.md and docs/briefs/r4_${p.key}.md (the goals you judge against, and the "You own" file list).

Then review:
1. The hand-in notes: docs/handin/r4_${p.key}.md.
2. The capture set: ${build.captures_dir || clone + '/Screenshots/handin_r4'} (read INDEX.md first; look at up to 16 images, one or two per Read call; if an image is wider than 1280 px make a smaller copy with: sips -Z 1280 <file> --out <scratch copy> under ${clone}/Screenshots/critic/ and read that). The state the findings were written against is in ${REFS}/review3/ and the state the engineer started from is in ${REFS}/final/ (both read-only).
3. The diff: git -C ${clone} diff ${BASE}..r4/${p.key} --stat, then read the changed code where a goal or a risk depends on it.

The engineer's own summary (do not trust it, verify): ${JSON.stringify((build.summary || '').slice(0, 1400))}
Engineer says unverified: ${JSON.stringify((build.unverified || []).slice(0, 8))}

Judge every numbered goal in the brief: met / partly / not_met / not_shown (no capture proves it), with evidence (file name + what you actually see). Default to not_met or not_shown unless a capture proves the goal. Then list concrete findings, most important first (at most 10): visual defects (primitive-looking geometry, stretched or wrong materials, floating or intersecting objects, lamps without bodies, flat or clipped lighting, saturated monochrome, empty space, unreadable text), anything new that looks worse than the starting state, anything that can break gameplay (new colliders on the walkable route, moved triggers, anchors or doors), performance risks, compile or console errors mentioned in notes, and rule violations. For each: severity, evidence, and an exact fix the engineer can apply (file, function, values). List files changed outside the package's ownership. Your FIRST action must be a trivial tool call (ls of the clone).`
}

function fixPrompt(p, build, review) {
  const clone = `${ROOT}/${p.clone}`
  const findings = JSON.stringify((review.findings || []).slice(0, 10)).slice(0, 8000)
  const goals = JSON.stringify((review.goals || []).filter(g => g.verdict !== 'met').slice(0, 12)).slice(0, 3600)
  return `You are continuing the round 4 look-pass package "${p.title}" of "Cretacea: Blackout" (Unity 6 HDRP) in the project clone ${clone} on branch r4/${p.key} (already checked out, previous engineer's commit ${build.commit}). An independent art director reviewed the hand-in and found problems. Fix them. This is the last pass: finish everything you touch.

Read first (in the clone): docs/LOOK_BIBLE.md, docs/briefs/_common.md, docs/briefs/r2_common.md, docs/briefs/r3_common.md, docs/briefs/r4_common.md, docs/briefs/r4_${p.key}.md, docs/handin/r4_${p.key}.md. Start your hidden editor with: bash ${clone}/Tools/lookdev/clone_editor.sh start ${clone}

${RULES.replace('<KEY>', p.clone)}

GOALS NOT YET MET (critic's verdicts): ${goals}

FINDINGS TO FIX (all high and medium; low if cheap): ${findings}

Critic's overall: ${JSON.stringify((review.overall || '').slice(0, 700))}
Files flagged as outside ownership (revert those changes unless explicitly allowed by the brief): ${JSON.stringify((review.ownership_violations || []).slice(0, 10))}

For each finding: reproduce it in a capture, fix it in the builder/look code you own, rebuild, capture again and compare. If a finding is wrong, say why with evidence instead of changing things. Do not regress what already works. Time box: about 45 minutes of work. When done: full hand-in again (final rebuild with NavMesh, Play-mode smoke test with zero new console errors, route_test.py 45/45 and verify_playmode_doors where the brief asks, refreshed capture set + INDEX.md in Screenshots/handin_r4, append a "Fix round" section to docs/handin/r4_${p.key}.md listing each finding and what you did, stop your editor, commit on the branch without the generated scenes), then return the structured result.`
}

const results = await pipeline(
  PACKAGES,
  p => agent(buildPrompt(p), { label: `build:${p.key}`, phase: 'Build', schema: BUILD_SCHEMA, model: 'opus', effort: 'high' }),
  (build, p) => {
    if (!build) return null
    return agent(criticPrompt(p, build), { label: `critic:${p.key}`, phase: 'Critique', schema: CRITIC_SCHEMA, model: 'opus', effort: 'max' })
      .then(review => ({ build, review }))
  },
  (prev, p) => {
    if (!prev) return null
    if (!prev.review || !(prev.review.findings || []).some(f => f.severity !== 'low')) return { key: p.key, build: prev.build, review: prev.review, fix: null }
    return agent(fixPrompt(p, prev.build, prev.review), { label: `fix:${p.key}`, phase: 'Fix', schema: BUILD_SCHEMA, model: 'opus', effort: 'high' })
      .then(fix => ({ key: p.key, build: prev.build, review: prev.review, fix }))
  }
)
return results.map((r, i) => r ? r : { key: PACKAGES[i].key, failed: true })
