#!/usr/bin/env python3
"""Original Lethargy recipient payload and active marker across actual reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_warlock_chaos_acceptance import configuration
STAGE=ROOT/'.local/class-skills/sigil-lethargy-lifecycle'
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('sigil-lethargy-check','sigil-lethargy-reload-check')
    reference=configuration()['extra_files']['sigil-lethargy-cases.json']
    check(stage=STAGE,extra_files={'sigil-lethargy-cases.json':reference},extra_families=('sigil-lethargy',),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),))
