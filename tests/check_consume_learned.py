#!/usr/bin/env python3
"""Approved normal Consume selection/menu,original/bound/numeric/aura saves."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/consume-learned'
def configuration():
    families=('consume-suppliers','consume-world','bound-demon-consume','consume-aura')
    names=tuple(n for f in families for n in (f+'-check',f+'-reload-check'))
    return dict(stage=STAGE,extra_families=families,extra_connected_checks=names,extra_wait_results={n:n+'-result' for n in names},extra_target_input_commands=tuple('consume-learned-'+m for m in ('right','quick','defiler','menu')),extra_ground_input_commands=('consume-learned-menu',),extra_connection_settle=.15,connected_save=True,extra_pre_save_commands=('consume-aura-save-check',),extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/warlock-final-talents-approval.json',ROOT/'tools/reference/d2r-consume/sources.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
