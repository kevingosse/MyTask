using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskLibrary
{
    public class MyTask
    {
        private readonly ManualResetEventSlim _mutex = new();
        private readonly ConcurrentQueue<MyTask> _continuations = new();
        private bool _isCompleted;

        internal MyTask(MyTaskScheduler scheduler = null)
        {
            Scheduler = scheduler;
        }

        public MyTaskScheduler Scheduler { get; }

        public bool IsCompleted
        {
            get => _isCompleted;
            internal set
            {
                _isCompleted = value;

                if (value)
                {
                    _mutex.Set();
                    InvokeContinuations();
                }
            }
        }

        public void Wait()
        {
            _mutex.Wait();
        }

        public MyTask ContinueWith(Action<MyTask> continuation, MyTaskScheduler scheduler = null)
        {
            var task = new MyTaskContinuation(continuation, this, scheduler);

            AddContinuation(task);

            return task;
        }

        public MyTask<T> ContinueWith<T>(Func<MyTask, T> continuation, MyTaskScheduler scheduler = null)
        {
            var task = new MyTaskContinuation<T>(continuation, this, scheduler);

            AddContinuation(task);

            return task;

        }

        internal virtual void Invoke()
        {
            throw new InvalidOperationException();
        }

        private void ScheduleAndStart()
        {
            Scheduler.QueueTask(this);
        }

        private protected void AddContinuation(MyTask continuation)
        {
            if (IsCompleted)
            {
                continuation.ScheduleAndStart();
                return;
            }

            _continuations.Enqueue(continuation);
        }

        private void InvokeContinuations()
        {
            if (_continuations.Count == 1)
            {
                _continuations.TryDequeue(out var continuation);

                if (!continuation.Scheduler.TryExecuteTaskInline(continuation))
                {
                    continuation.ScheduleAndStart();
                }

                return;
            }

            while (_continuations.TryDequeue(out MyTask continuation))
            {
                continuation.ScheduleAndStart();
            }
        }
    }

    [AsyncMethodBuilder(typeof(MyTaskBuilder<>))]
    public class MyTask<T> : MyTask
    {
        internal MyTask(MyTaskScheduler scheduler = null)
            : base(scheduler)
        { }


        public T Result { get; internal set; }

        public MyTask ContinueWith(Action<MyTask<T>> continuation, MyTaskScheduler scheduler = null)
        {
            var task = new MyTaskContinuation(f => continuation((MyTask<T>)f), this, scheduler);

            AddContinuation(task);

            return task;

        }

        public MyTask<TResult> ContinueWith<TResult>(Func<MyTask<T>, TResult> continuation, MyTaskScheduler scheduler = null)
        {
            var task = new MyTaskContinuation<TResult>(f => continuation((MyTask<T>)f), this, scheduler);

            AddContinuation(task);

            return task;
        }
    }

    public abstract class MyTaskScheduler
    {
        public static readonly MyTaskScheduler Default = new MyThreadPoolTaskScheduler(); 

        protected internal abstract void QueueTask(MyTask task);
        protected internal virtual bool TryExecuteTaskInline(MyTask task)
        {
            return false;
        }

        protected void ExecuteTask(MyTask task)
        {
            if (task.Scheduler != this)
            {
                throw new InvalidOperationException();
            }

            task.Invoke();
        }

    }

    public class MyThreadPoolTaskScheduler : MyTaskScheduler
    {
        protected internal override void QueueTask(MyTask task)
        {
            ThreadPool.QueueUserWorkItem(_ => ExecuteTask(task));
        }

        protected internal override bool TryExecuteTaskInline(MyTask task)
        {
            if (Thread.CurrentThread.IsThreadPoolThread)
            {
                ExecuteTask(task);
                return true;
            }

            return false;
        }
    }

    internal class MyTaskContinuation : MyTask
    {
        private MyTask _antecedent;
        private Action<MyTask> _action;

        public MyTaskContinuation(Action<MyTask> continuation, MyTask antecedent, MyTaskScheduler scheduler)
            : base(scheduler ?? MyTaskScheduler.Default)
        {
            _action = continuation;
            _antecedent = antecedent;
        }


        internal override void Invoke()
        {
            _action(_antecedent);
            IsCompleted = true;
        }
    }

    internal class MyTaskContinuation<T> : MyTask<T>
    {
        private MyTask _antecedent;
        private Func<MyTask, T> _action;

        public MyTaskContinuation(Func<MyTask, T> continuation, MyTask antecedent, MyTaskScheduler scheduler)
            : base(scheduler ?? MyTaskScheduler.Default)
        {
            _action = continuation;
            _antecedent = antecedent;
        }
        
        internal override void Invoke()
        {
            Result = _action(_antecedent);
            IsCompleted = true;
        }
    }

    public class MyTaskCompletionSource
    {
        public MyTaskCompletionSource()
        {
            Task = new();
        }

        public MyTask Task { get; }

        public void Complete()
        {
            Task.IsCompleted = true;
        }
    }

    public class MyTaskCompletionSource<T>
    {
        public MyTaskCompletionSource()
        {
            Task = new();
        }

        public MyTask<T> Task { get; }

        public void Complete(T result)
        {
            Task.Result = result;
            Task.IsCompleted = true;
        }

    }
}
