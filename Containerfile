# Stage 1: Build the application
FROM registry.redhat.io/rhel8/dotnet-80:8.0 AS build

WORKDIR /app

RUN chown -R 1001:0 /app && \
    mkdir -p /app/dumps && chmod -R 750 /app/dumps && chown -R 1001:0 /app/dumps # <--- KEEP THIS
    # REMOVE THIS LINE: && mkdir -p /app/dumps/tmp && chmod -R 750 /app/dumps/tmp && chown -R 1001:0 /app/dumps/tmp


# Switch to root to install global .NET tools
USER root

# Explicitly create the directory for global tools before installation
# Then, install .NET diagnostic tools globally into this directory.
RUN mkdir -p /app/tools && chown -R 1001:0 /app/tools && \
 dotnet tool install --tool-path /app/tools dotnet-trace \
 && dotnet tool install --tool-path /app/tools dotnet-counters \
 && dotnet tool install --tool-path /app/tools dotnet-dump \
 && dotnet tool install --tool-path /app/tools dotnet-gcdump
 
RUN chown -R 1001:0 /opt/app-root/.local/share/NuGet/
# Switch back to non-root user for building the application
USER 1001
# --- Build Application ---
COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/out --no-restore

# Stage 2: Create the final runtime image
FROM registry.redhat.io/rhel8/dotnet-80:8.0 AS final

WORKDIR /app

COPY --from=build /app/out .
COPY --from=build /app/README.md .
COPY --from=build /app/tools /app/tools

USER 1001

# Add the directory containing the tool executables to the PATH
ENV PATH="/app/tools:${PATH}"

# --- Environment Variables for Crash Dumps ---
ENV COMPlus_DbgEnableElfDumpOnCrash=1
ENV COMPlus_DbgCrashDumpType=3
ENV COMPlus_DbgMiniDumpName=/app/dumps/dump.dmp

# --- Application Config ---
ENV ASPNETCORE_URLS=http://+:8881
EXPOSE 8881

# --- Run the Application ---
ENTRYPOINT ["dotnet", "/app/DotNetMemoryLeakApp.dll"]