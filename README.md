# DotNet Memory Leak App

## Table of Contents

- [DotNet Memory Leak App](#dotnet-memory-leak-app)
  - [1. Repository Overview](#1-repository-overview)
  - [2. Key Features](#2-key-features)
  - [3. Motivation](#3-motivation)
  - [4. Deployment Guide (OpenShift / MicroShift)](#4-deployment-guide-openshift--microshift)
    - [4.1. Prerequisites](#41-prerequisites)
    - [4.2. Building & Pushing the Container Image](#42-building--pushing-the-container-image)
    - [4.3. Deployment to OpenShift / MicroShift](#43-deployment-to-openshift--microshift)
  - [5. Usage & Diagnostic Workflow](#5-usage--diagnostic-workflow)
    - [5.1. Triggering a Memory Leak](#51-triggering-a-memory-leak)
    - [5.2. Monitoring Application Logs](#52-monitoring-application-logs)
    - [5.3. Collecting Crash Dumps](#53-collecting-crash-dumps)
      - [5.3.1. Option A: On-Demand Dumps via Tools Embedded in Application Image (UNSECURE)](#531-option-a-on-demand-dumps-via-tools-embedded-in-application-image-unsecure)
      - [5.3.2. Option B: Automatic OOM Dumps](#532-option-b-automatic-oom-dumps)
      - [5.3.3. Option C: On-Demand Sidecar via Deployment Patching](#533-option-c-on-demand-sidecar-via-deployment-patching)
      - [5.3.4. Option D: On-Demand Dumps via Ephemeral Debug Container (kubectl debug)](#534-option-d-on-demand-dumps-via-ephemeral-debug-container-kubectl-debug)
        - [5.3.4.1  Example Commands:](#5341--example-commands)
        - [5.3.4.2  The Mystery of the Missing /app/dumps Directory](#5342--the-mystery-of-the-missing-appdumps-directory)
        - [5.3.4.3  Why `kubectl debug` is used instead of `oc debug`](#5343--why-kubectl-debug-is-used-instead-of-oc-debug)
        - [5.3.4.4  `kubectl` vs. `oc`: A Quick Comparison](#5344--kubectl-vs-oc-a-quick-comparison)
      - [5.3.5. Option E: Secure On-Demand Dumps via Shell-less Ephemeral Container](#535-option-e-secure-on-demand-dumps-via-shell-less-ephemeral-container)
      - [5.3.6. Option F: Deploying with a Hardened Security Context](#536-option-f-deploying-with-a-hardened-security-context)
    - [5.4. Limits & LimitaRanges](#54-limits--limitaranges)
  - [6. Security & Troubleshooting Considerations](#6-security--troubleshooting-considerations)
    - [6.1. Pod Security](#61-pod-security)
    - [6.2. SYS_PTRACE Capability](#62-sys_ptrace-capability)
    - [6.3. seccompProfile](#63-seccompprofile)
    - [6.4. TMPDIR and IPC Issues](#64-tmpdir-and-ipc-issues)
    - [6.5. Resource Limits and OOM Killer Race Conditions](#65-resource-limits-and-oom-killer-race-conditions)
  - [7. External References & Further Reading](#7-external-references--further-reading)
  - [8. Contributing](#8-contributing)
  - [9. License](#9-license)
---

## 1. Repository Overview

This repository does contains a `.NET` web service specifically engineered to **simulate, monitor, and analyze memory leaks** within a containerized environment. Designed for deployment on platforms like **OpenShift** and **MicroShift**, it provides a robust framework for understanding `.NET` memory management in cloud-native settings. The application is configured to automatically generate crash dumps upon encountering Out-Of-Memory (OOM) conditions, facilitating in-depth post-mortem analysis.

## 2. Key Features

* **Controlled Memory Leak Simulation**: Provides an endpoint to trigger a continuous memory allocation pattern.
* **Automated Crash Dump Generation**: Configured to automatically create `.NET` crash dumps (`ELF` format) when the application experiences an OOM or other critical failures.
* **On-Demand Diagnostic Tooling**: Integrates `.NET` CLI diagnostic tools (`dotnet-dump`, `dotnet-trace`, `dotnet-counters`, `dotnet-gcdump`) directly into the application image and/or a dedicated sidecar container for live analysis.
* **Containerized Environment Focus**: Demonstrates best practices for deploying `.NET` applications in OpenShift/MicroShift, including security configurations, resource management via LimitRanges, and ServiceAccount/SCC usage.
* **Air-Gapped Deployment Support**: Includes instructions and examples for deploying in environments without direct internet access to container registries.

## 3. Motivation

Effective memory management is paramount for application stability and performance. This repository aims to:

* **Provide a controlled, reproducible environment** to observe and understand `.NET` memory leak behavior in containers.
* **Enable practical crash dump analysis** for debugging `.NET` applications in cloud-native environments, aligning with Microsoft's diagnostic recommendations for `.NET` apps on Containers.
* **Showcase robust deployment strategies** for `.NET` applications on OpenShift/MicroShift, emphasizing security and operational best practices.
* **Facilitate debugging in restricted environments**, including air-gapped scenarios, by demonstrating image loading and offline tooling.

## 4. Deployment Guide (OpenShift / MicroShift)

This guide walks you through deploying the `DotNet Memory Leak App` to an OpenShift or MicroShift cluster.

### 4.1. Prerequisites

Before proceeding, ensure you have:

* **MicroShift** installed and running, or access to an OpenShift cluster.
* `kubectl` or `oc` CLI configured and connected to your cluster.
* A container registry (e.g., Quay.io, or local Podman storage) available for pushing images.
* **Cluster Administrator Privileges (Potentially Required)**: Depending on your cluster's security policies, you might need administrator assistance to modify Pod Security Enforcement levels or bind your ServiceAccount to a more permissive `SecurityContextConstraints` (SCC) like `privileged`, especially if you intend to use interactive debugging tools.

### 4.2. Building & Pushing the Container Image

In an **internet-connected environment**, build and push the container image to your chosen registry:

~~~
# Navigate to the project root directory
cd DotNetBuggyApp-main

# Build the image using the optimized Containerfile-new.dockerfile
# (Ensure your Containerfile-new.dockerfile includes the necessary tool installations and TMPDIR setup as discussed)
podman build -t quay.io/your-namespace/dotnet-memory-leak-app:v1 -f Containerfile-new.dockerfile .

# Push the image to your container registry
podman push quay.io/your-namespace/dotnet-memory-leak-app:v1
~~~

For **air-gapped deployments**, save the image as a tarball:

~~~
podman save -o dotnet-memory-leak-app.tar quay.io/your-namespace/dotnet-memory-leak-app:v1
Transfer dotnet-memory-leak-app.tar to your air-gapped environment's worker nodes.
~~~

### 4.3. Deployment to OpenShift / MicroShift
Navigate to the kubernetes directory within your project:

~~~
cd DotNetBuggyApp-main/kubernetes
~~~

Load the saved image (Air-Gapped Only):
In an air-gapped system, load the image into the local Podman storage on your worker nodes:

~~~
sudo podman load -i dotnet-memory-leak-app.tar
sudo podman images # Confirm the image is available
~~~

(If not air-gapped, skip this step. Kubernetes will pull the image from the registry.)

Apply the Kubernetes manifests using kustomize. This will create the namespace, service account, roles, PVC, deployment, service, and route.

~~~
oc apply -k .
Expected Output (May vary, note warnings):
You might see warnings about PodSecurity policies (e.g., "would violate PodSecurity "restricted:latest"") if your cluster has strict default policies. These warnings indicate that certain requested capabilities (like SYS_PTRACE) or security contexts might be blocked. Despite warnings, the core objects should be created.

namespace/dotnet-memory-leak-app created
serviceaccount/dotnet-app-sa created
role.rbac.authorization.k8s.io/dotnet-app-role created
clusterrole.rbac.authorization.k8s.io/dotnet-app-clusterrole created
rolebinding.rbac.authorization.k8s.io/dotnet-app-rolebinding created
clusterrolebinding.rbac.authorization.k8s.io/dotnet-app-clusterrolebinding created
service/dotnet-memory-leak-service created
limitrange/dotnet-limitrange created
persistentvolumeclaim/dotnet-memory-leak-dumps created
Warning: would violate PodSecurity "restricted:latest": unrestricted capabilities (container "diagnostic-tools-sidecar" must set securityContext.capabilities.drop=["ALL"]; container "diagnostic-tools-sidecar" must not include "SYS_PTRACE" in securityContext.capabilities.add)
deployment.apps/dotnet-memory-leak-app created
route.route.openshift.io/dotnet-memory-leak-route created
securitycontextconstraints.security.openshift.io/dotnet-scc created
~~~

## 5. Usage & Diagnostic Workflow
This section outlines how to use the application and leverage its diagnostic capabilities.

### 5.1. Triggering a Memory Leak
To initiate the memory leak, access the /triggerMemoryLeak endpoint of your deployed application via its OpenShift Route.

~~~
# Get the route hostname
export ROUTE_HOST=$(oc get route dotnet-memory-leak-route -n dotnet-memory-leak-app -o jsonpath='{.spec.host}')

# Trigger the memory leak
curl http://$ROUTE_HOST/triggerMemoryLeak
~~~

The application will begin allocating memory in 1MB chunks, logging its progress.

### 5.2. Monitoring Application Logs
As the memory leak progresses, you can observe the application's logs, which will show memory allocation updates and eventually OutOfMemoryException messages.

~~~
# Get the pod name
export POD_NAME=$(oc get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')

# Stream application logs
oc logs "$POD_NAME" -n dotnet-memory-leak-app -f
# You will see output similar to:

info: Program[0]
      Allocated 1516.00 MB this round, Total: 1516.00 MB
fail: Program[0]
      OutOfMemoryException caught! Application is likely to crash soon.
      System.OutOfMemoryException: Exception of type 'System.OutOfMemoryException' was thrown.
         at Program.<>c.<<<Main>$>b__0_0>d.MoveNext() in /app/Program.cs:line 37
~~~

### 5.3. Collecting Crash Dumps
The project offers multiple strategies for collecting crash dumps, suitable for different debugging scenarios and cluster security postures.

#### 5.3.1. Option A: On-Demand Dumps via Tools Embedded in Application Image (UNSECURE)

This method involves bundling the .NET diagnostic tools directly into the main application's container image. This allows an operator to execute dotnet-dump and other tools from a shell within the running application container itself.

Configuration:

Your `Containerfile` or `Dockerfile` must be adapted to install the .NET diagnostic tools alongside your application code. This is typically done by installing them as global tools.

Example Containerfile layer:

~~~
# Install .NET diagnostic tools
RUN dotnet tool install --global dotnet-dump --version 8.*
RUN dotnet tool install --global dotnet-trace --version 8.*
RUN dotnet tool install --global dotnet-counters --version 8.*

# Add the tools to the PATH
ENV PATH="${PATH}:/root/.dotnet/tools"
~~~

Our `Containerfile` in this repository does already include that changes. To make this workflow simpler, we are using the same image in all options here by now.

**Workflow:**

- Gain shell access to the running application container.
- Identify the application's process ID (PID).
- Execute dotnet-dump to collect the dump and save it to the shared volume.

Example Commands:

~~~
# 1. Get the pod name
export POD_NAME=$(oc get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')

# 2. Access the application container's shell
# Note: We are targeting the main 'dotnet-app' container
oc rsh -c dotnet-app "$POD_NAME"

# 3. Inside the container, find the main app's PID (usually PID 1)
ps -ef

# 4. Collect a dump of the application (replace <PID> with the actual PID)
dotnet-dump collect --process-id <PID> -o /app/dumps/app_collected_dump.dmp

# 5. Exit the container's shell
exit
~~~

**Cons of this Approach:**

- **Larger Image Size:** Including the SDK or diagnostic tools in your final application image increases its size. This goes against the best practice of keeping production images as lean as possible, leading to slower deployment times and higher storage costs.
- **Increased Attack Surface:** Every tool and library added to your production image is a potential vector for security vulnerabilities. A minimal image with only the necessary runtime and application code is more secure.
- **Immutable Tooling:** The diagnostic tools are version-locked with the application image. If a new, critical version of dotnet-dump is released, you must rebuild and redeploy the entire application image to update it. Other methods, like sidecars or ephemeral containers, allow for more flexible tool versioning.
- **Requires a Shell:** This method depends on having a shell (e.g., /bin/sh or /bin/bash) available in the production container, which is often discouraged from a security perspective.


#### 5.3.2. Option B: Automatic OOM Dumps
This is the primary and most reliable method for capturing the application's state during an OutOfMemory crash, especially in strict security environments. The .NET runtime automatically generates a full dump when the application crashes due to an unhandled exception. The **cons** with this approach is an **increased surface of attack**, with the introduction diagnostic tools as mentioned before. 

Configuration:

- The base OCI does include .NET tools, like `dotnet-dump`, embeeded together with the application runtime. 
- These variables are pre-configured in your deployment.yaml:
  - COMPlus_DbgEnableElfDumpOnCrash=1: Enables ELF crash dump generation.
  - COMPlus_DbgCrashDumpType=3: Specifies a "full" dump.
  - COMPlus_DbgMiniDumpName=/app/dumps/dump.dmp: Sets the output path.
- The /app/dumps directory is backed by a PersistentVolumeClaim to ensure persistence.

Workflow:
- Trigger the memory leak.
- Allow the application to run until it crashes (you'll see restarts in oc get pods).
- Once the pod crashes and restarts, a dump file named dump.dmp (or similar, like core.<PID>) will be present in the /app/dumps volume.

Example Commands (after application crashes and restarts):
~~~
# Get the name of a running pod (it might be a new instance after restart)
export POD_NAME=$(oc get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')

# Verify the dump file exists (replace with your actual pod name)
oc rsh "$POD_NAME" -c dotnet-app -- ls -l /app/dumps/

# Copy the dump file from the pod to your local machine for analysis
oc cp "$POD_NAME":/app/dumps/dump.dmp ./crash_dump.dmp -n dotnet-memory-leak-app
~~~

Optional: Analyze the dump locally using dotnet-dump

~~~
dotnet-dump analyze ./crash_dump.dmp
~~~

#### 5.3.3. Option C: On-Demand Sidecar via Deployment Patching
This method allows for interactive, real-time dump collection by running .NET diagnostic tools from a dedicated sidecar container within the same pod.
It uses oc patch to temporarily modify the Deployment resource, which performs a controlled rollout of a new pod containing the application and a debug sidec

Step 1: Build the Correct Debug Image
Create a Containerfile that starts from the correct .NET SDK version and installs the version-matched diagnostic tools. This image will be used for the on-demand sidecar.

~~~
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

# The user/group setup should match your environment's requirements
RUN chown 1001:0 /app

# Switch to root to install tools
USER root

# Install the .NET 8 versions of the diagnostic tools into a specific path
RUN mkdir -p /app/tools && chown 1001:0 /app/tools && \
    dotnet tool install --tool-path /app/tools dotnet-dump --version "8.*" && \
    dotnet tool install --tool-path /app/tools dotnet-gcdump --version "8.*" && \
    dotnet tool install --tool-path /app/tools dotnet-trace --version "8.*" && \
    dotnet tool install --tool-path /app/tools dotnet-counters --version "8.*"

# Switch back to the non-root user
USER 1001
~~~

Build this image and push it to your internal container registry (e.g., quay.io/rhn_support_arolivei/dotnet-debug:v1).

Step 2: Prepare Patch Files
Create two JSON patch files. One to add the debugger and one to remove it.

add-sidecar.json:
~~~
[
  {
    "op": "add",
    "path": "/spec/template/spec/containers/-",
    "value": {
      "name": "debugger",
      "image": "quay.io/rhn_support_arolivei/dotnet-debug:v1",
      "command": ["sleep", "infinity"],
      "env": [
        {
          "name": "TMPDIR",
          "value": "/app/dumps/tmp"
        }
      ],
      "volumeMounts": [
        {
          "mountPath": "/app/dumps",
          "name": "dump-storage"
        }
      ]
    }
  }
]
~~~

remove-sidecar.json:
~~~
[
  {
    "op": "remove",
    "path": "/spec/template/spec/containers/1"
  }
]
~~~


Step 3: Execute the Debugging Workflow
Ensure your application is running using its base Deployment configuration.
Patch the Deployment to add the sidecar. This triggers a rolling update.

~~~
oc patch deployment dotnet-memory-leak-app -n dotnet-memory-leak-app --type=json --patch-file add-sidecar.json
~~~

Wait for the new pod to be ready (it will show 2/2 containers).
~~~
oc get pods -n dotnet-memory-leak-app --watch
~~~

Exec into the debugger sidecar:
~~~
export POD_NAME=$(oc get pods -l app=dotnet-memory-leak-app -n dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')
oc exec -it "$POD_NAME" -n dotnet-memory-leak-app -c debugger -- /bin/bash
~~~

Collect the dump. Because you are in the same pod, you can see the application's process and use its PID.

~~~
# Inside the debugger shell, find the process ID
ps -ef

# Use the PID (e.g., 2) to collect the dump with the version-matched tool
/app/tools/dotnet-dump collect -p 2 -o /app/dumps/dump_SUCCESS.dmp
~~~

Remove the sidecar. After collecting the dump, patch the deployment again to remove the debug container and return to the original, hardened state.

~~~
oc patch deployment dotnet-memory-leak-app -n dotnet-memory-leak-app --type=json --patch-file remove-sidecar.json
~~~

#### 5.3.4. Option D: On-Demand Dumps via Ephemeral Debug Container (kubectl debug)

This method allows you to dynamically inject a temporary container into an existing pod for on-demand debugging, without permanent changes to the deployment.yaml.

**How it works:**
kubectl debug creates a new, temporary container within an existing pod.
The `--target` flag ensures this ephemeral container joins the process namespace of your main application container.
You specify an image for the ephemeral container that contains the necessary diagnostic tools.

**Pros:**
- No Permanent Deployment Changes: Ideal for ad-hoc troubleshooting without altering your deployment.yaml.
- On-Demand Resources: The debug container only consumes resources when actively used.

**Cons:**
- Kubernetes Version Dependent: The kubectl debug `--target` command requires Kubernetes 1.25+ and enabled EphemeralContainers feature gates.

##### 5.3.4.1  Example Commands:

~~~
# 1. Get the pod name
export POD_NAME=$(kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')

# 2. Launch an ephemeral debug container
#    Use your application image (if it has tools and shell) or a full SDK image (recommended for debugging)
#    NOTE: This command might fail if your cluster/client doesn't support --target,
#          or if security policies block SYS_PTRACE.
kubectl debug -it "$POD_NAME" --image=quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1 --target=dotnet-app -- /bin/bash

# If the above fails or you need a richer toolset, try with the SDK image:
# kubectl debug -it "$POD_NAME" --image=registry.redhat.io/rhel8/dotnet-80:8.0 --target=dotnet-app -- /bin/bash

# 3. Inside the ephemeral debug container, find the main app's PID and collect a dump.
#    (You might need to run: mount -t proc proc /proc first if ps -ef fails)
ps -ef
# Look for 'dotnet /app/DotNetMemoryLeakApp.dll'. Note its PID.

# 4. Collect a dump of the main application (replace <PID> with the actual PID)
TMPDIR=/proc/<PID>/root/tmp /app/tools/ dotnet-dump collect --process-id <PID> -o /app/dumps/ephemeral_collected_dump.dmp

# 5. Exit the debug container and copy the dump file
oc cp "$POD_NAME":/app/dumps/app_collected_dump.dmp ./app_collected_dump.dmp -n dotnet-memory-leak-app
~~~

Sample output
~~~
bash-4.4$ ps -ef
UID         PID   PPID  C STIME TTY          TIME CMD
1000150+      1      0  0 12:46 ?        00:00:01 dotnet /app/DotNetMemoryLeakApp.dll
1000150+     90      0  0 14:14 pts/0    00:00:00 /bin/bash
1000150+    106     90  0 14:20 pts/0    00:00:00 ps -ef
bash-4.4$ 
bash-4.4$ TMPDIR=/proc/1/root/tmp /app/tools/dotnet-dump collect --process-id 1 -o /app/dumps/app_collected_dump.dmp

Writing full to /app/dumps/app_collected_dump.dmp
Complete
bash-4.4$ 
bash-4.4$ ls -la /app/dumps/
ls: cannot access '/app/dumps/': No such file or directory
bash-4.4$ ls -la /app/      
total 0
drwxr-xr-x. 1 default root  19 Aug 22 16:31 .
dr-xr-xr-x. 1 root    root  28 Sep  9 14:46 ..
drwxr-xr-x. 3 default root 103 Aug 22 16:31 tools
bash-4.4$ df 
Filesystem            1K-blocks     Used Available Use% Mounted on
overlay                52363264 12386196  39977068  24% /
tmpfs                     65536        0     65536   0% /dev
shm                       65536        0     65536   0% /dev/shm
tmpfs                   1625532    76316   1549216   5% /etc/passwd
/dev/mapper/rhel-root  52363264 12386196  39977068  24% /etc/hosts
devtmpfs                   4096        0      4096   0% /proc/keys
bash-4.4$ exit

[redhat@rhel96-microshift419-vm2 tmp]$ oc cp "$POD_NAME":/app/dumps/app_collected_dump.dmp ./app_collected_dump.dmp -n dotnet-memory-leak-app
Defaulted container "dotnet-app" out of: dotnet-app, debugger-xxlvc (ephem), debugger-sgcvg (ephem), debugger-xpn9g (ephem)
tar: Removing leading `/' from member names
[redhat@rhel96-microshift419-vm2 tmp]$ file app_collected_dump.dmp
app_collected_dump.dmp: ELF 64-bit LSB core file, x86-64, version 1 (GNU/Linux), SVR4-style, from 'dotnet', real uid: 1000150000, effective uid: 1000150000, real gid: 0, effective gid: 0, execfn: '/usr/bin/dotnet', platform: 'x86_64'
[redhat@rhel96-microshift419-vm2 tmp]$ du -m app_collected_dump.dmp
241	app_collected_dump.dmp
[redhat@rhel96-microshift419-vm2 tmp]$ 
[redhat@rhel96-microshift419-vm2 tmp]$ oc rsh dotnet-memory-leak-app-56457fbcdc-2rpf9 
Defaulted container "dotnet-app" out of: dotnet-app, debugger-xxlvc (ephem), debugger-sgcvg (ephem), debugger-xpn9g (ephem), debugger-pvldq (ephem), debugger-shvx2 (ephem)
sh-4.4$ df
Filesystem                                        1K-blocks     Used Available Use% Mounted on
overlay                                            52363264 12386868  39976396  24% /
tmpfs                                                 65536        0     65536   0% /dev
shm                                                   65536        0     65536   0% /dev/shm
tmpfs                                               1625532    76308   1549224   5% /etc/passwd
/dev/topolvm/7910c082-ecca-40b0-85e7-89a7cdf8728b   5177344   562484   4614860  11% /app/dumps
/dev/mapper/rhel-root                              52363264 12386868  39976396  24% /etc/hosts
tmpfs                                               2097152       16   2097136   1% /run/secrets/kubernetes.io/serviceaccount
devtmpfs                                               4096        0      4096   0% /proc/keys
sh-4.4$ ls -l /app/dumps/
total 493328
-rw-------. 1 1000150000 1000150000 252518400 Sep  9 14:16 app_collected_dump.dmp
-rw-------. 1 1000150000 1000150000 252649472 Sep  9 14:25 app_collected_dump2.dmp
sh-4.4$ 

~~~


##### 5.3.4.2 The Mystery of the Missing /app/dumps Directory

If you were sharing processes, why did this command fail inside your debug container?

~~~
bash-4.4$ ls -l /app/dumps
ls: cannot access '/app/dumps': No such file or directory
~~~

This reveals another critical concept: sharing the process namespace is not the same as sharing the mount namespace (the filesystem).

Ephemeral containers injected with `--target` get their own separate set of volumeMounts. They do not inherit the mounts from the target container. That's why your debug container had no knowledge of the `/app/dumps` directory, which is a PVC mount in your original dotnet-app container.

**So How Did the File Get Written?**
If the debug container couldn't see /app/dumps, how did this command succeed?

~~~
TMPDIR=/proc/1/root/tmp /app/tools/dotnet-dump collect ... -o /app/dumps/app_collected_dump2.dmp
~~~

Because dotnet-dump is just a control tool. It attaches to the target process (PID 1) and tells it what to do. The actual work of writing the memory dump to the file is performed by the target dotnet process itself.

Since the dotnet process (PID 1) is running in the original container, it has full access to its own filesystem, including the mounted PVC at `/app/dumps`.

Think of it like this: You used a remote control (dotnet-dump) from your room to tell a robot (dotnet process) in the next room to write a file on a table (/app/dumps) that only exists in its room. 🤖

##### 5.3.4.3  **Why `kubectl debug` is used instead of `oc debug`**

For live-process debugging, such as collecting a memory dump, the debugging tool must run within the same Process ID (PID) namespace as the target application. This allows the debug tool to see and interact with the application's running processes.

The key difference between the two commands lies in how they achieve this:
- `kubectl debug` with the --target flag is specifically designed for this purpose. It works by adding a temporary ephemeral container to the existing, running Pod. This new container joins the Pod's existing namespaces, including the PID namespace, giving it direct access to the application's processes.
- Currently, `oc debug`, in contrast, creates an entirely new and separate Pod by copying the configuration from the original Deployment or DeploymentConfig. While this new pod has a similar environment (volumes, service account), it has its own isolated PID namespace. As a result, it cannot see or interact with the processes running in the original application pod, making it unsuitable for live dump collection.

Therefore, `kubectl debug` is the appropriate conceptual tool for this task, as its function is to attach to a live process, whereas `oc debug` is currently designed for inspecting state by creating a separate, isolated environment.

##### 5.3.4.4  `kubectl` vs. `oc`: A Quick Comparison

While `kubectl` is the standard command-line tool for any Kubernetes cluster, `oc` is the specialized command-line tool for OpenShift clusters (including MicroShift).

The most important thing to know is that **`oc` is a superset of `kubectl`**. This means that any `kubectl` command you know will also work with `oc`. You can simply replace `kubectl` with `oc` and it will function as expected.

For example:
* `kubectl get pods` is the same as `oc get pods`.
* `kubectl apply -f my-app.yaml` is the same as `oc apply -f my-app.yaml`.

However, `oc` includes extra, powerful features designed specifically for OpenShift's developer and enterprise-focused workflows.

Here is a summary of the key differences:

| Feature | `kubectl` (Standard Kubernetes) | `oc` (OpenShift) |
| :--- | :--- | :--- |
| **Core Functionality** | Manages standard Kubernetes resources (Pods, Deployments, Services, etc.). | **Includes all `kubectl` functionality** and extends it. |
| **Focus** | A general-purpose tool for cluster administrators and operators. | Adds many features focused on developer productivity and application lifecycle. |
| **Authentication** | Relies on a pre-configured `kubeconfig` file for cluster access. | Includes a built-in `oc login` command that integrates with OpenShift's OAuth server for easy authentication. |
| **Project Management**| Manages `Namespaces`. | Manages `Projects`, which are essentially Namespaces with added user permissions and security policies. Provides easy commands like `oc new-project` and `oc project <name>`. |
| **Application Deployment** | Deploys applications from YAML manifests (`kubectl apply`). | Adds powerful commands like **`oc new-app`** which can build and deploy an application directly from source code (e.g., from a Git repository) or an existing image. |
| **Builds & Images** | Does not have built-in concepts for building container images. | Natively understands OpenShift-specific resources like **`BuildConfig`** and **`ImageStream`**. It includes commands like `oc start-build` to trigger image builds from source. |
| **Networking** | Manages `Ingress` resources for external access, which requires a separate ingress controller. | Natively manages **`Route`** resources, which are a simpler, integrated way to expose services to the outside world, often with automated TLS configuration. |

**Summary: When to Use Which?**

* **Use `kubectl` if:** You are writing scripts that need to be portable across any Kubernetes cluster (not just OpenShift) and you are only interacting with standard Kubernetes resources.
* **Use `oc` if:** You are working with an OpenShift or MicroShift cluster. It provides a much richer, more integrated experience by giving you access to all the advanced features OpenShift builds on top of Kubernetes. **For daily work on OpenShift, `oc` is always the recommended tool.**

#### 5.3.5. Option E: Secure On-Demand Dumps via Shell-less Ephemeral Container
This method enhances Option D by adhering to strict security policies that forbid shells even in debug images. It uses a purpose-built, shell-less debug container with a compiled utility that automates the dump collection process.

**How it works:**
1.  A Go utility (`tools/pid-finder`) is compiled into a static binary.
2.  A multi-stage `Containerfile-debug` builds a debug image that contains the `.NET` diagnostic tools and this Go utility as its `ENTRYPOINT`.
3.  When this debug container is launched, the Go utility executes automatically. It finds the target `.NET` process, collects a full core dump, and then sleeps indefinitely.
4.  This approach is fully automated, requires no interactive shell, and minimizes the attack surface of the debug image.

**Workflow:**

**Step 1: Build the Secure Debug Image**
Use the modified `Containerfile-debug` to build the image. This must be done from the root of the repository.

~~~ 
# Build the secure debug image
podman build -t quay.io/your-namespace/dotnet-secure-debug:v1 -f Containerfile-debug .

# Push the image to your container registry
podman push quay.io/your-namespace/dotnet-secure-debug:v1
~~~

**Step 2: Launch the Ephemeral Debug Container**
Use `kubectl debug` to attach the ephemeral container to your running application pod. The command is simpler because the container's entrypoint does all the work.

~~~ 
# Get the pod name
export POD_NAME=$(kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')

# Launch the secure ephemeral debug container
# The --image should point to the one you just built.
kubectl debug -it "$POD_NAME" \
  --image=quay.io/your-namespace/dotnet-secure-debug:v1 \
  --share-processes \
  --target=dotnet-app
~~~

You will see the output from the Go utility as it finds the process and collects the dump.

**Step 3: Copy the Dump File**
The dump is saved to `/app/dumps/coredump.dmp` inside the target container's filesystem (since the `dotnet-dump` command is executed by the target process). You can copy it out using `kubectl cp`.

~~~ 
# Copy the dump file from the application pod to your local machine
kubectl cp "$POD_NAME":/app/dumps/coredump.dmp ./coredump.dmp -n dotnet-memory-leak-app -c dotnet-app
~~~

This method provides a secure and non-interactive way to obtain diagnostics, making it ideal for production environments with strict security postures.


Sample output: 

~~~
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ sudo podman load -i dotnet-secure-debug-v1.tar 
[sudo] password for redhat: 
Getting image source signatures
Copying blob bfaed8e0c4d1 done   | 
Copying blob 28de103bd9c3 skipped: already exists  
Copying blob 85bbf55a0c9b skipped: already exists  
Copying blob 37aef32bd2f2 skipped: already exists  
Copying blob ec17d09b16f1 done   | 
Copying config c57643bf44 done   | 
Writing manifest to image destination
Loaded image: quay.io/rhn_support_arolivei/dotnet-secure-debug:v1
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ oc get pods
NAME                                      READY   STATUS    RESTARTS   AGE
dotnet-memory-leak-app-77b88ddf46-j4dn5   1/1     Running   0          6s
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ oc rsh dotnet-memory-leak-app-77b88ddf46-j4dn5 
sh-4.4$ ps -ef
UID         PID   PPID  C STIME TTY          TIME CMD
root          1      0  0 16:09 ?        00:00:00 /usr/bin/pod
1000170+      2      0  1 16:09 ?        00:00:00 dotnet /app/DotNetMemoryLeakApp.dll
1000170+     22      0  0 16:09 pts/0    00:00:00 /bin/sh
1000170+     24     22  0 16:09 pts/0    00:00:00 ps -ef
sh-4.4$ ls -l /proc/2/root/tmp
total 0
prwx------. 1 1000170000 1000170000 0 Sep 11 16:09 clr-debug-pipe-2-27034808-in
prwx------. 1 1000170000 1000170000 0 Sep 11 16:09 clr-debug-pipe-2-27034808-out
srw-------. 1 1000170000 1000170000 0 Sep 11 16:09 dotnet-diagnostic-2-27034808-socket
sh-4.4$ df
Filesystem                                        1K-blocks     Used Available Use% Mounted on
overlay                                            52363264 15721224  36642040  31% /
tmpfs                                                 65536        0     65536   0% /dev
shm                                                   65536        0     65536   0% /dev/shm
tmpfs                                               1625532    76020   1549512   5% /etc/passwd
/dev/mapper/rhel-root                              52363264 15721224  36642040  31% /tmp
/dev/topolvm/01b27068-1c48-46ea-92c6-49ba0ad97c40   5177344   314060   4863284   7% /app/dumps
tmpfs                                               2097152       16   2097136   1% /run/secrets/kubernetes.io/serviceaccount
devtmpfs                                               4096        0      4096   0% /proc/keys
sh-4.4$ ls -l /app/dumps/
total 0
sh-4.4$ 
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ export POD_NAME=$(kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app -o jsonpath='{.items[0].metadata.name}')
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ kubectl debug -it "$POD_NAME" --image=quay.io/rhn_support_arolivei/dotnet-secure-debug:v1 --target=dotnet-app
Targeting container "dotnet-app". If you don't see processes from this container it may be because the container runtime doesn't support this feature.
--profile=legacy is deprecated and will be removed in the future. It is recommended to explicitly specify a profile, for example "--profile=general".
Defaulting debug container name to debugger-rlb4q.
If you don't see a command prompt, try pressing enter.

------------------------------------------------------------------------
Successfully triggered core dump generation in the application container.
The dump file is being written to '/app/dumps/coredump.dmp' inside the 'dotnet-app' container.
You can now copy the file from the application container.
Example: kubectl cp <pod-name>:/app/dumps/coredump.dmp ./coredump.dmp -c dotnet-app
This debug container will automatically exit in 10 seconds.
------------------------------------------------------------------------
Exiting debug container.
Session ended, the ephemeral container will not be restarted but may be reattached using 'kubectl attach dotnet-memory-leak-app-77b88ddf46-j4dn5 -c debugger-rlb4q -i -t' if it is still running
[redhat@rhel96-microshift419-vm2 DotNetBuggyApp]$ oc rsh dotnet-memory-leak-app-77b88ddf46-j4dn5 
Defaulted container "dotnet-app" out of: dotnet-app, debugger-rlb4q (ephem)
sh-4.4$ ls -la /app/dumps/
total 236132
drwxrwsrwx. 2 root       1000170000        26 Sep 11 16:10 .
drwxr-xr-x. 1 root       root              19 Sep 11 16:09 ..
-rw-------. 1 1000170000 1000170000 241799168 Sep 11 16:10 coredump.dmp
sh-4.4$
~~~

#### 5.3.6. Option F: Deploying with a Hardened Security Context

This method demonstrates how to run the application under a highly restrictive, non-root security context. It serves as a best-practice example for production environments where security is paramount. This approach uses a dedicated service account and a custom Security Context Constraint (SCC) to enforce strict security rules from the start.

**Configuration:**

This approach is defined in two files:

1.  `deployment-secure.yaml`: A new deployment manifest that runs the pod with a locked-down security context.
2.  `scc-and-rbac-secure.yaml`: Contains the necessary `ServiceAccount` and a custom `SecurityContextConstraints` (SCC) named `restricted-v2`.

Key security settings enforced by this configuration include:
- `runAsNonRoot: true`: Ensures the container does not run as root.
- `runAsUser: 1000` / `runAsGroup: 1000`: Forces the container to run with a specific, non-privileged user and group ID.
- `readOnlyRootFilesystem: true`: Prevents any part of the container's root filesystem from being written to. Writable paths for dumps (`/app/dumps`) and temporary files (`/tmp`) are provided by volume mounts.
- `capabilities: { drop: ["ALL"] }`: Drops all Linux capabilities, reducing the process's potential privileges to the absolute minimum.
- `seccompProfile: { type: RuntimeDefault }`: Applies the default seccomp profile of the container runtime, blocking a wide range of potentially dangerous syscalls.

**Workflow:**

The new resources are included in the `kustomization.yaml` file. To deploy this hardened application alongside the default one, simply apply the kustomization.

**Example Command:**

~~~
# Apply all configurations, including the secure deployment
oc apply -k .
~~~

After the command succeeds, you will have two deployments running: the original `dotnet-memory-leak-app` and the new, hardened `dotnet-memory-leak-app-secure`. This allows you to compare their behavior and verify that the application still functions correctly under much stricter security constraints.

Sample output:

~~~
[redhat@rhel96-microshift419-vm2 kubernetes]$ kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app-secure
NAME                                             READY   STATUS    RESTARTS   AGE
dotnet-memory-leak-app-secure-86c78f8bf4-gmlls   1/1     Running   0          25s
[redhat@rhel96-microshift419-vm2 kubernetes]$ export POD_NAME=$(kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app-secure -o jsonpath='{.items[0].metadata.name}')
[redhat@rhel96-microshift419-vm2 kubernetes]$ kubectl debug -it "$POD_NAME" --image=quay.io/rhn_support_arolivei/dotnet-secure-debug:v1 --target=dotnet-app-secure
Targeting container "dotnet-app-secure". If you don't see processes from this container it may be because the container runtime doesn't support this feature.
--profile=legacy is deprecated and will be removed in the future. It is recommended to explicitly specify a profile, for example "--profile=general".
Defaulting debug container name to debugger-5klct.
If you don't see a command prompt, try pressing enter.
Exiting debug container.
Session ended, the ephemeral container will not be restarted but may be reattached using 'kubectl attach dotnet-memory-leak-app-secure-86c78f8bf4-gmlls -c debugger-5klct -i -t' if it is still running
[redhat@rhel96-microshift419-vm2 kubernetes]$ oc get pvc
NAME                       STATUS   VOLUME                                     CAPACITY   ACCESS MODES   STORAGECLASS          VOLUMEATTRIBUTESCLASS   AGE
dotnet-memory-leak-dumps   Bound    pvc-89927e32-e3fe-4f26-8d61-e974e2d628d3   5Gi        RWO            topolvm-provisioner   <unset>                 91s
[redhat@rhel96-microshift419-vm2 kubernetes]$ oc rsh dotnet-memory-leak-app-secure-86c78f8bf4-gmlls 
Defaulted container "dotnet-app-secure" out of: dotnet-app-secure, debugger-5klct (ephem)
sh-4.4$ ls -la /app/dumps/
total 235740
drwxrwsrwx. 3 root 1000        37 Sep 11 16:40 .
drwxr-xr-x. 1 root root        19 Sep 11 16:39 ..
-rw-------. 1 1000 1000 241397760 Sep 11 16:40 coredump.dmp
drwxrwsrwx. 2 1000 1000         6 Sep 11 16:39 tmp
sh-4.4$ date
Thu Sep 11 16:41:03 UTC 2025
sh-4.4$ 
[redhat@rhel96-microshift419-vm2 kubernetes]$ oc get pods dotnet-memory-leak-app-secure-6f7f4c4f49-thxdc -o yaml|egrep -A 10 "securityContext|share"
    securityContext:
      allowPrivilegeEscalation: false
      capabilities:
        drop:
        - ALL
      privileged: false
      readOnlyRootFilesystem: true
    terminationMessagePath: /dev/termination-log
    terminationMessagePolicy: File
    volumeMounts:
    - mountPath: /app/dumps
--
    securityContext:
      allowPrivilegeEscalation: false
      capabilities:
        drop:
        - ALL
      readOnlyRootFilesystem: true
    stdin: true
    targetContainerName: dotnet-app-secure
    terminationMessagePath: /dev/termination-log
    terminationMessagePolicy: File
    tty: true
--
  securityContext:
    fsGroup: 1000
    runAsGroup: 1000
    runAsNonRoot: true
    runAsUser: 1000
    seLinuxOptions:
      level: s0:c13,c12
    seccompProfile:
      type: RuntimeDefault
  serviceAccount: secure-app-sa
  serviceAccountName: secure-app-sa
  shareProcessNamespace: false
  terminationGracePeriodSeconds: 30
  tolerations:
  - effect: NoExecute
    key: node.kubernetes.io/not-ready
    operator: Exists
    tolerationSeconds: 300
  - effect: NoExecute
    key: node.kubernetes.io/unreachable
    operator: Exists
    tolerationSeconds: 300
[redhat@rhel96-microshift419-vm2 kubernetes]$ export POD_NAME=$(kubectl get pods -n dotnet-memory-leak-app -l app=dotnet-memory-leak-app-secure -o jsonpath='{.items[0].metadata.name}')
[redhat@rhel96-microshift419-vm2 kubernetes]$ kubectl debug -it "$POD_NAME" --image=quay.io/rhn_support_arolivei/dotnet-secure-debug:v1 --target=dotnet-app-secure
Targeting container "dotnet-app-secure". If you don't see processes from this container it may be because the container runtime doesn't support this feature.
--profile=legacy is deprecated and will be removed in the future. It is recommended to explicitly specify a profile, for example "--profile=general".
Defaulting debug container name to debugger-pkjlt.
If you don't see a command prompt, try pressing enter.

------------------------------------------------------------------------
Successfully triggered core dump generation in the application container.
The dump file is being written to '/app/dumps/coredump.dmp' inside the 'dotnet-app' container.
You can now copy the file from the application container.
Example: kubectl cp <pod-name>:/app/dumps/coredump.dmp ./coredump.dmp -c dotnet-app
This debug container will automatically exit in 10 seconds.
------------------------------------------------------------------------
Exiting debug container.
Session ended, the ephemeral container will not be restarted but may be reattached using 'kubectl attach dotnet-memory-leak-app-secure-6f7f4c4f49-7d55w -c debugger-pkjlt -i -t' if it is still running
[redhat@rhel96-microshift419-vm2 kubernetes]$ 
~~~

### 5.4. Limits & LimitaRanges


In a Kubernetes environment, managing compute resources like CPU and Memory is not just a best practice; it is critical for ensuring application performance and cluster stability. This is especially true for single-node deployments like MicroShift and self-contained air-gapped systems.

What are Resource Requests and Limits?
When defining a Pod, you can specify resource requests and limits for each of its containers:

- **Requests**: This is the amount of CPU and Memory that Kubernetes guarantees for a container. The Kubernetes scheduler uses this value to decide where to place the Pod, ensuring it only runs on a node with enough available capacity.
- **Limits**: This is the maximum amount of CPU and Memory a container is allowed to use.
  - If a container exceeds its Memory limit, it is terminated by the kernel (an "Out of Memory" or OOMKill).
  - If a container exceeds its CPU limit, it is "throttled," meaning its CPU usage is artificially capped, which can degrade its performance.

YAML
~~~
# Example snippet for a container's spec
resources:
  requests:
    memory: "64Mi"
    cpu: "250m" # 250 millicores (0.25 of a core)
  limits:
    memory: "128Mi"
    cpu: "500m" # 500 millicores (0.5 of a core)
~~~

**Why This is Critical for a MicroShift / Single-Node Cluster**
In a multi-node cluster, a single "runaway" application might crash one worker node, but the cluster and other applications remain operational. In a single-node cluster like MicroShift, the node is the "cluster".

- Preventing Node Starvation: If a single container without limits consumes all available Memory or CPU, it can starve the node's critical system processes, including the kubelet and the underlying operating system. This can cause the node to enter a NotReady state, effectively bringing down the entire cluster and making it unresponsive.
- Protecting the Control Plane: MicroShift runs its control plane components (like the API server) on the same single node. Enforcing limits on your applications ensures they cannot disrupt the resources needed by the control plane, thereby protecting the stability and availability of the cluster itself.
- Ensuring Quality of Service (QoS): By setting resource requests, you tell Kubernetes which Pods are more important. Pods with guaranteed resources (Guaranteed QoS class) are the last to be killed if the node runs out of memory, ensuring your critical applications survive.

**Why This is Critical in an Air-Gapped Environment**
An air-gapped environment has a fixed, finite amount of hardware resources.

- Inability to Scale Out: Unlike a cloud environment where you can automatically provision more nodes in response to high load, an air-gapped system cannot be easily expanded. You must work within the physical constraints of your hardware.
- Enforcing Capacity Management: Limits are your primary tool for enforcing capacity management. They prevent any single application or team from consuming a disproportionate share of the fixed resources, which could cause a cascading failure of other essential services running in the same environment.

**Putting it into Practice: The LimitRange Object**
Defining requests and limits for every Pod manually can be tedious. Kubernetes provides a policy object called LimitRange that you can apply to a namespace to enforce sane defaults and constraints.

A LimitRange can:
- Assign default request and limit values to containers that do not define their own.
- Enforce minimum and maximum values for CPU and Memory.
- Enforce a ratio between requests and limits.

Example limitrange.yaml:
This LimitRange enforces that every container in the namespace will get default resources if not specified, and it prevents any single container from requesting too much.

~~~
apiVersion: v1
kind: LimitRange
metadata:
  name: resource-limits-for-namespace
spec:
  limits:
  - type: Container
    # Default resource request for any container created without one.
    defaultRequest:
      cpu: "100m"
      memory: "64Mi"
    # Default resource limit for any container created without one.
    default:
      cpu: "500m"
      memory: "256Mi"
    # Maximum resource limit any container in the namespace is allowed to have.
    max:
      cpu: "1"         # 1 full core
      memory: "1Gi"
    # Minimum resource limit any container in the namespace is allowed to have.
    min:
      cpu: "50m"
      memory: "32Mi"
~~~

By applying a LimitRange to your namespaces, you create a powerful safety net that significantly improves the stability and predictability of your cluster—a necessity for a production-grade, single-node system.

## 6. Security & Troubleshooting Considerations
Deploying and debugging applications in OpenShift/Kubernetes, especially with advanced diagnostic tools, often involves navigating strict security policies.

### 6.1. Pod Security

Kubernetes environments, including OpenShift and MicroShift, utilize powerful security enforcement mechanisms to govern pod behavior and permissions. These are primarily implemented through policies such as Security Context Constraints (SCCs), which are specific to OpenShift, and the Kubernetes-native Pod Security Admission (PSA).

In a hardened cluster, it is common for a default, restrictive security policy to be applied at the namespace or cluster level. Such policies rigorously control the security-sensitive attributes a pod can request in its specification.

Consequently, if the securityContext defined in a deployment manifest includes settings that are disallowed by the active policy (for example, attempting to run as a specific user ID or requesting certain capabilities), the Kubernetes API server will reject the configuration. This typically results in an error during deployment, with messages such as Warning: would violate PodSecurity or Error creating: pods "..." is forbidden: violates PodSecurity "restricted".

### 6.2. SYS_PTRACE Capability
Requirement: Tools like dotnet-dump collect need the CAP_SYS_PTRACE capability to attach to another process and inspect its memory.
Challenge: Strict security policies often disallow or strip this capability from containers (e.g., drop: ALL is common in restricted policies). You might see errors like Invalid value: "SYS_PTRACE": capability may not be added.
Solution: To enable SYS_PTRACE for interactive debugging, you typically need to:
Ensure your securityContext in deployment.yaml add: - SYS_PTRACE (and doesn't drop: ALL).
Bind your ServiceAccount to a more permissive SCC (e.g., privileged) or have a cluster administrator adjust the namespace's Pod Security Enforcement to baseline or eventually bind a custom SCC to the ServiceAccount.

### 6.3. seccompProfile
Requirement: Containers often define a seccompProfile (e.g., RuntimeDefault) for enhanced security by filtering syscalls.
Challenge: In very strict environments, even setting seccompProfile: type: RuntimeDefault might be forbidden, leading to errors like Forbidden: seccomp may not be set.
Solution: If seccomp is blocked, you might need to remove the seccompProfile lines from your deployment.yaml's securityContext and rely solely on the privileged SCC to provide an unconfined or permissive seccomp profile.

### 6.4. TMPDIR and IPC Issues
Requirement: dotnet diagnostic tools use temporary directories (often /tmp/) for inter-process communication (IPC) when connecting to a target process.
Challenge: If /tmp/ is not properly writable for the container's assigned user, or if TMPDIR environment variables are inconsistent between the diagnostic tool and the target application, connection issues can arise (e.g., "Please verify that /tmp/ is writable by the current user").
Solution:
Explicitly set TMPDIR=/app/dumps/tmp for both the application and diagnostic containers in deployment.yaml.
Ensure /app/dumps/tmp is created and made world-writable (chmod 777) at runtime via a command/args in your container definition, as volume mounts can overwrite built-in directories.

### 6.5. Resource Limits and OOM Killer Race Conditions
Challenge: If your application is actively consuming memory and approaching its resources.limits.memory, the operating system's OOM killer might terminate the process before the .NET runtime has a chance to fully write a crash dump, especially for full dumps.
Solution:
Temporarily increase the resources.limits.memory for your application container in deployment.yaml to provide a larger buffer, allowing more time for dump generation.
Rely on the automatic OOM dumps (Option A), as they are designed to capture the state at the moment of crash.

## 7. External References & Further Reading
For a deeper dive into the technologies and concepts explored in this project, refer to the following official documentation and resources:

- **.NET Diagnostics**
  - .NET Diagnostics in Containers: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-in-containers
  - Debug Memory Leaks in .NET: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak
  - `dotnet-dump` global tool: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump
  - `dotnet-trace` global tool: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace
  - `dotnet-counters` global tool: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
  - `dotnet-gcdump` global tool: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-gcdump
- **OpenShift, MicroShift & Kubernetes Security**
  - Understanding Security Context Constraints (SCCs) in OpenShift: https://docs.openshift.com/container-platform/latest/authentication/managing-security-context-constraints.html
  - Kubernetes Pod Security Standards: https://kubernetes.io/docs/concepts/security/pod-security-standards/
  - Managing Pod Security Admission: https://kubernetes.io/docs/tasks/configure-pod-container/enforce-standards-admission-controller/
  - Using seccomp in OpenShift: https://docs.openshift.com/container-platform/latest/security/seccomp-profiles.html
- **OpenShift & Container Best Practices**
  - Red Hat Universal Base Images (UBI): https://access.redhat.com/containers
  - Building Container Images in OpenShift: https://docs.openshift.com/container-platform/latest/builds/index.html
  - Troubleshooting OpenShift Pods: https://docs.openshift.com/container-platform/latest/nodes/nodes-pods-debug-container-issues.html

## 8. Contributing
Feel free to open issues or submit pull requests for any improvements or bug fixes.

## 9. License
This project is licensed under the Apache License 2.0. See the LICENSE file for details.