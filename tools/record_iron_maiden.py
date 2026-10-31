#!/usr/bin/env python3
"""Seal tested Iron Maiden fractional event, shared retaliation and saved casting evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/iron-maiden'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={name:inventory.validate_inventory(STAGE/(name+'-inputs.json')) for name in ['birth','audit']}
    for name,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(name+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts'] and value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/lower-resist-native-evidence.json';prior=json.loads(previous.read_text());ref=prior['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    added=set(after)-set(before);changed={p for p in before if before[p]!=after[p]}
    assert added=={'src/Sanctuary.Content/IronMaiden.cs','tests/Sanctuary.Integration/IronMaidenChecks.cs'}
    assert changed=={'src/Sanctuary.Content/'+name+'.cs' for name in ['GearEffects','NecromancerCurseCasts','NecromancerEffects','PaladinSmite','PaladinThorns','PoisonDaggerContact','PreparedMonsterCredit','PreparedMonsterDamage','PreparedWeaponEvents','SkillCombat','WeaponMissiles']}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/lower-resist/birth-native-checks.log';assert sha(baseline)==prior['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'iron-maiden-check':1,'iron-maiden-reload-check':2,'iron-maiden-right-check':3,'iron-maiden-right-result':3,'iron-maiden-quick-check':3,'iron-maiden-quick-result':3})
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==476 and sum(markers(baseline).values())==461
    text=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',849),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/lower-resist'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in a['InputHashes'].items():assert sha(ROOT/path)==digest,path
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    event_path=ROOT/'tools/reference/iron-maiden-source-evidence.json';event=json.loads(event_path.read_text())
    assert event['contexts']==dict(event=8640,legacyBloodGolemTail=15552) and not event['hostStaticReview']['executed']
    receiver_path=ROOT/'tools/reference/iron-maiden-receiver-source-evidence.json';receiver=json.loads(receiver_path.read_text());assert receiver['receiverContexts']==384
    for proof in [event,receiver]:
        for path,digest in proof.get('inputs',{}).items():assert sha(ROOT/path)==digest,path
        for path,digest in proof['artifacts'].items():assert sha(ROOT/path)==digest,path
    for key in ['reusedReference','reusedReceiverCpp','pinnedTable']:assert sha(ROOT/receiver[key]['path'])==receiver[key]['sha256']
    saved=json.loads((STAGE/'iron-maiden-fixtures.json').read_text());assert saved['Generation']==2 and saved['Curse']['Rank']==21 and saved['Old']['Rank']==1
    assert all('PhysicalDelta' not in saved[k] and 'ElementDeltas' not in saved[k] for k in ['Curse','Old'])
    lower=json.loads((STAGE/'lower-resist-fixtures.json').read_text());assert lower['Generation']==2 and lower['Curse']['ElementDeltas']==dict(Fire=-63,Lightning=-63,Cold=-63,Poison=-63)
    assert lower['PartialCurse']['ElementDeltas']==dict(Fire=None,Lightning=-6,Cold=-6,Poison=-6) and 'ElementDeltas' not in lower['Old']
    for name in ['fire-golem-birth','fire-golem-absorption','fire-golem-combat','fire-golem-aura','fire-golem-death','amplify-damage','weaken','decrepify']:
        assert json.loads((STAGE/(name+'-fixtures.json')).read_text())['Generation']==2
    files=[previous,event_path,receiver_path,Path(__file__),*[ROOT/p for p in added|changed]]
    files += [ROOT/p for p in ['tools/probe_iron_maiden.py','tools/probe_iron_maiden_receiver.py','tests/check_iron_maiden.py','tests/check_iron_golem_birth.py','tools/build_class_skills.py',
        'docs/iron-maiden-checkpoint.md','docs/iron-maiden-source-review.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/pet-adjustment-review.md','docs/skeletal-mage-upgrade-plan.md','docs/skill-effects.md','docs/skill-icons.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json','tools/reference/weapon-event-order-review.json']]
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log']]
    files += [STAGE/(name+'-fixtures.json') for name in ['iron-maiden','lower-resist','decrepify','weaken','amplify-damage','inner-sight','cloak','life-tap','fire-golem-birth','fire-golem-absorption','fire-golem-combat','fire-golem-aura','fire-golem-death']]
    result=dict(referenceVersion='3.3.93847',component='Iron Maiden fractional attacker event and shared no-stat curse casting',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=476,retainedPredecessorMarkers=461,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[849,21],unchangedInstalledArtifacts=106,
            sourceEventContexts=8640,sourceLegacyBloodGolemTailContexts=15552,sourceReceiverContexts=384,nativeEventArithmeticComparisons=1008,nativeReceiverComparisons=384,
            connectedRightQuickCasts=6,naturalExpiryChecks=6,naturalRank1Frames=300,mana256=1280,handlers=186,necromancerHandlers=30),
        inputInventories={name:dict(path=str((STAGE/(name+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(name+'-inputs.json'))) for name in inputs},
        changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Learned Iron Maiden shares do30/filter3 curse casting,eligibility,rank/creator arbitration,radius7,cost5mana and300+60*(rank-1)frame duration. No aura stat,new Mobile type or save schema is fabricated.',
            'Fractional pre-life-clip physical reflection runs in the attacker damage-event phase before defender Life Tap. Ordinary monster percentage is200+25*(rank-1);player/hireling branch selects the quarter formula. Shared SourcePercentage preserves early division;unrepresentable signed intermediates explicitly refuse/log rather than wrap.',
            'Native and prepared boundaries deliver Iron Maiden once;post-hit compatibility callbacks suppress repeats. Shared physical retaliation uses existing mitigation/health,including Bone Armor and prepared Psychic Ward,and adds no weapon/mastery/potion damage or recursive weapon event.',
            'A synchronous prepared-packet owner snapshot preserves responsible damage and owner attribution when reflection retires the attacking pet before original HP commitment. Native/prepared reflected death and continuation,overkill before physical clipping,Life Tap order,physical resistance/pools,quarter-rate player Ward,source arithmetic,rank/caster/replacement and area/payment/cancel/input checks pass.',
            'Six real right/quick casts retain normal12second expiry. Two reloads preserve original deadlines,mana,gear/binding,target-bound legacy/current curse records and resumed effective-rank fractional native return. Persistence inspection alone holds saved deadlines1day;component event fixtures supply their own explicit curse state.',
            '476native markers retain all461predecessor markers over3boots/2reloads/3saves. Four audits,71Python,573unchanged named decisions and clean builds pass. Runtime data,staged kernel and106installed artifacts remain unchanged.'],
        remaining=[
            'Source harness executes8640general/15552legacy-tail/384receiver contexts with declared unit/stat/formula/downstream services. Native positive-rank arithmetic comparison covers1008event rows;rank0VM behavior and the uncorroborated enemyBloodGolem healing tail are not enabled. The tail is explicitly warned for unowned Blood minions.',
            'Complete current executable,within-phase NPC/item event-registration order,full native mitigation/resource/status/HP/death/mode chronology,PvP/hireling learned casts,missile exceptions,source world/geometry and graphical-client parity remain open. Source flags16417/hitclass141 are retained in reference evidence;the native return uses the existing direct retaliation adapter,not a complete source packet emitter.',
            'Prepared ownership capture covers qualified prepared packets;preexisting native-only pet bridges retain their broader credit/lifecycle limits. Psychic Ward reactive event37 timing stays gated;only its existing physical-pool consumer is reused.',
            'No complete class,deployment or broadfull-class claim. Effects/audio and talent/right/quick icons remain separate identified placeholders. Approved Necromancer pet enhancements are design-only;their unresolved details remain explicit.'])
    dest=ROOT/'tools/reference/iron-maiden-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Iron Maiden:476native markers/3boots/2reloads,6real12second casts;source,4audits/71Python and installed preservation verified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
