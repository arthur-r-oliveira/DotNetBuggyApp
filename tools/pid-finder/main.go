package main

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

const (
	// The name of the DLL for the target application.
	// This helps distinguish it from other dotnet processes.
	targetProcessName = "DotNetMemoryLeakApp.dll"

	// Path to the dotnet-dump tool inside the debug container.
	dumpToolPath = "/app/tools/dotnet-dump"

	// Where to save the collected dump.
	dumpOutputPath = "/app/dumps/coredump.dmp"

	// The name of the application container, for clear user messages.
	appContainerName = "dotnet-app"
)

func main() {
	fmt.Println("Starting secure dump utility...")

	pid, err := findTargetProcess()
	if err != nil {
		fmt.Printf("Error finding target process: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("Found target process '%s' with PID: %d\n", targetProcessName, pid)

	if err := collectDump(pid); err != nil {
		fmt.Printf("Error collecting dump: %v\n", err)
		os.Exit(1)
	}

	fmt.Println("\n------------------------------------------------------------------------")
	fmt.Println("Successfully triggered core dump generation in the application container.")
	fmt.Printf("The dump file is being written to '%s' inside the '%s' container.\n", dumpOutputPath, appContainerName)
	fmt.Println("You can now copy the file from the application container.")
	fmt.Printf("Example: kubectl cp <pod-name>:%s ./coredump.dmp -c %s\n", dumpOutputPath, appContainerName)
	fmt.Println("This debug container will automatically exit in 10 seconds.")
	fmt.Println("------------------------------------------------------------------------")

	time.Sleep(10 * time.Second)

	fmt.Println("Exiting debug container.")
}

// findTargetProcess scans the /proc filesystem to find the PID of the target .NET application.
func findTargetProcess() (int, error) {
	var targetPid int = -1

	procDirs, err := filepath.Glob("/proc/[0-9]*")
	if err != nil {
		return -1, fmt.Errorf("could not list process directories: %w", err)
	}

	for _, procDir := range procDirs {
		pidStr := filepath.Base(procDir)
		pid, err := strconv.Atoi(pidStr)
		if err != nil {
			continue // Not a valid PID directory
		}

		cmdlinePath := filepath.Join(procDir, "cmdline")
		cmdline, err := ioutil.ReadFile(cmdlinePath)
		if err != nil {
			continue // Process might have terminated
		}

		// The /proc/<pid>/cmdline file contains arguments separated by null characters.
		// We check if our target DLL name is present in the command line arguments.
		if bytes.Contains(cmdline, []byte(targetProcessName)) {
			targetPid = pid
			break
		}
	}

	if targetPid == -1 {
		return -1, fmt.Errorf("process '%s' not found", targetProcessName)
	}

	return targetPid, nil
}

// collectDump executes the 'dotnet-dump collect' command against the specified PID.
func collectDump(pid int) error {
	// We execute 'dotnet-dump' directly, without a shell.
	cmd := exec.Command(dumpToolPath, "collect",
		"--process-id", strconv.Itoa(pid),
		"-o", dumpOutputPath,
	)

	// The debug container has a different mount namespace than the target container.
	// To ensure dotnet-dump can communicate with the target process, it must use
	// the same temporary directory for its diagnostic socket.
	// We can access the target container's /tmp directory through the /proc filesystem.
	tmpDirForTarget := fmt.Sprintf("/proc/%d/root/tmp", pid)
	cmd.Env = append(os.Environ(), "TMPDIR="+tmpDirForTarget)

	// Capture stdout and stderr for better error reporting.
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	fmt.Printf("Executing: %s with TMPDIR=%s\n", strings.Join(cmd.Args, " "), tmpDirForTarget)

	err := cmd.Run()

	if err != nil {
		// Construct the error message safely.
		errorMsg := "dotnet-dump failed with error: %w\n"
		errorMsg += "STDOUT:\n%s\n"
		errorMsg += "STDERR:\n%s"
		return fmt.Errorf(errorMsg, err, stdout.String(), stderr.String())
	}

	return nil
}