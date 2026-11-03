#!/usr/bin/env python3
"""Bind research evidence without relabeling it as a production skill change."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/defiler-compiled-formula'
PARENT='524328631d6b8eae7e2e040660bf0dfc34ffa11e'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 proofpath=ROOT/'tools/reference/defiler-compiled-formula-evidence.json';proof=json.loads(proofpath.read_text())
 assert proof['referenceVersion']=='3.3.93847' and proof['currentCompiledDataVerified'] and not proof['currentFormulaExecutionVerified'] and not proof['handlerEnabled'] and not proof['currentExecutableParity']
 assert (proof['passiveStatMatches'],proof['passiveCalcPresenceMatches'],proof['rawLegacyEvaluatorCases'],proof['tenfoldCandidateMismatchCases'])==(5148,5148,5355,5354)
 for n,digest in {**proof['inputs'],**proof['artifacts']}.items():assert sha(ROOT/n)==digest,n
 snapshot(ROOT/'.local/class-skills/defiler-body/pre-build-1.json')
 previous=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/defiler-body-native-evidence.json'],cwd=ROOT));assert previous['verified']['nativeMarkers']==393 and previous['verified']['focusedNativeMarkers']==61
 for n,digest in previous['tested']['artifacts'].items():
  path=ROOT/'.local/class-skills/defiler-body'/('native/Distribution/'+n if n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll') else 'content/Sanctuary.Content.dll' if n=='Assemblies/Sanctuary.Content.dll' else 'integration/Sanctuary.Integration.dll')
  assert sha(path)==digest,n
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for n,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==digest,n
 protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
 for key,rel in (('saves','.local/server/Saves'),('configuration','.local/server/Configuration')):
  p=Path(installed['runtimeRoot'])/rel;assert {str(f.relative_to(p)):sha(f) for f in p.rglob('*') if f.is_file()}==protected[key]
 assert 'current multiplier remains unverified' in (STAGE/'source-check.log').read_text()
 files=[Path(__file__).resolve(),ROOT/'tools/probe_defiler_compiled_formula.py',proofpath,STAGE/'extraction.json',STAGE/'source-check.log',
  *(ROOT/'docs'/n for n in ('defiler-compiled-formula-checkpoint.md','defiler-body-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md'))]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,component='Compiled passive data identity and legacy-decoder incompatibility research',productionChanged=False,handlerEnabled=False,goalComplete=False,
  verified=dict(passiveStatMatches=5148,passiveCalcPresenceMatches=5148,rawLegacyEvaluatorCases=5355,tenfoldCandidateMismatchCases=5354,currentFormulaExecutionVerified=False,unchangedProductionBuild=True,retainedProductionMarkers=393,retainedFocusedMarkers=61,protectedInstalledArtifactsUnchanged=106),
  limitations=['Data/format evidence only.5354mismatches are incompatibility observations,not current damage comparisons. No byte stripping,normalization,multiplier or mechanical handler is enabled.',
   'Production source/runtime data and the five accepted assemblies match the prior verified checkpoint;no new native regression was needed because no production change was made. Defiler offense/Health Link and the overall class roadmap remain unfinished.'],
  evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 p=ROOT/'tools/reference/defiler-compiled-boundary-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Compiled boundary:5148stat/5148presence matches;5355old-decoder cases;production393/61build and106installed/38save/20configuration files unchanged;multiplier still unverified.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
