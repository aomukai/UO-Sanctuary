#!/usr/bin/env python3
"""Prepared held input/shared world timer with retained channel and life controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_inferno_held_input import wires as inferno_wires
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    connected=('bound-demon-life','bind-demon-channel','bind-demon-input')
    names=tuple(n for f in connected for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-input',extra_families=connected,extra_connected_checks=names,
        extra_wait_results={n:n+'-result' for n in names},extra_connection_settle=.05,
        extra_wire_callbacks=(lambda session,port,boot:inferno_wires(session,port,boot,sources=(1,)),),
        extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_inferno_held_input.py',ROOT/'tools/reference/bind-demon-channel-rule-review.json'))
