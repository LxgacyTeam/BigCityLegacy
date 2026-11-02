using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

internal static class LegacyClientThread
{
    private static readonly ConcurrentQueue<Action> pending = new ConcurrentQueue<Action>();
    private static int thread;
    internal static void Initialize() { thread=Thread.CurrentThread.ManagedThreadId; }
    internal static void Post(Action action) { pending.Enqueue(action); }
    internal static Task<T> Invoke<T>(Func<T> action)
    {
        var result=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action run=()=> { try { result.TrySetResult(action()); } catch(Exception ex) { result.TrySetException(ex); } };
        if(Thread.CurrentThread.ManagedThreadId==thread) run();else Post(run);
        return result.Task;
    }
    internal static void Tick()
    { Action action;int budget=64;while(budget-->0&&pending.TryDequeue(out action)) try{action();}catch(Exception ex){Debug.LogError("[BCL API] "+ex);} }
}
