
# Testing .NET Coredump Generation on an Edge Server

This document provides a prescriptive guide to build your .NET application, transfer it to an edge server, and test the automatic coredump generation.

## Part 1: Build the Application

First, we need to publish the .NET application in a self-contained format, so it can run on the edge server without needing the .NET SDK installed there.

The following command publishes the application for the `linux-x64` runtime, creating a `publish` directory inside `bin/Release/net8.0/linux-x64/`.

```bash
dotnet publish DotNetBuggyApp.sln -c Release -r linux-x64
```

Next, create a compressed archive of the `publish` directory. This will make it easier to transfer to the edge server.

```bash
tar -czvf dotnet-app.tar.gz -C bin/Release/net8.0/linux-x64/publish .
```

## Part 2: Transfer to Edge Server

1.  Use `scp` or a similar tool to copy the archive to your edge server. Replace `user` and `edge-server-ip` with your credentials.
    ```bash
    scp dotnet-app.tar.gz user@edge-server-ip:~/
    ```

## Part 3: Run and Test on Edge Server

1.  SSH into your edge server.
    ```bash
    ssh user@edge-server-ip
    ```

2.  Create a directory, and extract the application archive.
    ```bash
    mkdir dotnet-app && tar -xzvf dotnet-app.tar.gz -C dotnet-app
    ```

3.  Navigate into the application directory.
    ```bash
    cd dotnet-app
    ```

4.  Make the main application file executable.
    ```bash
    chmod +x DotNetMemoryLeakApp
    ```

5.  **Crucially, configure the .NET runtime to generate coredumps on a crash.** These environment variables instruct the runtime to create a full dump in the `/tmp` directory when an unhandled exception occurs.
    ```bash
    export COMPlus_DbgEnableElfDumpOnCrash=1
    export COMPlus_DbgCrashDumpType=3
    export COMPlus_DbgMiniDumpName=/tmp/coredump.%p
    ```

6.  Run the application. It will listen on port 8080 by default.
    ```bash
    ./DotNetMemoryLeakApp
    ```

7.  From a **second terminal** on the same edge server, use `curl` to send a request to the `/triggerMemoryLeak` endpoint. This will start the memory allocation process.
    ```bash
    curl http://localhost:8080/triggerMemoryLeak
    ```

8.  **Monitor and Verify.** Keep an eye on the output of the running application. It will log its increasing memory usage. Eventually, it will crash with an `OutOfMemoryException`.

    Once it crashes, check for the coredump file in the `/tmp` directory. You should see a file named something like `coredump.1234`, where `1234` is the process ID.
    ```bash
    ls -l /tmp/
    ```

This procedure will allow you to confirm that the .NET runtime can successfully generate a coredump on the bare metal OS of your edge server.
