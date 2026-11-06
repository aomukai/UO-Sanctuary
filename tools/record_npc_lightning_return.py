#!/usr/bin/env python3
"""Strict learned original-item incoming lightning acceptance and retained cohorts."""
import argparse
import sys
from pathlib import Path

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-lightning-return'
REPEAT=BASE/'npc-lightning-return-repeat'
RETAINED=BASE/'npc-lightning-return-retained'
FREEZE=BASE/'npc-lightning-return-freeze'
FULL=BASE/'npc-lightning-return-full-fill'
PARENT='e70cfc48526b43f7e1bd8eea4c69f9e7dc3d571a'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_lightning_return_retained import settings
from check_npc_damage_fill import configuration
from record_npc_item_blind import families

CHANGED=['src/Sanctuary.Content/'+name for name in (
    'NpcLightningReturn.cs','NpcCombatEvents.cs','NpcItemProcGraph.cs','ReviveEquipmentEvents.cs',
    'PreparedMonsterDamage.cs','PreparedWeaponEvents.cs','IronGolemContact.cs')]
CHANGED+=['tests/Sanctuary.Integration/'+name for name in (
    'NpcLightningReturnChecks.cs','NpcFreezeHitChecks.cs','NpcItemBlindChecks.cs',
    'ReviveEquippedChecks.cs','ReviveEventChecks.cs')]


