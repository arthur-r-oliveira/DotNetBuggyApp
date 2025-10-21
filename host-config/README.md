# Host Configuration for .NET Coredump Capture

This directory contains the host-level configuration files required to enable automatic coredump capture for .NET applications running in hardened MicroShift/OpenShift environments.

## Files

- **`99-coredump.conf`**: Kernel sysctl configuration file for enabling coredumps
- **`ansible-playbook.yaml`**: Ansible playbook for automated host configuration
- **`README.md`**: This documentation file

## Quick Start

### Manual Configuration

1. Copy the sysctl configuration to each MicroShift host:
   ```bash
   sudo cp host-config/99-coredump.conf /etc/sysctl.d/
   ```

2. Create the coredump directory:
   ```bash
   sudo mkdir -p /var/crashdumps
   sudo chmod 777 /var/crashdumps
   ```

3. Apply the configuration:
   ```bash
   sudo sysctl --system
   ```

### Automated Configuration with Ansible

1. Create an inventory file with your MicroShift nodes:
   ```ini
   [microshift_nodes]
   microshift-node-1 ansible_host=192.168.1.100
   microshift-node-2 ansible_host=192.168.1.101
   ```

2. Run the playbook:
   ```bash
   ansible-playbook -i inventory host-config/ansible-playbook.yaml
   ```

## Configuration Details

### Kernel Parameters

- **`kernel.core_pattern`**: Defines where coredumps are saved with pattern:
  - `%e` = executable name
  - `%p` = process ID
  - `%h` = hostname
  - `%t` = timestamp

- **`fs.suid_dumpable`**: Allows setuid processes (including containers) to generate coredumps

### Directory Structure

```
/var/crashdumps/
├── core.dotnet.1234.hostname.1234567890
├── core.dotnet.5678.hostname.1234567891
└── ...
```

## Integration with Kubernetes

This host configuration works in conjunction with the Kubernetes manifests in `kubernetes/host-coredump/`:

1. **PersistentVolume**: Maps `/var/crashdumps` to cluster storage
2. **PersistentVolumeClaim**: Allows pods to access the dump directory
3. **CronJob**: Automatically cleans up old dump files

## Troubleshooting

### Verify Configuration

Check if coredumps are enabled:
```bash
# Check core pattern
sysctl kernel.core_pattern

# Check suid_dumpable
sysctl fs.suid_dumpable

# List existing dumps
ls -la /var/crashdumps/
```

### Common Issues

1. **Permission Denied**: Ensure `/var/crashdumps` has 777 permissions
2. **No Dumps Generated**: Verify the application is marked as dumpable (see `Program.cs`)
3. **Storage Full**: Check the CronJob is running for cleanup

## Security Considerations

- The coredump directory has 777 permissions to allow container processes to write
- Dump files may contain sensitive application data
- Implement proper access controls and retention policies
- Consider encryption for sensitive environments

## References

- [.NET Diagnostics in Containers](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostics-in-containers)
- [Linux Core Dump Documentation](https://man7.org/linux/man-pages/man5/core.5.html)
