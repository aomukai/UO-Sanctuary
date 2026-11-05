#!/usr/bin/env python3
"""Run the prior full-fill/hand/item/resource acceptance on the flat-drain candidate."""
import os
from pathlib import Path
from check_npc_damage_fill import configuration
from check_iron_golem_birth import check
if __name__=='__main__':
    root=Path(__file__).resolve().parents[1]
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(root),SANCTUARY_FIRST_LOOP_ROOT=str(root))
    config=configuration(compact=True);config['stage']=root/'.local/class-skills/npc-flat-drains-full-fill'
    check(**config)
