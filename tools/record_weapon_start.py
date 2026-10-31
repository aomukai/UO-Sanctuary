#!/usr/bin/env python3
"""Bind direct weapon-start timing and NPC armor-loss verification."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/weapon-start-effects';sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE;inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for k,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(k+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    prev_path=ROOT/'tools/reference/poison-dagger-contact-native-evidence.json';prev=json.loads(prev_path.read_text());ref=prev['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    changed={p for p in before if before[p]!=after[p]};added=set(after)-set(before)
    assert set(before)<=set(after) and added=={'src/Sanctuary.Content/WeaponStartEffects.cs','tests/Sanctuary.Integration/WeaponStartChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+p+'.cs' for p in ['ClassStats','NamedCombat','PoisonDaggerCombat','PoisonDaggerContact','TimedEffects']}
    data='Data/Sanctuary/Balance/prevent-heal-immune-monsters.json'
    assert set(inputs['birth']['copiedData'])-set(old['copiedData'])=={data}
    assert all(inputs['birth']['copiedData'][p]==h for p,h in old['copiedData'].items())
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/p)==inputs['birth']['artifacts'][p]==old['artifacts'][p]
    baseline=ROOT/'.local/class-skills/poison-dagger-contact/birth-native-checks.log';assert sha(baseline)==prev['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'weapon-start-check':1,'weapon-start-reload-check':2})
    assert sum(expected.values())==130 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Weapon start:751current monster PMH flags')==1 and log.count('Poison Dagger contact:real pre-HP notification')==3
    from review_weapon_start import build as review
    review(True)
    from build_weapon_start_rules import build as rules
    rules(True)
    s=json.loads((STAGE/'weapon-start-fixtures.json').read_text());assert s['Loss']==100 and s['Defense']>0 and s['Until']
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/name).read_text());b=json.loads(subprocess.check_output(['git','show','a648895:tools/reference/'+name],cwd=ROOT))
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        audits.append(a)
    assert [len(a['InputHashes']) for a in audits]==[822,21] and len(audits[0]['Definitions'])==573
    log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installation['installedArtifacts'])==106
    for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text());assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),prev_path,baseline,*[ROOT/p for p in sorted(changed|added)]]
    paths += [ROOT/p for p in ['tools/build_weapon_start_rules.py','tools/review_weapon_start.py','tools/reference/weapon-start-source-review.json',
        'data/Sanctuary/Balance/prevent-heal-immune-monsters.json','tests/check_weapon_start.py','tests/check_weapon_start_audits.py',
        'docs/weapon-start-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log',
        'birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','weapon-start-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Poison Dagger direct setup effects and NPC base armor loss',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=130,boots=3,actualReloads=2,saves=3,currentMonsterRules=751,preventHealImmuneRules=9,realSpineripperResultCases=4,realWoestaveArmor=True,realShaftstopNpcArmorCap=True,
            auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[822,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|added)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Poison Dagger applies no-heal after a successful start hit roll and before shield blocking;misses exclude it,Stop does not undo it,and release does not duplicate its deadline.',
            'Current owned monster flags exclude nine species,including three Colossal rows,instead of guessing exclusions from the older hard-coded Uber range.',
            'Flat target armor loss runs during damage preparation. Its existing persistent NPC lane now precedes defense percentages/Inner Sight and includes attached item armor in the zero cap.',
            'Real Spineripper,Woestave and an attached Shaftstop original verify live consumers. Exact healing deadlines/armor loss persist over two native reloads;all127predecessor markers remain covered.'],
        remaining=['Registered item callback/RNG order and other weapon producers\' legacy setup timing remain open. Positive target-armor changes are explicitly unsupported.',
            'Native creature base armor remains the existing Sanctuary projection;full modern stat graph/source RNG/current executable observation is not established by the static review.',
            'Previous incoming modifier/special-recipient,armor/Iron Maiden,initial-poison modern applicability,geometry/component/RNG and charged-item/Blood Mana gaps remain. No full-class run,skill/class completion,deployment or final assets are claimed.',
            'Continue the Necromancer summon/projectile audit while these nonblocking item-event gaps remain documented.'])
    path=ROOT/'tools/reference/weapon-start-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Weapon start:130markers/3boots/2reloads,751current immunity rules,4audits/71Python;installed artifacts unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
