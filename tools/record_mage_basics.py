#!/usr/bin/env python3
"""Bind approved Mage basics to frozen native/build/audit evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/mage-basics'
sha=inventory.sha


def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/f'{n}-inputs.json') for n in ('birth','audit')}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/f'{n}-inputs.json')
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/fire-upgrades-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={'src/Sanctuary.Content/NecromancerMageMissiles.cs'}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs' for n in ('MageElementChecks','MageGlacialChecks','MageHistoricalChecks','MageMissileChecks','MageWorldChecks')}
    added={'src/Sanctuary.Content/MageBasicSkills.cs','tests/Sanctuary.Integration/MageBasicChecks.cs'}
    assert before.keys()<=after.keys() and after.keys()-before.keys()==added
    assert {p for p in before if before[p]!=after[p]}==changed
    data='data/Sanctuary/Balance/necromancer-summons.json';key='Data/Sanctuary/Balance/necromancer-summons.json'
    assert old['copiedData'].keys()==inputs['birth']['copiedData'].keys()
    assert {p for p,h in old['copiedData'].items() if inputs['birth']['copiedData'][p]!=h}=={key}
    previous=json.loads(subprocess.check_output(['git','show','d5cff2e:'+data],cwd=ROOT));current=json.loads((ROOT/data).read_text())
    source=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/missiles.json').read_text())['icebolt']
    projected={k:v for k,v in source.items() if isinstance(v,(int,str)) and not k.startswith('*') and k not in ('lineNumber','Missile')}
    assert current[:-1]==previous and current[-1]==dict(key='icebolt',fields=projected)
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    baseline=ROOT/'.local/class-skills/fire-upgrades/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['mage-basics-check']+=1;expected['mage-basics-reload-check']+=2;expected['mage-basics-live-result']+=3
    assert sum(expected.values())==388 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Mage basics:48independent source-delta contexts,')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    for kind in ('content','integration'):
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in (('named-item-runtime-audit.json',886),('first-loop-affix-audit.json',21)):
        audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
        assert len(audit['InputHashes'])==count and audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for p,digest in audit['InputHashes'].items():assert sha(ROOT/p)==digest
        previous=json.loads(subprocess.check_output(['git','show','d5cff2e:tools/reference/'+n],cwd=ROOT))
        strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
        assert strip(audit)==strip(previous)
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ': failed' not in (STAGE/'audit-native-checks.log').read_text() and 'exported from the selected build' in (STAGE/f'audit-run-{audit_run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text() and 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest
    saved=json.loads((STAGE/'mage-basics-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']<saved['Maximum']
    assert len(saved['Flights'])==39 and {f['Packet']['Missile'] for f in saved['Flights']}=={'firebolt','icebolt','chargedbolt'}
    assert sum(f.get('Charged') is not None for f in saved['Flights'])==33 and all(f['Frame']==3 for f in saved['Flights'])
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[s for s in status['skills'] if s['implementation']=='handler-present']
    assert len(handlers)==186 and sum(s['characterClass']=='Necromancer' for s in handlers)==30 and not status['completedClasses']
    docs=('mage-basics-checkpoint','skeletal-mage-upgrade-plan','pet-adjustment-review','class-skill-reuse-notes','class-skill-roadmap','class-skills-resume-checkpoint','necromancer-mechanical-audit','skill-effects','skill-icons')
    files=[parent,baseline,Path(__file__).resolve(),ROOT/'tests/check_mage_basics.py',ROOT/'tools/build_class_skills.py',ROOT/data,ROOT/'tools/reference/iron-affix-reuse-review.json',*[ROOT/p for p in changed|added],*[ROOT/f'docs/{n}.md' for n in docs]]
    names=('birth-inputs.json','birth-native-checks.log','audit-inputs.json','audit-native-checks.log','mage-basics-fixtures.json','named-item-runtime-audit.json','first-loop-affix-audit.json','python-tests.log','named-readiness.log','catalogue-check.log')
    files += [STAGE/n for n in names];files += [p for p in STAGE.iterdir() if p.is_file() and p.name.startswith(('pre-build-','content-build-','integration-build-','run-','audit-run-','attempt-'))]
    result=dict(referenceVersion='3.3.93847',component='Approved Mage Sorceress basics,flat captured Skeleton Mastery bonus and live hard5triple volleys',installed=False,goalComplete=False,completedClasses=[],mageUpgradePackageComplete=False,approvedMageBasicsEnabled=True,build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=388,retainedFireIronBloodMagePlayerPetSentryMarkers=382,boots=3,actualReloads=2,saves=3,independentRankMasteryElementContexts=48,rankOneSorceressGoldens=3,savedNewFlights=39,savedChargedPaths=33,naturalThreeElementSessions=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[886,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/f'{n}-inputs.json').relative_to(ROOT)),sha256=sha(STAGE/f'{n}-inputs.json')) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'User-approved mapping keeps captured Mage life,fixes basic spell rank to captured effective Raise Skeletal Mage,and adds old mastered-minus-zero-Mastery elemental endpoint deltas at that same summon rank. Each basic Charged Bolt receives the bonus;fixed secondaries and buffs do not.48actual element/rank/Mastery contexts compare with the retained complete-source damage matrix;three rank1Sorceress goldens and actual per-bolt HP/intrinsic seed controls pass.',
            'New Fire Bolt/Ice Bolt reuse saved Mage vector/steps/typed impact/status/critical and50frame flight;Charged Bolt reuses source-compared heading/aim seed/path and the existing capped77frame mover. Live hard5uses shared3ray fan and three normal Charged Bolt volleys,up to72. Concatenated ordinals preserve distinct deterministic paths without extra actor-stream draws. All children capture Slow consistently;offline lifetimes expire without fighting.',
            'Optional omitted-null charged origin/path/index extends the existing saved record without changing mobile layouts or persistence version.39new three-spell flights including33charged paths/packets/streams/clocks survive two real reloads;copied charged resumption leaves held originals untouched. Invalid paths refuse atomically. Already-fired legacy poison/other packets remain unchanged;explicit LaunchLegacy fixtures retain original source/flight coverage.',
            'The exact pinned Ice Bolt row is appended through the existing generator,retaining every older runtime row/order. Shared avoidance uses the row normally;no fallback or eligibility is weakened. Historical/world/migration fixtures now assert new basic spell/rank/full volley semantics,while fixed-secondary and other class scopes remain in regression.',
            'Three connected sessions each exercise all three elements,train hard5during natural windup,observe the complete volley before damage,and deliver actual collisions without NPC/owner mana. Clean builds,388native markers retaining382/four audits/71Python/573named/eight-class240pass;886/21fresh fingerprints with audit decisions/kernel/installed106unchanged. Only appended Ice Bolt runtime data differs. No deployment or class completion.'],
        remaining=[
            'The shared charged continuous movement,tile projection,room/collision/factory/current global seed/event/PvP/Sunder/client and source-after-removal policies remain bounded host adapters. This does not certify all modern executable behavior.',
            'Held saves/copied resumption do not establish installed old-DLL/concurrent/unheld active-save acceptance. Broader historical paths,source stat/event graphs and class acceptance remain. Mastery mapping is resolved,not an unanswered design blocker.',
            'Safe-area parking,barriers,remaining Necromancer checks,Druid and class roadmap remain. Basic/triple/charged travel/impact/status/audio and Raise Mage talent/hotkey artwork remain identified placeholders in separate registers. No class is complete.'])
    output=ROOT/'tools/reference/mage-basics-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==rendered
    else:output.write_text(rendered)
    print('Mage basics:388markers/3boots/2reloads,flat Mastery/triples/charged paths;installed unchanged. No class complete.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('build','run','audit-run'):p.add_argument('--'+name,type=int,required=True)
    p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
