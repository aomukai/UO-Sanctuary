#!/usr/bin/env python3
"""Seal tested Life Tap casting fractional event, shared retaliation and saved casting evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/life-tap-cast'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={name:inventory.validate_inventory(STAGE/(name+'-inputs.json')) for name in ['birth','audit']}
    for name,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(name+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts'] and value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/iron-maiden-native-evidence.json';prior=json.loads(previous.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    added=set(after)-set(before);changed={p for p in before if before[p]!=after[p]}
    assert added=={'tests/Sanctuary.Integration/LifeTapCastChecks.cs'}
    assert changed=={'src/Sanctuary.Content/NecromancerCurseCasts.cs','src/Sanctuary.Content/NecromancerEffects.cs','tests/Sanctuary.Integration/PoisonExplosionChecks.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/iron-maiden/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'life-tap-cast-check':1,'life-tap-cast-reload-check':2,'life-tap-cast-right-check':3,'life-tap-cast-right-result':3,'life-tap-cast-quick-check':3,'life-tap-cast-quick-result':3})
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==491 and sum(markers(baseline).values())==476
    text=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',849),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/iron-maiden'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in a['InputHashes'].items():assert sha(ROOT/path)==digest,path
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    event_path=ROOT/'tools/reference/life-tap-event-source-evidence.json';event=json.loads(event_path.read_text())
    assert (event['eventContexts'],event['acceptedContexts'])==(22464,1536)
    receiver_path=ROOT/'tools/reference/life-tap-receiver-source-evidence.json';receiver=json.loads(receiver_path.read_text());assert receiver['receiverContexts']==384
    for proof in [event,receiver]:
        for path,digest in proof.get('inputs',{}).items():assert sha(ROOT/path)==digest,path
        for path,digest in proof['artifacts'].items():assert sha(ROOT/path)==digest,path
    for key in ['reusedReference','reusedReceiverCpp','pinnedTable','pinnedState']:assert sha(ROOT/receiver[key]['path'])==receiver[key]['sha256']
    saved=json.loads((STAGE/'life-tap-cast-fixtures.json').read_text());assert saved['Generation']==2 and saved['Curse']['Rank']==21 and saved['Old']['Rank']==1 and saved['HealerLife']==2817
    assert all('PhysicalDelta' not in saved[k] and 'ElementDeltas' not in saved[k] for k in ['Curse','Old'])
    lower=json.loads((STAGE/'lower-resist-fixtures.json').read_text());assert lower['Generation']==2 and lower['Curse']['ElementDeltas']==dict(Fire=-63,Lightning=-63,Cold=-63,Poison=-63)
    assert lower['PartialCurse']['ElementDeltas']==dict(Fire=None,Lightning=-6,Cold=-6,Poison=-6) and 'ElementDeltas' not in lower['Old']
    for name in ['fire-golem-birth','fire-golem-absorption','fire-golem-combat','fire-golem-aura','fire-golem-death','amplify-damage','weaken','decrepify','iron-maiden']:
        assert json.loads((STAGE/(name+'-fixtures.json')).read_text())['Generation']==2
    files=[previous,event_path,receiver_path,Path(__file__),*[ROOT/p for p in added|changed]]
    files += [ROOT/p for p in ['tools/probe_life_tap_event.py','tools/probe_life_tap_receiver.py','tests/check_life_tap_cast.py','tests/check_iron_golem_birth.py','tools/build_class_skills.py',
        'docs/life-tap-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/pet-adjustment-review.md','docs/skeletal-mage-upgrade-plan.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json','tools/reference/weapon-event-order-review.json']]
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log']]
    files += [STAGE/(name+'-fixtures.json') for name in ['life-tap-cast','iron-maiden','lower-resist','decrepify','weaken','amplify-damage','inner-sight','cloak','life-tap','fire-golem-birth','fire-golem-absorption','fire-golem-combat','fire-golem-aura','fire-golem-death']]
    result=dict(referenceVersion='3.3.93847',component='Life Tap learned casting and actual fractional percentage helper',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=491,retainedPredecessorMarkers=476,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[849,21],unchangedInstalledArtifacts=106,
            sourceEventContexts=22464,acceptedSourceEventContexts=1536,sourceReceiverContexts=384,nativeHealingArithmeticComparisons=22464,nativeReceiverComparisons=384,
            connectedRightQuickCasts=6,naturalExpiryChecks=6,naturalRank1Frames=400,mana256=2304,healPercent=50,handlers=186,necromancerHandlers=30),
        inputInventories={name:dict(path=str((STAGE/(name+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(name+'-inputs.json'))) for name in inputs},
        changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Learned Life Tap now shares do30/filter3 no-stat curse casting,eligibility,rank/creator arbitration,radius4+(rank-1),cost9mana and400+60*(rank-1)frame duration. Weaker ranks reject;equal refresh retains creator;higher/different replaces;Attract/Possessed reject.',
            'LifeTapHealing256 uses the actual shared source percentage helper,including early division above1048576FP. At1048577physical/50percent healing is524250FP,not524288FP. Current50percent and source actor/state/cap behavior stay unchanged. Existing fraction/ranged/Smite/overkill/zero-drain/saved-missile tests remain retained.',
            '22464complete event/actual-percentage contexts and384no-stat receiver contexts pass ASan/UBSan;native helper/receiver differentials match. Actual NPC large fractional healing,maximum/zero/expired/absent guards and native pre-victim-life-clip overkill healing pass. Retained Iron Maiden event/death/owner-credit regression remains verified.',
            'Six real right/quick casts retain unmodified16second expiry and once-only payment. Two reloads preserve rank/caster/deadline,mana,real Wormskull rank21/binding,legacy curse shape and fractional healerHP with resumed healing. Persistence inspection alone holds deadlines1day.',
            'The retained Poison Explosion saved inspection corpse explicitly uses the existing one-hour fixture retention instead of native seven-minute monster-corpse decay; its cloud,reservation and loot checks remain strict and separately diagnostic. Gameplay corpse decay is unchanged.',
            '491native markers retain all476predecessor markers across3boots/2reloads/3saves. Four audits,71Python,573unchanged named decisions and clean builds pass. Runtime data,staged kernel and106installed artifacts remain unchanged.'],
        remaining=[
            'Actual older callback/percentage bodies execute with declared unit/stat/owner/formula/overlay services;the new scaffold is safely extracted as data from the prior producer without running it or overwriting accepted artifacts. Full source host/current executable,registration,mitigation/resource/HP/death,PvP/world/client chronology remains separate.',
            'The22464-row native differential takes the source event guard result as a gate and compares healing arithmetic/output. It does not independently execute all22464native guard paths;actual max/zero/absent/expired and native overkill controls are separate fixture cases.',
            'Current healing percent50is supported,with synthetic0..100controls. Values outside that domain explicitly refuse. Active legacy elite Amplify compound replacement lacks rank/caster metadata and stays reported/skipped;full species/room/flags/allegiance/geometry and special missile behavior remain open.',
            'No new Mobile/save schema,handler,class completion,deployment or broadfull-class claim. Final effects/audio and tree/right/quick icons remain separate placeholders. Approved pet enhancements remain design-only.'])
    dest=ROOT/'tools/reference/life-tap-cast-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Life Tap casting:491native markers/3boots/2reloads,6real16second casts,actual source helper;4audits/71Python and installed preservation verified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
