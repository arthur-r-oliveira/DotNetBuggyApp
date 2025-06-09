using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.IO; // Required for File.ReadAllText
using Markdig; // Required for Markdown.ToHtml
using Microsoft.AspNetCore.Http; // Required for StatusCodes

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Set the ASP.NET Core URL to listen on TCP port 8881
app.Urls.Add("http://+:8881");

app.UseHttpsRedirection();

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

app.Run();