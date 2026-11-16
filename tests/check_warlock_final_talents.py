#!/usr/bin/env python3
"""All four approved learned talents together;reuse accepted current binaries."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_warlock_remaining_review import FAMILIES
STAGE=ROOT/'.local/class-skills/warlock-final-talents'
def configuration():
    names=tuple(n for f in FAMILIES for n in (f+'-check',f+'-reload-check'))
    inputs=tuple('engorge-learned-'+m for m in ('left','right','quick','ground'))+tuple('blood-boil-learned-'+m for m in ('left','right','quick'))+tuple('consume-learned-'+m for m in ('right','quick','defiler','menu'))
    return dict(stage=STAGE,extra_families=FAMILIES,extra_checks=('blood-oath-learned-check',),extra_connected_checks=(*names,'blood-oath-learned-check'),extra_wait_results={**{n:n+'-result' for n in names},'blood-oath-learned-check':'blood-oath-learned-result'},extra_target_input_commands=inputs,extra_ground_input_commands=('engorge-learned-ground','blood-boil-learned-left','blood-boil-learned-right','blood-boil-learned-quick','consume-learned-menu'),extra_connection_settle=.15,connected_save=True,extra_pre_save_commands=('consume-aura-save-check',),extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_warlock_remaining_review.py',ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/warlock-final-talents-approval.json',ROOT/'tools/reference/blood-oath-source-review.json',ROOT/'tools/reference/engorge-source-review.json',ROOT/'tools/reference/blood-boil-source-review.json',ROOT/'tools/reference/d2r-consume/sources.json',ROOT/'tools/reference/bind-demon-monsters-source.json'))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
