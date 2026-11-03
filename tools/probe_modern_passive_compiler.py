#!/usr/bin/env python3
"""Compare an unchanged modern compiler port with owned passive bytecode.

Compilation equivalence is deliberately separate from runtime execution.
The ignored upstream checkout must already exist at the pinned commit.
"""
import argparse
import csv
import hashlib
import json
import struct
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UPSTREAM = ROOT / '.local/research/D2RBinTool'
STAGE = ROOT / '.local/class-skills/modern-passive-compiler'
COMMIT = 'fe6078123d6f011f46af9ea0802739ba672b0547'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def probe(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    inputs = {}

    def pin(path):
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return path.read_bytes()

    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=UPSTREAM).decode().strip() == COMMIT
    subprocess.run(['git', 'diff', '--exit-code', 'HEAD', '--', 'src', 'README.md', 'CMakeLists.txt'], cwd=UPSTREAM, check=True)
    upstream = {}
    for path in sorted((UPSTREAM / 'src').glob('*')):
        if path.is_file():
            rel = str(path.relative_to(UPSTREAM))
            raw = pin(path)
            blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
            assert blob == subprocess.check_output(['git', 'rev-parse', COMMIT + ':' + rel], cwd=UPSTREAM).decode().strip()
            upstream[rel] = dict(sha256=sha(path), gitBlob=blob)
    pin(UPSTREAM / 'README.md')
    prior = json.loads(pin(ROOT / 'tools/reference/defiler-compiled-formula-evidence.json'))
    assert prior['currentCompiledDataVerified'] and not prior['currentFormulaExecutionVerified']
    owned = ROOT / '.local/class-skills/defiler-compiled-formula'
    for name in ('skills.bin', 'skillscode.bin'):
        path = owned / name
        assert sha(path) == prior['inputs'][str(path.relative_to(ROOT))]
        pin(path)
    binary, code = (owned / 'skills.bin').read_bytes(), (owned / 'skillscode.bin').read_bytes()
    source = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads(pin(source / 'sources.json'))

    def table(name):
        path = source / 'json' / (name + '.json')
        assert sha(path) == manifest['files']['json/' + name + '.json']['sha256']
        return json.loads(pin(path))

    skills, stats = table('skills'), table('itemstatcost')
    curve_path = ROOT / '.local/class-skills/warlock-foundation/skillcalc.txt'
    pin(curve_path)
    curves = list(csv.DictReader(curve_path.read_text().splitlines(), delimiter='\t'))
    seeds = []
    for key, row in sorted(skills.items(), key=lambda kv: int(kv[0])):
        if 'skill' in row:
            seeds.append(['S', int(key), row['skill'].lower()])
        else:
            assert int(key) == 362  # Owned empty record; no name to resolve.
    for row in sorted(stats.values(), key=lambda r: r['*ID']):
        seeds.append(['T', row['*ID'], row['Stat'].lower()])
    for i, row in enumerate(curves):
        seeds.append(['C', i, row['code']])
    seed_path = STAGE / 'seeds.tsv'
    seed_path.write_text(''.join('\t'.join(map(str, row)) + '\n' for row in seeds))
    cases = []
    for skill in range(429):
        for slot in range(12):
            text = skills[str(skill)].get('passivecalc' + str(slot + 1))
            offset = struct.unpack_from('<i', binary, 4 + skill * 748 + 216 + slot * 4)[0]
            assert (offset >= 0) == (text is not None)
            if text is not None:
                text = str(text)
                assert '\t' not in text and '\n' not in text
                cases.append(dict(skill=skill, slot=slot + 1, text=text, offset=offset))
    defiler = next(r for r in cases if r['skill'] == 377 and r['slot'] == 2)
    controls = []
    for rank in range(1, 256):
        for hard in range(21):
            # Numeric analogues exercise the upstream constant folder, not
            # the game runtime, parent callback, or skill callback.
            # Replace the callback first: 'lvl' is also a suffix of 'blvl'.
            text = defiler['text'].replace("skill('Demonic Mastery'.blvl)", str(hard)).replace('par2', '10').replace('lvl', str(rank))
            controls.append(dict(rank=rank, hardMastery=hard, text=text))
    case_path = STAGE / 'expressions.tsv'
    case_path.write_text(''.join(str(i) + '\t' + row['text'] + '\n' for i, row in enumerate(cases + controls)))
    driver = STAGE / 'compile.cpp'
    driver.write_text(r'''#include "calc_compiler.h"
#include "excel_engine.h"
#include <cassert>
#include <cstdio>
#include <fstream>
#include <string>
int main(int argc,char**argv){
 assert(argc==3);d2dt::Engine engine(3);std::string line;
 std::ifstream seeds(argv[1]);assert(seeds.good());
 while(std::getline(seeds,line)){
  auto a=line.find('\t'),b=line.find('\t',a+1);assert(a==1&&b!=std::string::npos);
  auto id=std::stoul(line.substr(a+1,b-a-1));auto text=line.substr(b+1);
  if(line[0]=='C'){auto n=engine.GetIndex("skillcalc_code_index")->code.Add(d2dt::PackCodeU32(text.c_str()));assert(n==id);}
  else engine.GetIndex(line[0]=='S'?"skills_skill_index_p0":"itemstatcost_stat_index")->str.Seed(text,id);
 }
 std::ifstream expressions(argv[2]);assert(expressions.good());
 while(std::getline(expressions,line)){
  auto tab=line.find('\t');assert(tab!=std::string::npos);auto text=line.substr(tab+1);
  uint8_t out[4096]={};int size=d2dt::CompileCalcExpression(engine,3,d2dt::CALC_FAM_SKILLS,text.c_str(),out,sizeof(out));
  assert(size>0&&size<=4096);std::printf("%s\t",line.substr(0,tab).c_str());
  for(int i=0;i<size;i++)std::printf("%02x",out[i]);std::printf("\n");
 }
}
''')
    exe = STAGE / 'compile'
    sources = ['calc_compiler.cpp', 'excel_engine.cpp', 'tables.cpp', 'idx_defaults.cpp']
    command = ['g++', '-std=c++17', '-O1', '-g', '-DD2DT_BUILD_93854', '-fsanitize=undefined', '-fno-sanitize-recover=all',
               '-I' + str(UPSTREAM / 'src'), str(driver), *(str(UPSTREAM / 'src' / name) for name in sources), '-o', str(exe)]
    run = subprocess.run(command, capture_output=True)
    (STAGE / 'compiler.log').write_bytes(run.stdout + run.stderr)
    assert run.returncode == 0
    run = subprocess.run([str(exe), str(seed_path), str(case_path)], capture_output=True)
    (STAGE / 'compiled.tsv').write_bytes(run.stdout)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr
    output = run.stdout.decode().splitlines()
    assert len(output) == len(cases) + len(controls)
    mismatches = []
    for i, line in enumerate(output[:len(cases)]):
        index, compiled = line.split('\t')
        assert int(index) == i
        raw = bytes.fromhex(compiled)
        case = cases[i]
        actual = code[case['offset']:case['offset'] + len(raw)]
        case.update(compiledHex=compiled, byteExact=raw == actual)
        if raw != actual:
            mismatches.append(dict(**case, ownedHex=actual.hex()))
    for i, line in enumerate(output[len(cases):]):
        index, compiled = line.split('\t')
        assert int(index) == len(cases) + i
        raw = bytes.fromhex(compiled)
        assert raw[-1] == 0 and raw[0] in (7, 8, 9)
        size, format = {7: (1, '<b'), 8: (2, '<h'), 9: (4, '<i')}[raw[0]]
        assert len(raw) == size + 2
        value = struct.unpack_from(format, raw, 1)[0]
        control = controls[i]
        control.update(compiledHex=compiled, foldedValue=value)
        assert value == control['rank'] - 1 + control['hardMastery']
    comparison = STAGE / 'comparison.json'
    comparison.write_text(json.dumps(dict(cases=cases, numericAnalogues=controls, mismatches=mismatches), indent=2) + '\n')
    assert not mismatches, mismatches[:3]
    assert defiler['compiledHex'] == prior['codeBytes']
    pin(Path(__file__).resolve())
    result = dict(referenceVersion='3.3.93847', component='Modern passive formula compilation equivalence',
                  upstreamRepository='https://github.com/tomrus88/D2RBinTool', upstreamCommit=COMMIT,
                  upstreamVariant='D2DT_BUILD_93854', upstreamSources=upstream,
                  passiveSlotsExamined=5148, compiledFormulas=len(cases), byteExactFormulas=len(cases),
                  rawDefilerCompiledExactly=True, rawDefilerText=defiler['text'], rawDefilerHex=defiler['compiledHex'],
                  constantFoldNumericAnalogueCases=len(controls), constantFoldResult='rank-1+hardMastery',
                  currentFormulaExecutionVerified=False, productionChanged=False, handlerEnabled=False,
                  compilerCommand=command, inputs=inputs,
                  artifacts={str(p.relative_to(ROOT)): sha(p) for p in
                             (seed_path, case_path, driver, exe, STAGE / 'compiler.log', STAGE / 'compiled.tsv', STAGE / 'sanitizer.log', comparison,
                              STAGE / 'diagnostic-1/probe.py', STAGE / 'diagnostic-1/run.log')},
                  established=[
                      'Complete unchanged upstream modern compiler and engine/index helpers compile every populated owned passive formula to byte-identical bytes at its owned skills.bin offset. Raw text is not wrapped or repaired.',
                      'The function emission includes callback index and operand count. The raw malformed Defiler expression compiles to its actual19bytes;the internal02after callback03is its two-argument count,while the later02is an unmatched-parenthesis compiler marker.',
                      '5355explicit numeric analogues retain the malformed parentheses but replace dynamic operands with numbers. The complete upstream constant-folding path returns rank-1+hardMastery,without the outer factor10. This is evidence against silently repairing the text,not an execution comparison for the dynamic formula.',
                  ], limitations=[
                      '93854 is the upstream reverse-engineered compiler variant;agreement with owned93847data establishes these compiler outputs only,not complete executable parity.',
                      'Neither the upstream decompiler nor this compilation probe executes runtime bytecode. In particular,this does not establish the game runtime treatment of the unmatched-parenthesis marker or enable Defiler damage.',
                      'UBSan is clean. This probe does not claim ASan or current-game runtime comparisons. No production parser,saved body,installed assembly,save or configuration is changed.',
                  ])
    target = ROOT / 'tools/reference/modern-passive-compiler-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print(f'Modern compiler: {len(cases)} populated passive formulas byte-exact; {len(controls)} numeric controls fold without factor10; dynamic runtime execution remains unverified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    probe(parser.parse_args().check)
