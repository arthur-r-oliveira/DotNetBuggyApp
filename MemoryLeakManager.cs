using System;
using System.Collections.Generic;

public static class MemoryLeakManager
{
    public static readonly List<byte[]> MemoryHog = new List<byte[]>();
    public static long TotalAllocatedBytes = 0;
    public static readonly object LockObject = new();
}
