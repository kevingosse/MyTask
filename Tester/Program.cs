using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaskLibrary;

namespace Tester
{
    class Program
    {

        static void Main(string[] args)
        {
            var sc = new SingleThreadedSynchronizationContext();

            sc.Post(_ => new Api().CallAsync(), null);


            Console.WriteLine("Done");
            Console.ReadLine();
        }

        public static MyTask<int> AsyncMethod()
        {
            var tcs = new MyTaskCompletionSource<int>();

            var thread = new Thread(() =>
            {
                Thread.Sleep(100);
                tcs.Complete(42);
            });

            thread.Start();

            return tcs.Task;
        }
    }
}
