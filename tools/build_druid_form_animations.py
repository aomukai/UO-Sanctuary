#!/usr/bin/env python3
"""Pack the original werewolf and werebear pose sheets for ClassicUO."""

from pathlib import Path

from build_summon_animations import write_archive


ART = Path(__file__).resolve().parents[1] / "art" / "druid-forms"


if __name__ == "__main__":
    write_archive(ART, "druid-forms", (("werewolf", 79), ("werebear", 87)))
