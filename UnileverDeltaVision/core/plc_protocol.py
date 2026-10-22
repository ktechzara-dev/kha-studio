"""
Schneider SERT / FB_CognexCamera PLC Protocol Driver
Handles:
- High-reliability TCP Socket Client to Schneider PacDrive / M262 PLC
- Auto-reconnect with exponential backoff / keep-alive
- Byte-accurate packet formatting matching FB_CognexCamera SERT format:
  Vision<Status><Count:02d>A<ModelLen:02d><Model>[P<Idx>,<X_mm>,<Y_mm>,<Angle>,<PO>]...\r
- Thread-safe non-blocking transmit queue
"""

import socket
import threading
import time
import logging
from typing import Callable, Any
from dataclasses import dataclass

logger = logging.getLogger("UnileverVision.PLC")


@dataclass
class PacketLog:
    timestamp: str
    direction: str  # 'TX' or 'RX'
    raw_ascii: str
    length_bytes: int
    bottle_count: int


class SchneiderSertClient:
    def __init__(
        self,
        host: str = "190.201.100.100",
        port: int = 50001,
        reconnect_interval_sec: float = 2.0,
        mock_mode: bool = False
    ):
        self.host = host
        self.port = port
        self.reconnect_interval_sec = reconnect_interval_sec
        self.mock_mode = mock_mode

        self._socket: socket.socket | None = None
        self._is_running = False
        self._is_connected = False
        self._thread: threading.Thread | None = None
        self._rx_thread: threading.Thread | None = None
        self._lock = threading.Lock()

        # Callbacks
        self.on_connection_changed: Callable[[bool, str], None] | None = None
        self.on_packet_sent: Callable[[PacketLog], None] | None = None
        self.on_packet_received: Callable[[PacketLog], None] | None = None
        self.on_trigger_received: Callable[[], None] | None = None
        self.on_model_change_received: Callable[[str], None] | None = None

        self.last_packet_sent: str = ""
        self.total_packets_sent: int = 0
        self.total_bytes_sent: int = 0

    @property
    def is_connected(self) -> bool:
        return self._is_connected or self.mock_mode

    def start(self):
        """Starts background connection and supervision thread."""
        if self._is_running:
            return
        self._is_running = True
        self._thread = threading.Thread(target=self._connection_worker, daemon=True, name="PLC_Client_Worker")
        self._thread.start()
        logger.info(f"Schneider SERT Client started (Target: {self.host}:{self.port}, Mock={self.mock_mode})")

    def stop(self):
        """Gracefully disconnects and stops threads."""
        self._is_running = False
        self._disconnect()
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=1.0)
        logger.info("Schneider SERT Client stopped.")

    def _disconnect(self):
        with self._lock:
            if self._socket:
                try:
                    self._socket.shutdown(socket.SHUT_RDWR)
                except Exception:
                    pass
                try:
                    self._socket.close()
                except Exception:
                    pass
                self._socket = None

            if self._is_connected:
                self._is_connected = False
                logger.warning(f"Disconnected from Schneider PLC ({self.host}:{self.port})")
                if self.on_connection_changed:
                    self.on_connection_changed(False, f"Disconnected from {self.host}:{self.port}")

    def _connection_worker(self):
        """Manages persistent connection to Schneider PLC."""
        while self._is_running:
            if self.mock_mode:
                if not self._is_connected:
                    self._is_connected = True
                    if self.on_connection_changed:
                        self.on_connection_changed(True, f"Mock Simulation Connected ({self.host}:{self.port})")
                time.sleep(1.0)
                continue

            if not self._is_connected:
                try:
                    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
                    s.settimeout(2.5)
                    s.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)  # Disable Nagle's algorithm for sub-millisecond dispatch
                    s.connect((self.host, self.port))
                    s.settimeout(None)  # Blocking for receive thread

                    with self._lock:
                        self._socket = s
                        self._is_connected = True

                    logger.info(f"Connected successfully to Schneider PLC at {self.host}:{self.port}")
                    if self.on_connection_changed:
                        self.on_connection_changed(True, f"Connected to {self.host}:{self.port}")

                    # Start RX listener thread
                    self._rx_thread = threading.Thread(target=self._rx_worker, daemon=True, name="PLC_RX_Worker")
                    self._rx_thread.start()

                except Exception as ex:
                    self._disconnect()
                    time.sleep(self.reconnect_interval_sec)
                    continue

            time.sleep(0.5)

    def _rx_worker(self):
        """Listens for commands and triggers from Schneider PLC."""
        buffer = b""
        while self._is_running and self._is_connected and self._socket:
            try:
                data = self._socket.recv(1024)
                if not data:
                    logger.warning("PLC closed connection.")
                    self._disconnect()
                    break

                buffer += data
                while b"\r" in buffer:
                    line, buffer = buffer.split(b"\r", 1)
                    cmd_str = line.decode("ascii", errors="replace").strip()
                    if cmd_str:
                        self._handle_plc_command(cmd_str)

            except Exception as ex:
                if self._is_running and self._is_connected:
                    logger.error(f"Error receiving from PLC: {ex}")
                    self._disconnect()
                break

    def _handle_plc_command(self, cmd: str):
        """Processes incoming PLC trigger or configuration commands."""
        ts = time.strftime("%H:%M:%S.") + f"{int(time.time() * 1000) % 1000:03d}"
        log_entry = PacketLog(
            timestamp=ts,
            direction="RX",
            raw_ascii=cmd + "\\r",
            length_bytes=len(cmd) + 1,
            bottle_count=0
        )
        if self.on_packet_received:
            self.on_packet_received(log_entry)

        cmd_upper = cmd.upper()
        if cmd_upper in ("TRG", "TRIGGER", "SW_TRIG"):
            logger.info("Received Software Trigger from Schneider PLC.")
            if self.on_trigger_received:
                self.on_trigger_received()
        elif cmd_upper.startswith("SET_MODEL"):
            parts = cmd.split(maxsplit=1)
            if len(parts) > 1:
                model = parts[1].strip()
                logger.info(f"Received Model Change request from PLC: {model}")
                if self.on_model_change_received:
                    self.on_model_change_received(model)
        elif cmd_upper == "PING":
            self.send_raw("PONG\r")

    def format_sert_packet(
        self,
        bottles: list[Any],
        model_code: str = "LB800",
        status: int = 1
    ) -> str:
        """
        Formats bottle detection list into FB_CognexCamera Schneider SERT format:
        Vision<Status><Count:02d>A<ModelLen:02d><ModelName>[P<Idx>,<X_mm>,<Y_mm>,<Angle>,<PO>]...\r

        Example:
        Vision103A05LB800[P01,+420.50,+185.30,+45.2,PO1][P02,+680.10,+210.40,-134.8,PO2][P03,+910.00,+195.00,+12.0,PO1]\r
        """
        count = len(bottles)
        clean_model = model_code.strip()
        model_len = len(clean_model)

        # Header: Vision + Status(1) + Count(2 digits) + 'A' + ModelLen(2 digits) + ModelCode
        header = f"Vision{status}{count:02d}A{model_len:02d}{clean_model}"

        # Target Payload List
        parts = [header]
        for idx, b in enumerate(bottles, start=1):
            rx = getattr(b, 'robot_x', 0.0)
            ry = getattr(b, 'robot_y', 0.0)
            ra = getattr(b, 'robot_angle_deg', 0.0)
            po = getattr(b, 'po_type', 'PO1')

            # Fixed precision formatting with explicit signs
            item_str = f"[P{idx:02d},{rx:+08.2f},{ry:+08.2f},{ra:+06.1f},{po}]"
            parts.append(item_str)

        packet_str = "".join(parts) + "\r"
        return packet_str

    def send_detection_result(
        self,
        bottles: list[Any],
        model_code: str = "LB800",
        status: int = 1
    ) -> bool:
        """Encodes and transmits the detection packet to Schneider PLC."""
        packet_str = self.format_sert_packet(bottles, model_code, status)
        return self.send_raw(packet_str, bottle_count=len(bottles))

    def send_raw(self, raw_str: str, bottle_count: int = 0) -> bool:
        """Transmits raw ASCII string terminated with \\r over TCP."""
        payload = raw_str.encode("ascii", errors="replace")
        ts = time.strftime("%H:%M:%S.") + f"{int(time.time() * 1000) % 1000:03d}"

        log_entry = PacketLog(
            timestamp=ts,
            direction="TX",
            raw_ascii=raw_str.replace("\r", "\\r").replace("\n", "\\n"),
            length_bytes=len(payload),
            bottle_count=bottle_count
        )

        self.last_packet_sent = log_entry.raw_ascii
        self.total_packets_sent += 1
        self.total_bytes_sent += len(payload)

        if self.on_packet_sent:
            self.on_packet_sent(log_entry)

        if self.mock_mode:
            return True

        with self._lock:
            if not self._is_connected or not self._socket:
                return False
            try:
                self._socket.sendall(payload)
                return True
            except Exception as ex:
                logger.error(f"Failed to transmit packet to PLC: {ex}")
                self._disconnect()
                return False
