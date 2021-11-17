using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TaskLibrary
{
    public interface IAwaiter<T> : INotifyCompletion
    {
        T GetResult();
        bool IsCompleted { get; }
        void OnCompleted(Action action);
    }

    public struct MyTaskAwaiter<T> : IAwaiter<T>
    {
        private readonly MyTask<T> _task;
        private readonly bool _captureContext;

        public MyTaskAwaiter(MyTask<T> task, bool captureContext)
        {
            _task = task;
            _captureContext = captureContext;
        }


        public T GetResult() => _task.Result;

        public bool IsCompleted => _task.IsCompleted;

        public void OnCompleted(Action action)
        {
            SynchronizationContext currentSynchronizationContext = null;

            if (_captureContext)
            {
                currentSynchronizationContext = SynchronizationContext.Current;
            }

            _task.ContinueWith(_ =>
            {
                if (currentSynchronizationContext != null)
                {
                    currentSynchronizationContext.Post(_ => action(), null);
                }
                else
                {
                    action();
                }
            });
        }
    }

    public struct MyTaskBuilder<T>
    {
        private MyTask<T> _task;
        private IAsyncStateMachine _stateMachine;

        public MyTask<T> Task
        {
            get
            {
                if (_task == null) _task = new MyTask<T>();

                return _task;
            }
        }

        public static MyTaskBuilder<T> Create() => new();

        public void SetResult(T result)
        {
            Task.Result = result;
            Task.IsCompleted = true;
        }

        public void SetException(Exception ex)
        {
            throw new NotImplementedException();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            _stateMachine = stateMachine;
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : IAwaiter<T>
            where TStateMachine : IAsyncStateMachine
        {
            _ = Task;

            if (_stateMachine == null)
            {
                Console.WriteLine("Boxing");
                var boxedStateMachine = (IAsyncStateMachine)stateMachine;
                _stateMachine = boxedStateMachine;
                boxedStateMachine.SetStateMachine(boxedStateMachine);
            }

            awaiter.OnCompleted(_stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : IAwaiter<T>
            where TStateMachine : IAsyncStateMachine
        {
            AwaitOnCompleted(ref awaiter, ref stateMachine);
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }
    }

    public struct ConfiguredTaskAwaitable<T>
    {
        private readonly MyTask<T> _task;
        private readonly bool _captureContext;

        public ConfiguredTaskAwaitable(MyTask<T> task, bool captureContext)
        {
            _task = task;
            _captureContext = captureContext;
        }

        public MyTaskAwaiter<T> GetAwaiter()
        {
            return new MyTaskAwaiter<T>(_task, _captureContext);
        }
    }


    public static class MyTaskExtensions
    {
        public static MyTaskAwaiter<T> GetAwaiter<T>(this MyTask<T> task)
        {
            return new MyTaskAwaiter<T>(task, true);
        }

        public static ConfiguredTaskAwaitable<T> ConfigureAwait<T>(this MyTask<T> task, bool captureContext)
        {
            return new ConfiguredTaskAwaitable<T>(task, captureContext);
        }

    }

    public class Api
    {
        public static void DisplayCurrentSynchronizationContext()
        {
            Console.WriteLine(SynchronizationContext.Current?.ToString() ?? "No synchronization context");
        }

        public async MyTask<int> CallAsync()
        {
            // ...
            DisplayCurrentSynchronizationContext();
            int i = await DoSomethingAsync1();
            DisplayCurrentSynchronizationContext();

            int j = await DoSomethingAsync2().ConfigureAwait(false);
            DisplayCurrentSynchronizationContext();

            int k = await DoSomethingAsync3();
            DisplayCurrentSynchronizationContext();

            return await DoSomethingElseAsync(i + j + k);
        }

        struct StateMachine : IAsyncStateMachine
        {
            public Api This;

            public MyTaskBuilder<int> Builder;

            private int _state;

            private MyTaskAwaiter<int> _awaiter;

            private int _i;
            private int _j;
            private int _k;

            public void SetStateMachine(IAsyncStateMachine stateMachine)
            {
                Builder.SetStateMachine(stateMachine);
            }

            public void MoveNext()
            {
                switch (_state)
                {
                    case 0:
                    {
                        _state = 1;

                        _awaiter = This.DoSomethingAsync1().GetAwaiter();

                        if (_awaiter.IsCompleted)
                        {
                            goto case 1;
                        }

                        Builder.AwaitOnCompleted(ref _awaiter, ref this);

                        return;
                    }


                    case 1:
                    {
                        _state = 2;

                        _i = _awaiter.GetResult();

                        _awaiter = This.DoSomethingAsync2().GetAwaiter();

                        if (_awaiter.IsCompleted)
                        {
                            goto case 2;
                        }

                        Builder.AwaitOnCompleted(ref _awaiter, ref this);

                        return;
                    }

                    case 2:
                    {
                        _state = 3;

                        _j = _awaiter.GetResult();

                        _awaiter = This.DoSomethingAsync3().GetAwaiter();

                        if (_awaiter.IsCompleted)
                        {
                            goto case 3;
                        }

                        Builder.AwaitOnCompleted(ref _awaiter, ref this);

                        return;
                    }

                    case 3:
                    {
                        _state = 4;

                        _k = _awaiter.GetResult();

                        _awaiter = This.DoSomethingElseAsync(_i + _j + _k).GetAwaiter();

                        if (_awaiter.IsCompleted)
                        {
                            goto case 4;
                        }

                        Builder.AwaitOnCompleted(ref _awaiter, ref this);

                        return;
                    }

                    case 4:
                    {
                        Builder.SetResult(_awaiter.GetResult());
                        return;
                    }
                }
            }
        }


        private MyTask<int> DoSomethingAsync1()
        {
            return Delay(500).ContinueWith(_ => 1);
        }

        private MyTask<int> DoSomethingAsync2()
        {
            return Delay(500).ContinueWith(_ => 10);
        }

        private MyTask<int> DoSomethingAsync3()
        {
            return Delay(500).ContinueWith(_ => 100);
        }

        public MyTask<int> DoSomethingElseAsync(int input)
        {
            return Delay(500).ContinueWith(_ => input * 2);
        }

        public static MyTask Delay(int ms)
        {
            var tcs = new MyTaskCompletionSource();

            var thread = new Thread(() =>
            {
                Thread.Sleep(ms);
                tcs.Complete();
            });

            thread.Start();

            return tcs.Task;
        }
    }

}
