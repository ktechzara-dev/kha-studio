"""
Mock Schneider SERT / FB_CognexCamera PLC Server
Simulates Schneider PacDrive 3 / Modicon M262 controller listening on TCP 50001.
Used for offline software-in-the-loop testing of Unilever Delta Vision.
"""

import socket
import threading
import time
import re
import sys
import logging

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("MockPLC")


class MockSchneiderPlcServer:
    def __init__(self, host: str = "0.0.0.0", port: int = 50001):
        self.host = host
        self.port = port
        self.server_sock: socket.socket | None = None
        self.client_sock: socket.socket | None = None
        self.is_running = False
        self.total_packets_received = 0

    def start(self):
        self.is_running = True
        self.server_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_sock.bind((self.host, self.port))
        self.server_sock.listen(2)

        print("\n" + "=" * 70)
        print(f"🚀 SCHNEIDER ELECTRIC SERT PLC SIMULATOR (FB_CognexCamera)")
        print(f"📡 Listening on {self.host}:{self.port} ...")
        print(f"⌨️  Commands: [t] Trigger, [m <name>] Set Model, [p] Ping, [q] Quit")
        print("=" * 70 + "\n")

        thread = threading.Thread(target=self._accept_loop, daemon=True)
        thread.start()

    def _accept_loop(self):
        while self.is_running and self.server_sock:
            try:
                client, addr = self.server_sock.accept()
                print(f"\n[+] Vision Client connected from {addr[0]}:{addr[1]}")
                self.client_sock = client
                self._handle_client(client)
            except Exception as ex:
                if self.is_running:
                    logger.error(f"Accept loop error: {ex}")
                break

    def _handle_client(self, client: socket.socket):
        buffer = b""
        while self.is_running:
            try:
                data = client.recv(2048)
                if not data:
                    print("[-] Vision Client disconnected.")
                    break

                buffer += data
                while b"\r" in buffer:
                    line, buffer = buffer.split(b"\r", 1)
                    raw_str = line.decode("ascii", errors="replace").strip()
                    if raw_str:
                        self._parse_and_display_packet(raw_str)

            except Exception as ex:
                print(f"[!] Socket read error: {ex}")
                break

        try:
            client.close()
        except Exception:
            pass
        self.client_sock = None

    def _parse_and_display_packet(self, packet_str: str):
        """
        Parses Vision<Status><Count:02d>A<ModelLen:02d><ModelName>[P<Idx>,<X>,<Y>,<Angle>,<PO>]...
        """
        self.total_packets_received += 1
        ts = time.strftime("%H:%M:%S.") + f"{int(time.time() * 1000) % 1000:03d}"

        # Match header
        header_match = re.match(r"^Vision(\d)(\d{2})A(\d{2})([A-Za-z0-9_]+)", packet_str)
        if not header_match:
            print(f"[{ts}] RX RAW: {packet_str}")
            return

        status = int(header_match.group(1))
        count = int(header_match.group(2))
        model_len = int(header_match.group(3))
        model_name = header_match.group(4)[:model_len]

        # Match bottle payloads: [P01,+420.50,+185.30,+45.2,PO1]
        items = re.findall(r"\[P(\d+),([+-]?\d+\.\d+),([+-]?\d+\.\d+),([+-]?\d+\.\d+),([A-Za-z0-9_]+)\]", packet_str)

        print("\n" + "-" * 70)
        status_text = "🟢 PASS (OK)" if status == 1 else "🔴 FAIL"
        print(f"[{ts}] #PKG {self.total_packets_received} | {status_text} | Model: {model_name} | Bottles: {count}")
        print(f"    {'ID':<5} | {'Robot X [mm]':<14} | {'Robot Y [mm]':<14} | {'Angle [deg]':<12} | {'PO':<6}")
        print("    " + "-" * 62)

        for item in items:
            p_id, px, py, pa, po = item
            print(f"    P{int(p_id):02d}  | {float(px):+12.2f} mm | {float(py):+12.2f} mm | {float(pa):+10.1f}°  | {po:<6}")

        print(f"    Payload: {packet_str[:80]}..." if len(packet_str) > 80 else f"    Payload: {packet_str}")
        print("-" * 70)

    def send_command(self, cmd: str):
        if not self.client_sock:
            print("[!] No Vision Client connected.")
            return

        try:
            payload = (cmd + "\r").encode("ascii")
            self.client_sock.sendall(payload)
            print(f"[TX -> Vision] Sent: {cmd}\\r")
        except Exception as ex:
            print(f"[!] Error sending command: {ex}")

    def stop(self):
        self.is_running = False
        if self.client_sock:
            try:
                self.client_sock.close()
            except Exception:
                pass
        if self.server_sock:
            try:
                self.server_sock.close()
            except Exception:
                pass


def main():
    port = 50001
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except ValueError:
            pass

    server = MockSchneiderPlcServer(host="0.0.0.0", port=port)
    server.start()

    try:
        while server.is_running:
            line = input().strip()
            if not line:
                continue
            if line.lower() == "q":
                break
            elif line.lower() in ("t", "trg"):
                server.send_command("TRG")
            elif line.lower() == "p":
                server.send_command("PING")
            elif line.lower().startswith("m "):
                model = line[2:].strip()
                server.send_command(f"SET_MODEL {model}")
            else:
                server.send_command(line)
    except (KeyboardInterrupt, EOFError):
        pass
    finally:
        server.stop()
        print("\n[+] Mock PLC Server terminated.")


if __name__ == "__main__":
    main()
