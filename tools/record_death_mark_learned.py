#!/usr/bin/env python3
"""Authenticate approved learned Death Mark,actual inputs and original saves."""
import argparse,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/death-mark-learned'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    a=pin(STAGE/'source-snapshot-1.json')['inputs'];b=pin(STAGE/'source-snapshot-5.json')['inputs']
    production=lambda d:{n:h for n,h in d['sourceInputs'].items()if not n.startswith('tests/')}
    assert production(a)==production(b)and a['projectInputs']==b['projectInputs']and a['copiedData']==b['copiedData']
    for kind in ('content','integration'):
        number=1 if kind=='content'else 4;log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/death-mark-action-native-evidence.json');approval=pin('tools/reference/death-mark-action-approval.json')
    assert approval['approved']and e.sha(ROOT/approval['proposal'])==approval['proposalAtApprovalSha256']
    assert e.sha(ROOT/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    pin(approval['proposal'])
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers'])
    for kind in ('left','right','quick'):expected.update({f'death-mark-input-{kind}-check':3,f'death-mark-input-{kind}-result':3})
    text=e.cohort(STAGE,expected,138);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Client: 127.0.0.1: Connected.')==57
    assert text.count('actual learned Death Mark relocated original source-host Defiler;')==9
    assert text.count('original three-species saved hosts resumed,cast again and retained wounds/birth without replay')==2
    assert pin(STAGE/'death-mark-action-fixtures.json')['Generation']==2
    assert pin(STAGE/'defiler-link-casting-fixtures.json')['Generation']==2
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert len(re.findall(r'CombatAbility\.\w+',code))==16 and 'CombatAbility.DeathMark'in code
    status=pin('data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']
    assert sum(r['implementation']=='handler-present'for r in status['skills'])==225
    row=next(r for r in status['skills']if r['talentId']=='warlock.deathmark');assert row['implementation']=='handler-present'and row['verification']=='partial-native-coverage'and not row['complete']
    for name in ('src/Sanctuary.Content/WarlockCombat.cs','src/Sanctuary.Content/DeathMarkCasting.cs',
        'tests/Sanctuary.Integration/DeathMarkActionChecks.cs','tests/Sanctuary.Integration/DeathMarkInputChecks.cs','tests/check_death_mark_learned.py',
        'tools/record_death_mark_learned.py','tools/build_skill_roadmap.py','docs/death-mark-learned-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    pin(STAGE/'native-1/birth-native-checks.log');pin(STAGE/'native-1/native-run-1.log')
    pin(ROOT/'.local/class-skills/death-mark-input-focus/native-1/birth-native-checks.log')
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Approved learned Death Mark with actual mouse/quick inputs and original demon hosts',
        handlers=225,warlockHandlers=16,completedClasses=[],learnedDeathMarkEnabled=True,playerActionApproved=True,
        nativeMarkers=138,markers=dict(expected),actualInputs=27,deathMarkActualInputs=9,authenticatedSessions=57,boots=3,reloads=2,saves=3,
        contentBuild=1,integrationBuild=4,sourceSnapshot=5,nativeRun=2,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,
        serializationVersionChanged=False,newLiveReferenceReads=0,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Approved required-demon startup/native metadata0 now enters the shared player action through learned availability and saved left/right/quick bindings.',
            'Nine actual socket inputs across3boots cover targeted left,ground-only right and female quick/Tarnhelm effective rank7;SC windup stays unpaid,one fractional debit and20-frame local delay follow actual original-body relocation.',
            'Actual recipient marks retain effective rank/native metadata0;ground casts relocate without allocating a target mark. Original Defiler birth/wounds/controller retain.',
            'All120prepared-parent markers and18existing real summon inputs remain passing,including three-species automatic source interruption/recovery,no rollback on late allocation failure,approved flying box consistency and two original saved-host/binding reloads.'],
        diagnostics=['Native1 C1/I1 failed a fixture expectation that ignored ordinary source-frame pet regeneration during windup. Focused C1/I2 diagnostic confirmed correct result1/payment/mana/relocation/metadata with natural healing;all tested inputs/source/assemblies retained.',
            'Final I4 uses a larger wound and the source-frame recovery upper bound,allowing relocation observation skips. The synchronous prepared release test retains exact no-heal proof. Content1 production inputs remain unchanged.'],
        limits=['Current startup/unit12c producer remains unverified under the explicitly approved adaptation;no current executable parity claimed.',
            'No pending-player-midwindup save claimed;retained connected Defiler S1 midwindup saves keep their own scope.',
            'Remaining14Warlock talents plusAmazonSlowMissiles,broader class/property/recipient/PvP/client certification and separate final effects/audio/icons remain unfinished.'],inputs=inputs)
    path=ROOT/'tools/reference/death-mark-learned-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Learned Death Mark evidence stale'
    else:path.write_text(encoded)
    print('Learned Death Mark:C1/I4/S5/N2,138markers/27realinputs/2reloads;225handlers/Warlock16;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
