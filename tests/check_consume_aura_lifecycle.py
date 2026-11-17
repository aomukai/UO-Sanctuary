#!/usr/bin/env python3
"""Original Consume provider and captured wound pools across active save/reload."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
STAGE=ROOT/'.local/class-skills/consume-aura-lifecycle'
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('consume-aura-check','consume-aura-reload-check')
    check(stage=STAGE,extra_families=('consume-aura',),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,
        connected_save=True,extra_pre_save_commands=('consume-aura-save-check',),extra_inputs=(Path(__file__).resolve(),))
