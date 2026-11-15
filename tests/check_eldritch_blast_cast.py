#!/usr/bin/env python3
"""Approved world/learned lifecycle plus actual left/right/quick auto-pulse input."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    families=('eldritch-blast-world','eldritch-blast-cast')
    names=tuple(f'{f}-{s}'for f in families for s in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/eldritch-blast-cast',extra_families=families,extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_target_input_commands=tuple('eldritch-blast-cast-'+s for s in ('left','right','quick')),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/eldritch-blast-world-rule-approval.json'))
