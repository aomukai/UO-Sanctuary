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
STAGE=ROOT/'.local/class-skills/iron-golem-casting'
FROZEN='e25f4f3'
sha=inventory.sha

def record(check=False):
    check_metadata(True)
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:check_snapshot(STAGE/'pre-build-6.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior_path=ROOT/'tools/reference/iron-golem-ordered-contact-evidence.json';prior=json.loads(prior_path.read_text());old=prior['inputInventory']
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/IronGolemCasting.cs','tests/Sanctuary.Integration/IronGolemCastChecks.cs'}
    assert not set(old_cs)-set(new_cs)
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}==set(['shared/ClassAbilities.cs', 'src/Sanctuary.Content/ClassCombat.cs', 'src/Sanctuary.Content/DruidSpiritWolfCasting.cs', 'src/Sanctuary.Content/NecromancerCombat.cs', 'src/Sanctuary.Content/SkillBindingNetwork.cs', 'src/Sanctuary.Content/SkillBindings.cs', 'tests/Sanctuary.Integration/EquipmentPropertyWriterChecks.cs', 'tests/Sanctuary.Integration/EquipmentStatGraphChecks.cs', 'tests/Sanctuary.Integration/EquipmentStatValueChecks.cs', 'tests/Sanctuary.Integration/GenericPetWorldIdentityChecks.cs', 'tests/Sanctuary.Integration/GolemRosterChecks.cs', 'tests/Sanctuary.Integration/IronGolemBirthChecks.cs', 'tests/Sanctuary.Integration/IronGolemBirthGraphChecks.cs', 'tests/Sanctuary.Integration/IronGolemBodyChecks.cs', 'tests/Sanctuary.Integration/IronGolemItemChecks.cs', 'tests/Sanctuary.Integration/IronGolemOrderedContactChecks.cs', 'tests/Sanctuary.Integration/IronGolemRetaliationChecks.cs', 'tests/Sanctuary.Integration/IronGolemWorldChecks.cs', 'tests/Sanctuary.Integration/ItemMaximumOperationChecks.cs', 'tests/Sanctuary.Integration/MeleeResultChecks.cs', 'tests/Sanctuary.Integration/NecromancerPetAiChecks.cs', 'tests/Sanctuary.Integration/NpcItemSupplierChecks.cs'])
    for path,digest in old['sourceInputs'].items():
        if not path.endswith('.cs') and path in inputs['sourceInputs'] and path!='tests/check_iron_golem_birth.py':assert inputs['sourceInputs'][path]==digest,path
    assert set(inputs['copiedData'])==set(old['copiedData'])
    for name,digest in old['copiedData'].items():
        if name!='Data/Sanctuary/Balance/class-skill-status.json':assert inputs['copiedData'][name]==digest,name
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['artifacts'][name]==old['artifacts'][name]
    passed=lambda path:Counter(re.findall(r'\b([a-z0-9-]+): passed\b',path.read_text()))
    baseline=ROOT/'.local/class-skills/iron-golem-ordered-contact/birth-native-checks.log'
    assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=passed(baseline)+Counter({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2,'iron-golem-right-check':3,'iron-golem-right-result':3,'iron-golem-quick-check':3,'iron-golem-quick-result':3})
    log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(expected.values())==94 and passed(STAGE/'birth-native-checks.log')==expected
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'Learned Iron cast:authoritative item/quick SC windup,35mana once' in log
    assert log.count('Ordered Iron contact:6real original packets')==3
    assert log.count('Native notifications:256native/ordered')==3
    assert '60488' in log and '45840' in log
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-6.log')).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
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
    assert [len(a['InputHashes']) for a in audits]==[817,21] and len(audits[0]['Definitions'])==573
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==186 and sum(r['characterClass']=='Necromancer' for r in handlers)==30 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    paths=[Path(__file__).resolve(),prior_path,baseline,ROOT/'tools/build_npc_combat_metadata.py',ROOT/'tools/build_class_skills.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tests/check_iron_golem_casting.py',ROOT/'tests/check_iron_golem_casting_audits.py',ROOT/'tests/check_iron_golem_birth.py',
        ROOT/'tools/reference/item-property-writers-source-evidence.json',ROOT/'.local/class-skills/item-property-writers-source-probe.json']
    paths += [ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json') for name in ['monstats','monstats2','itemstatcost']]
    paths += [ROOT/name for name in ['docs/iron-golem-casting-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-6.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-5.log',
        'content-build-6.log','integration-build-6.log','audit-native-checks.log','audit-run-5.log','python-tests.log','monster-return-fixtures.json','item-property-writers-fixtures.json','iron-golem-cast-fixtures.json','client-build-2.log','client-baseline-build.log']]
    client_log=(STAGE/'client-build-2.log').read_text();baseline_log=(STAGE/'client-baseline-build.log').read_text()
    assert all('Build succeeded.' in t and '0 Error(s)' in t for t in [client_log,baseline_log])
    def warnings(t):return Counter(line.split(': warning ',1)[1].rsplit(' [',1)[0] for line in t.splitlines() if ': warning ' in line)
    assert warnings(client_log)==warnings(baseline_log)
    client=STAGE/'client/cuo.dll';assert client.exists()
    status_saved=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert status_saved['Original']>0 and status_saved['Body']>0 and status_saved['Mana']%256==17
    patch=ROOT/'patches/ClassicUO/0037-iron-golem-item-targeting.patch'
    paths += [patch,ROOT/'shared/ClassAbilities.cs']
    result=dict(referenceVersion='3.3.93847',component='Learned Iron Golem item-target casting',learnedIronGolemEnabled=True,installed=False,completedClasses=[],goalComplete=False,
        verified=dict(nativeMarkers=94,boots=3,actualReloads=2,saves=3,rightInputCasts=3,quickInputCasts=3,allocationPaymentRollbackCases=3,cancellationCases=3,
            retainedOriginalsPerBoot=6,retainedNotificationComparisonsPerBoot=256,retainedPropertyWriterCasesPerBoot=45840,retainedPaladinAuditMarkers=4,pythonTests=71,
            namedDefinitions=573,auditFingerprints=[817,21],unchangedInstalledArtifacts=106,handlers=186,necromancerHandlers=30,clientBuild=True,newClientWarnings=0),
        inputInventory=inputs,auditInputInventory=audited,clientSha256=sha(client),clientSourceSha256=sha(STAGE/'runtime/upstream/ClassicUO/src/ClassicUO.Client/Game/ClassCombatClient.cs'),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'The learned handler accepts authoritative ground-item serials through right-click and quick use. The client patch preserves those serials and the server enforces right-only binding. The inherited native ground-summon placement helper searches nearby free space while cast aim remains on the original item.',
            'Supported creation runs unpaid SC windup, rechecks the original/placement/current capabilities, allocates the real prepared world actor, pays35mana once,then joins the shared golem roster. Failed allocation or lost mana returns the exact original without replacing the old paid golem. Moved items/casters and insufficient mana cancel without consumption.',
            'Actual network right/quick inputs pass on all three boots. The active hidden original/full rolls,world controller,injured FP life,fractional mana,seed,roster and bindings survive two reloads. Native mana regeneration is observed at WorldSave rather than frozen. Unpaid pending casts do not replay.',
            '94native markers retain prior item/body/world/ordered/return/notification and original-property checks. Paladin/catalogue and71Python pass;all573named decisions and106installed artifacts remain unchanged. The isolated client compiles with the same pre-existing warnings as its unmodified baseline.'],
        remaining=[
            'All30Necromancer talents now have handlers;this is not a class completion certificate. Full-class regression,graphical client playtesting,current executable exactness and deployment remain unclaimed.',
            'The prior explicit item/proc/Iron Maiden/Sunder/mode/recipient refusals remain. Existing Baranar originals lack an independent cold-duration fallback and Hellplague fireskill projection is unsupported. Neither is silently omitted or rerolled.',
            'Native range/LOS/nearby ground-search,AI/RNG/display/mode adapters and modern initial-poison/Thorns coexistence retain their recorded uncertainty. Cast allocation-failure rollback is the explicit native transaction policy,not proof of every modern engine failure path.',
            'Final summon/body/contact/return/audio effects and talent/right-hotkey icons remain separate identified placeholders. The current installed build and saves are unchanged.'])
    path=ROOT/'tools/reference/iron-golem-casting-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Learned Iron casting:94markers/3boots/2reloads;right+quick inputs,rollback,persistence,71Python;186handlers/Necromancer30,no class complete.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
