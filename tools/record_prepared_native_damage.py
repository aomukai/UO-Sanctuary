#!/usr/bin/env python3
"""Bind pre-HP native notifications to the tested build and retained combat scope."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
import build_skill_roadmap

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/prepared-native-damage'
FROZEN='7bd9cf6'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:
        check_snapshot(STAGE/'pre-build-5.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior_path=ROOT/'tools/reference/npc-item-drain-native-evidence.json'
    prior=json.loads(prior_path.read_text())
    old=prior['inputInventory']
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    new_cs={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/PreparedMonsterNativeDamage.cs','tests/Sanctuary.Integration/PreparedNativeDamageChecks.cs'}
    assert not set(old_cs)-set(new_cs)
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/PreparedMonsterDamage.cs','src/Sanctuary.Content/PreparedMonsterCredit.cs'}
    # Retain actual source probe inputs, not merely the prior pass count.
    for path,digest in old['sourceInputs'].items():
        if not path.endswith('.cs') and path in inputs['sourceInputs']:
            assert inputs['sourceInputs'][path]==digest,path
    assert inputs['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['artifacts'][name]==old['artifacts'][name]
    passed=lambda path:Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))
    baseline=ROOT/'.local/class-skills/npc-item-drain/birth-native-checks.log'
    baseline_relative=str(baseline.relative_to(ROOT))
    assert sha(baseline)==prior['artifacts'][baseline_relative]
    expected=passed(baseline)+Counter({'prepared-native-damage-check':3})
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(expected.values())==73 and passed(STAGE/'birth-native-checks.log')==expected
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Native notifications:256native/ordered')==3
    assert log.count('3region/4reentrant refusals pass')==3
    assert log.count('Monster weapon phases:5184complete-source/native compositions')==3
    assert log.count('NPC contact:3840complete-source releases/1616health commits')==3
    assert 'NPC allocation:27000complete-source mitigation/total/prediction comparisons,21actual original' in log
    assert '3minimum-only item deliveries and3miss-NoFill controls pass' in log
    assert '60488' in log and '64native damage-ledger/master comparisons' in log
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-5.log')).read_text()
        assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert passed(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        previous=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        for key,value in previous.items():
            if key not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes']:assert audit[key]==value,key
        audits.append(audit)
    assert [len(a['InputHashes']) for a in audits]==[810,21] and len(audits[0]['Definitions'])==573
    python=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==185 and sum(r['characterClass']=='Necromancer' for r in handlers)==29 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    native_sources=['Projects/Server/Mobiles/Mobile.cs','Projects/Server/Regions/Region.cs',
        'Projects/UOContent/Mobiles/BaseCreature.cs','Projects/UOContent/Mobiles/Animals/Mounts/BaseMount.cs']
    paths=[Path(__file__).resolve(),prior_path,baseline]
    paths += [ROOT/name for name in ['tests/check_prepared_native_damage.py','tests/check_prepared_native_damage_audits.py',
        'docs/prepared-native-damage-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-5.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log',
        'run-3.log','content-build-5.log','integration-build-5.log','audit-native-checks.log','audit-run-3.log','python-tests.log']]
    result=dict(referenceVersion='3.3.93847',component='Native pre-health notifications for ordered NPC damage',
        learnedIronGolemEnabled=False,liveIronMigrated=False,installed=False,completedClasses=[],goalComplete=False,
        verified=dict(nativeNotificationComparisonsPerBoot=256,regionRefusalsPerBoot=3,reentrantRefusalsPerBoot=4,
            combinedMarkers=73,boots=3,actualReloads=2,saves=3,retainedPaladinAuditMarkers=4,pythonTests=71,
            auditFingerprints=[810,21],namedDefinitions=573,unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,
        nativeReviewedSources={name:sha(STAGE/'native'/name) for name in native_sources},
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'Native Region.OnDamage, spell interruption, actual responsible-owner credit, disruptive action, paralysis clearing, visible damage, OnDamage and mounted-recipient notifications execute in native order before the source HP write. No incoming native Damage call is replayed.',
            '256 paired native/ordered controls per boot cover four life balances, eight damage sizes, four visible-damage policies and mounted/unmounted recipients. OnDamage is exactly once when display HP falls and absent for sub-display loss. Whole/fraction/lethal HP and deferred return death agree.',
            'Region veto/modification/reentrant HP mutation and spell/disruption/damage/mount HP mutations fail explicitly before source HP subtraction. These failures are not rollback transactions; earlier callback side effects may already have occurred.',
            '73 markers/three boots/two actual reloads retain allocation, original-item drains, source phases/contact/return, owner credit/rewards and connected Iron world checks. Current catalogue decisions and installed artifacts are unchanged.'],
        remaining=[
            'This is the native pre-HP bridge, not completed live Iron contact/event/return integration. Modifier/event capability preflight, actual return metadata and durable credit after attacker removal remain required before that migration and learned casting.',
            'BaseCreature.Damage modifiers such as native Evil Omen, Blood Oath and tamed-pet scaling are not replayed by the bridge. The real actor consumer must provide explicit support or refusal; this increment does not authorize silently ignoring them.',
            'Native SkillHealth death cleanup can observe stale fractional remainder before its final Clear; source return instead sees committed zero HP. The paired trace intentionally covers pre-HP notifications, with final HP/death verified separately. It does not certify arbitrary callbacks after native death.',
            'Retained reconstructed-source proofs do not establish exact current executable parity. Initial poison current applicability, procs/sunder, modern Thorns coexistence and full client acceptance remain unfinished.',
            'No class completion, full-class regression, old-content-binary migration, deployment or new final art is claimed. Effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/prepared-native-damage-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Prepared native notifications:256comparisons+7refusals per boot;73markers/3boots/2reloads;71Python;live Iron migration pending.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
