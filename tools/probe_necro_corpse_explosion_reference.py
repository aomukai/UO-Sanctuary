#!/usr/bin/env python3
"""Reuse complete do55 bodies with the current player74 parameters."""
import argparse,hashlib,json,re,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 subprocess.run([sys.executable,str(ROOT/'tools/probe_corpse_explosion_reference.py'),'--check'],check=True,stdout=subprocess.DEVNULL)
 parent=json.loads((ROOT/'tools/reference/corpse-explosion-source-evidence.json').read_text())
 ref=ROOT/'tools/reference/d2r-named-equipment';raw=(ref/'json/skills.json').read_bytes();manifest=json.loads((ref/'sources.json').read_text())
 assert hashlib.sha256(raw).hexdigest()==manifest['files']['json/skills.json']['sha256']
 row=next(x for x in json.loads(raw).values() if x.get('*Id')==74)
 fields=['srvdofunc','Param1','Param2','Param3','Param4','Param5','EType','aurarangecalc','calc1','calc2','calc3']
 assert [row[k] for k in fields]==[55,70,120,8,1,50,'fire','ln34','par1','par2','par5']
 cpp=(STAGE/'corpse-explosion-source-probe.cpp').read_text()
 cpp=re.sub(r'\b312\b','74',cpp).replace('rangeValue=10+rank-1;minPercent=40;maxPercent=80;','rangeValue=8+rank-1;minPercent=70;maxPercent=120;')
 source=STAGE/'necro-corpse-explosion-source-probe.cpp';source.write_text(cpp);binary=source.with_suffix('')
 subprocess.run(['g++','-std=c++17','-O1','-g','-Wall','-Wextra','-Werror','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(source),'-o',str(binary)],check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'necro-corpse-explosion-sanitizer.log').write_bytes(run.stderr)
 assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
 rows=json.loads(run.stdout);assert len(rows)==8640
 out=STAGE/'necro-corpse-explosion-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
 e=dict(referenceVersion='3.3.93847',legacyCommit=parent['legacyCommit'],sourceHashes=parent['sourceHashes'],routineHashes=parent['routineHashes'],parentEvidence='tools/reference/corpse-explosion-source-evidence.json',pinnedSkillsSha256=hashlib.sha256(raw).hexdigest(),playerFields={k:row[k] for k in fields},fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=len(rows)),established=['Complete older producer, callback and percentage bodies execute with pinned player74 damage70..120, range8+rank-1 and50percent fire. Exclusive fractional roll, caster-level ratio and separate physical clipping remain shared with NPC312.'],limits=parent['limits'][:2]+['Declared base-life/random/fire-writer/area/event services remain inherited from the parent probe. This does not certify player input, native defenses/potions, actual corpse persistence or the modern executable.'],sanitizers='ASan/UBSan exit0 and empty stderr required')
 p=ROOT/'tools/reference/necro-corpse-explosion-source-evidence.json';text=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==text
 else:p.write_text(text)
 print('8640complete player74do55source contexts; sanitizer-clean; native consumers separate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
