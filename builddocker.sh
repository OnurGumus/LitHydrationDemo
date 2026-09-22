#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

# Explicit context prevents a changed kubectl default from targeting another cluster.
KUBE_CONTEXT="${KUBE_CONTEXT:-microk8s-low}"
# Timestamp distinguishes builds made from the same commit with local changes.
TAG="$(date -u +%Y%m%d-%H%M%S)-$(git rev-parse --short HEAD)"
IMAGE="docker.3dpack.ing/litdemo:${TAG}"

docker buildx build --platform linux/amd64 --tag "$IMAGE" --push .

# Render locally, validate against the API, then apply exactly what was validated.
MANIFEST=$(mktemp)
trap 'rm -f "$MANIFEST"' EXIT
sed "s|image: docker.3dpack.ing/litdemo:deploy|image: $IMAGE|" kubernetes-deployment.yaml > "$MANIFEST"
kubectl --context "$KUBE_CONTEXT" apply --dry-run=server -f "$MANIFEST"
kubectl --context "$KUBE_CONTEXT" apply -f "$MANIFEST"
kubectl --context "$KUBE_CONTEXT" -n default rollout status deployment/litdemo --timeout=180s
printf '\nDeployed %s\nhttps://litdemo.novian.works\n' "$IMAGE"
