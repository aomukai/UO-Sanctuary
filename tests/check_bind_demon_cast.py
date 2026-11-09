#!/usr/bin/env python3
"""Focused current Bind Demon orchestration; no unrelated old class matrices."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/bind-demon-cast',extra_checks=('bind-demon-cast-check',),
          extra_files={'bind-demon-cast-cases.json':ROOT/'.local/class-skills/bind-demon-cast-source/cases.json'},
          extra_inputs=(ROOT/'tests/check_bind_demon_cast.py',))
