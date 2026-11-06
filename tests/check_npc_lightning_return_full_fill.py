#!/usr/bin/env python3
"""Retain original generated-item/fill and saved resources on the lightning build."""
import os
from pathlib import Path
from check_npc_damage_fill import configuration
import check_iron_golem_birth as native
from check_npc_freeze_hit_full_fill import FillSession

if __name__=='__main__':
    root=Path(__file__).resolve().parents[1];os.environ.update(SANCTUARY_PROJECT_ROOT=str(root),SANCTUARY_FIRST_LOOP_ROOT=str(root))
    c=configuration(compact=True);c['stage']=root/'.local/class-skills/npc-lightning-return-full-fill';c['extra_inputs']+=(Path(__file__).resolve(),)
    native.Session=FillSession;native.check(**c)
