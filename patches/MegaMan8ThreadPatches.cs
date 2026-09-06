using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;

namespace MegaMan8Recomp.Patches;

internal static class MegaMan8ThreadPatches
{
    private const uint HandleBase = 0xFF000000u;
    private const int ThreadCount = 4;

    private sealed class ThreadControl
    {
        public required int Slot { get; init; }
        public required CpuContext Context { get; init; }
        public required AutoResetEvent RunGate { get; init; }
        public Thread? HostThread { get; set; }
        public ExceptionDispatchInfo? Failure { get; set; }
        public volatile bool Closed;
        public volatile bool YieldedToRoot;
    }

    private sealed class RootRequest
    {
        public required Action Action { get; init; }
        public AutoResetEvent Completed { get; } = new(false);
        public ExceptionDispatchInfo? Failure { get; set; }
    }

    private sealed class CooperativeThreadExit : Exception;

    private static readonly object Sync = new();
    private static readonly ThreadControl?[] Threads = new ThreadControl[ThreadCount];
    private static readonly ConcurrentQueue<RootRequest> RootRequests = new();

    [ThreadStatic]
    private static int _currentSlot;

    [ThreadStatic]
    private static bool _slotAssigned;

    private static IMemory? _memory;

    public static void VSync(CpuContext c, IMemory m)
    {
        if (CurrentSlot == 0)
        {
            PresentFrameWithThrottledCd(c, m);
            return;
        }

        InvokeOnRoot(() =>
        {
            // PresentFrame advances CD callbacks through Runtime.Cpu. While the
            // worker is waiting, make its emulated register set the active one;
            // otherwise callbacks corrupt the scheduler's root CpuContext.
            var rootContext = RecompOne.Runtime.Runtime.Cpu;
            RecompOne.Runtime.Runtime.SetContext(c, m);
            try
            {
                PresentFrameWithThrottledCd(c, m);
            }
            finally
            {
                if (rootContext is not null)
                    RecompOne.Runtime.Runtime.SetContext(rootContext, m);
            }
        });
    }

    private static void PresentFrameWithThrottledCd(CpuContext c, IMemory m)
    {
        // Mega Man 8 calls VSync once per visible update. Couple IRQ0 to that
        // presentation so its frame callback runs once, at a stable point,
        // instead of also being raised by the runtime's polling clock.
        RecompOne.Runtime.Interrupts.PresentDrivenVBlank = true;

        // LibCd.Tick normally drains as many as 400,000 ready callbacks in one
        // frame. MM8 has a bounded producer/consumer queue: once it fills, its
        // task must run before another sector arrives. Temporarily detach the
        // ready callback while PresentFrame ticks, then deliver exactly one
        // sector after the frame has been presented.
        var snapshot = c.Snapshot();
        c.A0 = 0;
        LibCd.CdReadyCallback(c, m);
        var readyCallback = c.V0;
        c.Restore(snapshot);

        try
        {
            LibEtc.VSync(c, m);
        }
        finally
        {
            snapshot = c.Snapshot();
            c.A0 = readyCallback;
            LibCd.CdReadyCallback(c, m);
            c.Restore(snapshot);
        }

        if (readyCallback == 0) return;

        snapshot = c.Snapshot();
        c.A0 = 1;
        c.A1 = 0;
        LibCd.CdReady(c, m);
        c.Restore(snapshot);
    }

    public static void OpenThread(CpuContext c, IMemory m)
    {
        EnsureRoot(c, m);

        var entry = c.A0;
        var stack = c.A1;
        var globalPointer = c.A2;
        ThreadControl? control = null;

        lock (Sync)
        {
            for (var slot = 1; slot < ThreadCount; slot++)
            {
                if (Threads[slot] is { Closed: false }) continue;

                var context = new CpuContext
                {
                    GP = globalPointer,
                    SP = stack,
                    FP = stack,
                    RA = 0,
                    SR = c.SR
                };

                control = new ThreadControl
                {
                    Slot = slot,
                    Context = context,
                    RunGate = new AutoResetEvent(false)
                };
                Threads[slot] = control;
                break;
            }
        }

        if (control is null)
        {
            c.V0 = 0xFFFFFFFFu;
            return;
        }

        var created = control;
        created.HostThread = new Thread(() => RunThread(created, m, entry))
        {
            IsBackground = true,
            Name = $"MM8 PS1 thread {created.Slot}"
        };
        created.HostThread.Start();
        c.V0 = HandleBase | (uint)created.Slot;
        Console.WriteLine(
            $"[MM8.Tasks] OpenTh handle=0x{c.V0:X8} entry=0x{entry:X8} sp=0x{stack:X8} gp=0x{globalPointer:X8}");
    }

