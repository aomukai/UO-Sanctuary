#!/usr/bin/env python3
"""Execute pinned community3.3calculator routines; corroborate current Druid animation rows."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
COMMIT = 'b7d791031b73b0c6e56a3659e9198578aed21261'
SOURCE = BASE / 'warren-ias-b7d7910'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((SOURCE / 'tree.json').read_text())
    assert tree['sha'] == COMMIT
    blobs = {row['path']:row['sha'] for row in tree['tree'] if row['type'] == 'blob'}
    files = {}
    for name in ['calculator.js', 'constants.js', 'README.md', 'index.html']:
        raw = (SOURCE / name).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blobs[name]
        files[name] = hashlib.sha256(raw).hexdigest()
    assert 'D2R 3.3' in (SOURCE / 'index.html').read_text()
    driver = BASE / 'modern-form-rate-source-probe.mjs'
    driver.write_text(r'''
import fs from 'node:fs';
import vm from 'node:vm';
const folder=process.argv[2];
const elements=new Map();
function element(id){
 if(!elements.has(id))elements.set(id,{value:'0',checked:false,visible:false,options:Array.from({length:16},()=>({})),style:{},
  get offsetWidth(){return this.visible?1:0;},get offsetHeight(){return this.visible?1:0;},getClientRects(){return this.visible?[{}]:[];},addEventListener(){}});
 return elements.get(id);
}
const context=vm.createContext({document:{getElementById:element},window:{addEventListener(name,callback){if(name!=='load')throw Error(name);context.loadReference=callback;}},console:{log(){}}});
const constants=new vm.SourceTextModule(fs.readFileSync(folder+'/constants.js','utf8'),{context});
await constants.link(()=>{throw Error('Unexpected constants import');});
await constants.evaluate();
let original=fs.readFileSync(folder+'/calculator.js','utf8');
const marker='\tsetupConstants();';
if(original.split(marker).length!==2)throw Error('Initialization marker changed');
// Only bypass DOM initialization and expose closures. The reference numerical
// routines and table-generation loop remain complete and unchanged.
const expose=String.raw`
globalThis.formReference=function(formIndex,typeKey,wsm,wias,gias,rank,slow){
 tableVariable=tv.FANATICISM;character=char.DRUID;wereform=formIndex;skill=skills.STANDARD;
 primaryWeapon={name:typeKey,type:wt[typeKey],WSM:wsm};secondaryWeapon=weapons.NONE;
 for(const e of Object.values(container))e.visible=false;
 for(const e of Object.values(number))e.value='0';
 for(const e of Object.values(checkbox))e.checked=false;
 container.PRIMARY_WEAPON_IAS.visible=true;container.WEREWOLF.visible=formIndex===wf.WEREWOLF;
 number.PRIMARY_WEAPON_IAS.value=String(wias);number.IAS.value=String(gias);
 number.WEREWOLF.value=String(rank);number.SLOWED_BY.value=String(slow);
 const values=calculateEIAS(true),type=primaryWeapon.type;
 const table=doCoreTableLogic(primaryWeapon,fpd1(type,false,true),true)[0].table;
 return [calculateFramesPerDirection(type),as(type,fpd1(type,false,true)),fpd3(),values[0],values[1],values[4],limitEIAS(values[0]),table[0][1]];
};return;
`;
const calculator=new vm.SourceTextModule(original.replace(marker,expose),{context});
await calculator.link(specifier=>{if(specifier!=='./constants.js')throw Error(specifier);return constants;});
await calculator.evaluate();context.loadReference();
const cases=[];
for(const form of [1,2])for(const type of ['UNARMED','ONE_HANDED_SWINGING','ONE_HANDED_THRUSTING','TWO_HANDED_SWORD','TWO_HANDED_THRUSTING','TWO_HANDED','BOW','CROSSBOW'])
 for(const wsm of [-30,-10,0,10,20])for(const ias of [0,15,40,80,120,240])for(const split of [0,1,2])for(const rank of [1,3,20,28,60])for(const slow of [0,50]){
  const wias=split===0?0:split===1?ias:Math.floor(ias/2),gias=ias-wias;
  cases.push([form,type,wsm,wias,gias,rank,slow,context.formReference(form,type,wsm,wias,gias,rank,slow)]);
 }
process.stdout.write(JSON.stringify(cases)+'\n');
''')
    process = subprocess.run(['node', '--no-warnings', '--experimental-vm-modules', str(driver), str(SOURCE)], capture_output=True)
    log = BASE / 'modern-form-rate-source-probe.log';log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr, process.stderr.decode()
    cases = json.loads(process.stdout)
    assert len(cases) == 14400
    animation = ROOT / '.local/research/d2r-3.3.93847/AnimData.d2'
    assert sha(animation) == json.loads((ROOT / 'tools/reference/d2r-local-animation-evidence.json').read_text())['sha256']
    raw, offset, records = animation.read_bytes(), 0, {}
    for _ in range(256):
        count = struct.unpack_from('<I', raw, offset)[0];offset += 4
        for _ in range(count):
            code = raw[offset:offset + 8].split(b'\0')[0].decode('ascii')
            frames, speed = struct.unpack_from('<II', raw, offset + 8)
            if code.startswith(('DZA1', '40A1', 'TGA1')):
                records[code] = dict(frames=frames, speed=speed, events=[[i,v] for i,v in enumerate(raw[offset + 16:offset + 160]) if v])
            offset += 160
    assert offset == len(raw)
    codes = dict(UNARMED='HTH', ONE_HANDED_SWINGING='1HS', ONE_HANDED_THRUSTING='1HT', TWO_HANDED_SWORD='2HS', TWO_HANDED_THRUSTING='2HT', TWO_HANDED='STF', BOW='BOW', CROSSBOW='XBW')
    partition = {}
    for form, weapon, wsm, wias, gias, rank, slow, actual in cases:
        human = records['DZA1' + codes[weapon]];transformed = records[('40' if form == 2 else 'TG') + 'A1HTH']
        frames, speed, shape_frames, eias, sias, item_eias, limited, cycle = actual
        assert frames == human['frames'] and speed == human['speed'] == 256 and shape_frames == transformed['frames']
        item = 120 * (wias + gias) // (120 + wias + gias)
        skill = 10 + 70 * (110 * rank // (rank + 6)) // 100 if form == 2 else 0
        expected_eias = item + skill - slow - wsm
        capped = max(-85, min(150, expected_eias))
        progress = (speed + int(speed * capped / 100)) * shape_frames // frames
        expected_cycle = (shape_frames * 256 + progress - 1) // progress - 1
        assert [eias, sias, item_eias, limited, cycle] == [expected_eias, skill - slow, item, capped, expected_cycle], (form, weapon, wsm, wias, gias, rank, slow, actual)
        key = (form, weapon, wsm, wias + gias, rank, slow)
        if key in partition:
            assert partition[key] == actual, 'Weapon/off-weapon IAS partition changed the single-weapon form result'
        partition[key] = actual
    fixture = BASE / 'modern-form-rate-source-probe.json';fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', supplementaryResearch=True, nativeAccepted=False, currentExecutableParity=False,
        source=dict(repository='https://github.com/Warren1001/IAS_Calculator', commit=COMMIT, declaredVersion='D2R 3.3', files=files,
            licenseFilePresent=any('license' in name.lower() for name in blobs), cache=str(SOURCE.relative_to(ROOT))),
        contexts=14400, distinctIasContexts=len(partition), ranks=[1,3,20,28,60], weaponClasses=list(codes),
        animation=dict(sha256=sha(animation), records=records), fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        harnessSha256=sha(driver), stderrSha256=sha(log),
        established=[
            'The pinned author calculator declares3.3. Its complete numerical closures/table-generation loop execute14400single-weapon Druid standard-attack contexts through a NodeVM with mock DOM inputs and initialization bypass. All source files retain Git-blob/SHA256pins in the ignored research cache.',
            'All8Druid human A1frame counts/speeds and wolf13/bear12form frame counts corroborate licensed current AnimData. The model normalizes through human weapon animation length; simply using the raw form animation speed is not the same calculation.',
            'Weapon/off-weapon IAS partition produces identical single-weapon outcomes in4800distinct total-IAS contexts. Skill speed/slow/WSM join item effective IAS before the model clamp-85..150; two-stage truncation and whole-frame ceilings remain explicit.',
        ], remaining=[
            'This is supplementary original calculator research,not a disassembly or direct current-build observation. Exact skill/event/impact/cancellation cadence,all form actions,modern cross-form transitions and live player input remain separate. No native rate or learned form handler is enabled.',
            'Only effective ranks1..60and one Druid weapon are compared; calculator UIcaps skill inputs at60. Higher native rank cases,dual wield,other transformed classes,Maul/Fury/Hunger/Rabies sequences and unrelated skills require distinct evidence.',
            'Cached implementation files are not redistributed in tracked source. A future independently written native arithmetic projection needs its own source/native comparisons and bounded current-engine claims.',
        ])
    output = ROOT / 'tools/reference/modern-form-rate-source-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Modern form rate research:14400author-calculator contexts,4800IAS totals,8current Druid animation classes; native rates not enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