def build(check=False):
    born=e.load(STAGE/'birth-inputs.json')
    for stage,file in [(s,'birth-inputs.json')for s in(STAGE,REPEAT,RETAINED,FREEZE)]+[(STAGE,'audit-inputs.json')]:
        current=e.load(stage/file)
        assert current['artifacts']==born['artifacts'] and current['copiedData']==born['copiedData']
        snapshot(STAGE/'source-snapshot-13.json',stage/file)
        for path,digest in current['sourceInputs'].items():assert e.sha(ROOT/path)==digest,path
    for kind,n in [('Content',4),('Integration',13)]:
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(s in(STAGE/f'{kind.lower()}-build-{n}.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    # The final test-only repairs do not rebuild or modify production inputs.
    old=e.load(STAGE/'source-snapshot-8.json')['inputs'];new=e.load(STAGE/'source-snapshot-13.json')['inputs']
    assert old['projectInputs']==new['projectInputs'] and old['copiedData']==new['copiedData']
    assert {p:h for p,h in old['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in new['sourceInputs'].items()if not p.startswith('tests/')}
    assert {p for p in old['sourceInputs']if old['sourceInputs'][p]!=new['sourceInputs'][p]}=={
        'tests/Sanctuary.Integration/NpcItemBlindChecks.cs','tests/Sanctuary.Integration/ReviveEventChecks.cs',
        'tests/Sanctuary.Integration/NpcFreezeHitChecks.cs'}
    # Full-fill completed on frozen Integration12 and the identical production
    # DLL/data. The sole later C# difference is private Contact in a fixture
    # that this cohort does not execute; its Configure and reused helpers are
    # byte-identical. Keep both actual assembly/input identities explicit.
    full=e.load(FULL/'birth-inputs.json');frozen=e.load(FULL/'source-snapshot-12.json')['inputs']
    assert full['copiedData']==born['copiedData']==frozen['copiedData']==new['copiedData']
    assert {p:h for p,h in full['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:h for p,h in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    assert e.sha(FULL/'integration/Sanctuary.Integration.dll')==full['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert {p:h for p,h in full['sourceInputs'].items()if p.endswith('.cs')}==frozen['sourceInputs']
    freeze_file='tests/Sanctuary.Integration/NpcFreezeHitChecks.cs'
    assert {p for p in frozen['sourceInputs']if frozen['sourceInputs'][p]!=new['sourceInputs'][p]}=={freeze_file}
    original=FULL/'frozen-integration-12/NpcFreezeHitChecks.cs';assert e.sha(original)==frozen['sourceInputs'][freeze_file]
    before=original.read_text();after=(ROOT/freeze_file).read_text()
    def split(text):
        start=text.index('    private static void Contact(');end=text.index('    private static void Filled(',start)
        return text[:start],text[start:end],text[end:]
    a,b,c=split(before);d,changed,f=split(after)
    assert a==d and c==f and 'SourceBlocking.BlockRoll=n=>n-1;'in changed and 'frames=(prepared.Impact.Packet.ResultFlags&1)!=0?'in changed
    for path,digest in full['sourceInputs'].items():
        if path!=freeze_file:assert e.sha(ROOT/path)==digest,path
    source_path=ROOT/'tools/reference/npc-lightning-return-source-evidence.json';source=e.load(source_path)
    assert source['contexts']==dict(damage=864,absorb=144,guards=455) and len(source['headerBindings'])==140
    assert len(source['completeRoutineHashes'])==7 and not source['currentExecutableParity']
    assert [source['headerBindings'][k]for k in('STAT_ITEM_ATTACKERTAKESLIGHTDAMAGE','UNITFLAG_CANBEATTACKED','DAMAGERESULTFLAG_32')]==[128,4,32]
    for group in('inputs','artifacts'):
        for path,digest in source[group].items():assert e.sha(ROOT/path)==digest,path
    assert not(BASE/'npc-lightning-return-source/sanitizer.log').read_bytes()
    for stage in(STAGE,REPEAT):
        text=e.cohort(stage,families(('npc-lightning-return',)),12)
        assert text.count('NPC lightning return:learned original Coif Iron/frozen Revive,')==3
        held=e.load(stage/'npc-lightning-return-fixtures.json')
        assert held['Generation']==2 and len(held['Actors'])==2 and {a['Revive']for a in held['Actors']}=={True,False}
        assert all(a['Owner']!=a['Source'] and a['Life']>0 and a['Seed']>0 and a['Gear']for a in held['Actors'])
    c=settings();extra={'monster-contact-check':3,'iron-golem-ordered-contact-check':3,
        'npc-flat-drains-timer-check':3,'npc-flat-drains-timer-result':3,'blind-policy-check':3,
        'venom-result':1,'venom-reload-result':2,'revive-native-swing-result':3,
        'revive-native-swing-save-result':3,'blind-world-live-result':3,'blood-life-tap-live-result':3}
    e.cohort(RETAINED,families(c['extra_families'],extra),78)
    for name in('npc-flat-drains','revive-event','revive-native-swing','blind-world','blood-life-tap','npc-item-blind'):
        assert e.load(RETAINED/(name+'-fixtures.json'))['Generation']==2
    e.cohort(FREEZE,families(('npc-freeze-hit',)),12)
    assert e.load(FREEZE/'npc-freeze-hit-fixtures.json')['Generation']==2
    c=configuration(compact=True);e.cohort(FULL,families(c['extra_families']),60)
    assert 'npc-freeze-hit' not in c['extra_families']
    originals=e.load(FULL/'generated-npc-stats-fixtures.json')
    assert originals['Generation']==2 and len(originals['Holders'])==1566
    assert sum(len(r['Items'])for r in originals['Holders'])==1674
    assert len(e.load(FULL/'generated-npc-live-resources-fixtures.json')['Actors'])==5
    bridge=e.load(FULL/'bridge-evidence.json')
    assert bridge['contexts']==96 and bridge['generation']==2
    for path,digest in bridge['artifacts'].items():assert e.sha(ROOT/path)==digest,path
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(x['exitCode']==0 for x in commands)
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present'for s in status['skills'])==197
    files=[Path(__file__).resolve(),source_path,ROOT/'tools/skill_checkpoint_evidence.py']+[ROOT/p for p in CHANGED]
    files +=[ROOT/p for group in('inputs','artifacts')for p in source[group]]
    files +=[ROOT/'tests'/name for name in('check_npc_lightning_return.py','check_npc_lightning_return_retained.py','check_npc_lightning_return_freeze.py','check_npc_lightning_return_full_fill.py')]
    files +=[ROOT/'docs'/name for name in('npc-lightning-return-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md',
        'skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','necromancer-mechanical-audit.md','pet-adjustment-review.md')]
    files +=[ROOT/p for p in bridge['artifacts']]+e.stage_files(STAGE,REPEAT,RETAINED,FREEZE,FULL)
    files +=[FULL/'integration/Sanctuary.Integration.dll',original]
    archives=[]
    for stage in(STAGE,REPEAT,RETAINED,FREEZE,FULL):
        for directory in stage.iterdir():
            if not directory.is_dir()or not directory.name.startswith(('failed-','interrupted-','prior-')):continue
            files+=e.stage_files(directory)
            for kind in('Content','Integration'):
                path=directory/kind.lower()/f'Sanctuary.{kind}.dll'
                if path.exists():files.append(path)
            archives.append(str(directory.relative_to(ROOT)))
    record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=4,integrationBuild=13,sourceSnapshot=13,
        fullFillArtifacts=full['artifacts'],fullFillIntegrationBuild=12,
        fullFillFixtureDifference='Only private NpcFreezeHitChecks.Contact changed after full-fill;not executed there. Configure,reused helpers,all other C#,production DLL and data identical;both test assemblies preserved.',
        sourceContexts=source['contexts'],completeSourceRoutines=7,headerConstants=140,newMarkers=12,repeatMarkers=12,
        retainedMarkers=78,freezeMarkers=12,fullFillMarkers=60,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        originalNativeHolders=1566,originalNativeItems=1674,originalItemReplayContexts=96,nativePayloadRestoredFromManifest=False,
        pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,preservedArchives=archives,
        scope='Original learned unmodified Coif Iron/frozen Revive stat128 incoming melee callback;one actual suppressed lightning missile HP/credit/return packet. Legacy or graph owns incoming dispatch once;native AOS has a separate qualified entry. Preflight precedes mutation;Iron Maiden precedes defender lightning. Existing shared mitigation and target-preservation scope reused. Direct resistance/immunity,zero-physical phase,nested lethal/ordering,forced native target changes,ordinary post-scope targeting,RNG/mana/outgoing-proc exclusions,actual native weapon/ranged/spell routes,original wounded actors/items/subscriptions and real resume survive two reloads. Retained blind now controls independent shield/block inputs and authenticates its reload socket;no production blocking/network change.',
        limits=[
            'Older source composes seven unchanged bodies with declared world/event/native kill/recovery/regeneration services;not modern executable/global NPC seed/PvP/client parity.',
            'Player recipients,unadapted Paragon/Lightning Enchanted recovery,uninterruptible death-delay,unsupported items/events/native modifiers and native scalar packet-dependent on-struck curse carriers remain explicit refusals. Existing native regeneration clock retained.',
            'Current reproduced blind mismatch is isolated by opposite defender block draws with the same attacker seed;the older undiagnosed blind native2log cannot be retroactively assigned an exact cause.',
            'Alternate-column6000resume failure lacks field diagnostics and remains untraced. Known valid6100actual resume acceptance does not certify all terrain/landing behavior.',
            'Earlier fixture failures and explicit full-fill interruptions are preserved;partial runs are not completed acceptance. Independent native event-order review corrected Iron Maiden before lightning.',
            'Natural pursuit/outgoing Coif A1 for that exact equipped original and other item callback families remain further gameplay acceptance. No completed class,installed save/config/client change,new icon identity or final asset.0x374A/hue0x481is the identified lightning contact placeholder.'])
    e.write('npc-lightning-return-native-evidence.json',record,files,check)
    print('NPC lightning:source1463/140constants;native12+12+78+12+60/two reloads;71Python/fresh audits/protection;explicit remaining scopes.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
