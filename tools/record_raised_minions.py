#!/usr/bin/env python3
"""Bind native raised-helper ownership to focused world/save evidence."""
import argparse,json,hashlib,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
R=e.ROOT;S=R/'.local/class-skills/raised-minions';P='69a70e27cf97bd24360c947eef56762500dd398e'

def record(check=False):
    inputs={}
    def pin(path):
        path=R/path if isinstance(path,str)else path;inputs[str(path.relative_to(R))]=e.sha(path)
        return e.load(path)if path.suffix=='.json'else None
    born=pin(S/'birth-inputs.json');current=snapshot(S/'source-snapshot-4.json',S/'birth-inputs.json')['inputs'];pin(S/'source-snapshot-4.json')
    content=pin(S/'source-snapshot-2.json')['inputs']
    assert content['projectInputs']==current['projectInputs']and content['copiedData']==current['copiedData']
    assert{n for n in content['sourceInputs']if content['sourceInputs'][n]!=current['sourceInputs'][n]}=={'tests/Sanctuary.Integration/RaisedMinionChecks.cs'}
    for n,h in born['sourceInputs'].items():assert e.sha(R/n)==h,n
    parent=pin(R/'.local/class-skills/native-corpse/birth-inputs.json')
    a={n:h for n,h in parent['sourceInputs'].items()if n.endswith('.cs')};b={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    assert b.keys()-a.keys()=={'src/Sanctuary.Content/RaisedMinions.cs','tests/Sanctuary.Integration/RaisedMinionChecks.cs'}and not a.keys()-b.keys()
    assert{n for n in a if a[n]!=b[n]}=={'src/Sanctuary.Content/'+n+'.cs'for n in('GraveyardUndead','MinionWardrobeProfiles','MonsterVariants','SummonDifficulties')}
    assert born['copiedData']==parent['copiedData']
    for kind,num in(('content',2),('integration',3)):
        path=S/f'{kind}-build-{num}.log';pin(path);assert all(x in path.read_text()for x in('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(S/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(S/'native/Distribution'/n)
    prior=pin('tools/reference/native-corpse-native-evidence.json');expected=Counter(prior['markers'])
    expected.update({'raised-minions-check':1,'raised-minions-reload-check':2,'raised-minions-live-check':3,'raised-minions-live-result':3})
    log=e.cohort(S,expected,39);pin(S/'birth-native-checks.log');pin(S/'native-run-2.log')
    assert log.count('Client: 127.0.0.1: Connected.')==9 and log.count('raise animations started')==3
    for mode in('ce-right','poison-quick'):assert log.count(f'native-corpse-{mode}: windup')==3
    saved=pin(S/'raised-minions-fixtures.json');assert saved['Generation']==2 and saved['Life256']>0 and len({saved[n]for n in('Owner','Caster','Child')})==3
    assert pin(S/'native-corpse-fixtures.json')['Generation']==2 and len(pin(S/'corpse-base-fixtures.json')['Rows'])==4
    failed=pin(S/'failed-run-1/birth-inputs.json');pin(S/'failed-run-1/birth-native-checks.log')
    assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    pin(S/'failed-integration-1/integration-build-1.log');pin(S/'failed-integration-1/RaisedMinionChecks.cs')
    frozen=[]
    for stem,proposal in(('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in(f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(R/n)==hashlib.sha256(subprocess.check_output(['git','show',P+':'+n],cwd=R)).hexdigest();frozen.append(n)
    for n in('src/Sanctuary.Content/RaisedMinions.cs','src/Sanctuary.Content/GraveyardUndead.cs','src/Sanctuary.Content/MinionWardrobeProfiles.cs','src/Sanctuary.Content/MonsterVariants.cs','src/Sanctuary.Content/SummonDifficulties.cs',
        'tests/Sanctuary.Integration/RaisedMinionChecks.cs','tests/check_raised_minions.py','tools/record_raised_minions.py','docs/raised-minions-checkpoint.md','docs/native-corpse-sources.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/necromancer-coverage-matrix.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    result=dict(referenceVersion='3.3.93847',component='Persistent owned Lich raised-helper ownership prerequisite',handlers=236,completedClasses=[],nativeLichReviveAdmitted=False,
        contentBuild=2,integrationBuild=3,contentSourceSnapshot=2,sourceSnapshot=4,nativeRun=2,nativeMarkers=39,markers=dict(expected),authenticatedSessions=9,
        actualInputs=6,ownedLichRaiseControls=3,wildLichRaiseControls=3,reloads=2,boots=3,saves=3,artifacts=born['artifacts'],
        preservation=e.protection('tools/reference/slow-missiles-learned-client-evidence.json',44),newTimers=0,newPersistenceProfiles=['SanctuaryRaisedMinions'],
        existingSerializationVersionsChanged=False,clientRebuilt=False,nativeRebuilt=False,newLiveReferenceReads=0,installed=False,frozenPendingFiles=frozen,
        verified=['Actual delayed native Lich raises create player-allied dependent helpers;wild Lich controls retain unowned raise behavior.',
            'Actual corpse consumption retains protected gold;helper allegiance accepts wild enemies and rejects its player.',
            'Nightmare/Normal helper projection uses existing variants and preserves current life percentage.',
            'Parking/resume preserves wounds;actual caster deletion removes its helpers without deleting another caster\u2019s helper.',
            'Original player/caster/child identities and fractional wounds survive two reloads parked offline without expiry.',
            'Six retained CorpseExplosion/PoisonExplosion inputs and mapped/native corpse save controls pass.'],
        limits=['Native Lich Revive eligibility,original capture/activation,combat/facet admission remain unfinished;the fixture parent behavior is test-only.',
            'Dependent helper cleanup is a prepared Sanctuary mapping for the complete native Lich Revive proposal,not a newly observed or implicitly approved D2R rule.',
            'The first native run timed out before raising;cause unassigned.Only diagnostics changed before the unchanged gameplay build passed the full cohort.',
            'No full class,PvP,graphical,broader native-species or monster-conversion certification;four older Warlock proposals remain frozen.'],inputs=inputs)
    path=R/'tools/reference/raised-minions-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Raised helpers C2/I3/S4/N2:39markers/9connections/3owned+3wild raises/6retained inputs/2reloads;native Lich Revive remains unadmitted.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
