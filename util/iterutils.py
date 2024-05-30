import ctypes
import multiprocessing as mp


def grouped(num, itr):
    if hasattr(itr, "__iter__") and not hasattr(itr, "__next__"):
        itr = iter(itr)
    while True:
        elem_list = []
        for _ in range(num):
            try:
                elem_list.append(next(itr))
            except StopIteration:
                if len(elem_list) > 0:
                    yield elem_list
                return
        yield elem_list


def _process(is_running: mp.Value, in_q: mp.Queue, out_q: mp.Queue, ignore_output, func):
    while is_running.value:
        try:
            item = in_q.get(timeout=5)
        except mp.queues.Empty:
            continue
        except TimeoutError:
            continue
        if item is None:
            break
        ret = func(*item)
        if ret is None or not is_running.value:
            break
        if not ignore_output:
            out_q.put(ret, timeout=10)


class MPPipeline:
    def __init__(self, func, num_workers=8, ignore_output=False):
        self.func = func
        self._in_q = mp.Queue()
        self._out_q = mp.Queue()
        self._is_running = mp.Value(ctypes.c_bool, True)
        self._ignore_output = ignore_output
        self._pool = mp.Pool(num_workers, initializer=_process, initargs=(self._is_running, self._in_q, self._out_q, ignore_output, func))

    def enqueue(self, item):
        self._in_q.put(item, block=False)

    def dequeue(self):
        return self._out_q.get()

    def shutdown(self):
        self._is_running.value = False
