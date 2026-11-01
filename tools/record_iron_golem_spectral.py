#!/usr/bin/env python3
"""Bind shared Iron hard20 Spectral Hit to frozen native/build/audit evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/iron-golem-spectral'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/iron-golem-strong-native-evidence.json'
    prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('EliteCombat','GearEffects','IronGolemBody','IronGolemContact','IronGolemUpgrades','NecromancerSummons','NpcEquipmentDamage','NpcEquipmentImpact')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs' for n in ('IronGolemDamageChecks','IronGolemStrongChecks')}
    added={'src/Sanctuary.Content/SpectralHit.cs','src/Sanctuary.Content/NpcSpectralHit.cs','tests/Sanctuary.Integration/IronGolemSpectralChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    assert inputs['birth']['copiedData']==old['copiedData']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/iron-golem-strong/birth-native-checks.log'
    assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['iron-golem-spectral-check']+=1;expected['iron-golem-spectral-reload-check']+=2;expected['iron-golem-spectral-live-result']+=3
    assert sum(expected.values())==376 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Iron Spectral:2000wild RNG parity contexts,')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',884),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','51a153c:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ': failed' not in (STAGE/'audit-native-checks.log').read_text()
    assert 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    saved=json.loads((STAGE/'iron-golem-spectral-fixtures.json').read_text());assert saved['Generation']==2 and saved['Poison']['PoisonSource']==saved['Owner'] and saved['Cold']['ChillPercent']>0
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('iron-golem-spectral-checkpoint','pet-adjustment-review','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons')
    files=[parent,baseline,Path(__file__).resolve(),ROOT/'tests/check_iron_golem_spectral.py',ROOT/'tools/review_iron_affix_reuse.py',ROOT/'tools/reference/iron-affix-reuse-review.json',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    names=('birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','iron-golem-spectral-fixtures.json','named-item-runtime-audit.json','first-loop-affix-audit.json','python-tests.log','named-readiness.log','catalogue-check.log')
    files += [STAGE/n for n in names]
    files += [p for p in STAGE.iterdir() if p.is_file() and p.name.startswith(('pre-build-','content-build-','integration-build-','run-','audit-run-','attempt-'))]
    result=dict(referenceVersion='3.3.93847',component='Approved Iron live hard20 shared Spectral Hit,post-direct survivor cold/aggregate poison',installed=False,goalComplete=False,completedClasses=[],ironUpgradePackageComplete=False,extraStrongEnabled=True,spectralHitEnabled=True,build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=376,retainedStrongCurveBloodCursePetPlayerSentryMarkers=370,boots=3,actualReloads=2,saves=3,wildRngParityContexts=2000,actualWildChannels=5,preparedChannels=5,actualOriginalPoisonCoexistenceCases=2,savedActorTypes=2,naturalWindupHard20UnlockSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[884,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'SpectralHit extracts existing amount/type producer with exact wild RNG overload/order retention. Iron uses its captured innate Clay raw endpoints,independent of item-local/Extra Strong/aura percentages. Existing resistance order,under75threshold and two-immunity cap are shared.',
            'Direct magic/fire/lightning/cold additions follow NPC intrinsic critical. Poison stays aggregate1600ms outside the initial item-poison rate/HP packet. Cold and poison apply only after qualified direct damage/return to survivors. Actual NPC/captured pierce mitigates poison;existing durable owner credit excludes player mastery/weapon/potion context.',
            'Prepared/historical live hard20 provider,hard19+actual soft1refusal,train/respec/relearn,unchanged wounded resources/maximum/recipe/controller/stream,typed/lethal/range/no-fill/physical-zero/poison-immunity/critical controls and actual historical fallback are exercised. Actual Jade Tan Do original poison retains its initial rate and stronger timed poison alongside magic/Spectral poison. Held poison/chill persist across two real restarts;separate expiry continues after original source removal.',
            'All predecessor370marker scopes remain. Three connected natural G4windup sessions verify live hard20 release,with no HP/status from canceled predictions. Clean rebuilds,376markers/four audits/71Python/573named/eight-class240pass;fresh884/21fingerprints with decisions/data/kernel/installed106unchanged. No deployment or class completion.'],
        remaining=[
            'Complex original-item cold/pierce/absorption composition,reflected source death in the same release,broader historical-controller outcomes and PvP/protection remain additional acceptance work. Original unsupported properties retain their gates.',
            'Older source differs from the approved current wild affix. Modern executable/global RNG/stat-list/event/room/client equivalence and installed old-DLL/concurrent/unheld-save acceptance are not certified.',
            'Final five-channel feedback/audio and Iron talent/hotkey artwork remain explicit placeholders in separate registers. Mage mapping,Fire/barriers/parking,Druid/remaining classes and other reference/acceptance gaps remain. No class is complete.'])
    output=ROOT/'tools/reference/iron-golem-spectral-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Iron Spectral:376markers/3boots/2reloads,shared all-five outcomes and live hard20;installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
