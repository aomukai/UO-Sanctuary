#!/usr/bin/env python3
"""Reuse the exact existing43patch preparation for the Hex self-target change."""
from pathlib import Path
import prepare_psychic_ward_client as existing
if __name__=='__main__':
    existing.STAGE=existing.ROOT/'.local/class-skills/hex-siphon-cast'
    existing.STAGE.mkdir(exist_ok=True)
    existing.prepare()
    print('Hex client prepared;Ward and all three self-Hex metadata entries retained.')
