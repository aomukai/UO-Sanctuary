#!/usr/bin/env python3
"""Prepared nested Sentinel plus affected shared birth and missile consumers."""
import os,re
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-sentinel'
 names=('BladeMissileChecks','BladeSpawnChecks','BladeLifecycleChecks','BladeBodyChecks','BladeControllerChecks','AssassinTrapCreationChecks')
 for name in names:
  p=ROOT/'tests/Sanctuary.Integration'/f'{name}.cs'
  for f in re.findall(r'"([\w-]+(?:source-probe|source-cases)\.json)"',p.read_text()):c['extra_files'][f]=ROOT/'.local/class-skills'/f
 c['extra_families']=('shadow-sentinel','blade-spawn','blade-missile','blade-lifecycle')
 c['extra_wait_results']={'blade-missile-check':'blade-missile-result','blade-missile-reload-check':'blade-missile-reload-result','blade-lifecycle-check':'blade-offline-result','blade-lifecycle-reload-check':'blade-offline-result'}
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/assassin-trap-summon-engine-evidence.json',ROOT/'tools/reference/blade-sentinel-engine-evidence.json',ROOT/'tools/probe_shadow_fury_callback.py',ROOT/'tools/reference/shadow-fury-callback-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
