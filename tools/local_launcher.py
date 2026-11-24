"""Launch the installed local game and supervise its save and shutdown."""

import fcntl
import ipaddress
import json
import os
import pty
from queue import Empty, SimpleQueue
import re
import socket
import subprocess
import termios
from threading import Event, Thread
import time


STATUS_COMMAND = "sanctuary-status"
STATUS_REPLY = "Britain baseline status:"
SAVE_DONE = "Writing world save snapshot done"
SAVE_FAILURES = ("Preparing to save world failed", "Saving world failed", "Writing world save snapshot failed")
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def local_addresses(settings_path):
    settings = json.loads(settings_path.read_text())
    try:
        port = int(settings["port"])
        host = settings["ip"]
        addresses = ["127.0.0.1", "::1"] if host.lower() == "localhost" else [str(ipaddress.ip_address(host))]
        if not 1 <= port <= 65535 or any(not ipaddress.ip_address(address).is_loopback for address in addresses):
            raise ValueError
    except (AttributeError, KeyError, TypeError, ValueError) as error:
        raise ValueError("Set the ClassicUO profile to a local address and port before playing.") from error
    return [(address, port) for address in addresses]


def socket_open(address):
    try:
        with socket.create_connection(address, timeout=0.2):
            return True
    except OSError:
        return False


def request_client_quit(pid):
    from Xlib import X, Xatom, display, error, protocol

    try:
        connection = display.Display()
    except error.DisplayError as failure:
        raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.") from failure
    try:
        pid_atom = connection.intern_atom("_NET_WM_PID")
        protocols = connection.intern_atom("WM_PROTOCOLS")
        delete = connection.intern_atom("WM_DELETE_WINDOW")
        windows = connection.screen().root.query_tree().children
        while windows:
            window = windows.pop()
            try:
                owner = window.get_full_property(pid_atom, Xatom.CARDINAL)
                if owner is not None and list(owner.value) == [pid] and delete in window.get_wm_protocols():
                    event = protocol.event.ClientMessage(window=window, client_type=protocols,
                                                         data=(32, [delete, X.CurrentTime, 0, 0, 0]))
                    window.send_event(event)
                    connection.sync()
                    return True
                windows.extend(window.query_tree().children)
            except error.BadWindow:
                continue
        return False
    finally:
        connection.close()


class LocalGame:
    def __init__(self, local, prepare_launch, command_environment):
        self.logs = local / "logs"
        self.prepare_launch = prepare_launch
        self.command_environment = command_environment
        self.server = None
        self.client = None
        self.console = None
        self.output = None
        self.ready = False
        self.snapshot_in_progress = False
        self.shutdown_requested = False

    def spawn(self, spec, name, stdin):
        argv, folder, environment = spec
        self.logs.mkdir(parents=True, exist_ok=True)
        log = self.log_path(name)
        if log.is_file():
            log.replace(log.with_suffix(".previous.log"))
        with log.open("wb") as output:
            return subprocess.Popen(argv, cwd=folder, env=self.command_environment(environment),
                                    stdin=stdin, stdout=output, stderr=subprocess.STDOUT,
                                    start_new_session=True)

    def log_path(self, name):
        return self.logs / f"launcher-{name}.log"

    def play(self, progress, closing):
        server = self.prepare_launch("server")
        client_argv, client_folder, client_env = self.prepare_launch("client")
        addresses = local_addresses(client_folder / "settings.json")
        if any(map(socket_open, addresses)):
            raise ValueError("The local game port is already in use. Close the other server and try again.")

        self.console, terminal = pty.openpty()
        try:
            flags = termios.tcgetattr(terminal)
            flags[3] &= ~(termios.ECHO | termios.ECHONL)
            termios.tcsetattr(terminal, termios.TCSANOW, flags)
            self.server = self.spawn(server, "server", terminal)
        except (OSError, termios.error):
            self.release_server()
            raise
        finally:
            os.close(terminal)

        try:
            self.output = self.log_path("server").open("r", encoding="utf-8", errors="replace")
            listeners = {f"Listening: [{host}]:{port}" if ":" in host else f"Listening: {host}:{port}"
                         for host, port in addresses}
            self.wait_for(listeners.__contains__, "the local server to listen", timeout=120, starting=True)
            self.sync_console()
            if not any(map(socket_open, addresses)):
                raise RuntimeError("ModernUO reported ready, but its local port is unavailable.")
        except (OSError, RuntimeError):
            self.stop_startup()
            raise

        self.ready = True
        if closing.is_set():
            return self.stop(progress)
        progress("Opening ClassicUO…")
        try:
            # Tk and the client use the same X11 display, including on XWayland.
            client = client_argv, client_folder, {**client_env, "SDL_VIDEODRIVER": "x11"}
            self.client = self.spawn(client, "client", subprocess.DEVNULL)
        except OSError:
            self.stop(progress)
            raise

    def stop_startup(self):
        # A startup failure can leave the server blocked on an interactive setup prompt.
        if self.server.poll() is None:
            self.server.terminate()
            try:
                self.server.wait(timeout=10)
            except subprocess.TimeoutExpired as error:
                raise RuntimeError("ModernUO is still starting. Try Save & Shut Down again.") from error
        self.release_server()

    def stop(self, progress):
        if self.shutdown_requested:
            return self.finish_shutdown(progress)
        if not self.ready:
            self.stop_startup()
            return "Startup stopped."

        client_status = None
        if self.client is not None:
            if self.client.poll() is None:
                progress("Closing ClassicUO…")
                deadline = time.monotonic() + 20
                while self.client.poll() is None and not request_client_quit(self.client.pid):
                    if time.monotonic() >= deadline:
                        raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.")
                    time.sleep(0.15)
                try:
                    self.client.wait(timeout=20)
                except subprocess.TimeoutExpired as error:
                    raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.") from error
            client_status = self.client.returncode
            self.client = None

        if self.server is None or self.server.poll() is not None:
            self.release_server()
            raise RuntimeError("ModernUO exited before the launcher could save the world.")

        progress("Saving the world…")
        self.sync_console()
        if self.snapshot_in_progress:
            self.wait_for_save()
            self.sync_console()
        self.send("save")
        self.wait_for(lambda message: message == "Saving world", "the final save to start", saving=True)
        self.wait_for_save()

        self.send("shutdown")
        self.shutdown_requested = True
        result = self.finish_shutdown(progress)
        if client_status:
            return f"{result} ClassicUO exited with code {client_status}."
        return result

    def finish_shutdown(self, progress):
        progress("Shutting down ModernUO…")
        try:
            status = self.server.wait(timeout=30)
        except subprocess.TimeoutExpired as error:
            raise RuntimeError("The world was saved. ModernUO is still shutting down.") from error
        self.release_server()
        if status:
            raise RuntimeError(f"The world was saved. ModernUO then exited with code {status}.")
        return "World saved."

    def release_server(self):
        if self.console is not None:
            os.close(self.console)
        if self.output is not None:
            self.output.close()
        self.console = self.output = self.server = None
        self.ready = False
        self.snapshot_in_progress = False
        self.shutdown_requested = False

    def send(self, command):
        os.write(self.console, f"{command}\n".encode())

    def sync_console(self):
        self.send(STATUS_COMMAND)
        self.wait_for(lambda message: message.startswith(STATUS_REPLY), "ModernUO’s console")

    def wait_for_save(self):
        self.wait_for(lambda message: message.startswith(SAVE_DONE), "the save to reach disk", timeout=120, saving=True)

    def wait_for(self, matches, description, timeout=30, starting=False, saving=False):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            position = self.output.tell()
            line = self.output.readline()
            if not line.endswith("\n"):
                self.output.seek(position)
                if self.server.poll() is not None:
                    raise RuntimeError(f"ModernUO exited while waiting for {description}.")
                time.sleep(0.08)
                continue

            message = ANSI.sub("", line).split(" <s:", 1)[0].split("] ", 1)[-1].strip()
            if starting and ("create the owner account now?" in message or
                             "Pre-bake the pathfinding cache" in message or message == "No world save data found."):
                raise RuntimeError("ModernUO needs console setup. Run ./dev server --installed in a terminal first.")
            if message == "Saving world":
                self.snapshot_in_progress = True
            elif message.startswith((SAVE_DONE, *SAVE_FAILURES)):
                self.snapshot_in_progress = False
                if saving and message.startswith(SAVE_FAILURES):
                    raise RuntimeError("The world save failed. ModernUO is still running.")
            if matches(message):
                return
        raise RuntimeError(f"Timed out waiting for {description}.")


