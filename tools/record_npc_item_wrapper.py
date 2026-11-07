#!/usr/bin/env python3
"""Bind protected original item wrapper state and actual saved projectiles."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-wrapper';WIDE=BASE/'npc-item-wrapper-retained';ISO=BASE/'npc-item-wrapper-isolate';SHARED=BASE/'npc-item-wrapper-shared'
PARENT='aeec1eef48484cc9ca3474ca99d55906c68bb034'
def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');parent=e.load(ROOT/'tools/reference/npc-item-chain-root-native-evidence.json');extra=Counter({'npc-item-wrapper-check':1,'npc-item-wrapper-reload-check':2})
    for previous,stage,count in [(BASE/'npc-item-chain-root',STAGE,90),(BASE/'npc-item-chain-root-retained',WIDE,398)]:
        p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
    e.cohort(ISO,expected({'extra_families':['npc-item-wrapper']}),12)
    sys.path.insert(0,str(ROOT/'tests'));from check_npc_item_wrapper import configuration
    e.cohort(SHARED,expected(configuration(shared=True)),255)
    for stage in [STAGE,WIDE,ISO,SHARED]:
        assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text()
        if stage in [ISO,SHARED]:
            snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts'] and other['copiedData']==born['copiedData']
            for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    phrase='NPC original item wrappers:320target/position/flag/failure/nested restoration cases,ordinary combatant and live serverdo state,notification-after-restore,no owner payment/RNG;five actual spell bodies and89saved partial projectiles resume natural exactHP/RNG through two reloads;full original graph/proc/game stream remain separate'
    for stage in [STAGE,WIDE,ISO]:
        assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
        s=e.load(stage/'npc-item-wrapper-fixtures.json');assert s['Generation']==2 and len(s['Spells'])==88 and len(s['Chains'])==1 and len(s['Items'])==7 and len(s['Held'])==5
        b=json.loads(s['Body']);binding=b['EquipmentWeapons']['Identity'];assert b['Owner']==s['Owner'] and s['Random']['Actor']==s['Actor'] and s['Random']['ValkyrieBinding']==binding
        r=json.loads(s['Resources']);assert r['Actor']==s['Actor'] and r['Owner']==s['Owner']
        assert {h['Skill']for h in s['Held']}=={36,38,44,48,53}
        counts=Counter(f['Npc']['Packet']['Skill']for f in s['Spells']);assert counts=={36:1,38:3,44:42,48:42}
        for f in s['Spells']:
            n=f['Npc'];p=n['Packet'];h=next(h for h in s['Held']if h['Skill']==p['Skill'])
            assert f['Owner']==s['Owner'] and n['Actor']==s['Actor'] and n['Binding']==binding and p['Rank']==f['Rank']==3 and f['Frame']==1 and f['Id']in h['Ids'] and h['Life']==65000*256
            if p['Skill']in(44,48):assert f['RadialIndex']%3!=0 and f['Id']==f['Cast']+f['RadialIndex']
            if p['Skill']==38:assert f['Charged']['Ordinal']in[1,2,4] and len(f['Charged']['Path'])==39
        f=s['Chains'][0];n=f['OriginalNpc'];h=next(h for h in s['Held']if h['Skill']==53)
        assert h['Ids']==[f['Cast']] and h['Life']==65000*256 and f['Owner']==s['Owner'] and n['Source']['Actor']==s['Actor'] and n['Source']['Binding']==binding
        assert n['Source']['Packet']['Skill']==53 and n['Source']['Packet']['Rank']==f['Rank']==3 and n['Ordinal']==0 and f['Remaining']==5 and f['Frame']==1
    for n in ['content-build-1.log','integration-build-1.log']:assert all(t in(STAGE/n).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text() and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
    source=e.load(ROOT/'tools/reference/item-effect-casts-source-evidence.json');assert source['cases']==18912 and len(source['completeRoutineHashes'])==3
    for p,h in source['artifacts'].items():assert e.sha(ROOT/p)==h,p
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
    files=e.stage_files(STAGE,WIDE,ISO,SHARED)+[Path(__file__).resolve(),ROOT/'tests/check_npc_item_wrapper.py',ROOT/'tests/Sanctuary.Integration/NpcItemWrapperChecks.cs',ROOT/'tools/reference/item-effect-casts-source-evidence.json']
    files+=[ROOT/'docs'/n for n in ['npc-item-wrapper-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,originalProcDispatch=False,currentExecutableParity=False,
        sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedRun=1,retainedRun=1,isolatedRun=1,sharedRun=1,auditRun=1,focusedMarkers=90,retainedMarkers=398,isolatedMarkers=12,sharedMarkers=255,wrapperControlsPerBoot=320,savedPartialProjectiles=89,originalItems=7,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Five original NPC item spell bodies enter the shared ItemEffectCasts wrapper/common executor through full actual metadata and protected dispatch. Eight existing do30 curses reuse the same base temporary-frame services with their original executor;all87/395parent markers remain.',
            '320target/position/original-target/flag/failure/nested cases per boot verify ordinary combatant protection,shared synchronous nested state,allocation-visible serverdo,exception cleanup,notification-after-restore and unchanged owner/source resources. No-attempt direct calls leave the pool unchanged.',
            '89wrapper-created partial projectiles cover all5spells and retain real source items/resources/wounds and own streams through two actual reloads;loaded intervals do not replay and natural exact HP/RNG resumes. Shared255player/Mage/LightningStrike input/live/save consumers remain.'],
        limits=['This is an internal prepared original-Valkyrie capability,not automatic admission of27original proc-bearing loadouts or a learned cast. Full original stat-graph registrationhead/order/history and actual world/game allocation stream remain separate;no filtering or event-only item copies are introduced.',
            'Ambient target/path/flag state is shared only during synchronous execution,not persisted AI/controller state. Other random/corpse/last-attacker/start services refuse. Provider side effects are not transactional;current binary callback/dispatch/target/NHD/critical/removal,nonzero shields/PvP and complete AI/initializer/class/client remain open.',
            'No native/compiler failure this stage. Charged Strike retains its separate legacy RNG follow-up. Final spell/proc/impact/audio and talent/hotkey art remain separately identified placeholders;no deployment.'])
    e.write('npc-item-wrapper-native-evidence.json',result,files,check)
    print('NPC original wrappers:90focused/398retained/12isolated/255shared markers,two reloads;320nested/restoration controls and89saved5spell projectiles;202handlers,full graph/proc/cast remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
