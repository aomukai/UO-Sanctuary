#!/usr/bin/env python3
"""Qualify the required self-cast client rebuild,not an optional dependency audit."""
import argparse,hashlib,json,re,shutil,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/psychic-ward-cast';PIN='ee79d7ebc1cc0e53ff84fe6cae17d389e2737f93';UPSTREAM=Path('/media/aomukai/FILES/Sanctuary/upstream/ClassicUO')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
    manifest=STAGE/'client-inputs-1.json';data=json.loads(manifest.read_text());client=STAGE/'client-root/upstream/ClassicUO';patches=sorted((ROOT/'patches/ClassicUO').glob('*.patch'));assert len(patches)==43
    for name,h in data['patchHashes'].items():assert sha(ROOT/name)==h,name
    for name,h in data['clientInputs'].items():assert sha(client/name)==h,name
    for name,h in data['sharedInputs'].items():assert sha(ROOT/name)==sha(STAGE/'client-root'/name)==h,name
    assert 'or CombatAbility.PsychicWard' in (STAGE/'client-root/shared/ClassAbilities.cs').read_text()
    route=(client/'src/ClassicUO.Client/Game/ClassCombatClient.cs').read_text();assert 'if (ClassAbilities.SelfTarget(_heldAbility))'in route and 'Send(CombatRequest.HoldCast, 0, _heldAbility'in route
    with tempfile.TemporaryDirectory(prefix='ward-client-roundtrip-',dir='/dev/shm')as temp:
        temp=Path(temp);subprocess.run(['git','init','-q'],cwd=temp,check=True)
        for name in data['clientInputs']:(temp/name).parent.mkdir(parents=True,exist_ok=True);shutil.copy2(client/name,temp/name)
        for patch in reversed(patches):subprocess.run(['git','apply','-R',str(patch)],cwd=temp,check=True)
        for name in data['clientInputs']:
            base=subprocess.run(['git','-C',str(UPSTREAM),'show',PIN+':'+name],capture_output=True)
            if base.returncode==0:assert(temp/name).read_bytes()==base.stdout,name
            else:assert not(temp/name).exists(),name
        for patch in patches:subprocess.run(['git','apply',str(patch)],cwd=temp,check=True)
        for name,h in data['clientInputs'].items():assert sha(temp/name)==h,name
    log=STAGE/'client-build-1.log';text=log.read_text();assert 'Build succeeded.'in text and '0 Error(s)'in text;warnings=int(re.findall(r'(\d+) Warning\(s\)',text)[-1]);assert 'shared/ClassAbilities.cs('not in text
    result=dict(nativeCommit=PIN,inputManifest=dict(path=str(manifest.relative_to(ROOT)),sha256=sha(manifest)),patches=43,affectedPaths=len(data['clientInputs']),buildWarnings=warnings,buildErrors=0,graphicalInputVerified=False,intentionalSharedChanges=['shared/ClassAbilities.cs'],
      scope='Required isolated client rebuild with Ward self-target metadata. Existing43patches reverse to exact local pinned source then forward to identical built inputs. All linked shared sources match workspace. No new patch,dependency update or installed-client change.',
      limits=['Existing/isolated-build warnings retained;no optional audit or package migration.','Connected server input is verified separately;actual graphical interaction/rendering remains unverified.'],
      files={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__).resolve(),ROOT/'tools/prepare_psychic_ward_client.py',log,STAGE/'client/cuo.dll']})
    out=ROOT/'tools/reference/psychic-ward-cast-client-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print(f'Ward client:43patch exact roundtrip,shared self-target built,{warnings}warnings/0errors;installed client untouched.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
