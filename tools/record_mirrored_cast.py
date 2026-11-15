#!/usr/bin/env python3
"""Authenticate approved learned Mirrored input and four-kind automatic hits."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mirrored-cast';PARENT='1711d84b1c2d21200d950c7ad4641c4d8d214ef2'

def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return e.load(p)if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-1.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/mirrored-world/birth-inputs.json');parent=pin('tools/reference/mirrored-world-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'tests/Sanctuary.Integration/MirroredCastChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('MirroredBladesAction','MirroredBladesPackets','WarlockCombat')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('MirroredBladesChecks','MirroredPacketChecks','MirroredWorldChecks')}
    assert set(now)-set(old)==added and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert set(born['copiedData'])==set(previous['copiedData'])and{n for n in born['copiedData']if born['copiedData'][n]!=previous['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        p=STAGE/f'{kind}-build-1.log';pin(p)
        assert all(s in p.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('cleave-action','magic-arrow-cast','mirrored-packets'):expected.update({family+'-check':1,family+'-reload-check':2})
    for family in ('mirrored-world','mirrored-cast','echoing-strike-cast'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    labels=[*('echoing-strike-cast-'+s for s in ('left','right','quick')),'mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick']
    for name in labels:expected.update({name+'-check':3,name+'-result':3})
    text=e.cohort(STAGE,expected,78);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-1.log')
    assert text.count('Client: 127.0.0.1: Connected.')==30
    for name in labels:assert text.count(name+': windup')==3
    assert text.count('Mirrored learned:approved ordinary dispatch/SQ25/all bindings,unpaid windup,three independent contacts,one fractional debit/no replay pass')==1
    assert text.count('Mirrored learned:saved Witch/original sword/fractional resources/all bindings retain;post-load learned action works without pending replay')==2
    assert text.count('Mirrored world:actual expired/facet/offline/class-history/death controls retire released throws without replay')==1
    assert text.count('Mirrored packets:192independent channel/count/critical controls')==1
    for n in ('mirrored-packets-fixtures.json','mirrored-world-fixtures.json','mirrored-cast-fixtures.json','echoing-strike-cast-fixtures.json'):
        assert pin(STAGE/n)['Generation']==2
    world=e.load(STAGE/'mirrored-world-fixtures.json');assert [len(r['Flights'])for r in world['Rows']]==[0,3,3,3]
    saved=e.load(STAGE/'mirrored-cast-fixtures.json');assert saved['Life']%256 and saved['Mana']%256
    pin(STAGE/'cleave-action-fixtures.json');pin(STAGE/'magic-arrow-cast.json')
    approval=pin('tools/reference/mirrored-world-rule-approval.json')
    assert approval['approved']and approval['answer']=='Approve the complete Mirrored Blades rule (Recommended)'and approval['candidateCommit']==PARENT
    assert approval['questionItemId']=='["request_user_input_async","call_215e6a0eb609419cb06e46de887256b7",0]'
    assert approval['testedCandidateEvidenceSha256']==e.sha(ROOT/approval['testedCandidateEvidence'])
    original=subprocess.check_output(['git','show',PARENT+':'+approval['proposal']],cwd=ROOT)
    assert hashlib.sha256(original).hexdigest()==approval['proposalAtApprovalSha256']
    current=original.decode().replace('**Pending approval.**','**Approved by the user on2026-11-15 JST.** [Exact decision](../tools/reference/mirrored-world-rule-approval.json). The body below is the original approved proposal; implementation/acceptance status is recorded separately.\n\n**Original proposal.**',1)
    assert current==(ROOT/approval['proposal']).read_text();pin(approval['proposal'])
    client=pin('tools/reference/eldritch-blast-cast-client-evidence.json');cm=pin(ROOT/client['inputManifest']['path'])
    assert inputs[client['inputManifest']['path']]==client['inputManifest']['sha256']
    for group in ('patchHashes','sharedInputs'):
        for n,h in cm[group].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added|changed),'tests/check_mirrored_cast.py','tools/record_mirrored_cast.py','tools/build_skill_roadmap.py',
        'docs/mirrored-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md',
        'docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.MirroredBlades'in code and 'CombatAbility.EchoingStrike'in code
    result=dict(referenceVersion='3.3.93847',component='Approved learned Mirrored Blades',handlers=230,warlockHandlers=21,completedClasses=[],approved=True,learnedEnabled=True,
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=78,markers=dict(expected),boots=3,reloads=2,saves=3,authenticatedSessions=30,
        actualInputs=21,actualMirroredInputs=12,retainedEchoingInputs=9,actualMirroredAutomaticContacts=36,
        mirroredInputCases={'leftMelee':3,'rightBow':3,'quickCrossbow':3,'quickThrow':3},graphicalInputVerified=False,
        packetControls=192,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        approval='tools/reference/mirrored-world-rule-approval.json',frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['Complete whole rule approved and recorded with original proposal/candidate/native hashes. Public Warlock dispatcher admits SQ25through BeginLearned,availability and unsupported-context reporting;the transient action retains exclusive release. All three bindings reuse existing non-self protocol metadata.',
            'Twelve actual Mirrored socket casts across three boots cover left melee,right bow,quick crossbow and quick throw. Each has unpaid windup,three real automatic landed contacts,one8mana debit and one arrow/bolt/throw debit where applicable. Source SQ25combat and travelling-projectile clocks run automatically;no manual clock drives these actors.',
            'Retained192packet/four-kind native contacts,Crushing Blow/Open Wounds/resources/fallback/shield/interruption/empty-ammo/target-death/empty-swing/expiry/facet/offline/history/death/metadata-restore and saved-nine-projectile continuation controls pass. Cleave default melee and Magic Arrow original flight/item-effect/reload consumers retain.',
            'Original saved Witch/sword/fractional resources/all bindings retain across two reloads,and a fresh learned action works after each load without pending replay. Saved prepared future-date missiles and deleted real-time socket actors remain separate claims. Nine actual Echoing casts/automatic hits retain.',
            'C1/I1/S1/N1passes78markers/30connections/three saved boots/two reloads. No client patch/build,kernel build,timer,profile,version,live-current read,installed deployment or artwork. Accepted client/shared sources,installed build/config/saves and four pending proposal/evidence pairs remain unchanged.'],
        limits=['Protected start68/do166/native factory/global RNG/event parity and broader player chance-to-cast/standard-arrow Slow Missiles,unmapped/special/PvP/class-pet contexts remain unverified or unsupported under the approved rule. No class certified.',
            'Native socket verification is not graphical playtesting or full class/property acceptance. Future inspection dates qualify persistence only;the12real-time socket actors are transient and not claimed saved.',
            'Four independent proposals remain pending. Final mirror/weapon/projectile/contact effects,audio and talent/hotkey icons remain separately needed;identified native/shared placeholders only.'],inputs=inputs)
    target=ROOT/'tools/reference/mirrored-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Mirrored learned evidence stale'
    else:target.write_text(encoded)
    print('Mirrored learned:C1/I1/S1/N1,78markers/30connections/12actual four-kind casts+36contacts/2reloads;230handlers,Warlock21.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
