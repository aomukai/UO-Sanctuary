#!/usr/bin/env python3
"""Bind learned Inferno casting, distinct E9 timing and retained lightning cohorts."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/inferno-learned'
PARENT='4690140afeef6546c999bbcbf6f9c969ec109689'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)

def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    sys.path.insert(0,str(ROOT/'tests'))
    from check_inferno_learned import configuration
    config=configuration();assert (len(config['extra_families']),len(config['extra_checks']),len(config['extra_target_input_commands']))==(12,8,9)
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==141
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in config['extra_families']:
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2,name
    for name in config['extra_checks']:assert log.count(name+': passed')==3,name
    for name in config['extra_target_input_commands']:
        assert log.count(name+'-check: passed')==log.count(name+'-result: passed')==3,name
        assert log.count(name+(': acknowledged' if name.endswith('left') else ': windup'))==3,name
    for name in ('sentry-retirement','sentry-learned','lightning-learned','inferno-learned'):
        assert log.count(name+'-result: passed')==1 and log.count(name+'-reload-result: passed')==2,name
    assert log.count('lightning-sentry-live-result: passed')==3 and log.count('inferno-sentry-live-result: passed')==3
    for name in ('sentry-learned','lightning-learned','inferno-learned'):
        saved=json.loads((STAGE/(name+'-fixtures.json')).read_text())
        assert saved['Generation']==2 and saved['Actor']['Creation']['Rank']==21 and saved['Actor']['Owner']==saved['Owner']
        assert saved['Bolt']['Actor']==saved['Body'] and saved['Bolt']['Owner']==saved['Owner'] and saved['Bolt']['Frame']>=4
        if name=='lightning-learned':assert saved['Bolt']['LastCollision']==saved['Target'] and saved['Actor']['Control']['Dispatches']==1
        if name=='inferno-learned':
            assert saved['Bolt']['LastCollision']==saved['Target'] and saved['Actor']['InfernoControl']['Attempts']==1
            assert saved['Bolt']['HardWake']==1 and saved['Bolt']['Frames']==21
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
        text=(STAGE/(kind+'-build-1.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.','Current sentry monster/models:601 fields','Local sentry animation:11records pinned'):
        assert word in text
    models=json.loads((ROOT/'tools/reference/d2r-local-sentry-model-evidence.json').read_text())
    for info in models['files'].values():assert sha(ROOT/info['path'])==info['sha256']
    mon=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/monstats.json').read_text())
    model=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/monstats2.json').read_text())
    for ident in ('chargeboltsentry','lightningsentry','infernosentry'):
        row=mon[ident];body=model[row['MonStatsEx']]
        assert row['Code']==('e9' if ident=='infernosentry' else 'lg') and body['BaseW']=='hth' and body['mDT']==body['mDD']==body['limitCorpses']==1
        assert not row.get('SplGetModeChart') and not row.get('SplEndDeath') and not row.get('deathDmg')
    animations=json.loads((ROOT/'tools/reference/d2r-local-sentry-animation-evidence.json').read_text())['records']
    for code,frames in [('LGDTHTH',23),('E9DTHTH',25)]:
        assert next(row for row in animations if row['code']==code)==dict(code=code,frames=frames,speed=256,events=[])
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==190
    assert sum(s['implementation']=='handler-present' and s['characterClass']=='Assassin' for s in status['skills'])==21
    row=next(s for s in status['skills'] if s['talentId']=='assassin.wakeofinferno');assert row['verification']=='partial-native-coverage' and not row['complete']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    prior=json.loads((ROOT/'.local/class-skills/lightning-learned/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][path]==tested['artifacts'][path]
    changed={p for p,h in tested['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/'+n for n in ('ActionTiming.cs','AssassinCombat.cs','AssassinSentryBody.cs','AssassinSentryCasts.cs','AssassinSentryRetirement.cs','AssassinTrapRoster.cs','ClassCombat.cs')}
    changed={p for p,h in tested['copiedData'].items() if prior['copiedData'].get(p)!=h}
    assert changed=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/skill-effects.json'}
    for path in changed:
        name='data/Sanctuary/'+path.removeprefix('Data/Sanctuary/');old=json.loads(historical(name));new=json.loads((ROOT/name).read_text())
        if isinstance(old,dict):old=old['skills'];new=new['skills']
        assert [a['talentId'] for a,b in zip(old,new) if a!=b]==['assassin.wakeofinferno']
    files=[Path(__file__).resolve(),ROOT/'tests/check_inferno_learned.py',ROOT/'tests/check_lightning_learned.py',ROOT/'tests/check_sentry_learned.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'docs'/n for n in ('assassin-inferno-learned-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(ROOT/'tools'/n for n in ('corroborate_local_sentry_models.py','corroborate_local_sentry_animation.py')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','inferno-learned-fixtures.json','lightning-learned-fixtures.json','sentry-learned-fixtures.json','sentry-retirement-fixtures.json','content-build-1.log','integration-build-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log'))]
    files.extend(config['extra_files'].values());files.extend(config['extra_inputs'])
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Learned right/quick Wake of Inferno enabled with distinct E9/channel and retained CBS/Lightning acceptance; no complete class certificate.',
        verified=dict(nativeCommandMarkers=141,boots=3,reloads=2,saves=3,wireRequests=27,successfulRightQuickCasts=18,rejectedLeftRequests=9,paidRank21Cohorts=3,originalActorFlightResumes=6,auditMarkers=2,pythonTests=71,handlerCount=190,assassinHandlers=21,unchangedInstalledArtifacts=106),
        limits=['Validated free-cell placement only; occupied size-zero placement and nearby-cell spawn fallback remain unsupported.',
        'Exact modern corpse-pool insertion/callback order, zero-velocity path flags, inherited sunder and complete geometry/seed/event/item/owner/PvP/client behavior remain acceptance gaps.',
        'The identified LG/E9 death profiles use owned current animation/model metadata with reconstructed source scheduling. Explicit step controls verify ten shots/bursts and retired-owner damage; connected checks separately cover natural firing and offline expiry.',
        'Reload actor/flight inspection clocks are held/rearmed and wounded receivers move off held flight paths. No uninterrupted short flight lifetime through downtime is claimed.',
        'Inferno retains three-frame sparse emissions, its captured hard-Wake duration and DamageRate82; Death Sentry and Wake of Fire retain their separate deferred paths.',
        'Historical sentry retirement-fault states retain their existing fault; their original death entry cannot be inferred after old inactive aging. No guessed timer migration or actual old-build upgrade is claimed.',
        'No installed deployment, save reset, complete class or final effects/audio/icons.'],tested=tested,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/inferno-learned-native-evidence.json';text=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Learned Inferno/Lightning/CBS:141 markers/3 boots/2 reloads;18 casts/9 left rejections;6 original resumes;190 handlers;106 installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
