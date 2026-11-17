#!/usr/bin/env python3
"""Current Paladin regression, all learned input routes and original saves."""
import os
import sys
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_paladin_planned_acceptance import configuration as prior
from check_paladin_aura_input import INPUTS
from check_assassin_planned_acceptance import paced_clients

def configuration():
    c=prior();c['stage']=ROOT/'.local/class-skills/paladin-lifecycle-acceptance'
    c['extra_families']+=('fanaticism-mana',)
    c['extra_wait_results']['fanaticism-mana-reload-check']='fanaticism-mana-reload-check-result'
    c['extra_connected_checks']+=('fanaticism-mana-reload-check',)
    c['extra_target_input_commands']+=INPUTS;c['extra_instant_input_commands']+=INPUTS
    c['extra_connection_settle']=.15
    helpers=tuple(Path(m.__file__).resolve() for m in tuple(sys.modules.values())
        if getattr(m,'__file__',None) and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],Path(__file__).resolve(),*helpers)))
    assert len(c['extra_target_input_commands'])==89
    for p in c['extra_inputs']:assert p.is_file(),p
    return c

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
