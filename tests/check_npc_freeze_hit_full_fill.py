#!/usr/bin/env python3
"""Retain full NPC fill/hand/item/resource composition on the freeze receiver."""
import os
from pathlib import Path
from check_npc_damage_fill import configuration
import check_iron_golem_birth as native
from check_iron_golem_birth import check

class FillSession(native.Session):
    def expect(self,pattern,timeout=60):
        # This retained matrix performs 294,840 original-item rolls per boot.
        # Give that command a bounded budget without changing its assertions.
        if "generated-npc-physical" in pattern:timeout=max(timeout,180)
        return super().expect(pattern,timeout)
if __name__=='__main__':
    root=Path(__file__).resolve().parents[1]
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(root),SANCTUARY_FIRST_LOOP_ROOT=str(root))
    c=configuration(compact=True);c['stage']=root/'.local/class-skills/npc-freeze-hit-full-fill'
    c['extra_inputs']+=(Path(__file__).resolve(),)
    native.Session=FillSession
    check(**c)
