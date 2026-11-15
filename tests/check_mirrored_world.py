#!/usr/bin/env python3
"""Complete prepared Mirrored world plus retained packets and learned Echoing."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{f}-{s}'for f in ('mirrored-world','echoing-strike-cast')for s in ('check','reload-check'))
    check(extra_files={'magic-arrow-action-source-probe.json':ROOT/'.local/class-skills/magic-arrow-action-source-probe.json'},stage=ROOT/'.local/class-skills/mirrored-world',extra_families=('cleave-action','magic-arrow-cast','mirrored-packets','mirrored-world','echoing-strike-cast'),
        extra_manifest_names={'magic-arrow-cast':'magic-arrow-cast.json'},
        extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_target_input_commands=tuple('echoing-strike-cast-'+s for s in ('left','right','quick')),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/echoing-strike-world-rule-approval.json'))
