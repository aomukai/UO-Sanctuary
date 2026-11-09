#!/usr/bin/env python3
"""Qualify a concrete unapproved Ward policy; no D2R event parity inferred."""
import argparse,json,subprocess
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/psychic-ward-proposal';PARENT='9096bb1ae7f31a8b77e902d988179ade3f7c86fc'
def record(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(ROOT))]=e.sha(path)
        return json.loads(path.read_text())if path.suffix=='.json'else path.read_bytes()
    proposal=pin(ROOT/'src/Sanctuary.Content/PsychicWardReactionProposal.cs').decode();assert 'internal const bool Approved=false;'in proposal
    for name in ['PsychicWard.cs','DefensiveArmors.cs','SkillCombat.cs','WarlockCombat.cs','GenericPetStartupProposal.cs','CharacterProfiles.cs','SkillHealth.cs','ClassStats.cs']:
        path='src/Sanctuary.Content/'+name;assert (ROOT/path).read_bytes()==subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT),path
    # No live capture or review caster registration in production source.
    for p in (ROOT/'src/Sanctuary.Content').glob('*.cs'):
        assert 'PsychicWardReactionProposal.Capture('not in p.read_text(),p
        if p.name!='PsychicWardCastingProposal.cs':assert 'PsychicWardCastingProposal.'not in p.read_text(),p
    path='src/Sanctuary.Content/PreparedWeaponEvents.cs';before=subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT).decode()
    after=before.replace('int absorbedLife256=0,Action<int> leech=null)','int absorbedLife256=0,Action<int> leech=null,PsychicWardReactionProposal.Ticket ward=null)')
    after=after.replace('        ClayGolemSlow.Incoming(attacker,defender,ranged,TimedEffects.Now);','        // Review-only ticket:captured before mitigation,consumed once here.\n        // Existing production callers supply null until explicit approval.\n        ward?.Deliver(attacker,defender,ranged,TimedEffects.Now);\n        ClayGolemSlow.Incoming(attacker,defender,ranged,TimedEffects.Now);')
    assert pin(ROOT/path).decode()==after
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs']for n in [1,2,3]]
    for old in snapshots[:-1]:
        assert old['projectInputs']==snapshots[-1]['projectInputs']and old['copiedData']==snapshots[-1]['copiedData']
        assert [n for n in old['sourceInputs']if old['sourceInputs'][n]!=snapshots[-1]['sourceInputs'][n]]==['tests/Sanctuary.Integration/PsychicWardProposalChecks.cs']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    for kind,number in [('content',1),('integration',3)]:
        name=f'Sanctuary.{kind.title()}.dll';assert e.sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
        log=pin(STAGE/f'{kind}-build-{number}.log').decode();assert all(s in log for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for number in [1,2]:
        folder=STAGE/f'diagnostics-{number}';log=pin(folder/'birth-native-checks.log').decode();assert 'Ward proposal windup:'in log and 'Ward proposal reaction:7native packet cases'in log and 'psychic-ward-proposal-reload-check: failed'in log
        pin(folder/'birth-inputs.json');pin(folder/'PsychicWardProposalChecks.cs');pin(folder/'integration/Sanctuary.Integration.dll')
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'psychic-ward-proposal-check':1,'psychic-ward-proposal-reload-check':2})
    log=e.cohort(STAGE,expected,12)
    for message in ['Ward proposal windup:','Ward proposal reaction:7native packet cases','Ward proposal original:saved Warlock/Witch fractional pools']:assert log.count(message)==3,message
    pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');fixture=pin(STAGE/'psychic-ward-proposal-fixtures.json');assert fixture['Generation']==2 and len(fixture['Owners'])==2
    assert [o['Female']for o in fixture['Owners']]==[False,True]and[o['State']['Remaining256']for o in fixture['Owners']]==[641,642]
    native=ROOT/'.local/class-skills/player-melee-return/native/Projects/Server/Mobiles/Mobile.cs';text=pin(native).decode();assert 'public virtual void CheckStatTimers()'in text and 'm_HitsTimer ??= new HitsTimer(this);'in text and 'm_ManaTimer ??= new ManaTimer(this);'in text
    report=pin(ROOT/'data/Sanctuary/Balance/class-skill-status.json');rows=report['skills'];ward=next(r for r in rows if r['talentId']=='warlock.psychicward');assert ward['implementation']=='blocked'and not ward['complete']
    assert len([r for r in rows if r['implementation']=='handler-present'])==213
    protected=e.protection('tools/reference/ring-cast-client-evidence.json',43)
    for name in ['tools/skill_checkpoint_evidence.py','tools/reference/creeper-frame-installation-evidence.json','.local/class-skills/creeper-frame-installation.json','tools/reference/ring-cast-client-evidence.json']:
        value=pin(ROOT/name)
        if name.endswith('ring-cast-client-evidence.json'):pin(ROOT/value['inputManifest']['path'])
    for name in ['docs/psychic-ward-reaction-proposal.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skill-reuse-notes.md','docs/skill-mechanical-reference.md','tools/build_skill_roadmap.py','tests/check_psychic_ward_proposal.py']:pin(ROOT/name)
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',handlers=213,warlockHandlers=4,completedClasses=[],proposal=True,approved=False,currentEventParity=False,productionCaptureEnabled=False,learnedEnabled=False,newReferenceReadBytes=0,oldLargeOfflineMatricesRerun=0,
        nativePacketCasesPerBoot=7,originalSavedOwners=2,nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,newTimers=0,newSavedFormats=0,artifacts=born['artifacts'],preservation={**protected,'installed':False},
        verified=['Concrete candidate captures an eligible pre-absorption physical NPC melee registration and delivers once through shared incoming-return hook after attacker returns,before slow/LifeTap/leech/Thorns/HP. Actual mitigation/stun/HP services cover fractional/full absorption,depletion/overflow and excluded elemental/ranged/miss/zero contacts. No production caller captures tickets.',
            'Review self-cast reuses shared WKSC clock/session and existing do18 payment/group helper. Unpaid windup,Stop/movement/frozen/late-mana cancellation,one release/payment and allocation failure pass;public learned/bindings and approval guard refuse.',
            'Saved original Warlock/Witch pools,wounds,mana/rank/stun survive two reloads with native regeneration disabled only for named review fixtures. Initial diagnostics retained normal elapsed regeneration to full resources while Ward stayed exact;test isolation changes no gameplay recovery rule. Content reused with only test changes across source snapshots.'],
        limits=['Event37/40eligibility/order remain protected/unverified. Proposed min-pool/fully-absorbed/breaking-hit reaction is an explicit Sanctuary choice awaiting approval,not recovered source parity.',
            'No automatic native melee capture,public/connected input,EnergyShield coexistence,player-attacker/PvP certification,new timer/save schema,deployment or final artwork. Normal demon startup approval and other skills remain separate.'],inputs=inputs)
    path=ROOT/'tools/reference/psychic-ward-proposal-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Ward candidate:12markers/3boots/2reloads,7native packet cases per boot,2saved original owners;approval/public input remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
