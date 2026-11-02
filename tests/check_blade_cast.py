#!/usr/bin/env python3
"""Learned Blade Sentinel with retained source path, actor, weapon and lifecycle cohorts."""
import os,re
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
 names=('BladeAiChecks','ActorPathChecks','ActorMotionChecks','TowardPathChecks','TowardMotionChecks','AssassinTrapMovementChecks','AssassinTrapCreationChecks','BladeSpawnChecks','BladeMissileChecks','BladeControllerChecks','BladeClockChecks')
 probes=set()
 for name in names:
  probes.update(re.findall(r'"([\w-]+-source-probe\.json)"',(ROOT/'tests/Sanctuary.Integration'/(name+'.cs')).read_text()))
 inputs=('blade-cast-left','blade-cast-right','blade-cast-quick')
 return dict(stage=ROOT/'.local/class-skills/blade-cast',
  extra_files={name:ROOT/'.local/class-skills'/name for name in sorted(probes)},
  extra_families=('blade-body','blade-controller','blade-missile','blade-spawn','blade-lifecycle','assassin-trap-creation','blade-cast'),
  extra_checks=('blade-cast-availability-check','blade-cast-mechanics-check','actor-path-check','actor-motion-check','toward-path-check','toward-motion-check','assassin-trap-movement-check','blade-ai-check','blade-clock-check'),
  extra_connected_checks=('blade-cast-check','blade-cast-reload-check'),
  extra_wait_results={'blade-cast-check':'blade-cast-result','blade-cast-reload-check':'blade-cast-reload-result',
   'blade-controller-check':'blade-controller-result','blade-controller-reload-check':'blade-controller-reload-result',
   'blade-missile-check':'blade-missile-result','blade-missile-reload-check':'blade-missile-reload-result',
   'blade-lifecycle-check':'blade-offline-result','blade-lifecycle-reload-check':'blade-offline-result'},
  extra_target_input_commands=inputs,extra_ground_input_commands=inputs,
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/blade-travel-native-evidence.json',ROOT/'tools/reference/blade-lifecycle-native-evidence.json'))

if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
