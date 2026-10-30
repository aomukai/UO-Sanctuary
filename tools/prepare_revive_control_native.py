#!/usr/bin/env python3
"""Replay the native series separately for class-minion commandability."""
import argparse
from pathlib import Path
import prepare_revive_retention_native as native

native.STAGE = native.ROOT / '.local/class-skills/revive-native-control'

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path, nargs='?', default=Path('/media/aomukai/FILES/Sanctuary/upstream/ModernUO'))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    native.prepare(args.source, args.check)
