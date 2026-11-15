#!/usr/bin/env python3
"""Authenticate approved Eldritch learned input,shared clock/client and saves."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/eldritch-blast-cast';PARENT='6a1e4f459cd2379bd43411f1b3cc6c356e91fe33'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/eldritch-blast-world/birth-inputs.json');parent=pin('tools/reference/eldritch-blast-world-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'tests/Sanctuary.Integration/EldritchBlastCastChecks.cs'}
    changed={'shared/ClassAbilities.cs',*{'src/Sanctuary.Content/'+n+'.cs'for n in ('CharacterProfiles','ClassDeath','EldritchBlastWorld','TimedEffects','WarlockCombat')},
        'tests/Sanctuary.Integration/EldritchBlastSupplierChecks.cs','tests/Sanctuary.Integration/EldritchBlastWorldChecks.cs'}
    assert set(now)-set(old)==added and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert set(born['copiedData'])==set(previous['copiedData'])and{n for n in born['copiedData']if born['copiedData'][n]!=previous['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        path=STAGE/f'{kind}-build-1.log';pin(path);assert all(s in path.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('eldritch-blast-world','eldritch-blast-cast'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    for mode in ('left','right','quick'):expected.update({'eldritch-blast-cast-'+mode+'-check':3,'eldritch-blast-cast-'+mode+'-result':3})
    text=e.cohort(STAGE,expected,39);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==15
    for mode in ('left','right','quick'):
        assert text.count('eldritch-blast-cast-'+mode+': windup')==text.count('eldritch-blast-cast-'+mode+'-result: passed')==3
    assert text.count('Eldritch learned:SC/FCR/self and all bindings,one fractional payment/recast,cancel/low mana/movement,safe-area no recovery/reset,respec/class-switch cleanup pass')==1
    assert text.count('Eldritch learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay')==2
    assert text.count('Eldritch world:original capture/absolute clocks/seed/fractional wounds and mana/original helm/three Hex attachments persist')==2
    for name in ('eldritch-blast-world-fixtures.json','eldritch-blast-cast-fixtures.json'):assert pin(STAGE/name)['Generation']==2
    approval=pin('tools/reference/eldritch-blast-world-rule-approval.json')
    assert approval['approved']and approval['answer']=='Approve the complete Eldritch Blast rule (Recommended)'and approval['candidateCommit']==PARENT
    assert approval['questionItemId']=='["request_user_input_async","call_620b658f05fa4a6ab62296c95004319b",0]'
    assert approval['testedCandidateEvidenceSha256']==e.sha(ROOT/approval['testedCandidateEvidence'])
    original=subprocess.check_output(['git','show',PARENT+':'+approval['proposal']],cwd=ROOT)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    current=original.decode().replace('**Pending approval.**','**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/eldritch-blast-world-rule-approval.json). The body below is the original approved proposal; implementation/acceptance status is recorded separately.\n\n**Original proposal.**',1)
    assert current==(ROOT/approval['proposal']).read_text();pin(approval['proposal'])
    client=pin('tools/reference/eldritch-blast-cast-client-evidence.json');cm=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']and client['inheritedWarningMessages']==44
    assert client['baselineWarningsIdentical']and not client['installed']and client['intentionalSharedChanges']==['shared/ClassAbilities.cs']
    for n,h in client['files'].items():assert e.sha(ROOT/n)==h,n
    for group in ('patchHashes','sharedInputs'):
        for n,h in cm[group].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added|changed),'tests/check_eldritch_blast_cast.py','tools/record_eldritch_blast_cast.py','tools/build_skill_roadmap.py',
        'tools/prepare_eldritch_blast_client.py','tools/review_eldritch_blast_client.py','docs/eldritch-blast-cast-checkpoint.md','docs/eldritch-blast-world-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):
        pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.EldritchBlast'in code
    result=dict(referenceVersion='3.3.93847',component='Approved learned Eldritch Blast',handlers=228,warlockHandlers=19,completedClasses=[],learnedEnabled=True,automaticTickEnabled=True,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=39,markers=dict(expected),boots=3,reloads=2,saves=3,authenticatedSessions=15,
        actualInputs=9,inputModes=['left','right','quick'],actualAutoPulses=9,graphicalInputVerified=False,nativeRebuilt=False,clientRebuilt=True,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        approval='tools/reference/eldritch-blast-world-rule-approval.json',frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['Complete rule approved;normal handler/availability/self-SC and left/right/quick bind/input reuse existing dispatch. Actual packet casts normalize own-player hover,remain free during windup,pay once on release,and wait for actual first pulse on existing40ms clock. Nine such casts/automatic pulses across three boots.',
            'Learned SC/FCR timing,recast no stacking,cancellation,early/late/insufficient mana,movement,safe-area suspension without recovery/reset,respec/class-switch cleanup pass. Existing actual death cleanup is retained in world cohort.',
            'World three-Hex/snapshot/pinned numeric/FP256 damage/recovery/immunity/potion/legacy/refusal/clock controls retain. Explicit encounter immunity is distinguished from unmapped actual Hell Lich identity. Saved prepared world captures and attachments retain across two reloads under declared inspection dates.',
            'Saved Witch/original dagger/fractional wounds+mana/all bindings retain;fresh post-load learned cast works without replay. Socket actors are transient controls,not claimed saved. No old compiler matrices/kernel rebuild/newtimer/newprofile/version/liveD2Rcapture/artwork.',
            'Isolated client rebuild changes only shared SelfTarget membership.44existing patch/client source hashes unchanged;all44inherited warning messages identical,zero errors. No new client patch/roundtrip repetition/graphical run/install. Four prior proposals frozen;installed build/config/saves preserved.'],
        limits=['Protected current do165/startup/numeric getter ordering parity remains unverified;approved snapshot/geometry/recipient/event/nova/recovery rule supplies explicit adaptations.',
            'Unmapped native source identities,old target Hex duration migration,special recipients/PvP,nonzero magic-mastery item acceptance beyond prepared modifiers,graphical playtest and broader class acceptance remain open. No class certified.',
            'Prepared save fixture uses explicitly future retained inspection dates;actual socket controls use real-time windup/pulse and are deleted afterward. Do not claim allnine socket states saved or a one-hour gameplay duration.',
            'Four independent BloodOath/Engorge/BloodBoil/Consume proposals remain pending. Reference3.3.93847 fixed despite newer installed D2R;effects/audio and talent/hotkey icons remain separately needed.'],inputs=inputs)
    out=ROOT/'tools/reference/eldritch-blast-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Eldritch learned evidence stale'
    else:out.write_text(encoded)
    print('Eldritch learned:C1/I1/S1/N1,39markers/15connections/9actual socket casts+automatic pulses/2reloads;228handlers,Warlock19;client44unchanged patches.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
