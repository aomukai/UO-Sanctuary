#!/usr/bin/env python3
"""Qualify the isolated Bind whitelist build and exact44patch roundtrip."""
import argparse
from collections import Counter
import hashlib,json,re,shutil,subprocess,tempfile
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-client';PARENT=ROOT/'.local/class-skills/hex-siphon-cast'
PIN='ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93';UPSTREAM=Path('/media/aomukai/FILES/Sanctuary/upstream/ClassicUO')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
    manifest=STAGE/'client-inputs-1.json';data=json.loads(manifest.read_text());old=json.loads((PARENT/'client-inputs-1.json').read_text());client=STAGE/'client-root/upstream/ClassicUO'
    patches=sorted((ROOT/'patches/ClassicUO').glob('*.patch'));assert len(patches)==44 and data['patches']==44 and len(data['patchHashes'])==44
    assert data['baseReceiptSha256']==sha(ROOT/data['baseReceipt']) and data['intentionalSharedChanges']==[]
    for name,h in data['patchHashes'].items():assert sha(ROOT/name)==h,name
    for name,h in data['clientInputs'].items():assert sha(client/name)==h,name
    assert {n:h for n,h in data['patchHashes'].items()if n not in data['addedPatches']}==old['patchHashes']
    assert [n for n in data['clientInputs']if data['clientInputs'][n]!=old['clientInputs'][n]]==['src/ClassicUO.Client/Game/ClassCombatChannelClient.cs']
    for name,h in data['sharedInputs'].items():assert sha(ROOT/name)==sha(STAGE/'client-root'/name)==h==old['sharedInputs'][name],name
    channel=(client/'src/ClassicUO.Client/Game/ClassCombatChannelClient.cs').read_text();route=(client/'src/ClassicUO.Client/Game/ClassCombatClient.cs').read_text()
    assert 'or CombatAbility.BindDemon;'in channel
    for s in ('if(IsChannel(ability)){return BeginChannel(world,slot+2);}','if(IsChannel(Right)){BeginChannel(world,1);return true;}'):
        assert s in route,s
    for s in ('_nextChannelUpdate=Time.Ticks+CombatProtocol.HoldHeartbeatMs','writer.WriteUInt8((byte)_channelAbility)','CombatRequest.ChannelEnd','ChannelBinding(_channelSource)==_channelAbility'):
        assert s in channel,s
    with tempfile.TemporaryDirectory(prefix='bind-client-roundtrip-',dir='/dev/shm')as temp:
        temp=Path(temp);subprocess.run(['git','init','-q'],cwd=temp,check=True)
        for name in data['clientInputs']:(temp/name).parent.mkdir(parents=True,exist_ok=True);shutil.copy2(client/name,temp/name)
        for patch in reversed(patches):subprocess.run(['git','apply','-R',str(patch)],cwd=temp,check=True)
        for name in data['clientInputs']:
            base=subprocess.run(['git','-C',str(UPSTREAM),'show',PIN+':'+name],capture_output=True)
            if base.returncode==0:assert(temp/name).read_bytes()==base.stdout,name
            else:assert not(temp/name).exists(),name
        for patch in patches:subprocess.run(['git','apply',str(patch)],cwd=temp,check=True)
        for name,h in data['clientInputs'].items():assert sha(temp/name)==h,name
    log=STAGE/'client-build-1.log';text=log.read_text();assert 'Build succeeded.'in text and '0 Error(s)'in text
    warnings=int(re.findall(r'(\d+) Warning\(s\)',text)[-1]);oldlog=(PARENT/'client-build-1.log').read_text()
    def warning_lines(t,root):return Counter(line.replace(str(root),'<stage>')for line in t.splitlines()if re.search(r'warning [A-Z]+\d+:',line))
    assert warnings==44 and warning_lines(text,STAGE)==warning_lines(oldlog,PARENT),'Warnings differ from accepted baseline'
    assert 'ClassCombatChannelClient.cs('not in text
    server=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon'not in server
    prior=json.loads((ROOT/'tools/reference/bind-demon-input-native-evidence.json').read_text())
    born=json.loads((ROOT/'.local/class-skills/bind-demon-input/birth-inputs.json').read_text())
    for name,h in born['sourceInputs'].items():assert sha(ROOT/name)==h,name
    files=[Path(__file__).resolve(),ROOT/'tools/prepare_bind_demon_client.py',ROOT/'patches/ClassicUO/0044-bind-demon-held-input.patch',manifest,log,STAGE/'client/cuo.dll',
        ROOT/'tools/reference/bind-demon-input-native-evidence.json',ROOT/'docs/bind-demon-client-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/class-skills-resume-checkpoint.md',ROOT/'docs/class-skill-reuse-notes.md',ROOT/'docs/skill-effects.md',ROOT/'docs/skill-icons.md']
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            path=ROOT/n;assert sha(path)==hashlib.sha256(subprocess.check_output(['git','show','832cb40ca020d6b3b5e3633c7abe9fdc0d79c3a5:'+n],cwd=ROOT)).hexdigest();files.append(path);frozen.append(n)
    result=dict(referenceVersion='3.3.93847',nativeCommit=PIN,component='Prepared isolated Bind Demon client held-channel whitelist',inputManifest=dict(path=str(manifest.relative_to(ROOT)),sha256=sha(manifest)),
        handlers=226,warlockHandlers=17,completedClasses=[],patches=44,affectedPaths=len(data['clientInputs']),changedClientPaths=['src/ClassicUO.Client/Game/ClassCombatChannelClient.cs'],
        addedPatches=data['addedPatches'],buildWarnings=warnings,buildErrors=0,baselineWarningsIdentical=True,graphicalInputVerified=False,actualBindSocketInputs=0,learnedBindDemonEnabled=False,intentionalSharedChanges=[],
        nativeRebuilt=False,contentRebuilt=False,newNativeRun=False,newLiveReferenceReads=0,installed=False,preservation=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),frozenPendingProposalFiles=frozen,
        retainedServer=dict(receipt='tools/reference/bind-demon-input-native-evidence.json',contentBuild=2,integrationBuild=3,sourceSnapshot=4,nativeRun=3,nativeMarkers=prior['nativeMarkers'],reloads=prior['reloads'],actualBindSocketInputs=0),
        scope='One-line whitelist addition reuses existing right/quick press,heartbeat,release and packet layout. All44patches reverse to exact pinned source and forward to identical built inputs. Shared sources and prior43patches unchanged;isolated client compiles with identical inherited warnings.',
        limits=['Build/source/patch verification only;no graphical interaction,actual Bind socket admission,installed-client deployment or gameplay certification.',
            'Prepared channel cost/timing/grouped chance remains unapproved. Full Bind SQ6/native quality/named species/sunder/underling composite and proposal remain unfinished;four earlier proposals pending/frozen.',
            'No new server/native build or repeated historical server suite;accepted server source inputs remain identical. 226handlers/Warlock17/no class certified.',
            'Reference3.3.93847 unchanged despite newer installed D2R;no process/CASC capture/artwork. Final effects and icons tracked separately.'],
        files={str(p.relative_to(ROOT)):sha(p)for p in files})
    out=ROOT/'tools/reference/bind-demon-client-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bind client evidence stale'
    else:out.write_text(encoded)
    print('Bind client:44patch exact roundtrip,one whitelist change,built with identical44warnings/0errors;shared/server/installed inputs unchanged;Bind admission gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
