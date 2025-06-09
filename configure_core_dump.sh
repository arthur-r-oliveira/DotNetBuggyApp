#!/bin/sh
echo '/app/dumps/core.%e.%p' > /proc/sys/kernel/core_pattern
exec dotnet DotNetMemoryLeakApp.dll
