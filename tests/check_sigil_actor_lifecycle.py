#!/usr/bin/env python3
"""Original active Sigil families, Rancor and Death save identity and positive resume."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
STAGE=ROOT/'.local/class-skills/sigil-actor-lifecycle'
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    families=('sigil-actor','sigil-family','rancor-pulse','sigil-death')
    names=tuple(n for f in families for n in (f+'-check',f+'-reload-check'))
    check(stage=STAGE,extra_families=families,extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),))
