# DotNet Memory Leak App

## Table of Contents

- [DotNet Memory Leak App](#dotnet-memory-leak-app)
  - [Table of Contents](#table-of-contents)
  - [1. Repository Overview](#1-repository-overview)
  - [2. Key Features](#2-key-features)
  - [3. Motivation](#3-motivation)
  - [4. Deployment Guide (OpenShift / MicroShift)](#4-deployment-guide-openshift--microshift)
    - [4.1. Prerequisites](#41-prerequisites)
    - [4.2. Building \& Pushing the Container Image](#42-building--pushing-the-container-image)
    - [4.3. Deployment to OpenShift / MicroShift](#43-deployment-to-openshift--microshift)
  - [5. Usage \& Diagnostic Workflow](#5-usage--diagnostic-workflow)
    - [5.1. Triggering a Memory Leak](#51-triggering-a-memory-leak)
    - [5.2. Monitoring Application Logs](#52-monitoring-application-logs)
    - [5.3. Collecting Crash Dumps](#53-collecting-crash-dumps)
      - [5.3.1. Option A: On-Demand Dumps via Tools Embedded in Application Image](#531-option-a-on-demand-dumps-via-tools-embedded-in-application-image)
      - [5.3.2. Option B: Automatic OOM Dumps](#532-option-b-automatic-oom-dumps)
      - [5.3.3. Option C: On-Demand Sidecar via Deployment Patching](#533-option-c-on-demand-sidecar-via-deployment-patching)
      - [5.3.4. Option D: On-Demand Dumps via Ephemeral Debug Container (kubectl debug)](#534-option-d-on-demand-dumps-via-ephemeral-debug-container-kubectl-debug)
  - [6. Security \& Troubleshooting Considerations](#6-security--troubleshooting-considerations)
    - [6.1. Pod Security Policies (PSP / PSA / SCC)](#61-pod-security-policies-psp--psa--scc)
    - [6.2. SYS\_PTRACE Capability](#62-sys_ptrace-capability)
    - [6.3. seccompProfile](#63-seccompprofile)
    - [6.4. TMPDIR and IPC Issues](#64-tmpdir-and-ipc-issues)
    - [6.5. Resource Limits and OOM Killer Race Conditions](#65-resource-limits-and-oom-killer-race-conditions)
  - [7. External References \& Further Reading](#7-external-references--further-reading)
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

#### 5.3.1. Option A: On-Demand Dumps via Tools Embedded in Application Image

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
This is the primary and most reliable method for capturing the application's state during an OutOfMemory crash, especially in strict security environments. The .NET runtime automatically generates a full dump when the application crashes due to an unhandled exception. The cons with this approach is an increased surface of attack, with the introduction diagnostic tools as mentioned before. 

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

(**WORKING IN PROGRESS**, _Enabling ephemeral containers in a MicroShift cluster requires modifying its configuration to activate the EphemeralContainers feature gate. This is done by editing MicroShift's central configuration file and then restarting the service._)

This method allows you to dynamically inject a temporary container into an existing pod for on-demand debugging, without permanent changes to the deployment.yaml.

How it works:

kubectl debug creates a new, temporary container within an existing pod.
The --target flag ensures this ephemeral container joins the process namespace of your main application container.
You specify an image for the ephemeral container that contains the necessary diagnostic tools.
Pros:

No Permanent Deployment Changes: Ideal for ad-hoc troubleshooting without altering your deployment.yaml.
On-Demand Resources: The debug container only consumes resources when actively used.
Cons:

Kubernetes Version Dependent: The kubectl debug --target command requires Kubernetes 1.25+ and enabled EphemeralContainers feature gates (your current client/cluster might not fully support this).
Security Policy Challenges: Similar to the sidecar, granting SYS_PTRACE to an ephemeral container will face the same PodSecurity restrictions, potentially requiring privileged SCC access.
Example Commands:

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
dotnet-dump collect --process-id <PID> -o /app/dumps/ephemeral_collected_dump.dmp
~~~

## 6. Security & Troubleshooting Considerations
Deploying and debugging applications in OpenShift/Kubernetes, especially with advanced diagnostic tools, often involves navigating strict security policies.

### 6.1. Pod Security Policies (PSP / PSA / SCC)
OpenShift/MicroShift/Kubernetes clusters enforce security through mechanisms like Pod Security Policies (PSP - deprecated in K8s, still used in older OpenShift), Pod Security Admission (PSA), and Security Context Constraints (SCC - OpenShift specific). These policies define what actions a pod is allowed to perform.

Your cluster likely has a default PodSecurity "restricted" policy or similar SCCs that are highly secure.
These policies may block certain securityContext settings in your deployment.yaml, leading to Warning: would violate PodSecurity or Error creating: pods ... is forbidden.

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