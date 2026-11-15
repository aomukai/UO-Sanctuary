#!/usr/bin/env python3
"""Stage one game-content bridge for prepared physical hit recovery."""
import argparse,difflib,hashlib,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/bind-demon-typed';PARENT=ROOT/'.local/class-skills/psychic-ward-cast/native'
PATCH=ROOT/'patches/ModernUO/0074-prepared-physical-hit-recovery.patch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare(check=False):
    relative='Projects/UOContent/Misc/AOS.cs';before=(PARENT/relative).read_text();needle='            var beforeDamage = m.Hits;'
    assert before.count(needle)==1
    after=before.replace(needle,'            canRecover |= skillResistance is { Physical256: > 0 } or { Physical: > 0 };\n\n'+needle)
    patch=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True),fromfile='a/'+relative,tofile='b/'+relative))
    if check:
        assert PATCH.read_text()==patch and(STAGE/'native'/relative).read_text()==after
    else:
        assert not PATCH.exists();PATCH.write_text(patch)
        candidate=STAGE/'native';assert candidate.is_symlink()and candidate.resolve()==PARENT.resolve();candidate.unlink()
        shutil.copytree(PARENT,candidate,ignore=shutil.ignore_patterns('.git'))
        subprocess.run(['git','init','--quiet',str(candidate)],check=True)
        (candidate/relative).write_text(after)
    result=dict(parent='.local/class-skills/psychic-ward-cast/native',patch=str(PATCH.relative_to(ROOT)),patchSha256=sha(PATCH),
        changedSources={relative:dict(before=hashlib.sha256(before.encode()).hexdigest(),after=sha(STAGE/'native'/relative))},
        serverEngineModified=False,apiChanged=False,newSavedFormats=0,
        rule='After prepared mitigation,positive physical damage admits existing native hit recovery even when the original native split was poison-only. Poison-only without surviving physical retains its old rule.')
    path=STAGE/'native-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Prepared physical recovery:one AOS game-content line,no engine/API/save change;separate native tree.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');prepare(p.parse_args().check)
