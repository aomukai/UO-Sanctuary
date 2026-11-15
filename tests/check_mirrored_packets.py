#!/usr/bin/env python3
"""Prepared four-kind Mirrored packets with retained learned Echoing inputs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/mirrored-packets',extra_families=('mirrored-packets','echoing-strike-cast'),
        extra_connected_checks=('echoing-strike-cast-check','echoing-strike-cast-reload-check'),
        extra_wait_results={n:n+'-result'for n in ('echoing-strike-cast-check','echoing-strike-cast-reload-check')},
        extra_target_input_commands=tuple('echoing-strike-cast-'+s for s in ('left','right','quick')),extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/echoing-strike-world-rule-approval.json'))
