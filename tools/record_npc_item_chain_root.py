#!/usr/bin/env python3
"""Bind actual do26/151 item roots,budget ordering and saved natural hops."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-chain-root';WIDE=BASE/'npc-item-chain-root-retained';ISO=BASE/'npc-item-chain-root-isolate';SHARED=BASE/'npc-item-chain-root-shared'
PARENT='657ea65763659e0fd4482acda8c319e5f4b6c157'
def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');parent=e.load(ROOT/'tools/reference/npc-charged-item-native-evidence.json');extra=Counter({'npc-item-chain-root-check':1,'npc-item-chain-root-reload-check':2})
    for previous,stage,count in [(BASE/'npc-charged-item',STAGE,87),(BASE/'npc-charged-item-retained',WIDE,395)]:
        p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
    e.cohort(ISO,expected({'extra_families':['npc-item-chain-root']}),12)
    sys.path.insert(0,str(ROOT/'tests'));from check_npc_item_chain_root import configuration
    e.cohort(SHARED,expected(configuration(shared=True)),255)
    for stage in [STAGE,WIDE,ISO,SHARED]:
        assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
        if stage in [ISO,SHARED]:
            snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
            for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    phrase='NPC item Chain roots:do26/151 actual factory origins/ownAR/zero-vector,budget after successful handle,126rank/flag roots,lone/five-hop/failure and targetless/range guards;shared per-hop beforeHP/exact HP/RNG;partial actual item root+child/original items/resources/wounds through two reloads;full wrapper/proc/game stream remain separate'
    for stage in [STAGE,WIDE,ISO]:
        assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
        s=e.load(stage/'npc-item-chain-root-fixtures.json');assert s['Generation']==2 and len(s['Flights'])==len(s['Held'])==2 and len(s['Items'])==7
        b=json.loads(s['Body']);binding=b['EquipmentWeapons']['Identity'];assert b['Owner']==s['Owner'] and s['Random']['Actor']==s['Actor'] and s['Random']['ValkyrieBinding']==binding
        r=json.loads(s['Resources']);assert r['Actor']==s['Actor'] and r['Owner']==s['Owner']
        assert {f['Cast']for f in s['Flights']}=={h['Cast']for h in s['Held']} and {f['OriginalNpc']['Ordinal']for f in s['Flights']}=={0,1}
        for f in s['Flights']:
            n=f['OriginalNpc'];p=n['Source']['Packet'];h=next(h for h in s['Held']if h['Cast']==f['Cast'])
            assert f['Owner']==s['Owner'] and n['Source']['Actor']==s['Actor'] and n['Source']['Binding']==binding and p['Skill']==53 and p['Rank']==f['Rank']==3
            assert p['Emissions']==5 and f['Remaining']==5-n['Ordinal'] and f['Frame']==1 and n['Seed']==(666<<32)|(7100+n['Ordinal'])
            assert f['NpcCaster']==f['NpcSeed']==0 and 'PlayerPacket'not in f and 'StrikeContext'not in f and n.get('Fault')is None
            if n['Ordinal']==0:assert not f['Delays'] and h['TargetLife']==h['PeerLife']==65000*256
            else:assert len(f['Delays'])==1 and h['TargetLife']<65000*256 and h['PeerLife']==65000*256
    for n in ['content-build-1.log','integration-build-1.log']:assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
    for file,count in [('npc-item-spell-origins-source-evidence.json',14112),('npc-chain-routes-source-evidence.json',11655)]:
        source=e.load(ROOT/'tools/reference'/file);assert source['cases']==count
        for group in ['inputs','currentTable','artifacts']:
            for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE,ISO,SHARED)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_chain_root.py',ROOT/'tests/Sanctuary.Integration/NpcItemChainRootChecks.cs']
    files+=[ROOT/'docs'/n for n in ['npc-item-chain-root-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
        sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,isolatedRun=1,sharedRun=1,auditRun=1,focusedMarkers=87,retainedMarkers=395,isolatedMarkers=12,sharedMarkers=255,worldRankFlagRootControlsPerBoot=126,savedItemChains=2,originalItems=7,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Common item do26/151 now creates actual original-NPC roots using a shared ordinary/item factory constructor. Source2xpositions,own AR,source range and moving zero-vector correction are retained. Private allocated handles begin budget0;only the actual successful-handle budget write validates/publishes them.',
            '126rank/flag roots,lone/five-hop/failed-root/failed-child and targetless/range cases exercise natural world travel,per-hop allocation before currentHP and exact own projectile/actor RNG. All84/392parent markers and255shared player/Mage/LightningStrike controls remain.',
            'Partial actual item root and already-created child preserve original items/resources/wounds and remaining budget through two actual reloads,age the first loaded interval without replay and resume natural contacts. A root recipient moves onto its continuing diagonal path;the child uses its ordinary target line.'],
        limits=['Actual world/game stream allocation and protected outer item-wrapper/full original registration/callback history remain explicit/unbound. Allocation providers may not mutate/reenter this pool;stale commits refuse and provider side effects are not transactional. No automatic27proc-loadout admission or learned cast.',
            'Current binary initial dispatch/target traversal/NHD/critical/callback/source-removal and nonzero shield/PvP recipients remain unverified or unsupported. Existing shared host elevation/terrain policies are retained,not a full geometry certificate. Charged Strike legacy RNG and full AI/initializer/class/client acceptance remain separate.',
            'No native or compiler failure this stage. Final link/proc/impact/audio and talent/hotkey assets remain separately identified placeholders;no deployment.'])
    e.write('npc-item-chain-root-native-evidence.json',result,files,check)
    print('NPC item Chain roots:87focused/395retained/12isolated/255shared markers,two reloads;actual do151 origin/handle budget and per-hop HP/RNG;202handlers,full proc/cast remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
