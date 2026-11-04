#!/usr/bin/env python3
"""Bind the approved Creeper curve, actual poison and mixed-profile reloads."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/creeper-balance'
PARENT='48caf8f64f0be5ea2493a1fd753bf6fcb69a425c'
sha=inventory.sha
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))

def record(check=False):
    inventory.STAGE=STAGE
    native,audit=[inventory.validate_inventory(STAGE/n)for n in('stats-inputs.json','audit-inputs.json')]
    for n in('stats-inputs.json','audit-inputs.json'):snapshot(STAGE/'pre-build-2.json',STAGE/n)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    current=load(STAGE/'pre-build-2.json')['inputs'];first=load(STAGE/'pre-build-1.json')['inputs']
    assert first['sourceInputs']==current['sourceInputs']and first['projectInputs']==current['projectInputs']
    prior=load(ROOT/'.local/class-skills/lightning-strike/pre-build-10.json')['inputs']
    sources={'src/Sanctuary.Content/DruidCreeperPoison.cs','tests/Sanctuary.Integration/CreeperPoisonChecks.cs','tests/Sanctuary.Integration/CreeperBalanceChecks.cs'}
    assert {p for p in set(prior['sourceInputs'])|set(current['sourceInputs'])if prior['sourceInputs'].get(p)!=current['sourceInputs'].get(p)}==sources
    assert prior['projectInputs']==current['projectInputs']
    assert {p for p in current['copiedData']if current['copiedData'][p]!=prior['copiedData'].get(p)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    parent=historical('tools/reference/lightning-strike-native-evidence.json')
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==parent['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    before=historical('data/Sanctuary/Balance/class-skill-status.json');after=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    assert {x['talentId']:[k for k in x if x[k]!=y[k]]for x,y in zip(before['skills'],after['skills'],strict=True)if x!=y}=={'druid.poisoncreeper':['remaining']}
    assert not after['completedClasses']and sum(s['implementation']=='handler-present'for s in after['skills'])==192
    expected=Counter({name+'-check':1 for name in('creeper-balance','vine-cast-windup','recycler-casting','creeper-ai','creeper-stats','creeper-fields','raven-stats','raven-flight','toward-path','valkyrie-creation','named-gear')})
    for family in('creeper-balance','vine-cast-windup','recycler-casting','spirit-wolf-world','creeper-body','creeper-world','creeper-ground','creeper-poison','creeper-casting'):
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    for name in('creeper-world-live-check','creeper-poison-live-result','creeper-fields-live-result','creeper-world-live-result','vine-cast-windup-live-check','vine-cast-windup-live-result'):expected[name]=3
    assert sum(expected.values())==53 and markers(STAGE/'stats-native-checks.log')==expected
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ': failed'not in log and ' ERR]'not in log and log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for word in('3636 complete poison creator','9180 do115/passive','7400complete getter/callback vectors and7375actual mat steps'):
        assert word in log
    saved=load(STAGE/'creeper-balance-fixtures.json');assert saved['Generation']==2 and saved['Life']%256!=0 and saved['Mana']%256!=0
    birth=saved['BodyState']['Snapshot'];assert birth['Birth']['Rank']==21 and birth['RabiesHardRank']==1
    assert len(saved['Grounds'])==2 and {g['PoisonContext']['Profile']for g in saved['Grounds']}=={'sanctuary-creeper-base-2000-v1','birth-owner-rabies-no-player-gear-provisional-v1'}
    for g in saved['Grounds']:
        assert g['Owner']==saved['Owner']and g['Actor']==saved['Body']and g['Fault']is None and len(g['Fields']['Mats'])>0
        expected_range=(5799,6413)if g['Rank']==21 else(768,848)
        assert all((m['Poison']['Damage']['Minimum256'],m['Poison']['Damage']['Maximum256'])==expected_range and m['Poison']['Damage']['Frames']==100 for m in g['Fields']['Mats'])
        if g['Rank']==20:assert g['Fields']['Mats'][0]['Remaining']==598
    assert saved['Poison']['PoisonSource']==saved['Owner']and saved['Poison']['PoisonMs']==3920 and saved['Poison']['PoisonClock']['Rate256']in range(4867,5373)
    source=load(ROOT/'tools/reference/creeper-components-source-evidence.json')
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']and source['fixture']['damage']==7272
    for p,h in source['generatedSourceHashes'].items():assert sha(ROOT/p)==h,p
    assert sha(STAGE/'creeper-components-before.json')==source['fixture']['sha256']
    for name in('creeper-damage','creeper-birth'):assert not(ROOT/'.local/class-skills'/(name+'-sanitizer.log')).read_text()
    assert '7272complete poison creator/getter/writer and9185complete do115/passive birth cases'in(STAGE/'components-source-check.log').read_text()
    decision=load(ROOT/'tools/reference/creeper-balance-decision.json');assert decision['approved']and decision['normalization']['rank20AverageTotal']==2000
    for kind in('content','integration'):
        assert all(w in(STAGE/f'{kind}-build-2.log').read_text()for w in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in(('named-item-runtime-audit.json',1021),('first-loop-affix-audit.json',21)):
        fresh=load(STAGE/n);old=historical('tools/reference/'+n);assert fresh==load(ROOT/'tools/reference'/n)and len(fresh['InputHashes'])==count
        for p,h in fresh['InputHashes'].items():assert sha(ROOT/p)==h,p
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    for n,word in(('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert word in(STAGE/n).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in(('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in sources),ROOT/'tools/build_skill_roadmap.py',ROOT/'tests/check_creeper_balance.py',ROOT/'tests/check_creeper_stats.py',
        *(ROOT/'tools/reference'/n for n in('creeper-balance-decision.json','creeper-components-source-evidence.json','creeper-frame-source-evidence.json','creeper-preflight.json')),
        *(ROOT/'docs'/n for n in('creeper-balance-checkpoint.md','pet-adjustment-review.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(p for p in STAGE.iterdir()if p.is_file()and p.name!='execution-state.json'and not p.name.startswith('receipt-'))]
    files.extend(ROOT/p for p in source['generatedSourceHashes'])
    files.extend(ROOT/'.local/class-skills'/(name+suffix)for name in('creeper-damage','creeper-birth')for suffix in('-sanitizer.log','-source-probe'))
    files.append(ROOT/'tools/probe_creeper_components_reference.py')
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,installed=False,goalComplete=False,completedClasses=[],
        component='Approved proportional Poison Creeper base curve before unchanged sole Rabies synergy; legacy/fresh poison profiles',
        builds=dict(content=2,integration=2),nativeRun=1,auditRun=1,
        verified=dict(nativeMarkers=53,boots=3,actualReloads=2,saves=3,baseAverageRank20=2000,frames=100,onlySynergy='Rabies10percentPerHardPoint',
            rawSourceComparisons=3636,sourceBirthComparisons=9180,frameVectors=7400,matSteps=7375,originalMixedProfilesAndPoisonResume=True,
            realSoftRank21HardRabies1=True,naturalPreparedVineScenes=3,pythonTests=71,handlers=192,auditFingerprints=[1021,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,clientPatchesUnchanged=40),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['This is user-approved Sanctuary balance, not an altered D2R reference table or modern executable certificate.',
            'Historical and missing-profile contexts retain raw source damage; saved emitted field packets and running poison are not reinterpreted. Mixed historical-profile fields are explicitly constructed controls, not an actual older-writer migration run.',
            'Birth-time hard Rabies/no-owner-gear transport remains provisional. Exact surface vulnerability windows and current poison transport remain unresolved; learned Poison Creeper stays gated.',
            'Saved original field/poison observation clocks are explicitly held. Inactive field aging and original poison resumption are checked through two actual reloads; no unheld concurrent-save claim.',
            'No class completion, native/client patch, deployment or final asset. Effects/audio and icons remain separate.'])
    out=ROOT/'tools/reference/creeper-balance-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==render,'Stale Creeper balance acceptance receipt'
    else:out.write_text(render)
    print('Creeper balance:rank20 base average2000/4sec,sole Rabies synergy,53 native markers/3boots/2reloads;192 handlers,learned vine exposure still gated;installed build and saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