    public static void CloseThread(CpuContext c, IMemory m)
    {
        EnsureRoot(c, m);
        var slot = SlotFromHandle(c.A0);
        ThreadControl? control;

        lock (Sync)
        {
            control = slot > 0 && slot < ThreadCount ? Threads[slot] : null;
            if (control is not null) control.Closed = true;
        }

        if (control is not null && slot != CurrentSlot)
        {
            control.RunGate.Set();
            control.HostThread?.Join(TimeSpan.FromSeconds(1));
        }

        c.V0 = 1;
    }

    public static void ChangeThread(CpuContext c, IMemory m)
    {
        EnsureRoot(c, m);
        var source = CurrentControl;
        var targetSlot = SlotFromHandle(c.A0);
        ThreadControl? target;

        lock (Sync)
            target = targetSlot >= 0 && targetSlot < ThreadCount ? Threads[targetSlot] : null;

        if (target is null || target.Closed)
        {
            c.V0 = 0;
            return;
        }

        if (CurrentSlot == 0)
            target.YieldedToRoot = false;
        else if (targetSlot == 0)
            source.YieldedToRoot = true;

        target.RunGate.Set();

        if (source.Closed)
            throw new CooperativeThreadExit();

        if (CurrentSlot == 0)
        {
            do
            {
                source.RunGate.WaitOne();
                PumpRootRequests();
            }
            while (!target.YieldedToRoot && !target.Closed);
        }
        else
        {
            source.RunGate.WaitOne();
        }

        if (source.Closed)
            throw new CooperativeThreadExit();

        target.Failure?.Throw();
        c.V0 = 1;
    }

    private static int CurrentSlot => _slotAssigned ? _currentSlot : 0;

    private static ThreadControl CurrentControl
    {
        get
        {
            lock (Sync)
                return Threads[CurrentSlot]
                    ?? throw new InvalidOperationException("MM8 cooperative thread is not registered");
        }
    }

    private static void EnsureRoot(CpuContext c, IMemory m)
    {
        lock (Sync)
        {
            if (!ReferenceEquals(_memory, m))
            {
                for (var i = 1; i < Threads.Length; i++)
                {
                    if (Threads[i] is not { } old) continue;
                    old.Closed = true;
                    old.RunGate.Set();
                    Threads[i] = null;
                }

                Threads[0] = new ThreadControl
                {
                    Slot = 0,
                    Context = c,
                    RunGate = new AutoResetEvent(false)
                };
                _memory = m;
            }
            else if (Threads[0] is null)
            {
                Threads[0] = new ThreadControl
                {
                    Slot = 0,
                    Context = c,
                    RunGate = new AutoResetEvent(false)
                };
            }
        }

        if (!_slotAssigned)
        {
            _currentSlot = 0;
            _slotAssigned = true;
        }
    }

    private static void RunThread(ThreadControl control, IMemory memory, uint entry)
    {
        _currentSlot = control.Slot;
        _slotAssigned = true;

        try
        {
            control.RunGate.WaitOne();
            if (control.Closed) return;
            Dispatcher.Call(control.Context, memory, entry);
        }
        catch (CooperativeThreadExit)
        {
            // Expected when the emulated task closes or replaces itself.
        }
        catch (Exception ex)
        {
            control.Failure = ExceptionDispatchInfo.Capture(ex);
            Console.Error.WriteLine(
                $"[MM8.Tasks] thread 0x{HandleBase | (uint)control.Slot:X8} failed: {ex}");
        }
        finally
        {
            control.Closed = true;
            lock (Sync)
            {
                if (ReferenceEquals(Threads[control.Slot], control))
                    Threads[control.Slot] = null;
            }

            lock (Sync)
                Threads[0]?.RunGate.Set();
        }
    }

    private static void InvokeOnRoot(Action action)
    {
        var request = new RootRequest { Action = action };
        RootRequests.Enqueue(request);

        lock (Sync)
            Threads[0]?.RunGate.Set();

        request.Completed.WaitOne();
        request.Failure?.Throw();
    }

    private static void PumpRootRequests()
    {
        while (RootRequests.TryDequeue(out var request))
        {
            try
            {
                request.Action();
            }
            catch (Exception ex)
            {
                request.Failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                request.Completed.Set();
            }
        }
    }

    private static int SlotFromHandle(uint handle)
    {
        return (handle & 0xFF000000u) == HandleBase ? (int)(handle & 0xFFu) : -1;
    }
}
