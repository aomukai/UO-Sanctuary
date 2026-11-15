#!/usr/bin/env python3
"""Bind native corpse difficulty bases to authored variants and real casts."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/native-corpse-difficulties';P='413c15dfb863dae0fbdffa6f47f29de9485a5694'

def record(check=False):
    inputs={}
    def pin(path):
        path=R/path if isinstance(path,str)else path;inputs[str(path.relative_to(R))]=e.sha(path)
        return e.load(path)if path.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');current=snapshot(S/'source-snapshot-2.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-2.json')
    content=pin(S/'source-snapshot-1.json')['inputs']
    assert content['projectInputs']==current['projectInputs']and content['copiedData']==current['copiedData']
    assert{n for n in content['sourceInputs']if content['sourceInputs'][n]!=current['sourceInputs'][n]}=={'tests/Sanctuary.Integration/NativeCorpseChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    parent=pin(R/'.local/class-skills/raised-minions/birth-inputs.json')
    a={n:h for n,h in parent['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'tests/Sanctuary.Integration/NativeCorpseDifficultyChecks.cs'}and not a.keys()-b.keys()
    assert{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/NativeCorpseSources.cs','tests/Sanctuary.Integration/NativeCorpseChecks.cs'}
    assert born['copiedData'].keys()==parent['copiedData'].keys()
    assert{n for n in born['copiedData']if born['copiedData'][n]!=parent['copiedData'][n]}=={'Data/Sanctuary/Balance/native-corpse-sources.json'}
    for kind,num in(('content',1),('integration',2)):
        path=S/f'{kind}-build-{num}.log';pin(path);assert all(x in path.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    native=pin('data/Sanctuary/Balance/native-corpse-sources.json');assert len(native)==1
    row=native[0];assert(row['level'],row['minimumLife'],row['maximumLife'])==(5,90,90)
    authored=pin('data/Sanctuary/Balance/creature-difficulties.json')
    def find(value):
        if isinstance(value,dict):
            if 'Sanctuary.GraveyardLich'in value.get('types',[]):return[value]
            return sum((find(v)for v in value.values()),[])
        return sum((find(v)for v in value),[])if isinstance(value,list)else[]
    matches=find(authored);assert len(matches)==1;variants=matches[0]['variants']
    assert len(row['difficultyBases'])==2
    for v in row['difficultyBases']:
        basis=variants[v['difficulty']];assert basis['enabled']and basis['applyStats']
        assert(v['level'],v['minimumLife'],v['maximumLife'])==(basis['level'],basis['life'],basis['life'])
    prior=pin('tools/reference/raised-minions-native-evidence.json');expected=Counter(prior['markers'])
    expected.update({'native-corpse-difficulties-check':1,'native-corpse-difficulties-reload-check':2})
    for mode in('nightmare-ce-right','hell-poison-quick'):expected.update({f'native-corpse-{mode}-check':3,f'native-corpse-{mode}-result':3})
    log=e.cohort(S,expected,54);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log')
    failed=pin(S/'failed-run-1/birth-inputs.json');pin(S/'failed-run-1/birth-native-checks.log')
    assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert log.count('Client: 127.0.0.1: Connected.')==15 and log.count('raise animations started')==3
    for mode in('ce-right','poison-quick','nightmare-ce-right','hell-poison-quick'):assert log.count(f'native-corpse-{mode}: windup')==3
    saved=pin(S/'native-corpse-difficulties-fixtures.json');assert saved['Generation']==2
    assert[(r['BaseLife']['Difficulty'],r['BaseLife']['Level'],r['BaseLife']['Base256'])for r in saved['Corpses']]==[(0,5,90*256),(1,36,1102*256),(2,69,9118*256)]
    assert all(r['BaseLife']['Seed']is not None and r['ReviveSnapshotIssue']and r['ReviveCreature']is None for r in saved['Corpses'])
    assert pin(S/'native-corpse-fixtures.json')['Generation']==pin(S/'raised-minions-fixtures.json')['Generation']==2
    assert len(pin(S/'corpse-base-fixtures.json')['Rows'])==4
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in('src/Sanctuary.Content/NativeCorpseSources.cs','tests/Sanctuary.Integration/NativeCorpseChecks.cs','tests/Sanctuary.Integration/NativeCorpseDifficultyChecks.cs',
        'tests/check_native_corpse_difficulties.py','tools/record_native_corpse_difficulties.py','docs/native-corpse-sources.md','docs/native-corpse-difficulties-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Native Lich corpse difficulty capture and explosion consumers',handlers=236,completedClasses=[],
        contentBuild=1,integrationBuild=2,contentSourceSnapshot=1,sourceSnapshot=2,nativeRun=2,nativeMarkers=54,markers=dict(expected),authenticatedSessions=15,
        actualInputs=12,inputCases=dict(normalCorpseExplosion=3,normalPoisonExplosion=3,nightmareCorpseExplosion=3,hellPoisonExplosion=3),
        ownedLichRaiseControls=3,wildLichRaiseControls=3,boots=3,reloads=2,saves=3,artifacts=born['artifacts'],
        authoredBases=dict(Normal=[5,90],Nightmare=[36,1102],Hell=[69,9118]),nativeLichReviveAdmitted=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,
        clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),
        frozenPendingFiles=frozen,verified=['Actual player-killed Normal,Nightmare,Hell Liches capture the existing variant level/base and independent original serial/seed.',
            'All three difficulty corpses support public CE/Poison release,once-only payment,consumption and conserved gold;CE actual damage verified.',
            'Twelve actual socket casts include Nightmare CE and Hell Poison automatic life loss plus retained Normal controls.',
            'Three original unused difficulty corpses preserve exact metadata/base/seed/loot/availability through two reloads;native Revive remains refused.',
            'Existing owned/wild raises,parked fractional helper life,native corpse and cached mapped-D2 corpse controls retain.'],
        limits=['Only exact native GraveyardLich is classified;unknown native species,levels or missing difficulties remain unsupported.',
            'First cohort timed out on a retained Normal CE input after reload;cause unassigned. Diagnostic-only I2 and unchanged C1 passed N2;this does not establish a fix for that timeout.',
            'Native Lich Revive eligibility/original activation/combat/facet admission and the prepared helper cleanup adaptation remain unfinished.',
            'Earlier unscaled-native-role documentation was incorrect and is corrected from existing authored difficulty data;no new D2 species or AI mapping is inferred.',
            'No full class,PvP,graphical or broader monster conversion acceptance;four older Warlock proposals remain frozen.'],inputs=inputs)
    path=R/'tools/reference/native-corpse-difficulties-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Native corpse difficulties C1/I2/S2/N2:54markers/15connections/12actual inputs/2reloads;existing Normal,Nightmare,Hell bases;Revive remains refused.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
