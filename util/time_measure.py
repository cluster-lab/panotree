import time


class TimeMeasureSession:
    def __init__(self, identifier: str, mult: float = 1.0, unit: str = 'ms'):
        self._start_at = None
        self._stop_at = None
        self._depth = 0
        self._time_measurements = []
        self._counter = 0
        self.identifier = identifier
        self._mult = mult
        self._unit = unit
        self.depth = 0

    def start(self):
        self._stop_at = None
        self._start_at = time.time()

    def stop(self):
        self._stop_at = time.time()
        self._time_measurements.append(self.elapsed)
        self._time_measurements = self._time_measurements[-100:]

    @property
    def elapsed(self):
        if self._stop_at is None:
            return time.time() - self._start_at
        return self._stop_at - self._start_at

    @property
    def average(self):
        if len(self._time_measurements) == 0:
            return 0
        return sum(self._time_measurements) / len(self._time_measurements)

    def print_avg(self):
        space = "".join([" >"] * self.depth)
        print(f'[TIME]{space}[{self.average * self._mult:08.3f}{self._unit}]{self.identifier}', flush=True)

    def reset(self):
        self._time_measurements = []


class TimeMeasureScope:
    def __init__(self, tm: 'TimeMeasure', identifier: str):
        self.tm = tm
        self.identifier = identifier

    def __enter__(self):
        self.tm._start_session(self.identifier)
        return self

    def __exit__(self, exception_type, exception_value, traceback):
        self.tm._stop_session(self.identifier)



class TimeMeasure:
    __TM_GLOBAL = None
    def __init__(self, identifier: str = '', mult=1000.0, unit='ms'):
        self._identifier = identifier
        self._mult = mult
        self._unit = unit
        self._session_stack = []
        self.sessions = dict()

    def measure(self, identifier: str):
        if identifier in self._session_stack:
            raise RuntimeError("Identifier must be unique")
        if identifier not in self.sessions:
            self.sessions[identifier] = TimeMeasureSession(identifier, self._mult, self._unit)
        return TimeMeasureScope(self, identifier)

    def _start_session(self, identifier: str):
        session = self.sessions[identifier]
        session.depth = len(self._session_stack)
        session.start()
        self._session_stack.append(identifier)

    def _stop_session(self, identifier: str):
        self._session_stack.pop()
        self.sessions[identifier].stop()

    def print_avg(self):
        for s in self.sessions.values():
            s.print_avg()

    def reset_all_avg(self):
        for s in self.sessions.values():
            s.reset()

    @classmethod
    def default(cls):
        if cls.__TM_GLOBAL is None:
            cls.__TM_GLOBAL = TimeMeasure()
        return cls.__TM_GLOBAL

