#!/usr/bin/env bash
# Print random values for .env CHANGE_ME fields. Copy output into docker/compose/.env
set -euo pipefail

echo "# Paste these into /opt/testgram/docker/compose/.env"
echo ""
echo "RabbitMQ__Connections__Default__Password=$(openssl rand -hex 24)"
echo "Minio__SecretKey=$(openssl rand -hex 24)"
echo "App__AccessHashSecretKey=$(openssl rand -hex 32)"
echo "App__EncryptionConfig__MessageKeys__0__Key=$(openssl rand -base64 32)"
echo "App__EncryptionConfig__IndexKeys__0__Key=$(openssl rand -base64 32)"
echo ""
echo "# BOT_TOKEN=...  # get from @BotFather manually"