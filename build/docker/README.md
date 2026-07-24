# Local Docker image builds (FamilyGram-Server)

Scripts tag images as `mytelegram/<service>` by default. For compose to use a local build, set
`REGISTRY_URL` to match `FamilyGramServerRegistry` in `docker/compose/.env` (usually
`ghcr.io/cyberoniontoni/familygram-server`).

CI on **`main`** publishes `:latest` to GHCR; prefer that for production.

### linux/amd64 (build)

```bash
export REGISTRY_URL="ghcr.io/cyberoniontoni/familygram-server"
./build-all-amd64.sh
```

### linux/arm64 (build)

```bash
export REGISTRY_URL="ghcr.io/cyberoniontoni/familygram-server"
./build-all-arm64.sh
```

### linux/amd64 & linux/arm64 (build and push)

```bash
export REGISTRY_URL="ghcr.io/cyberoniontoni/familygram-server"
./build-and-push-all-amd64-arm64.sh
```
