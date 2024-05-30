using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;
using UniRx;
using ObservableExtensions = UniRx.ObservableExtensions;

namespace ClusterLab.Infrastructure.Utils
{
    public static class Loan
    {
        public static T WithLogsError<T>(Func<T> f)
        {
            try
            {
                return f();
            }
            catch (ThreadAbortException e)
            {
                Debug.LogWarning(e);
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public static void WithLogsError(Action f)
        {
            try
            {
                f();
            }
            catch (ThreadAbortException e)
            {
                Debug.LogWarning(e);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public static Action<T> WithLogsError1<T>(Action<T> f)
        {
            return (arg) =>
            {
                try
                {
                    f(arg);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    throw;
                }
            };
        }

        public static void RunOnMainthread(Action f)
        {
            ObservableExtensions.Subscribe(Observable.Start(() => f(), Scheduler.MainThread));
        }

        public static void RunOnMainthreadSynchronized(Action f)
        {
            Observable.Start(() => f(), Scheduler.MainThread).Wait();
        }

        public static T RunOnMainthreadSynchronized<T>(Func<T> f)
        {
            return Observable.Start(() => f(), Scheduler.MainThread).Wait();
        }

        public static T Time<T>(string message, Func<T> f)
        {
            var sw = new Stopwatch();
            sw.Start();
            var ret = f();
            sw.Stop();
            var ts = sw.Elapsed;
            Debug.Log($"[TIME:{ts.TotalMilliseconds:N}ms]{message}");
            return ret;
        }

        public static void Time(string message, Action f)
        {
            Time(message, () =>
            {
                f();
                return Unit.Default;
            });
        }
    }
}
