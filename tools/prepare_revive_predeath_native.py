#!/usr/bin/env python3
"""Reuse the isolated native replay workflow for pre-death capture."""
import argparse
from pathlib import Path
import prepare_revive_retention_native as native

native.STAGE = native.ROOT / '.local/class-skills/revive-predeath-state'

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path, nargs='?', default=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO'))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    native.prepare(args.source, args.check)
