#!/usr/bin/env python3
"""Record prepared Death Mark action and the approved flying-placement fix."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/death-mark-action'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    # The final edit adds an integration-only zero-metadata recipient control.
    # Keep Content3 rather than rebuilding identical production inputs.
    before=pin(STAGE/'source-snapshot-3.json')['inputs'];after=pin(STAGE/'source-snapshot-4.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(before)==production(after)and before['projectInputs']==after['projectInputs']and before['copiedData']==after['copiedData']
    for kind,number in (('content',3),('integration',4)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/defiler-cast-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    old=pin('tools/reference/death-mark-cast-native-evidence.json')
    assert old['sourceCases']==122 and old['sourceBytes']==1431
    proof=pin('tools/reference/death-mark-cast-current-evidence.json');assert proof['cases']==122 and proof['dispatcherIndexLinked']
    # Retain source proofs without recompiling/repeating their old matrices.
    for name in ('tools/reference/death-mark-recipient-approval.json','tools/reference/death-mark-native-world-approval.json','tools/reference/defiler-flight-world-approval.json'):
        approval=pin(name);assert approval['approved'if 'defiler-flight' in name else 'approvedByUser']
    expected=Counter(parent['markers']);expected.update({'death-mark-action-check':1,'death-mark-action-check-result':1,'death-mark-action-reload-check':2,'death-mark-action-reload-check-result':2})
    text=e.cohort(STAGE,expected,120);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Client: 127.0.0.1: Connected.')==48
    assert text.count('Prepared Death Mark action:original three-species saved hosts resumed,cast again and retained wounds/birth without replay')==2
    assert pin(STAGE/'death-mark-action-fixtures.json')['Generation']==2
    assert pin(STAGE/'defiler-link-casting-fixtures.json')['Generation']==2
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert len(re.findall(r'CombatAbility\.\w+',code))==15 and 'CombatAbility.DeathMark'not in code
    for name in ('src/Sanctuary.Content/DeathMarkCasting.cs','src/Sanctuary.Content/ClassCombat.cs',
        'src/Sanctuary.Content/WarlockGoatmanCasting.cs','src/Sanctuary.Content/DeathMarkWorld.cs','src/Sanctuary.Content/GenericPetNativePlacement.cs',
        'src/Sanctuary.Content/DeathMarkCast.cs','src/Sanctuary.Content/DeathMarkWorldRelease.cs','src/Sanctuary.Content/DeathMarkNativeRelease.cs',
        'tests/Sanctuary.Integration/DeathMarkActionChecks.cs','tests/check_death_mark_action.py','tools/record_death_mark_action.py',
        'docs/death-mark-action-proposal.md','docs/death-mark-action-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    failed=pin(STAGE/'native-1/birth-inputs.json');assert failed['artifacts']!=born['artifacts']
    pin(STAGE/'native-1/birth-native-checks.log');pin(STAGE/'native-1/native-run-1.log')
    focus=ROOT/'.local/class-skills/death-mark-action-focus';pin(focus/'native-1/birth-native-checks.log');pin(focus/'native-2/birth-native-checks.log')
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared Death Mark player action and approved flying placement consistency',
        handlers=224,warlockHandlers=15,completedClasses=[],learnedDeathMarkEnabled=False,playerActionDecisionPending=True,
        nativeMarkers=120,markers=dict(expected),actualInputs=18,deathMarkActualInputs=0,authenticatedSessions=48,boots=3,reloads=2,saves=3,
        contentBuild=3,integrationBuild=4,sourceSnapshot=4,nativeRun=2,sourceCasesReused=122,sourceBytesReused=1431,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Prepared connected player action reuses shared SC/FCR,explicit required roster,unpaid cancellation/movement/late mana refusal,once-only fractional successful-do payment and20-frame local delay.',
            'Actual original three-species source hosts are interrupted/commanded/relocated by do155 and resume without rebirth,wound recovery or stream resets.',
            'Stronger existing recipient mark causes source result0 after pet commands;completed warps remain with no payment,cooldown or invented rollback.',
            'Explicit context98765 and proposed native metadata0 reach actual recipient state independently of rank/creator.',
            'Native flying placement now also enforces the already-approved full-box fit;the unchanged source pattern2 cross/search/order remains intact and ground placement is unchanged.',
            'Original saved three-species controllers cast again after both reloads;all114parent markers and18existing real summon inputs remain passing.'],
        diagnostics=['MainNative1 C1/I1 and focused diagnostic1 C2/I2 reproduced diagonally blocked Defiler landing/movement fault;source,assemblies and logs frozen before changes.',
            'C3/I3 focused diagnostic2 passed3boots/2reloads after full-box placement fix. Final I4 adds metadata0 application;production Content3 inputs unchanged.'],
        limits=['Requires-pet startup caller and opaque source unit12c producer remain unavailable;tested complete proposal requires explicit adaptation approval before learned integration.',
            'No Death Mark real left/right/quick inputs are claimed;18retained actual inputs are Goatman/Tainted/Defiler.',
            'No pending-player-midwindup save is claimed;existing connected Defiler S1 midwindup saves retain their own coverage.',
            'Current world/global caller/PvP/property/recipient/client/full-class certification and separate final effects/audio/icons remain open.'],inputs=inputs)
    path=ROOT/'tools/reference/death-mark-action-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Death Mark action evidence stale'
    else:path.write_text(encoded)
    print('Prepared Death Mark:C3/I4/S4/N2,120markers/18retained realinputs/2reloads;224handlers;decision pending;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
