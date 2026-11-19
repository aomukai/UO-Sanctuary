import io
from pathlib import Path
import subprocess
import unittest
from unittest.mock import Mock, call

from tools.local_launcher import LocalGame


STATUS = "Britain baseline status: doors=8"
SAVE = "Saving world"
ON_DISK = "Writing world save snapshot done (0.02 seconds)"


class LocalLauncherTests(unittest.TestCase):
    def game_at_console(self, messages):
        game = LocalGame(Path(".local"), Mock(), Mock())
        game.ready = True
        game.server = Mock()
        game.server.poll.return_value = None
        game.server.wait.return_value = 0
        transcript = "".join(f"[12:00:00 INF] {message} <s:Server.World>\n" for message in messages)
        game.output = io.StringIO(transcript)
        game.send = Mock()
        return game, transcript

    def test_waits_for_fresh_snapshot_before_shutdown(self):
        game, transcript = self.game_at_console([SAVE, STATUS, ON_DISK, STATUS, SAVE, ON_DISK])
        position_at_shutdown = []

        def record(command):
            if command == "shutdown":
                position_at_shutdown.append(game.output.tell())

        game.send.side_effect = record
        self.assertEqual(game.stop(Mock()), "World saved.")
        self.assertEqual(position_at_shutdown, [len(transcript)])
        self.assertEqual(game.send.call_args_list, [
            call("sanctuary-status"), call("sanctuary-status"), call("save"), call("shutdown"),
        ])

    def test_failed_save_keeps_server_running(self):
        game, _ = self.game_at_console([STATUS, SAVE, "Writing world save snapshot failed"])
        server = game.server
        try:
            with self.assertRaisesRegex(RuntimeError, "world save failed"):
                game.stop(Mock())
            self.assertEqual(game.send.call_args_list, [call("sanctuary-status"), call("save")])
            server.wait.assert_not_called()
            self.assertIs(game.server, server)
        finally:
            game.release_server()

    def test_waiting_for_slow_shutdown_does_not_request_another_save(self):
        game, _ = self.game_at_console([STATUS, SAVE, ON_DISK])
        game.server.wait.side_effect = [subprocess.TimeoutExpired("ModernUO", 30), 0]
        with self.assertRaisesRegex(RuntimeError, "still shutting down"):
            game.stop(Mock())
        self.assertEqual(game.stop(Mock()), "World saved.")
        self.assertEqual(game.send.call_args_list, [call("sanctuary-status"), call("save"), call("shutdown")])
