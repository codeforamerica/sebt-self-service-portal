#!/bin/bash
set -e

# Generates TLS certificates for local Redis development.
# Run from the repo root: ./scripts/dev/gen-redis-certs.sh
#
# Output (all gitignored):
#   certs/redisCA.{crt,key}        Self-signed CA certificate
#   certs/redis-server.{crt,key}   Server cert (SAN: redis, localhost, 127.0.0.1)
#
# The certs/ directory is bind-mounted into the Redis container by compose.yaml.
# The portal connects with Ssl=true; AcceptSelfSignedCertificates bypasses CA
# trust for dev. In production, Elasticache presents an AWS-signed cert that
# .NET trusts without any bypass.
#
# Certs are generated idempotently — existing files are not overwritten.
# Re-run if Redis TLS stops working (server cert expires after one year).

mkdir -p certs

# CA key and self-signed certificate (10-year validity)
[ -f certs/redisCA.key ] || openssl genrsa -out certs/redisCA.key 4096
[ -f certs/redisCA.crt ] || openssl req \
    -x509 -new -nodes -sha256 \
    -key certs/redisCA.key \
    -days 3650 \
    -subj '/O=SEBT Dev/CN=Redis Dev CA' \
    -out certs/redisCA.crt

# Extension config: server cert with SANs for docker hostname and localhost
SSL_CONF=certs/redis-openssl.cnf
cat > "$SSL_CONF" <<_END_
[ server_cert ]
keyUsage = digitalSignature, keyEncipherment
nsCertType = server
subjectAltName = DNS:redis,DNS:localhost,IP:127.0.0.1
_END_

# Server key and certificate (1-year validity, signed by CA above)
[ -f certs/redis-server.key ] || openssl genrsa -out certs/redis-server.key 2048
[ -f certs/redis-server.crt ] || openssl req \
    -new -sha256 \
    -subj '/O=SEBT Dev/CN=redis' \
    -key certs/redis-server.key | \
    openssl x509 \
        -req -sha256 \
        -CA certs/redisCA.crt \
        -CAkey certs/redisCA.key \
        -CAserial certs/redisCA.txt \
        -CAcreateserial \
        -days 365 \
        -extfile "$SSL_CONF" -extensions server_cert \
        -out certs/redis-server.crt

rm -f "$SSL_CONF"

echo "Redis TLS certs written to certs/"
echo "  CA:     certs/redisCA.crt"
echo "  Server: certs/redis-server.crt"
