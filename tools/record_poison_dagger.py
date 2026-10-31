#!/usr/bin/env python3
"""Bind Poison Dagger's combined packet to real delivery and reload checks."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/poison-dagger'
FROZEN='b5663e7'
sha=inventory.sha


def markers(path):return Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))


def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={scope:inventory.validate_inventory(STAGE/(scope+'-inputs.json')) for scope in ['birth','audit']}
    for scope,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(scope+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous_path=ROOT/'tools/reference/npc-high-level-resources-native-evidence.json'
    previous=json.loads(previous_path.read_text());old=previous['inputInventories']['birth']
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(after)-set(before)=={'src/Sanctuary.Content/PoisonDaggerPackets.cs','tests/Sanctuary.Integration/PoisonDaggerChecks.cs'}
    assert not set(before)-set(after)
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/GearEffects.cs','src/Sanctuary.Content/NecromancerCombat.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    source_path=ROOT/'tools/reference/poison-dagger-source-evidence.json';source=json.loads(source_path.read_text())
    assert source['cases']==6912 and source['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    for path,digest in source['artifacts'].items():assert sha(ROOT/path)==digest,path
    baseline=ROOT/'.local/class-skills/npc-high-level-resources/birth-native-checks.log'
    assert sha(baseline)==previous['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'poison-dagger-check':1,'poison-dagger-reload-check':2,
        'poison-dagger-right-check':3,'poison-dagger-right-result':3,'poison-dagger-quick-check':3,'poison-dagger-quick-result':3,
        'venom-check':1,'venom-result':1,'venom-reload-check':2,'venom-reload-result':2})
    assert sum(expected.values())==118 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Poison Dagger:6912 complete elemental-roll/shared-poison-lane comparisons')==3
    saved=json.loads((STAGE/'poison-dagger-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Poison']['PoisonClock']['Rate256']==304
    assert saved['Poison']['PoisonMs']==8600 and saved['Poison']['PoisonSource']==saved['Owner']
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(s in text for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old_audit=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        assert all(audit[k]==v for k,v in old_audit.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        audits.append(audit)
    assert [len(a['InputHashes']) for a in audits]==[818,21] and len(audits[0]['Definitions'])==573
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python
    installation=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installation['installedArtifacts'])==106
    for path,digest in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/path)==digest,path
    cast=json.loads((ROOT/'tools/reference/iron-golem-casting-evidence.json').read_text())
    assert sha(STAGE/'client/cuo.dll')==cast['clientSha256']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    paths=[Path(__file__).resolve(),previous_path,source_path,baseline]
    paths += [ROOT/p for p in [*changed,*set(after).difference(before),'tests/check_poison_dagger.py','tests/check_poison_dagger_audits.py',
        'docs/poison-dagger-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',
        f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','birth-native-checks.log','audit-inputs.json',
        'audit-native-checks.log','python-tests.log','poison-dagger-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Poison Dagger combined weapon and skill poison',installed=False,
        goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(sourceCases=6912,nativeMarkers=118,boots=3,actualReloads=2,saves=3,rightCasts=3,quickCasts=3,
            retainedVenomSourceCasesPerBoot=10240,auditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[818,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after).difference(before))},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'The complete skill roll supplies initial poison rate and length to the existing item-poison lane. '
            'Skill poison precedes ordinary weapon preparation; item poison then joins one packet. '
            'A prepared poison factory skips the independent Venom/item collector, avoiding a second poison application.',
            'Real Wormskull plus a socketed Emerald dagger produce304FP256 poison per frame for217frames at effective rank2. '
            'Learned strikes,misses,immunity,once-only mana,weapon-change cancellation,live equipment and captured packets pass. '
            'Periodic fractional damage,original equipped items,bindings and source credit survive two reloads.',
            'Six actual right/quick network casts verify natural windup and payment. '
            'Existing Venom,Iron Golem,NPC resource/damage and Paladin checks are retained.'],
        remaining=[
            'This increment fixes poison composition and delivery. The existing Poison Dagger start/release allocation timing, '
            'attack-rating composition,NPC shield-result handling and drain/event order still need complete skill acceptance.',
            'Blackbog\'s Sharp remains unavailable because its skill property is unsupported. '
            'Its attempted fixture refused normally; no item availability or property was fabricated to obtain a passing test.',
            'Older-source composition is not current executable proof. Native geometry,material/gem projection and broader '
            'player packet/RNG/return adapters keep their separate documented boundaries.',
            'No new full-class run is claimed;the predecessor build passed1180markers/35saves. '
            'Final poison effects/audio and talent/hotkey icons remain separate placeholders. Installed build/saves are unchanged.'])
    path=ROOT/'tools/reference/poison-dagger-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Poison Dagger:6912source cases,118native markers/3boots/2reloads,4audits/71Python;class acceptance remains open.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
