using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskLibrary
{
    public class SingleThreadedSynchronizationContext : SynchronizationContext
    {
        private Thread _thread;

        private BlockingCollection<Action> _queue = new BlockingCollection<Action>();

        public SingleThreadedSynchronizationContext()
        {
            _thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(this);

                foreach (var callback in _queue.GetConsumingEnumerable())
                {
                    callback();
                }
            });

            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            _queue.Add(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            using (var mutex = new ManualResetEventSlim())
            {
                _queue.Add(() =>
                {
                    d(state);
                    mutex.Set();
                });

                mutex.Wait();
            }
        }
    }
}
