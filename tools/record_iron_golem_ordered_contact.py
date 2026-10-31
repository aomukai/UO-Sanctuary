#!/usr/bin/env python3
"""Bind live prepared Iron contact/HP/return and explicit unsupported contexts."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
from build_npc_combat_metadata import build as check_metadata
import build_skill_roadmap

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/iron-golem-ordered-contact'
FROZEN='3a835f1'
sha=inventory.sha

def record(check=False):
    check_metadata(True)
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:check_snapshot(STAGE/'pre-build-9.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior_path=ROOT/'tools/reference/prepared-native-damage-evidence.json';prior=json.loads(prior_path.read_text());old=prior['inputInventory']
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/IronGolemContact.cs','src/Sanctuary.Content/NativeMonsterReturn.cs','src/Sanctuary.Content/NpcCombatEvents.cs','tests/Sanctuary.Integration/IronGolemOrderedContactChecks.cs'}
    assert not set(old_cs)-set(new_cs)
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/ClassLoot.cs','src/Sanctuary.Content/EquipmentPropertyWriter.cs','src/Sanctuary.Content/IronGolemBodyNumbers.cs',
        'src/Sanctuary.Content/IronGolemWorld.cs','src/Sanctuary.Content/ItemStatContext.cs','src/Sanctuary.Content/PreparedMonsterCredit.cs','tests/Sanctuary.Integration/MonsterReturnChecks.cs'}
    for path,digest in old['sourceInputs'].items():
        if not path.endswith('.cs') and path in inputs['sourceInputs']:assert inputs['sourceInputs'][path]==digest,path
    added={'Data/Sanctuary/Balance/monster-return-identities.json','Data/Sanctuary/Balance/npc-item-combat-events.json'}
    assert set(inputs['copiedData'])-set(old['copiedData'])==added
    for name,digest in old['copiedData'].items():assert inputs['copiedData'][name]==digest,name
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['artifacts'][name]==old['artifacts'][name]
    passed=lambda path:Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))
    baseline=ROOT/'.local/class-skills/prepared-native-damage/birth-native-checks.log'
    assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=passed(baseline)+Counter({'iron-golem-ordered-contact-check':3,'item-property-writers-check':1,'item-property-writers-reload-check':2})
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(expected.values())==79 and passed(STAGE/'birth-native-checks.log')==expected
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Ordered Iron contact:6real original packets')==3
    assert log.count('actual HP-before-lethal retaliation-before-deferred recipient death with removed-pet owner XP')==3
    assert log.count('5pre-roll capability/2unrepresented birth refusals pass')==3
    assert log.count('Native notifications:256native/ordered')==3
    assert log.count('Monster weapon phases:5184complete-source/native compositions')==3
    assert log.count('NPC contact:3840complete-source releases/1616health commits')==3
    assert '60488' in log and '45840' in log
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-9.log')).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert passed(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        previous=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        for key,value in previous.items():
            if key not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes']:assert audit[key]==value,key
        audits.append(audit)
    assert [len(a['InputHashes']) for a in audits]==[815,21] and len(audits[0]['Definitions'])==573
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==185 and sum(r['characterClass']=='Necromancer' for r in handlers)==29 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    paths=[Path(__file__).resolve(),prior_path,baseline,ROOT/'tools/build_npc_combat_metadata.py',
        ROOT/'tests/check_iron_golem_ordered_contact.py',ROOT/'tests/check_iron_golem_ordered_contact_audits.py',
        ROOT/'tools/reference/item-property-writers-source-evidence.json',ROOT/'.local/class-skills/item-property-writers-source-probe.json']
    paths += [ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json') for name in ['monstats','monstats2','itemstatcost']]
    paths += [ROOT/name for name in ['docs/iron-golem-ordered-contact-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-9.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-5.log',
        'content-build-9.log','integration-build-9.log','audit-native-checks.log','audit-run-5.log','python-tests.log','monster-return-fixtures.json','item-property-writers-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Live prepared Iron Golem ordered ordinary contact',liveIronMigrated=True,learnedIronGolemEnabled=False,
        installed=False,completedClasses=[],goalComplete=False,
        verified=dict(pinnedItemEventStats=21,pinnedMonsterIdentities=751,nativeOriginalsPerBoot=6,lethalRetaliationOwnerRewardPerBoot=1,
            preRollCapabilityRefusalsPerBoot=5,originalPreservingBirthRefusalsPerBoot=2,nativeNotificationComparisonsPerBoot=256,
            retainedPropertyWriterCasesPerBoot=45840,combinedMarkers=79,boots=3,actualReloads=2,saves=3,retainedPaladinAuditMarkers=4,pythonTests=71,
            auditFingerprints=[815,21],namedDefinitions=573,unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'The actual prepared Iron world action allocates source result/channels/mitigation and uses qualified contact, real native notifications/owner credit, one HP write, captured cold/poison, attack-attempt/flat return, clipped percentage return and source-selected native death/get-hit/block/knockback/refresh services. Legacy immediate NpcEquipmentDamage.TryResolve is removed from that action.',
            'Real Broadsword,Battlebranch,Lance of Yaggai,Rakescar,Gleamscythe and Shaftstop controls compare allocated totals/offensive RNG with actual once-only world delivery. Cold and periodic poison consume captured values. The next frame cannot replay the consumed packet.',
            'An actual one-HP exchange commits victim HP before lethal flat return destroys the golem/original,then performs victim native death once. Native responsible-owner credit plus an exact-packet transient owner fallback preserve the real Necromancer XP/reward entitlement after pet removal.',
            'Active current item registrations are projected from real original stat lists. Only stat78/event6 is consumed by shared flat return; unknown active procs and conditional hit stats fail preflight. Iron Maiden,uninterruptible completion and a native Evil Omen marker refuse before hit/damage RNG. Baranar unrecorded cold fallback and Hellplague unsupported fireskill refuse before original transfer.',
            '751class/base/model identities and21item event stats reproduce pinned primary tables. Source-row return mappings are checked against reviewed profile mode flags; configured native dungeon archetypes use explicit class/base -1,not invented D2 identities. Native room/AI/display/mode/RNG projections remain disclosed adapters.',
            '79markers/three boots/two actual reloads retain45840property writer cases,allocated/ordered/notification/world/retaliation and original persistence scopes. Paladin/catalogue and71Python pass;573named decisions and106installed artifacts remain unchanged.'],
        remaining=[
            'Learned Iron casting/payment/placement and full lifecycle/client/class acceptance are still disabled/unfinished. No class completion,full-class regression,old-content-binary migration or deployment is claimed.',
            'Active item procs besides flat ATD,conditional stun/no-heal/defense hit stats,Iron Maiden event4,positive NPC drains and unrepresented skill/item writers still require consumers. Baranar existing saved rolls lack an independent zero-parameter cold-duration fallback;do not invent/reroll it.',
            'Native Evil Omen/Blood Oath/tamed-pet/custom Damage modifiers,uninterruptible/death-delay completion,Paragon/Lightning Enchanted recovery callbacks and unreviewed recipient identities refuse pending explicit adapters. This is not a Revive ability whitelist or global UO spell conversion.',
            'Current executable initial-poison applicability and modern Thorns coexistence remain unobserved. Owner Sunder is refused both at birth and live release;its full consumer remains open. Native defender RNG,room/geometry/AI/block clock/hit-class display and client behavior do not constitute exact modern engine parity.',
            'Committed owner fallback is transient like native damage entries;no new persistent ledger or deferred-death replay is introduced. Region/callback failure remains fail-fast rather than transactional rollback.',
            'Effects/audio and talent/hotkey icons remain separate pending assets. Native contact/block/get-hit/overlay/body presentation is explicitly placeholder artwork.'])
    path=ROOT/'tools/reference/iron-golem-ordered-contact-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Live ordered Iron:6originals+lethal owner-credit exchange+7refusals per boot;79markers/3boots/2reloads;71Python;learned cast pending.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
