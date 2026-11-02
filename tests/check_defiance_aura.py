#!/usr/bin/env python3
"""Defiance's defense consumers using existing aura saves and shared instant inputs."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_might_aura import configuration as parent

def configuration(full=False):
    c=parent(full);c['stage']=ROOT/'.local/class-skills/defiance-aura'
    c['extra_checks']+=('defiance-aura-check',)
    names=tuple('defiance-aura-'+mode for mode in ('right','quick'))
    c['extra_target_input_commands']+=names;c['extra_instant_input_commands']+=names
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/inner-sight-engine-evidence.json',ROOT/'tools/reference/holy-shield-engine-evidence.json')
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration('--full' in sys.argv))
