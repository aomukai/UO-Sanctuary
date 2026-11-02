#!/usr/bin/env python3
"""Bind learned CBS casting and Assassin mouse normalization to exact inputs."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sentry-learned'
PARENT='7c536f39e0e2b382344f5232bb32bc8b647fd872'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sentry_learned import configuration
    config=configuration();assert (len(config['extra_families']),len(config['extra_checks']),len(config['extra_target_input_commands']))==(8,4,3)
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==69
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in config['extra_families']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in config['extra_checks']:assert log.count(name+': passed')==3,name
    for name in config['extra_target_input_commands']:
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3,name
        event='acknowledged' if name.endswith('left') else 'windup'
        assert log.count(name+': '+event)==3,name
    for family in ('sentry-retirement','sentry-learned'):
        assert log.count(family+'-result: passed')==1 and log.count(family+'-reload-result: passed')==2
    saved=json.loads((STAGE/'sentry-learned-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Actor']['Creation']['Rank']==21 and saved['Actor']['Owner']==saved['Owner']
    assert saved['Bolt']['Actor']==saved['Body'] and saved['Bolt']['Owner']==saved['Owner'] and saved['Bolt']['Frame']>=4
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
    assert audit['sourceInputs']=={p:h for p,h in tested['sourceInputs'].items() if p.endswith('.cs')}
    text=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',915),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-2.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    negative=STAGE/'negative-mouse-policy-1';text=(negative/'birth-native-checks.log').read_text()
    assert 'sentry-learned-availability-check: failed' in text and 'Pinned right-only/quick binding policy' in text
    old=json.loads((negative/'birth-inputs.json').read_text())
    for path,filename in [('src/Sanctuary.Content/SkillBindings.cs','SkillBindings.cs'),('tests/Sanctuary.Integration/SentryLearnedChecks.cs','SentryLearnedChecks.cs')]:
        assert sha(negative/filename)==old['sourceInputs'][path]
    assert sha(negative/'content/Sanctuary.Content.dll')==old['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert sha(negative/'integration/Sanctuary.Integration.dll')==old['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert 'Stale class skill output:' in (STAGE/'catalogue-stale-generator.log').read_text()
    text=(STAGE/'python-tests-stale-generator.log').read_text();assert 'test_generated_products_are_current' in text and 'FAILED (failures=1)' in text
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==188
    assert sum(s['implementation']=='handler-present' and s['characterClass']=='Assassin' for s in status['skills'])==19
    row=next(s for s in status['skills'] if s['talentId']=='assassin.chargedboltsentry');assert row['verification']=='partial-native-coverage' and not row['complete']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/sentry-retirement/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+n for n in ('ActionTiming.cs','AssassinCombat.cs','AssassinSentryCasts.cs','ClassCombat.cs','SkillBindings.cs')}
    changed={p for p,h in tested['copiedData'].items() if prior['copiedData'].get(p)!=h}
    assert changed=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/skill-effects.json'}
    for path in changed:
        name='data/Sanctuary/'+path.removeprefix('Data/Sanctuary/');old=json.loads(historical(name));new=json.loads((ROOT/name).read_text())
        if isinstance(old,dict):old=old['skills'];new=new['skills']
        assert [a['talentId'] for a,b in zip(old,new) if a!=b]==['assassin.chargedboltsentry']
    files=[Path(__file__).resolve(),ROOT/'tests/check_sentry_learned.py',ROOT/'tests/check_sentry_retirement.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'docs'/n for n in ('assassin-sentry-learned-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','sentry-learned-fixtures.json','sentry-retirement-fixtures.json','content-build-2.log','integration-build-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','python-tests-stale-generator.log','catalogue-checks.log','catalogue-stale-generator.log'))]
    files.extend(negative/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','SkillBindings.cs','SentryLearnedChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll'))
    files.extend(config['extra_files'].values());files.extend(config['extra_inputs'])
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Learned right/quick Charged Bolt Sentry casting enabled with scoped native acceptance; no complete class certificate.',
        verified=dict(nativeCommandMarkers=69,boots=3,reloads=2,saves=3,wireRequests=9,successfulRightQuickCasts=6,rejectedLeftRequests=3,assassinMouseRows=30,bindingMigrationContexts=132,originalActorFlightResumes=2,auditMarkers=2,pythonTests=71,handlerCount=188,assassinHandlers=19,unchangedInstalledArtifacts=106),
        limits=['Free-cell placement only; occupied size-zero placement and nearby-cell spawn fallback remain unsupported, refusing without payment.',
        'Exact modern corpse-pool insertion/callback order, saved shared charged-bolt motion, lost-mana legacy edge, inherited sunder and full owner/item/event/PvP/client presentation remain acceptance gaps.',
        'Actor and flight reload inspection clocks are held/rearmed. The wounded receiver is moved off the held flight path before saving. No uninterrupted short flight lifetime through downtime is claimed.',
        'Historic disabled-input assertions are replaced by learned availability/input coverage; their other mechanical assertions remain. The failing mouse-policy candidate and stale placeholder-generator diagnostics are preserved.',
        'No installed deployment, save reset, complete class or final effects/audio/icons.'],tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/sentry-learned-native-evidence.json';text=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Learned CBS:69 markers/3 boots/2 reloads;6 casts/3 left rejections;30 mouse rows/132 migrations;188 handlers;106 installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
