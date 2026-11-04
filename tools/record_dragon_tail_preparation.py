#!/usr/bin/env python3
"""Bind approved Tail policy, source lifetime, native hit/clock and regressions."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
from snapshot_skill_build import check as snapshot
from record_poison_dagger_contact import markers

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/dragon-tail-preparation'
BROADER=ROOT/'.local/class-skills/dragon-tail-preparation-broader'
PARENT='65ea5b74716177522e24ddae7ac41a39bc91a7d4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))

def build(check=False):
    narrow=load(STAGE/'birth-inputs.json');wide=load(BROADER/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    for tested in (STAGE/'birth-inputs.json',BROADER/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-4.json',tested)
    assert narrow['artifacts']==wide['artifacts']==audit['artifacts']
    assert narrow['copiedData']==wide['copiedData']==audit['copiedData']
    before=load(STAGE/'pre-build-1.json')['inputs'];after=load(STAGE/'pre-build-2.json')['inputs']
    assert before['sourceInputs']==after['sourceInputs']and before['projectInputs']==after['projectInputs']
    assert {p for p in before['copiedData']if before['copiedData'][p]!=after['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    first=load(STAGE/'first-runtime-inputs/birth-inputs.json')
    assert first['artifacts']==load(STAGE/'second-runtime-inputs/birth-inputs.json')['artifacts']
    for name in ('content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'):
        key='Assemblies/'+Path(name).name;assert sha(STAGE/'verified-build-2'/name)==first['artifacts'][key]
    final=load(STAGE/'pre-build-3.json')['inputs']
    assert {p for p in after['sourceInputs']if after['sourceInputs'][p]!=final['sourceInputs'][p]}=={'src/Sanctuary.Content/ClassStats.cs'}
    assert after['copiedData']==final['copiedData']and after['projectInputs']==final['projectInputs']
    accepted=load(STAGE/'pre-build-4.json')['inputs']
    assert {p for p in final['sourceInputs']if final['sourceInputs'][p]!=accepted['sourceInputs'][p]}=={'tests/Sanctuary.Integration/DeathLearnedChecks.cs'}
    assert final['copiedData']==accepted['copiedData']and final['projectInputs']==accepted['projectInputs']
    failed=load(BROADER/'failed-run-2/birth-inputs.json')
    for name in ('content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'):
        assert sha(STAGE/'verified-build-3'/name)==failed['artifacts']['Assemblies/'+Path(name).name]
    assert 'Original Death resume fault'in(BROADER/'failed-run-2/birth-native-checks.log').read_text()
    old=historical('tools/reference/wake-learned-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert narrow['artifacts'][name]==old['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    production={'src/Sanctuary.Content/'+p+'.cs'for p in ('ClassStats','DragonTailPreparation')}
    fixture='tests/Sanctuary.Integration/DragonTailPreparationChecks.cs'
    parent=load(ROOT/'.local/class-skills/wake-learned/pre-build-3.json')['inputs']
    assert {p for p in set(parent['sourceInputs'])|set(after['sourceInputs'])if parent['sourceInputs'].get(p)!=after['sourceInputs'].get(p)}==production|{fixture}
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('dragon-tail-preparation','dragon-kick','kick-packet','tiger-strike'):
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert sum(expected.values())==21 and markers(STAGE/'birth-native-checks.log')==expected
    import sys
    sys.path.insert(0,str(ROOT/'tools'))
    from record_wake_learned import expected as wake_expected
    combined=wake_expected()
    for k,v in expected.items():
        if k.startswith(('iron-golem','npc-resource')):continue
        combined[k]+=v
    assert sum(combined.values())==237 and markers(BROADER/'birth-native-checks.log')==combined
    death=load(BROADER/'death-learned-fixtures.json');failed_death=load(BROADER/'failed-run-2/death-learned-fixtures.json')
    assert death['Generation']==2 and death['Bolt']['Vy']==0 and death['Bolt']['Fault']is None
    assert failed_death['Generation']==1 and failed_death['Bolt']['Vy']<0
    for stage in (STAGE,BROADER):
        log=(stage/'birth-native-checks.log').read_text()
        assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
        saved=load(stage/'dragon-tail-preparation-fixtures.json')
        assert saved['Charge']['Owner']==saved['Owner']and saved['Charge']['Count']==3 and saved['Mana256']>0
        assert saved['Tail']['EffectFrame']>0 and saved['Tail']['Frames']>=saved['Tail']['EffectFrame']
    for log in ('content-build-1.log','content-build-2.log','content-build-3.log','integration-build-1.log','integration-build-2.log','integration-build-3.log','integration-build-4.log'):
        assert all(s in(STAGE/log).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1022),('first-loop-affix-audit.json',21)):
        current=load(STAGE/name);previous=historical('tools/reference/'+name)
        assert current==load(ROOT/'tools/reference'/name)and len(current['InputHashes'])==count
        for p,h in current['InputHashes'].items():assert sha(ROOT/p)==h,p
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items()if k not in omit}=={k:v for k,v in previous.items()if k not in omit}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    previous=historical('data/Sanctuary/Balance/class-skill-status.json');current=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId']:[k for k in x if x[k]!=y[k]]for x,y in zip(previous['skills'],current['skills'],strict=True)if x!=y}=={'assassin.dragontail':['remaining']}
    assert not current['completedClasses']and sum(x['implementation']=='handler-present'for x in current['skills'])==193
    decision=load(ROOT/'tools/reference/dragon-tail-sanctuary-decision.json')
    assert decision['decision']=='approved Sanctuary adaptation'and not decision['isD2RObservation']
    source=load(ROOT/'tools/reference/dragon-tail-lifetime-source-evidence.json');f=source['fixture'];assert f['cases']==384
    assert sha(ROOT/f['path'])==f['sha256']and sha((ROOT/f['path']).with_suffix('.cpp'))==f['cppSha256']
    for p,h in source['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/p)==h
    for name,phrase in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert phrase in(STAGE/name).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in ('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in production|{fixture,'tests/Sanctuary.Integration/DeathLearnedChecks.cs'}),
           *(ROOT/'tools'/p for p in ('probe_dragon_tail_lifetime_reference.py','prepare_dragon_tail_reference.py','build_skill_roadmap.py')),
           ROOT/'tests/check_dragon_tail_preparation.py',
           *(ROOT/'tools/reference'/p for p in ('dragon-tail-sanctuary-decision.json','dragon-tail-lifetime-source-evidence.json','dragon-tail-preflight.json')),
           *(ROOT/'docs'/p for p in ('dragon-tail-preparation-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
           ROOT/f['path'],(ROOT/f['path']).with_suffix('.cpp')]
    for stage in (STAGE,BROADER):
        files.extend(p for p in stage.rglob('*')if p.is_file()and not p.is_symlink()and p.suffix in ('.json','.log')and p.name!='execution-state.json'and not any(x in ('content','integration','native','runtime-root','upstream')for x in p.relative_to(stage).parts))
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',scope='Approved policy and prepared hit/KK suppliers only; no learned action or class completion',
        installed=False,handlerEnabled=False,diagnostics=['Broader run2 failed an existing Death Sentry original-bolt resume assertion. Its differing moving-target trajectory left the narrow controlled corridor; full failed inputs/logs/build3 binaries are retained. The fixture now freezes its target and reports full live fault state; production trap code is unchanged. Failed run2 is not counted as a pass.'],handlers=193,assassinHandlers=23,completedClasses=[],artifacts=narrow['artifacts'],copiedData=narrow['copiedData'],
        cases=dict(completeOlderLifetime=384,completePositiveKkClocks=4480,originalCurrentFamilyRates=4608,focusedNativeMarkers=21,broaderNativeMarkers=237,bootsPerNativeRun=3,reloadsPerNativeRun=2,savesPerNativeRun=3,pythonTests=71,namedDefinitions=573),
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        remaining=['Learned action/start allocation/progressive/contact/fire composition,ten-mana release and one-charge consumption,loss-of-contact/death/interrupt/end/input/fallback and save acceptance remain to implement and verify.',
                   'Current executable event/mode ordering and overflow behavior remain unverified; no PvP or invented UO monster dodge admission.',
                   'Final effects/audio and talent/hotkey icons remain separate placeholders.'],
        evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))})
    path=ROOT/'tools/reference/dragon-tail-preparation-native-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Dragon Tail preparation receipt stale'
    else:path.write_text(encoded)
    print('Dragon Tail preparation: approved policy,384 source lifetime cases,4480/4608 native clock/rate controls,21/237 native markers,71 Python tests and protected installation verified; learned handler remains unfinished.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
