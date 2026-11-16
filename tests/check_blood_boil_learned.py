#!/usr/bin/env python3
"""Approved normal Blood Boil inputs with original/bound/lifecycle regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/blood-boil-learned'
def configuration():
    families=('blood-boil-world','bound-demon-recipient')
    commands=tuple(n for f in families for n in (f+'-check',f+'-reload-check'))
    inputs=tuple('blood-boil-learned-'+m for m in ('left','right','quick'))
    return dict(stage=STAGE,extra_families=families,extra_connected_checks=commands,extra_wait_results={n:n+'-result' for n in commands},extra_target_input_commands=inputs,extra_ground_input_commands=inputs,extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/warlock-final-talents-approval.json',ROOT/'tools/reference/blood-boil-source-review.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
