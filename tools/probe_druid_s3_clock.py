#!/usr/bin/env python3
"""Compose the pinned modern author model and complete older scheduler for S3."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight = ROOT / 'tools/reference/druid-hunger-preflight.json'
    pin = json.loads(preflight.read_text())
    old_progress = ROOT / 'tools/reference/modern-form-progress-source-evidence.json'
    progress = json.loads(old_progress.read_text())
    harness = BASE / 'modern-form-progress-source-probe.mjs'
    assert sha(harness) == progress['harnessSha256']
    rate_pin = json.loads((ROOT / progress['predecessor']['path']).read_text())
    folder = ROOT / rate_pin['source']['cache']
    for name, digest in rate_pin['source']['files'].items():
        assert sha(folder / name) == digest
    # Adapt only the declared harness's selected skill and input/output lists.
    # The calculator numerical routines and observed first-loop advance stay intact.
    text = harness.read_text()
    for old, new in [
        ('function(formIndex,typeKey,wsm,wias,gias,rank,slow){', 'function(formIndex,typeKey,wsm,wias,gias,rank,slow,selectedSkill){'),
        ('skill=skills.STANDARD;', 'skill=skills[selectedSkill];'),
        ('for(const form of [1,2])', "for(const selectedSkill of ['HUNGER','RABIES'])for(const form of [1,2])"),
        ('cases.push([form,type,wsm,wias,gias,rank,slow,context.formReference(form,type,wsm,wias,gias,rank,slow)]);',
         'cases.push([selectedSkill,form,type,wsm,wias,gias,rank,slow,context.formReference(form,type,wsm,wias,gias,rank,slow,selectedSkill)]);'),
    ]:
        assert text.count(old) == 1, old
        text = text.replace(old, new, 1)
    driver = BASE / 'druid-s3-rate-source-probe.mjs'
    driver.write_text(text)
    process = subprocess.run(['node', '--no-warnings', '--experimental-vm-modules', str(driver), str(folder)], capture_output=True)
    rate_log = BASE / 'druid-s3-rate-source-probe.log'
    rate_log.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr
    inputs = json.loads(process.stdout)
    baseline_file = ROOT / progress['fixture']['path']
    assert sha(baseline_file) == progress['fixture']['sha256']
    baseline = json.loads(baseline_file.read_text())
    assert len(inputs) == 28800 and len(baseline) == 14400
    for index, row in enumerate(inputs):
        old = baseline[index % len(baseline)]
        assert row[1:8] == old[:7]
        assert row[8][:7] == old[7][:7] and row[8][8] == old[7][8]
        advance = row[8][8]
        assert row[8][7] == (10 * 256 + advance - 1) // advance - 1

    # Reuse the accepted scheduler harness, replacing only input rows and main.
    prior_path = ROOT / 'tools/reference/druid-form-attack-clock-engine-evidence.json'
    prior = json.loads(prior_path.read_text())
    original = BASE / 'druid-form-attack-clock-source-probe.cpp'
    assert sha(original) == prior['generatedSourceSha256']
    cpp = original.read_text().split('struct Input{int form,frames,rate;};')[0]
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit'] == prior['legacyCommit']
    routines = {}
    for relative, symbol in [
        ('source/D2Common/src/Units/Units.cpp', 'UNITS_GetFrameBonus'),
        ('source/D2Game/src/UNIT/SUnit.cpp', 'sub_6FCBC4D0'),
        ('source/D2Game/src/UNIT/SUnit.cpp', 'sub_6FCBCE70'),
    ]:
        raw = (BASE / 'd2moo-pinned-source' / relative).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        body = function(raw.decode(), symbol)
        assert function(cpp, symbol) == body
        routines[symbol] = hashlib.sha256(body.encode()).hexdigest()
    assert routines == prior['completeRoutineHashes']
    cpp += 'struct Input{int form,rate;};\nInput inputs[]={\n'
    cpp += ',\n'.join('{%d,%d}' % (139 if row[1] == 2 else 140, row[8][8]) for row in inputs)
    cpp += r'''
};
int main(){
 for(int selectedForm:{139,140})for(weapon=0;weapon<NUM_WEAPON_CLASSES;weapon++){
  form=selectedForm;D2UnitStrc p;p.dwAnimMode=PLRMODE_SPECIAL3;wield=true;assert(UNITS_GetFrameBonus(&p)==0);
 }
 printf("[");bool first=true;
 for(auto input:inputs){
  Anim anim;anim.dwFrames=10;anim.pFrameFlags[6]=1;
  D2UnitStrc p;p.dwAnimMode=PLRMODE_SPECIAL3;p.pAnimData=&anim;p.pDynamicPath=&p;D2GameStrc game;
  form=input.form;rate=input.rate;updates=0;events.clear();
  sub_6FCBC4D0(&p);
  assert(updates==1&&p.nActionFrame==0&&p.nSeqCurrentFramePrecise==0&&p.dwFrameCountPrecise==2560&&!p.pAnimSeq);
  sub_6FCBCE70(&game,&p);
  printf("%s[",first?"":",");first=false;bool sep=false;
  for(auto e:events){printf("%s[%d,%d,%d,%d]",sep?",":"",e.type,e.frame-game.dwGameFrame,e.kind,e.param);sep=true;}
  printf("]");
 }
 printf("]\n");
}
'''
    generated = BASE / 'druid-s3-clock-source-probe.cpp'
    generated.write_text(cpp)
    compiler = BASE / 'druid-s3-clock-compiler.log'
    with compiler.open('w') as log:
        subprocess.run(['g++', '-std=c++20', '-Wall', '-Wextra', '-Werror', '-Wno-parentheses', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(generated.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
    process = subprocess.run([str(generated.with_suffix(''))], capture_output=True)
    sanitizer = BASE / 'druid-s3-clock-sanitizer.log'
    sanitizer.write_bytes(process.stderr)
    assert process.returncode == 0 and not process.stderr
    schedules = json.loads(process.stdout)
    assert len(schedules) == len(inputs)
    cases = []
    for row, events in zip(inputs, schedules, strict=True):
        advance = row[8][8]
        action = (6 * 256 + advance - 1) // advance
        end = (10 * 256 + advance - 1) // advance
        assert events == [[1, action, 1, 0], [2, end, 0, 0]]
        cases.append(row + [events])
    fixture = BASE / 'druid-s3-clock-source-probe.json'
    fixture.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    result = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(preflight), predecessorClockSha256=sha(prior_path), predecessorProgressSha256=sha(old_progress),
        authorCommit=rate_pin['source']['commit'], sourceFiles=rate_pin['source']['files'], completeRoutineHashes=routines,
        contexts=len(cases), frameBonusControls=28, animation=pin['animation'],
        driverSha256=sha(driver), rateLogSha256=sha(rate_log), generatedSourceSha256=sha(generated), compilerLogSha256=sha(compiler), sanitizerLogSha256=sha(sanitizer),
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)),
        columns=['skill', 'authorForm', 'weapon', 'weaponModifier', 'weaponIas', 'gearIas', 'werewolfRank', 'slow', 'authorOutput', 'scheduledEvents'],
        established=[
            'Pinned3.3author calculator executes Hunger and Rabies across28,800single-weapon contexts. Its first-loop progress equals the accepted standard-attack progress for identical inputs: normalization retains A1form length13wolf/12bear, while S3action length is10. Raw S3animation speed240/208 is not substituted into that author model.',
            'Three complete older routines execute unchanged with each prepared progress: Druid frame bonus, ordinary initializer and event scheduler. Both forms use current S3event6/length10, yielding action ceil(6*256/progress) and ENDANIM ceil(10*256/progress). All28form/weapon S3frame-bonus controls are zero.',
            'The author cycle is ENDANIM minus one, as for the predecessor. This is a distinct quantity and does not certify modern accepted-input reentry. Rabies timing is prepared only; its infection/damage mechanism is not implemented by this probe. Rabies/bear inputs are synthetic clock controls,not permission to use the wolf-only skill in bear form.',
        ], remaining=[
            'Mode conversion, animation metadata transport and event queue insertion remain declared services. Rate selection is the pinned modern community model, not current executable disassembly. Exact reentry/global callback order/geometry/seed/client acceptance remain open.',
            'Connect S3without adding Hunger/Rabies to the A1only dispatcher. Verify live normalized timing, real input, movement/weapon/form/recovery cancellation and save/reload when their learned mechanics are connected. No new handler, installed change or final asset is claimed.',
        ])
    path = ROOT / 'tools/reference/druid-s3-clock-engine-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Druid S3:28,800author/scheduler contexts,28frame-bonus controls; Hunger/Rabies timing prepared, no handler enabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
