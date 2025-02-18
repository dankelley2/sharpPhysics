using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace physics.Engine
{
    public class WorkerThread
    {
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private readonly Thread _thread;

        public WorkerThread()
        {
            _thread = new Thread(Run) { IsBackground = true };
            _thread.Start();
        }

        public void Enqueue(Action action)
        {
            _queue.Add(action);
        }

        public void CompleteAdding() => _queue.CompleteAdding();

        private void Run()
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                action();
            }
        }
    }

    public class WorkerThreadPool
    {
        private readonly WorkerThread[] _workers;
        private int _nextWorker = 0;
        private readonly object _lock = new object();

        public int Count => _workers.Length;

        public WorkerThreadPool(int workerCount)
        {
            _workers = new WorkerThread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                _workers[i] = new WorkerThread();
            }
        }

        /// <summary>
        /// Enqueues a work item to one of the worker threads in round-robin fashion.
        /// </summary>
        public void Enqueue(Action action)
        {
            lock (_lock)
            {
                _workers[_nextWorker].Enqueue(action);
                _nextWorker = (_nextWorker + 1) % _workers.Length;
            }
        }

        /// <summary>
        /// Optionally, call CompleteAdding on all workers when shutting down.
        /// </summary>
        public void Shutdown()
        {
            foreach (var worker in _workers)
            {
                worker.CompleteAdding();
            }
        }
    }
}