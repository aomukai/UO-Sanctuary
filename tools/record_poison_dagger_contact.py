#!/usr/bin/env python3
"""Bind the actual Poison Dagger ordered contact candidate to its tested inputs."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/poison-dagger-contact'
sha=inventory.sha

def markers(path):return Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    previous_path=ROOT/'tools/reference/prepared-player-native-damage-evidence.json'
    previous=json.loads(previous_path.read_text());ref=previous['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/PoisonDaggerContact.cs','tests/Sanctuary.Integration/PoisonDaggerContactChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['CombatItemEvents','IronGolemContact','NativeMonsterReturn','PoisonDaggerCombat','PreparedMonsterContact','PreparedMonsterDamage','RewardLife']}
    assert old['copiedData']==inputs['birth']['copiedData']
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/prepared-player-native-damage/birth-native-checks.log'
    assert sha(baseline)==previous['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'poison-dagger-contact-check':3})
    assert sum(expected.values())==127 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Poison Dagger contact:real pre-HP notification')==3
    assert log.count('Native player notifications:256native/ordered')==log.count('Native notifications:256native/ordered')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','dbef118:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[820,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    weapons=ROOT/'tools/reference/d2r-named-equipment/json/weapons.json'
    knives={k:r['hit class'] for k,r in json.loads(weapons.read_text()).items() if r.get('type')=='knif'}
    assert len(knives)==14 and set(knives.values())=={'1ht'}
    enum=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Common/include/D2DataTbls.h'
    assert re.search(r'HITCLASS_OneHandThrust\s*=\s*6\s*,',enum.read_text())
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous_path,baseline,weapons,enum]
    paths += [ROOT/p for p in sorted(changed|added)]
    paths += [ROOT/p for p in ['tests/check_poison_dagger_contact.py','tests/check_poison_dagger_contact_audits.py',
        'docs/poison-dagger-contact-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','poison-dagger-fixtures.json','poison-dagger-attack-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Actual Poison Dagger ordered player contact',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=127,boots=3,actualReloads=2,saves=3,newContactRuns=3,nativePlayerComparisonsPerBoot=256,nativeNpcComparisonsPerBoot=256,
            retainedRightCasts=3,retainedQuickCasts=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[820,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},
        knifeHitClass=dict(rows=knives,sourceEnum='HITCLASS_OneHandThrust',value=6),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'The learned player actor now composes existing ordered contact/HP/retaliation/return services;NPC/Iron contact remains shared.',
            'Actual pre-HP native notification,direct player credit,once-only health/free,knife hit class and real Sapphire captured cold pass.',
            'Actual missed/blocked swings dispatch flat ATD;lethal retaliation precedes recipient death without losing player credit.',
            'Real Head Hunter\'s Glory/Silkweave/Tyrael\'s Might verify fractional eligible life/mana-on-kill before retaliation,once-only recovery and RIP. The no-reward control dies without recovery.',
            'Uninterruptible,Paragon and Evil Omen controls refuse before hit rolls or mana payment. All predecessor markers,573 named decisions,client/kernel and installed artifacts remain retained.'],
        remaining=[
            'Conditional item event registration/RNG order,whole-HP armor/Iron Maiden and broader outgoing proc consumers remain incomplete.',
            'Native incoming modifier/override,Paragon,uninterruptible/death-delay and unfrozen sand-leaper actor adapters are refused. Region/reentrant notification mutation remains unsupported.',
            'Lightning Enchanted retains the native producer without source-RNG parity. Current executable initial-poison applicability,geometry/component/RNG,charged-item/Blood Mana and final assets remain open.',
            'No new full-class run,complete skill/class,installation or asset generation is claimed. Revive retains living abilities;global monster spell conversion is separate.'])
    path=ROOT/'tools/reference/poison-dagger-contact-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Poison Dagger contact:127markers/3boots/2reloads,4audits/71Python;installed artifacts unchanged;remaining event/actor limits explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
