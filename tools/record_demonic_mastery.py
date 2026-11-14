#!/usr/bin/env python3
"""Verify the existing Mastery passive without repeating old active cohorts."""
import argparse,hashlib,json,re
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/demonic-mastery'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-1.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/death-mark-learned-native-evidence.json');previous=pin(ROOT/'.local/class-skills/death-mark-learned/birth-inputs.json')
    # All established mechanical production consumers are unchanged. The
    # existing passive is only added to the implementation-availability list.
    for name,h in previous['sourceInputs'].items():
        if not name.endswith('.cs')or name.startswith('tests/'):continue
        p=ROOT/name;raw=p.read_bytes()
        if name=='src/Sanctuary.Content/WarlockCombat.cs':raw=raw.replace(b'CombatAbility.DemonicMastery or ',b'',1)
        assert hashlib.sha256(raw).hexdigest()==h,name
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    source=pin('tools/reference/demonic-mastery-source-review.json')
    assert source['skill']==374 and {r['skill']for r in source['consumers']}=={373,376,377,382}
    for name,h in source['inputs'].items():assert e.sha(ROOT/name)==h,name
    for name in source['existingFormulaAndBirthProofs']:pin(name)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'demonic-mastery-check':1,'demonic-mastery-check-result':1,
        'demonic-mastery-reload-check':2,'demonic-mastery-reload-check-result':2})
    text=e.cohort(STAGE,expected,15);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==3
    assert text.count('Demonic Mastery:original Warlock/Witch three-species snapshots,wounds,RNG,controllers and effective/hard ranks retain across actual reload and gear recheck')==2
    saved=pin(STAGE/'demonic-mastery-fixtures.json');assert saved['Generation']==2 and len(saved['Owners'])==2
    assert {s['Female']for s in saved['Owners']}=={False,True}and all(len(s['Pets'])==3 for s in saved['Owners'])
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text();implemented=code.split('public static bool Implemented')[1].split(';',1)[0]
    assert len(re.findall(r'CombatAbility\.\w+',implemented))==17 and 'CombatAbility.DemonicMastery'in implemented
    assert 'CombatAbility.DemonicMastery'not in code.split('public static bool Handles')[1].split(';',1)[0]
    status=pin('data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==226
    row=next(r for r in status['skills']if r['talentId']=='warlock.demonicmastery');assert row['implementation']=='handler-present'and row['verification']=='partial-native-coverage'and not row['complete']
    for name in ('src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/DemonicMasteryChecks.cs','tests/check_demonic_mastery.py',
        'tools/review_demonic_mastery.py','tools/record_demonic_mastery.py','tools/build_skill_roadmap.py','docs/demonic-mastery-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Demonic Mastery existing passive and captured live consumers',handlers=226,warlockHandlers=17,completedClasses=[],
        demonicMasteryImplemented=True,activeCastAdded=False,newBonusWriters=0,nativeMarkers=15,markers=dict(expected),
        scriptedLearnedSummonTransactions=10,actualInputs=0,previousActualInputEvidence=27,originalOwners=2,originalBodies=6,
        authenticatedSessions=3,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Authenticated owned374 passive metadata/20parameters and all four existing table consumers. No independent cast,event or bonus writer invented.',
            'Actual learned three-species summons on Warlock/Witch use effective captured Mastery in unarmed damage/flat AR,Tainted fire packet/A2clock and source movement;DefilerS1 Other timing correctly excludes stored attackrate.',
            'Soft5/10do not increase the demon cap;hard5/10do. Gear/training changes affect new captures without rewriting surviving original pets or granting a soft-point cap.',
            'Original6bodies/2owners/helm inventories,births,wounds,RNG,controllers and effective/hard ranks retain across both actual reloads and gear rechecks.',
            'Passive has zero mana/no active route and refuses all mouse/quick binding attempts. Only production change is implementation availability.'],
        limits=['Bind Demon numeric references remain intact,but its binding/channel/world route is still unfinished;no live bound creature is claimed.',
            'No new socket input coverage or old numeric/source oracle rerun;prior27input and separate formula/birth proofs retain their historical scopes.',
            'BloodOath reaction35/BloodBoil do160 remain separate gaps. Current engine/global caller/curve/recipient/property/PvP/client and whole-class certification remain open.',
            'Remaining13Warlock talents plusAmazonSlowMissiles and separate final effects/icons remain unfinished. Mastery needs its passive tree icon only.'],inputs=inputs)
    path=ROOT/'tools/reference/demonic-mastery-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Demonic Mastery evidence stale'
    else:path.write_text(encoded)
    print('Demonic Mastery:C1/I1/S1/N1,15markers/10scripted summon transactions/6original bodies/2reloads;226handlers/Warlock17;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
