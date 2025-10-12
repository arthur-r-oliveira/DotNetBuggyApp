# Stage 1: Build the application
FROM registry.access.redhat.com/ubi9/dotnet-80:8.0 AS build

WORKDIR /app

# Switch to root to install global .NET tools for debug image
USER root

# Install .NET diagnostic tools for debug scenarios only
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
RUN dotnet publish DotNetMemoryLeakApp.csproj -c Release -o /app/out --no-restore

# Stage 2: Create the final runtime image (production - no debug tools)
FROM registry.access.redhat.com/ubi9/dotnet-80-runtime:8.0 AS final

WORKDIR /app

# Copy only the application binaries and README
COPY --from=build /app/out .
COPY --from=build /app/README.md .

# Create dumps directory with proper permissions for OpenShift arbitrary UID
USER root
RUN mkdir -p /app/dumps && \
    chgrp -R 0 /app && \
    chmod -R g+rwX /app && \
    chmod -R 750 /app/dumps

# Switch to non-root user (OpenShift will override with arbitrary UID)
USER 1001

# --- Environment Variables for Crash Dumps ---
ENV COMPlus_DbgEnableElfDumpOnCrash=1
ENV COMPlus_DbgCrashDumpType=3
ENV COMPlus_DbgMiniDumpName=/app/dumps/dump.dmp

# --- Application Config ---
ENV ASPNETCORE_URLS=http://+:8881
EXPOSE 8881

# --- Run the Application ---
ENTRYPOINT ["dotnet", "/app/DotNetMemoryLeakApp.dll"]
