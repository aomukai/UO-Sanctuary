#!/usr/bin/env python3
"""Bind explicitly approved Arctic input to its shared server/client paths."""
import argparse
import json
from pathlib import Path
import subprocess
import skill_checkpoint_evidence as e

PARENT='fe0eafedb1bdb4c323e58269d9e0f2cf83a21294'


def review(check=False):
    inputs={}
    def pin(relative):
        p=e.ROOT/relative;inputs[relative]=e.sha(p)
        return e.load(p) if p.suffix=='.json' else None if p.suffix in('.dll','.pdb') else p.read_text()
    approval=pin('docs/arctic-approved-input-rule.md')
    assert 'Approve this Arctic Blast rule' in approval and '0.375' in approval and '4.6875' in approval
    client=pin('tools/reference/arctic-held-input-client-evidence.json')
    assert client['patches']==42 and client['buildErrors']==0 and not client['graphicalInputVerified']
    for group in ['sharedInputs','patchHashes','artifacts']:
        for p,h in client[group].items():assert e.sha(e.ROOT/p)==h,p;pin(p)
    for p in ['src/Sanctuary.Content/PlayerInfernoInput.cs','src/Sanctuary.Content/PlayerInfernoModes.cs',
              'src/Sanctuary.Content/PlayerInfernoSequence.cs','src/Sanctuary.Content/PlayerInfernoChannel.cs',
              'src/Sanctuary.Content/ClassCombat.cs','src/Sanctuary.Content/DruidCombat.cs',
              'src/Sanctuary.Content/DruidArcticStream.cs','src/Sanctuary.Content/SkillBindings.cs',
              'tests/Sanctuary.Integration/ArcticHeldInputChecks.cs','tests/Sanctuary.Integration/InfernoHeldInputChecks.cs',
              'tests/Sanctuary.Integration/ArcticStreamChecks.cs','tests/check_arctic_held_input.py',
              'tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json']:
        pin(p)
    previous=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=e.ROOT))
    current=e.load(e.ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [s for s in previous['skills']if s['talentId']!='druid.arcticblast']==[s for s in current['skills']if s['talentId']!='druid.arcticblast']
    assert sum(r['implementation']=='handler-present'for r in current['skills'])==204 and not current['completedClasses']
    assert all(r['implementation']=='handler-present'for r in current['skills']if r['characterClass']=='Druid')
    old=subprocess.check_output(['git','show',PARENT+':src/Sanctuary.Content/DruidArcticStream.cs'],cwd=e.ROOT,text=True)
    assert old.replace('// Prepared player230 emissions; this does not register a learned channel.',
                       '// Player230 emissions; the shared held-input controller owns learned casting.')==pin('src/Sanctuary.Content/DruidArcticStream.cs')
    pin('tools/reference/arctic-potion-native-evidence.json')
    pin(str(Path(__file__).resolve().relative_to(e.ROOT)))
    result=dict(referenceVersion='3.3.93847',parent=PARENT,inputs=inputs,arcticInputExplicitlyApproved=True,
        learnedArcticHandler=True,handlers=204,druidHandlers=30,completedClasses=[],clientPatches=42,
        currentD2RInputTimingVerified=False,graphicalClientInputVerified=False,
        scope=[
            'The existing held transport/controller accepts Inferno or Arctic only,checks the requested skill against the actual left/right/class-specific quick binding,and carries that ability into the shared sequence and its own emitter. Normal one-shot cast routes refuse both channels;Druid direct Begin/Release also refuse Arctic.',
            'Arctic uses its separately approved six-mana threshold,0.375 rank-one entry,first emission at frame12 and two-frame renewal. Live rank/cost changes affect subsequent invocations;released streams retain captured rank/mastery and their existing lifetime. No resource event is driven directly by a heartbeat.',
            'Patch42 extends existing left/right/quick channel gestures,retains the captured ability in each wire message,and ends the gesture on rebinding instead of switching skills in flight. It uses the existing protocol7 envelopes and source identities.',
            'The accepted potion receiver and cold packet are unchanged apart from a comment. New fixtures retain both actual channel types,raw wire sources0/1/9 and distinct connected save owners. Global cold-pool snapshots include every fixture;separate future epochs prevent unrelated synthetic clock advances from rewriting saved wounds.'
        ],limits=[
            'Timing is the explicit Sanctuary rule,not a claim of current D2R input chronology. Modern Slow Missiles damage order remains unsupported and refuses before starting. Foreign/item-only skill suppliers,world geometry/Q12/source-cell projection and global cross-owner event ordering retain their existing limits.',
            'The patched client compiles and its42patch stack round-trips to the exact pinned base;actual rendered mouse/cursor and animation acceptance remain unverified. Its44existing project warnings are recorded without a dependency audit.',
            'Druid30/30means handlers are present. It doesnot certify every Druid interaction or complete the class/goal. All other class status rows are unchanged.',
            'Existing cold cast/stream/audio and tree/left/right/quick icon placeholders remain separate asset work. Installed build,saves and configuration are not deployed or migrated.'
        ])
    path=e.ROOT/'tools/reference/arctic-held-input-review-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Arctic held review stale'
    else:path.write_text(encoded)
    print('Approved learned Arctic/shared Inferno transport and42patch client reviewed;204handlers/Druid30,no class certificate or deployment.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
