#!/usr/bin/env python3
"""Flame Wave work:owned captured inputs with accepted Chain/Bolt/Mirrored regression."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{f}-{s}'for f in ('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world')for s in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/flame-wave-world',extra_families=('miasma-world','miasma-cast','mirrored-cast','miasma-chain-world','miasma-chain-cast','flame-wave-world'),
        extra_checks=('miasma-chain-birth-check','flame-wave-birth-check'),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_target_input_commands=('miasma-chain-cast-left','miasma-chain-cast-right','miasma-chain-cast-quick','miasma-cast-left','miasma-cast-right','miasma-cast-quick','mirrored-cast-left','mirrored-cast-right','mirrored-cast-quick','mirrored-crossbow-quick'),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/miasma-chain-world-rule-approval.json'))
