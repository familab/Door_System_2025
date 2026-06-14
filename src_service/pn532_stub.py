"""PN532 stub for development when hardware libraries are unavailable.
"""
import time

class PN532Stub:
    """Minimal PN532-like interface used by the application."""

    # Shadow the power_down method so the stub matches the hardened hardware instance
    # (where _init_pn532 sets reader.power_down = False to prevent accidental sleep calls).
    power_down = False

    def __init__(self, *args, **kwargs):
        self._last_activity = time.time()

    def SAM_configuration(self) -> None:
        return

    def read_passive_target(self, timeout: float = 0.1):
        time.sleep(timeout)
        return None
