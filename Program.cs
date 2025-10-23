using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO; // Required for File.ReadAllText
using Markdig; // Required for Markdown.ToHtml
using Microsoft.AspNetCore.Http; // Required for StatusCodes
using System.Runtime.InteropServices; // Required for DllImport
using System.Runtime; // Required for RuntimeInformation

// --- Host-level Coredump Configuration ---
// This section is crucial for enabling automatic crash dumps in hardened, host-level capture environments.
// It explicitly marks the process as "dumpable," which is often disabled by default in secure Linux environments.
if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    // Import the prctl function from the standard C library (libc)
    [DllImport("libc", SetLastError = true)]
    static extern int prctl(int option, ulong arg2, ulong arg3, ulong arg4, ulong arg5);

    // Define the constant for PR_SET_DUMPABLE. This tells the kernel that this process is allowed to produce a coredump.
    const int PR_SET_DUMPABLE = 4;

    // Set the process to be dumpable. A value of 1 means 'true'.
    prctl(PR_SET_DUMPABLE, 1, 0, 0, 0);

    // Set ulimit for coredumps programmatically
    try
    {
        // Set ulimit -c unlimited for coredump generation
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c \"ulimit -c unlimited\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
    }
    catch
    {
        // If shell is not available, continue without setting ulimit
        // The environment variable ULIMIT_CORE=unlimited should still work
    }
}
// --- End of Coredump Configuration ---

// --- Create Dumps Directory ---
try
{
    Directory.CreateDirectory("/app/dumps");
}
catch
{
    // If directory creation fails, continue - the volume mount should handle this
}
// --- End of Directory Creation ---

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Set the ASP.NET Core URL to listen on TCP port 8881
app.Urls.Add("http://+:8881");

// Configure health checks
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

// Add a simple root endpoint for basic connectivity
app.MapGet("/", () => "DotNet Memory Leak App is running!");

// Disable HTTPS redirection for MicroShift/OpenShift (TLS handled by Route)
// app.UseHttpsRedirection();

// New endpoint to render README.md as a webpage
app.MapGet("/readme", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    string readmePath = Path.Combine(AppContext.BaseDirectory, "README.md"); // Get path to README.md

    if (!File.Exists(readmePath))
    {
        logger.LogError($"README.md not found at: {readmePath}");
        return Results.NotFound("README.md file not found.");
    }

    try
    {
        string markdownContent = await File.ReadAllTextAsync(readmePath);
        // Configure Markdig for better HTML output (optional)
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        string htmlContent = Markdown.ToHtml(markdownContent, pipeline);

        return Results.Content(htmlContent, "text/html");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error reading or rendering README.md.");
        // FIX: Changed Results.StatusCode to Results.Problem to include a message
        return Results.Problem(
            detail: "An error occurred while rendering the README.md. Please check server logs.",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
})
.WithName("RenderReadme")
.WithOpenApi();


app.MapGet("/triggerMemoryLeak", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Memory leak trigger initiated!");

    try
    {
        long currentAllocationRoundBytes = 0;
        const int chunkSize = 1 * 1024 * 1024;

        while (true)
        {
            byte[] chunk = new byte[chunkSize];

            for (int i = 0; i < chunkSize; i++)
            {
                chunk[i] = (byte)(i % 256);
            }

            lock (MemoryLeakManager.LockObject)
            {
                MemoryLeakManager.MemoryHog.Add(chunk);
                MemoryLeakManager.TotalAllocatedBytes += chunkSize;
                currentAllocationRoundBytes += chunkSize;
            }

            logger.LogInformation($"Allocated {currentAllocationRoundBytes / (1024.0 * 1024.0):F2} MB this round, Total: {MemoryLeakManager.TotalAllocatedBytes / (1024.0 * 1024.0):F2} MB");

            await Task.Delay(10);
        }
    }
    catch (OutOfMemoryException ex)
    {
        logger.LogError(ex, "OutOfMemoryException caught! Application is likely to crash soon.");
        throw;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An unexpected error occurred during memory allocation.");
        throw;
    }

    return Results.Ok($"Started memory leak. Total allocated: {MemoryLeakManager.TotalAllocatedBytes / (1024.0 * 1024.0):F2} MB (This line will likely not be reached)");
})
.WithName("TriggerMemoryLeak")
.WithOpenApi();

// New endpoint to simulate a segmentation fault
app.MapGet("/crash", () =>
{
    unsafe
    {
        // Attempt to write to an invalid memory address
        // This will cause a segmentation fault
        int* ptr = (int*)0x1; // An invalid memory address
        *ptr = 123; // Dereference and assign a value, causing a crash
    }
    return Results.Ok("Attempting to crash the application..."); // This line will likely not be reached
})
.WithName("Crash")
.WithOpenApi();

app.Run();