def run_launcher(local, prepare_launch, command_environment):
    try:
        import tkinter as tk
        from tkinter import ttk
    except ImportError as error:
        raise ValueError("Install your Linux distribution’s Tkinter package (python3-tk on Ubuntu).") from error
    try:
        from Xlib import display as _display
    except ImportError as error:
        raise ValueError("Install Python Xlib for clean client shutdown (python3-xlib on Ubuntu).") from error

    local.mkdir(parents=True, exist_ok=True)
    with (local / "launcher.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise ValueError("The Sanctuary launcher is already open.") from error

        try:
            window = tk.Tk()
        except tk.TclError as error:
            raise ValueError(f"Could not open the launcher window: {error}") from error
        window.title("Sanctuary")
        window.resizable(False, False)
        panel = ttk.Frame(window, padding=20)
        panel.grid()
        status = tk.StringVar(value="Ready to play.")
        ttk.Label(panel, textvariable=status, width=42, wraplength=310, justify="center").grid(pady=(0, 12))
        game = LocalGame(local, prepare_launch, command_environment)
        messages = SimpleQueue()
        busy = False
        closing = Event()

        def run_task(action, text):
            nonlocal busy
            busy = True
            button.state(["disabled"])
            status.set(text)

            def worker():
                try:
                    result = action(lambda message: messages.put(("progress", message)))
                except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
                    messages.put(("error", f"{error}\nLogs: .local/logs/launcher-*.log"))
                else:
                    messages.put(("done", result or "Playing locally."))

            Thread(target=worker, daemon=True).start()

        def play_or_stop():
            if game.server is None:
                run_task(lambda progress: game.play(progress, closing), "Starting ModernUO…")
            else:
                run_task(game.stop, "Saving and shutting down…")

        def close_window():
            closing.set()
            if not busy:
                if game.server is None:
                    window.destroy()
                else:
                    play_or_stop()

        def poll():
            nonlocal busy
            while True:
                try:
                    kind, text = messages.get_nowait()
                except Empty:
                    break
                status.set(text)
                if kind == "progress":
                    continue
                busy = False
                button.configure(text="Play" if game.server is None else "Save & Shut Down")
                button.state(["!disabled"])
                if kind == "error":
                    closing.clear()

            if not busy:
                if closing.is_set() and game.server is None:
                    window.destroy()
                    return
                client_exited = game.client is not None and game.client.poll() is not None
                server_exited = game.server is not None and game.server.poll() is not None
                if closing.is_set() or client_exited or server_exited:
                    play_or_stop()
            window.after(100, poll)

        button = ttk.Button(panel, text="Play", command=play_or_stop, width=22)
        button.grid()
        button.focus_set()
        window.protocol("WM_DELETE_WINDOW", close_window)
        window.after(100, poll)
        window.mainloop()
