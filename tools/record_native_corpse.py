#!/usr/bin/env python3
"""Bind explicit native corpse classification and Necromancer consumer checks."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/native-corpse';P='3feb84d60abb0d9138ca6cb57f6229047e09759f'

def record(check=False):
    inputs={}
    def pin(p):
        p=R/p if isinstance(p,str)else p;inputs[str(p.relative_to(R))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');now=snapshot(S/'source-snapshot-9.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-9.json')
    content=pin(S/'source-snapshot-1.json')['inputs'];assert content['projectInputs']==now['projectInputs']and content['copiedData']==now['copiedData']
    assert{n for n in content['sourceInputs']if content['sourceInputs'][n]!=now['sourceInputs'][n]}=={'tests/Sanctuary.Integration/NativeCorpseChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    old=pin(R/'.local/class-skills/bone-armor-rule/birth-inputs.json');a={n:h for n,h in old['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/NativeCorpseSources.cs','tests/Sanctuary.Integration/NativeCorpseChecks.cs'}and not a.keys()-b.keys()
    assert{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/'+n+'.cs'for n in('CorpseBaseSnapshots','CorpseSkillRules','ReviveCasts')}
    assert born['copiedData'].keys()-old['copiedData'].keys()=={'Data/Sanctuary/Balance/native-corpse-sources.json'}and all(born['copiedData'][n]==h for n,h in old['copiedData'].items())
    for kind,number in(('content',1),('integration',9)):
        p=S/f'{kind}-build-{number}.log';pin(p);assert all(x in p.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==old['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    base=pin('tools/reference/bone-armor-rule-native-evidence.json');expected=Counter({n:v for n,v in base['markers'].items()if n.startswith(('iron-golem-birth-','iron-golem-items-','npc-resource-callbacks-'))})
    for family in('native-corpse','corpse-base'):expected.update({family+'-check':1,family+'-reload-check':2})
    expected.update({'corpse-base-result':3})
    for mode in('ce-right','poison-quick'):expected.update({f'native-corpse-{mode}-check':3,f'native-corpse-{mode}-result':3})
    log=e.cohort(S,expected,30);pin(S/'birth-native-checks.log');pin(S/'native-run-8.log');assert log.count('Client: 127.0.0.1: Connected.')==6
    for mode in('ce-right','poison-quick'):assert log.count(f'native-corpse-{mode}: windup')==3
    saved=pin(S/'native-corpse-fixtures.json');assert saved['Generation']==2 and saved['Metadata']['Source']=='sanctuary.native.graveyard-lich.v1'
    assert saved['Metadata']['BaseLife']['Base256']==23040 and saved['Metadata']['BaseLife']['Level']==5 and saved['Metadata']['ReviveSnapshotIssue']and saved['Metadata']['ReviveCreature']is None
    assert len(pin(S/'corpse-base-fixtures.json')['Rows'])==4
    for run in range(1,8):
        failed=pin(S/f'failed-run-{run}/birth-inputs.json');pin(S/f'failed-run-{run}/birth-native-checks.log')
        assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in('src/Sanctuary.Content/NativeCorpseSources.cs','src/Sanctuary.Content/CorpseBaseSnapshots.cs','src/Sanctuary.Content/CorpseSkillRules.cs','src/Sanctuary.Content/ReviveCasts.cs',
        'data/Sanctuary/Balance/native-corpse-sources.json','tests/Sanctuary.Integration/NativeCorpseChecks.cs','tests/check_native_corpse.py','tools/record_native_corpse.py',
        'docs/native-corpse-sources.md','docs/native-corpse-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/necromancer-mechanical-audit.md','docs/necromancer-coverage-matrix.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Explicit native Graveyard Lich corpse classification and Necromancer consumers',handlers=236,completedClasses=[],
        contentBuild=1,integrationBuild=9,contentSourceSnapshot=1,sourceSnapshot=9,nativeRun=8,nativeMarkers=30,markers=dict(expected),authenticatedSessions=6,actualInputs=6,
        inputCases=dict(nativeCorpseExplosionRight=3,nativePoisonExplosionQuick=3),boots=3,reloads=2,saves=3,nativeTypes=['Sanctuary.GraveyardLich'],fakeD2SpeciesAssigned=False,
        artifacts=born['artifacts'],preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Real player-killed Lich corpses capture authored level5/base90/original serial/difficulty/seed without a D2species identity;repeat capture does not reroll.',
            'Public RaiseSkeleton/Mage/CorpseExplosion/PoisonExplosion dispatch verifies one payment,consumed corpse and conserved loot;the raise paths are regressions,not new skill handlers.',
            'Six real corpse-target socket casts produce automatic CorpseExplosion/PoisonExplosion life loss with once-only cost and retained loot.',
            'Original fresh corpse metadata/base/seed/loot/availability survive two reloads;existing mappedD2corpse bounds/capture/save fixture retains its cached52100controls.',
            'Native Revive returns its explicit technical blocker without paying/reserving;unassigned flesh classification remains unassigned.'],
        limits=['Only exact native GraveyardLich is classified;unknown types/subclasses and historical unclassified corpses are not backfilled.',
            'Native Lich Revive still needs native identity/eligibility plus raised-helper ownership and lifecycle support;existing eligible Revives retain living abilities.',
            'FindPotion/GrimWard flesh classification remains unknown;broader shared-consumer/native-world/PvP/class/graphical acceptance is not claimed.',
            'No whole-class completion or new final assets;four older Warlock proposals remain frozen.'],inputs=inputs)
    p=R/'tools/reference/native-corpse-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==encoded
    else:p.write_text(encoded)
    print('Native corpse C1/I9/S9/N8:30markers/6actual casts/2reloads;native Lich CE/Poison supported,Revive/flesh gaps explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
