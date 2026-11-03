#!/usr/bin/env python3
"""Bind the saved prepared Goatman body and retained source supplier controls."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/goatman-body'
PARENT='92fbf1045d9e934d0048fbcec63d0339ad4f0290'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-3.json',STAGE/'birth-inputs.json')
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==15
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('goatman-body','goatman-foundation'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'goatman-body-fixtures.json').read_text());assert len(saved)==2 and {s['Female'] for s in saved}=={False,True}
    for s in saved:assert all(s[k]>0 for k in ('Owner','Body','Helm','Mana','Maximum')) and s['Life']==s['Maximum']-257 and s['State']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',922),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-3.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert len(old['skills'])==len(status['skills'])==240 and [a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b]==['warlock.summongoatman']
    previous=json.loads((ROOT/'.local/class-skills/vine-cast-windup/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/'+name+'.cs' for name in ('GoatmanPrepared','CreatureProfiles','WarlockGoatmanBody')}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    early=STAGE/'pre-incoming-acceptance-1';assert sha(early/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
    text=(early/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==15
    files=[Path(__file__).resolve(),ROOT/'tests/check_goatman_body.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/goatman-source-evidence.json',
        *(ROOT/'docs'/name for name in ('goatman-body-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','content-build-1.log','pre-build-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-3.log','integration-build-3.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','goatman-body-fixtures.json','goatman-foundation-fixtures.json')),
        *(p for p in early.rglob('*') if p.is_file())]
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Prepared saved Goatman life/ownership body verified; automatic GenericPet world/combat and learned skill remain gated.',
        verified=dict(nativeMarkers=15,boots=3,reloads=2,saves=3,originalBodies=2,retainedRawSupplierContextsPerBoot=30600,retainedFogSourceContextsPerBoot=3570,retainedBaseSourceContextsPerBoot=7650,retainedBaseNativeComparisonsPerBoot=3825,retainedDo56SourceContextsPerBoot=3456,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Body factory is internal, spends no mana and performs no demon cap replacement; no learned cast, moving actor or automatic attack/skill/event consumer is enabled.',
            'Actual health-service damage/heal is verified separately from full incoming hit/mitigation/block/event/return chronology. Recovery is an explicit base-life source projection,not a modern executable trace.',
            'Saved recovery inspection deadlines are held explicitly. Nonzero duplicated sunder and unsupported automatic melee requests refuse rather than inventing behavior.',
            'No fresh full1180class run, deployment, save reset, final asset approval or completed class is claimed; current ordinary source/world/AI/client and other Warlock talents remain unfinished.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/goatman-body-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Prepared Goatman body:15native markers/3boots/2reloads,2original Warlock/Witch bodies,source suppliers retained;71Python;191handlers/learned Goatman gated;106installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
