#!/usr/bin/env python3
"""Bind real Stealskull/Sentinel linked hits and retained Blade lifecycle evidence."""
import argparse,hashlib,json,subprocess,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE='0766ff8814f6d8077e798ff0e1c9384e24b0432c'
FOCUS=ROOT/'.local/class-skills/sentinel-stealskull-link'
STAGE=ROOT/'.local/class-skills/sentinel-stealskull-retained'
LABELS=('onehand','twohand','onehand-immune','twohand-immune')

def additions():
    return Counter({'sentinel-stealskull-'+n+s:3 for n in LABELS for s in ('-check','-assassin-check','-result')})

def archived(stage,born):
    with tarfile.open(stage/'accepted-assemblies-1.tar.gz')as a:
        for k in ('Content','Integration'):
            assert hashlib.sha256(a.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/amazon-lifecycle-acceptance/birth-inputs.json')
    assert born['artifacts']==e.load(FOCUS/'birth-inputs.json')['artifacts']
    for stage,snap,run in ((FOCUS,2,4),(STAGE,1,1)):
        snapshot(stage/f'source-snapshot-{snap}.json',stage/'birth-inputs.json');archived(stage,born)
        current=e.load(stage/'birth-inputs.json')
        for p,d in current['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
        assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
        for boot in range(3):
            rows=e.load(stage/f'sentinel-stealskull-boot-{boot}.json');assert len(rows)==4
            assert [r['Label'] for r in rows]==['sentinel-stealskull-'+n for n in LABELS]
            for r in rows:
                assert r['LifeDrain']==r['ManaDrain']==(1 if r['TwoHanded']else 3)
                assert r['Typed']and r['Fault']is None
                assert r['Direct']==r['PrimaryLoss']==sum(r['Known'].values())+r['LifeDrain']
                assert r['Shared']==r['SecondaryLoss']
                if r['Immune']:assert r['Shared']==sum(r['Known'].values())==0
                else:assert r['Shared']>0
    expected=Counter(e.load(ROOT/'tools/reference/blade-link-native-evidence.json')['markers'])+additions()
    log=e.cohort(STAGE,expected,147);assert log.count('Client: 127.0.0.1: Connected.')==39
    basic=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'hellslayer-readiness-check':1,'hellslayer-readiness-reload-check':2})
    focus=e.cohort(FOCUS,basic+additions(),48);assert focus.count('Client: 127.0.0.1: Connected.')==24
    assert 'Warning: Attempted to delete'not in log and log.count('Defiler link sharing refused unsupported context')==9
    wanted=sorted(['src/Sanctuary.Content/BladeSentinelMissiles.cs','tests/Sanctuary.Integration/BladeLinkChecks.cs','tests/Sanctuary.Integration/BladeStealskullLinkChecks.cs'])
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==wanted
    p=wanted[0];old=subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT,text=True)
    assert (ROOT/p).read_text()==old.replace('// Preserve scalar refusal while the extra flat NPC drain has no established channel.\n            int loss=state.Damage.Elements.LifeDrain>0?SkillHealth.Damage(body,target,direct):SkillHealth.TypedDamage(body,target,direct,DirectDamageParts.From(parts));','// Match ordinary NPC packets: retain flat drain in the primary\n            // total,while publishing only the five known settled components.\n            int loss=SkillHealth.TypedDamage(body,target,direct,DirectDamageParts.From(parts));',1)
    data='data/Sanctuary/Balance/class-skill-status.json';ledger=e.load(ROOT/data);old=json.loads(subprocess.check_output(['git','show',BASE+':'+data],cwd=ROOT))
    changed_rows=[b['talentId']for a,b in zip(old['skills'],ledger['skills'])if a!=b]
    assert changed_rows==['assassin.bladesentinel','assassin.bladefury','assassin.bladeshield']
    for a,b in zip(old['skills'],ledger['skills']):assert {k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}
    assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in ledger.items()if k!='skills'}and len(ledger['skills'])==240 and not ledger['completedClasses']
    assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==['Data/Sanctuary/Balance/class-skill-status.json']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d
    for p,d in born['artifacts'].items():
        if p not in ('Assemblies/Sanctuary.Content.dll','Assemblies/Sanctuary.Integration.dll'):assert parent['artifacts'][p]==d
    for kind,build in (('content',1),('integration',2)):
        assert all(t in(FOCUS/f'{kind}-build-{build}.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    old=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT))
    assert {k:v for k,v in old.items()if k not in ('InputHashes','ContentAssemblySha256')}=={k:v for k,v in named.items()if k not in ('InputHashes','ContentAssemblySha256')}
    assert sorted(p for p,d in named['InputHashes'].items()if old['InputHashes'].get(p)!=d)==[data,wanted[0]]
    from build_named_readiness import validate
    validate(named,assembly=FOCUS/'content/Sanctuary.Content.dll')
    negative=FOCUS/'reproduced-old-2.tar.gz'
    with tarfile.open(negative)as a:
        prefix=str(FOCUS.relative_to(ROOT))+'/'
        baseline=json.loads(a.extractfile(prefix+'birth-inputs.json').read())
        assert baseline['artifacts']['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert hashlib.sha256(a.extractfile(prefix+'content/Sanctuary.Content.dll').read()).hexdigest()==baseline['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert 'primary=16796/16796,shared=0/5340,typed=False,fault=Direct damage lacks settled typed components'in a.extractfile(prefix+'birth-native-checks.log').read().decode()
    paths=['tools/record_sentinel_stealskull_link.py','tests/check_sentinel_stealskull_link.py','tests/check_sentinel_stealskull_retained.py','tools/reference/named-item-runtime-audit.json','tools/reference/defiler-health-link-rule-approval.json','tools/reference/blade-sentinel-flat-drain-frontier.json','tools/build_skill_roadmap.py',data,'docs/sentinel-stealskull-link-checkpoint.md','docs/blade-sentinel-flat-drain-frontier.md','docs/assassin-coverage-matrix.md','docs/class-skill-roadmap.md','docs/class-skill-status-overview.md','docs/skill-effects.md','docs/skill-icons.md',*wanted]
    files=[*(ROOT/p for p in paths),*e.stage_files(FOCUS,STAGE),FOCUS/'accepted-assemblies-1.tar.gz',STAGE/'accepted-assemblies-1.tar.gz',*(FOCUS/(n+'.tar.gz')for n in ('failed-transport-1','reproduced-old-2','failed-candidate-transport-3'))]
    result=dict(component='Actual Stealskull Sentinel typed Health Link compatibility',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=147,markers=dict(sorted(expected.items())),focusedMarkers=48,retainedMarkers=111,newActualCoopCasts=12,retainedWireInputs=9,totalLearnedInputs=21,connections=39,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,productionCallsChanged=1,numericTablesChanged=False,nativeRebuilt=False,itemAdmissionChanged=False,persistenceVersionChanged=False,contentBuild=1,integrationBuild=2,focusedNativeRun=4,retainedNativeRun=1,transportFailuresFrozen=2,oldMechanicalFailureFrozen=True,intentionalGenericScalarWarnings=9,saveDeletionWarnings=0,classCertified=False,goalComplete=False,installed=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Existing approved Health Link copies only known components,matching ordinary NPC delivery;full primary flat drain and NPC leech preserved. Exact modern flat-drain semantics remain unverified. Real gear cases cover natural captures/contacts,not new saved co-op scene coverage. Retained original Blade actor/flight/controller/weapon/save scopes remain separately attributable. No new item admission,asset or full-class certificate.')
    e.write('sentinel-stealskull-link-native-evidence.json',result,files,check)
    print('Sentinel/Stealskull:147 retained+co-op results,48 focused,12 new casts,two reloads;full primary and NPC leech retained.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
