import logging
import time

from abc import ABC, abstractmethod
from queue import Empty, Queue
from threading import Thread


class QueueTask:

    def __init__(self, workload, destination):
        self.workload = workload
        self.destination = destination


class QueueWorkerABC(ABC):

    def __init__(self, input_queue):
        self.input_queue = input_queue
        self.should_shutdown = False

    def shutdown(self):
        self.should_shutdown = True

    def on_run(self):
        pass

    def on_shutdown(self):
        pass

    def process(self, workload):
        pass

    def run(self):
        self.on_run()
        while True:
            try:
                workload = self.input_queue.get(block=True, timeout=2.0)
            except Empty:
                pass
            else:
                result = self.process(workload.workload)
                workload.destination.put(result)
                self.input_queue.task_done()

            if self.should_shutdown:
                self.on_shutdown()
                return


def worker_runner(worker):
    worker.run()


class QueueExecutorABC(ABC):

    def __init__(self, name, *args):
        self.name = name
        self.input_queue = Queue()
        self.workers = self.create_workers(self.input_queue, *args)
        self.worker_threads = []

    def start(self):
        logging.info('Starting Executor: %s (%d workers)' % (self.name, len(self.workers)))
        self.worker_threads = [
            Thread(target=worker_runner, args=(worker,), daemon=True) for worker in self.workers]
        for i, worker_thread in enumerate(self.worker_threads):
            logging.info('Starting worker: %d' % i)
            worker_thread.start()
            time.sleep(0.5)

    def shutdown(self):
        for worker in self.workers:
            worker.shutdown()
        for i, worker_thread in enumerate(self.worker_threads):
            logging.info('Joining worker: %d' % i)
            worker_thread.join()

    def execute(self, workload, destination):
        self.input_queue.put(QueueTask(workload, destination))

    @abstractmethod
    def create_workers(self, *args):
        pass
