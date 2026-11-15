#!/usr/bin/env python3
"""Complete proposed Echoing action/world with retained original input proof."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('echoing-strike-world-check','echoing-strike-world-reload-check')
    check(stage=ROOT/'.local/class-skills/echoing-strike-world',extra_families=('echoing-strike-inputs','echoing-strike-world'),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,extra_inputs=(Path(__file__).resolve(),))
