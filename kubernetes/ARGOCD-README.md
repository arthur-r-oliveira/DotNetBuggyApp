# ArgoCD Application Manifests for Red Hat ACM

This directory contains ArgoCD Application manifests specifically configured for use with Red Hat Advanced Cluster Management (ACM).

## Files

### 1. `argocd-application.yaml`
Basic ArgoCD Application manifest with standard ACM integration.

**Features:**
- Standard ArgoCD Application configuration
- ACM-specific annotations and labels
- Automated sync with self-healing
- Health checks for key resources
- Kustomize-based resource management

### 2. `argocd-application-acm.yaml`
Advanced ArgoCD Application manifest with enhanced ACM features.

**Features:**
- Enhanced ACM compliance annotations
- Policy and governance labels
- Multi-cluster support annotations
- Advanced sync policies
- Compliance and risk level metadata
- Additional health checks

## Prerequisites

1. **ArgoCD installed** in your cluster
2. **Red Hat ACM** configured and running
3. **Repository access** - Update the `repoURL` in both manifests with your actual Git repository URL
4. **Proper RBAC** - Ensure ArgoCD has necessary permissions to create resources in the target namespace

## Usage

### Option 1: Basic Deployment
```bash
# Apply the basic ArgoCD Application
kubectl apply -f argocd-application.yaml
```

### Option 2: ACM-Enhanced Deployment
```bash
# Apply the ACM-enhanced ArgoCD Application
kubectl apply -f argocd-application-acm.yaml
```

## Configuration

### Repository URL
Update the `repoURL` field in both manifests:
```yaml
source:
  repoURL: https://github.com/your-org/DotNetBuggyApp.git  # Update this
```

### Target Cluster
The manifests are configured to deploy to the local cluster. For multi-cluster deployments, update the destination:
```yaml
destination:
  server: https://your-target-cluster-api-server
  namespace: dotnet-memory-leak-app
```

### Sync Policy
Both manifests include automated sync with self-healing. To disable automated sync:
```yaml
syncPolicy:
  automated: null  # Remove or comment out the automated section
```

## ACM Integration Features

### Compliance and Governance
- **Policy Categories**: CM Configuration Management
- **Controls**: CM-2 Baseline Configuration
- **Standards**: NIST SP 800-53
- **Risk Level**: Medium

### Resource Management
- **Namespace**: `dotnet-memory-leak-app`
- **Resources**: Deployment, Service, PVC, RBAC, Route
- **Image**: `quay.io/rhn_support_arolivei/dotnet-memory-leak-app:v1`

### Health Monitoring
The applications include health checks for:
- Service availability
- Deployment readiness
- PVC status (ACM-enhanced version)

## Troubleshooting

### Common Issues

1. **Sync Failures**
   - Check repository access permissions
   - Verify target namespace exists or has proper RBAC
   - Review ArgoCD logs: `kubectl logs -n argocd -l app.kubernetes.io/name=argocd-application-controller`

2. **ACM Integration Issues**
   - Ensure ACM is properly installed and configured
   - Verify cluster is registered with ACM
   - Check ACM operator logs

3. **Resource Creation Issues**
   - Verify ServiceAccount has necessary permissions
   - Check for SCC (Security Context Constraint) conflicts
   - Review resource quotas and limits

### Useful Commands

```bash
# Check ArgoCD Application status
kubectl get applications -n argocd

# View application details
kubectl describe application dotnet-memory-leak-app -n argocd

# Check sync status
argocd app get dotnet-memory-leak-app

# Force sync
argocd app sync dotnet-memory-leak-app

# View application logs
kubectl logs -n argocd -l app.kubernetes.io/name=argocd-application-controller
```

## Customization

### Environment-Specific Configurations
Use Kustomize overlays to customize for different environments:

```yaml
# kustomization.yaml
resources:
  - argocd-application.yaml
patches:
  - target:
      kind: Application
      name: dotnet-memory-leak-app
    patch: |-
      - op: replace
        path: /spec/source/kustomize/images/0/newTag
        value: "v2"  # Different image tag for different environment
```

### Multi-Cluster Deployment
For deploying across multiple clusters managed by ACM, create cluster-specific applications or use ACM's placement policies.

## Security Considerations

- The application uses non-root containers with restricted security contexts
- RBAC is configured with minimal required permissions
- Security Context Constraints (SCC) are applied for OpenShift compatibility
- All containers run with read-only root filesystems

## Support

For issues related to:
- **ArgoCD**: Check ArgoCD documentation and community support
- **Red Hat ACM**: Contact Red Hat support or check ACM documentation
- **Application-specific issues**: Review the main application documentation