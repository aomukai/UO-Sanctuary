#!/usr/bin/env python3
"""Authenticate approved Bind admission,actual wire casts and original saves."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-learned';PARENT='f4b91c6739b7352454c5f324bfecf41cc0ef5d5c'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    approval=pin('tools/reference/bind-demon-world-rule-approval.json');assert approval['approved']and approval['answer']=='Approve this rule'and approval['candidateCommit']==PARENT
    approved=subprocess.check_output(['git','show',PARENT+':'+approval['proposal']],cwd=ROOT);assert hashlib.sha256(approved).hexdigest()==approval['proposalAtApprovalSha256']
    assert e.sha(ROOT/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bind-demon-host-audit-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-host-audit/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BindDemonLearnedChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs'for n in ('WarlockCombat','ActionTiming','BindDemonChance','BindDemonChannel','TalentGump')}
    changed|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('BindDemonCastChecks','BindDemonChannelChecks','BindDemonInputChecks','BindDemonSupplierChecks','BoundDemonEngorgeChecks','BoundDemonHostChecks','BoundDemonLifeChecks','BoundDemonRecipientChecks','BoundDemonTypedChecks')}
    assert set(now)-set(old)==new and not set(old)-set(now)and{n for n in old if old[n]!=now[n]}==changed
    assert set(born['copiedData'])==set(previous['copiedData'])and{n for n in born['copiedData']if born['copiedData'][n]!=previous['copiedData'][n]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        log=STAGE/f'{kind}-build-3.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'bind-learned-wire-1-result':3,'bind-learned-wire-9-result':3})
    for f in ('bind-demon-host-audit','bind-demon-input','bind-demon-learned'):expected.update({f+'-check':1,f+'-check-result':1,f+'-reload-check':2,f+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,33);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert text.count('Client: 127.0.0.1: Connected.')==15
    assert text.count('Bind learned:ordinary hard training and approved availability enable right/quick assignments;')==1
    assert text.count('Bind learned:original owner and all preceding real right/quick native wire bodies preserve')==2
    for source in (1,9):
        for phrase in ('pressed-free','released-free','pressed','bound'):assert text.count(f'bind-learned-wire-{source}: {phrase} ')==3,(source,phrase)
        assert text.count('Bind learned wire:source'+str(source)+' actual packets reject left/legacy routes,replay/duplicate and free release;')==3
    assert text.count('bind-learned-wire-1: healthy-paid')==3
    for f in ('bind-demon-host-audit','bind-demon-input','bind-demon-learned'):assert pin(STAGE/(f+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bind-demon-learned-fixtures.json');wires=saved['Wires'];assert len(wires)==6 and Counter(w['Source']for w in wires)=={1:3,9:3}
    assert len({w['Body']for w in wires})==len({w['Owner']for w in wires})==6
    for w in wires:
        source=w['Source'];rank=20 if source==1 else 21;assert w['Female']==(source==9)and w['ExpectedRank']==w['State']['Birth']['Rank']==rank
        assert w['State']['Birth']['HardRank']==20 and w['State']['Normal']['Life256']==10000*256 and w['Life']==4000*256-7 and w['Mana']==(72 if source==1 else 81)*256
        assert w['State']['Actor']==w['Body']and w['State']['Owner']==w['Owner']and w['State']['RuntimeType']=='Server.Mobiles.Daemon'
    archive=STAGE/'native-1';early=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/'native-run-1.log');e.cohort(archive,expected,33)
    for n,h in early['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
    for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==early['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    earlier={n:h for n,h in early['sourceInputs'].items()if n.endswith('.cs')};assert set(earlier)==set(now)and{n for n in now if now[n]!=earlier[n]}=={'src/Sanctuary.Content/TalentGump.cs'}and early['copiedData']==born['copiedData']
    for i in range(1,6):pin(STAGE/f'source-snapshot-{i}.json')
    for kind in ('content','integration'):
        for i in range(1,4):
            log=STAGE/f'{kind}-build-{i}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    client=pin('tools/reference/bind-demon-client-evidence.json');manifest=pin(ROOT/client['inputManifest']['path']);assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    for n,h in manifest['patchHashes'].items():assert e.sha(ROOT/n)==h,n
    for n,h in manifest['sharedInputs'].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bind_demon_learned.py','tools/record_bind_demon_learned.py','tools/build_skill_roadmap.py','data/Sanctuary/Balance/class-skill-status.json','docs/bind-demon-world-rule-proposal.md','docs/bind-demon-learned-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status['completedClasses']==[]and sum(x['implementation']=='handler-present'for x in status['skills'])==227
    assert sum(x['implementation']=='handler-present'and x['characterClass']=='Warlock'for x in status['skills'])==18
    result=dict(referenceVersion='3.3.93847',component='Approved learned Bind Demon action and real wire admission',handlers=227,warlockHandlers=18,completedClasses=[],learnedBindDemonEnabled=True,
        nativeMarkers=33,markers=dict(expected),actualInputs=6,actualBindSocketInputs=6,authenticatedSessions=15,boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=3,sourceSnapshot=5,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        approvalRecord='tools/reference/bind-demon-world-rule-approval.json',proposalApproved=True,graphicalInputVerified=False,wireBodiesSaved=6,precedingWireBodiesCheckedAtReloads=[2,4],oldestWireBodiesReloads=2,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'installed':False},
        established=['Exact user decision/proposal hash/candidate commit authorizes defined qualified-body Bind. Ordinary hard training/right-quick assignment and shared held wire dispatch enabled;left/untrained/genericBegin-Release-Perform/legacy held bypasses refuse.',
            'Existing clock/approved fixedone-second/nine-mana rule/grouped chance/original host/stat/affix/aura/resource/pool/lifecycle retained. HUD interval stays1000ms even withFCR;talentcost no longer shows raw per-invocation amount.',
            'Six actual right/quick wire casts across3boots includeHello/Stop,negativeleft/legacycast,press replay/duplicate,free preattempt release,freshwindup,wrong-source release and hovered-other/stale heartbeat. Exactlyone successful RNG draw/commit percase.',
            'Right cases:firsthealthyattempt costsnine,noRNG/conversion;removegear whileholding andweaken target,nextattempt capturesrank20,leaves72of90mana. QuickWitchcases:rank21birth,onelaterhelmetremoval leavesbirthfixed,81mana. Each preservesoriginal4000FP8minus7wounds.',
            'Six originalDaemonbodies saved;firstpair survives2reloads,nextpair1,lastpairsaved. Preceding2then4bodies verified withserial/type/owner/birthrank/wounds/mana/gender/backpackedhelmet/bindings,no heldintent/offlinedebit. Original mainowner/gear/resources also retain.',
            'Approved historical binding/natural timer/error cleanup and nativehost/paragon/Sunder inventory controls retain. Candidate component fixtures nowcheckproper scope ratherthan global disabledBind. Otherfourpending skills remaingated/frozen.',
            'C1/I1compile-only;C2/I2/S4/N1passes33with exactarchive. C3changesonlycostdescription;C3/I3/S5/N2passes33. Client44patch/sharedinputs unchanged;no native/client build/profile/timer/install/artwork.'],
        limits=['227handlers/Warlock18/no classcertificate. NativeParagon/explicitelite,named/underling/unmappednative species,nonzeroSunder/specialrecipient/PvP/graphical/fullclass acceptance remain open. No silent remaps or reduction300guess.',
            'Definedtiming/cost/groupedchance/worldhost are userapprovedSanctuaryadaptations,not currentD2Rcallback/seed/SQ6parity. Nativeoriginalabilities retained until separate monsterspellmigration.',
            'Four earlier proposals remainpending/frozen. InstallednewerD2Rdoesnotreplace3.3.93847;installedartifacts/saves/config/clientpatches unchanged. Final effects/audio/tree-right-quickicons remainseparatelymissing;noimagegen.'],inputs=inputs)
    target=ROOT/'tools/reference/bind-demon-learned-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bind learned evidence stale'
    else:target.write_text(encoded)
    print('Bind learned:C3/I3/S5/N2,33markers/15connections/6actual Bindcasts/2reloads;227handlers/Warlock18;qualified action admitted under approval.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
