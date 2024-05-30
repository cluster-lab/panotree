using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

namespace ClusterLab.Infrastructure.Utils
{
    public class NoSuchElementException : Exception
    {
        public NoSuchElementException() { }
        public NoSuchElementException(string message) : base(message) { }
        public NoSuchElementException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class LinqUtils
    {
        public static IEnumerator YieldWithThrottling(double yieldTimeoutMillis, Func<bool> f)
        {
            var lastYieldAt = DateTime.Now;
            while (f())
            {
                var timeElapsed = (DateTime.Now - lastYieldAt).TotalMilliseconds;
                if (yieldTimeoutMillis < timeElapsed)
                {
                    yield return null;
                    lastYieldAt = DateTime.Now;
                }
            }
        }

        public static bool NotNullAndTrue(this bool? b)
        {
            return b.HasValue && b.Value;
        }

        public static void ForEach<T>(this IEnumerable<T> ie, Action<T> f)
        {
            foreach (var o in ie)
                f(o);
        }


        public static T MaxOf<T>(this IEnumerable<T> en, Func<T, float> conv)
        {
            T maxObj = en.First();
            float maxValue = float.MinValue;
            foreach (var v in en)
            {
                var newValue = conv(v);
                if (newValue > maxValue)
                {
                    maxValue = newValue;
                    maxObj = v;
                }
            }

            return maxObj;
        }

        public static int IndexOf<T>(this IEnumerable<T> en, Func<T, bool> cond)
        {
            var i = 0;
            foreach (var elem in en)
            {
                if (cond(elem))
                    return i;
                i++;
            }

            return -1;
        }

        public static IEnumerable<T> Continually<T>(Func<T> f)
        {
            while (true)
            {
                T ret;
                try
                {
                    ret = f();
                }
                catch (NoSuchElementException)
                {
                    yield break;
                }

                // for nullable
                if (ret == null)
                    yield break;
                yield return ret;
            }
        }

        public static ISet<T> ToSet<T>(this IEnumerable<T> en)
        {
            var hashSet = new HashSet<T>();
            foreach (var x in en)
            {
                hashSet.Add(x);
            }

            return hashSet;
        }

        public static T Tap<T>(this T obj, Action<T> f)
        {
            f(obj);
            return obj;
        }

        public static T FirstOr<T>(this IEnumerable<T> source, T alternate)
        {
            foreach (T t in source)
                return t;
            return alternate;
        }

        public static T FirstOr<T>(this IEnumerable<T> source, Func<T, bool> predicate, T alternate)
        {
            foreach (T t in source)
                if (predicate(t))
                    return t;
            return alternate;
        }

        public static void ZipForeach<A, B>(this IEnumerable<A> source, IEnumerable<B> dest, Action<A, B> func)
        {
            source.Zip(dest, (a, b) =>
            {
                func(a, b);
                return Unit.Default;
            }).ToList();
        }

        //本当はこう書きたいが、書き方がわからない。高階型使えない？
        //public static IEnumerable<T> Flatten<TList<?>, T>(this IEnumerable<TList<T>> li: where TList : IList<T>
        public static IEnumerable<T> Flatten<T>(this IEnumerable<List<T>> li)
        {
            return li.SelectMany(l => l);
        }

        public static IObservable<TResult> Flatten<TResult>(this IObservable<IEnumerable<TResult>> source)
        {
            return source.SelectMany(x => x);
        }

        public static bool IsNotEmptyOrNull<T>(this IList<T> li)
        {
            return li != null && li.Any();
        }

        public static IEnumerable<float> Elements(this Vector3 v)
        {
            return new[] { v.x, v.y, v.z };
        }

        public static IEnumerable<Tuple<int, T>> ZipWithIndex<T>(this IEnumerable<T> ie)
        {
            int i = 0;
            foreach (var o in ie)
                yield return Tuple.Create(i++, o);
        }
    }
}
