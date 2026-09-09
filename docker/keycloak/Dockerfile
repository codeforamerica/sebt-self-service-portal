ARG KEYCLOAK_IMAGE=quay.io/keycloak/keycloak:26.2@sha256:4883630ef9db14031cde3e60700c9a9a8eaf1b5c24db1589d6a2d43de38ba2a9

FROM ${KEYCLOAK_IMAGE} AS builder

WORKDIR /opt/keycloak

COPY sebt-realm.preview.json /opt/keycloak/data/import/sebt-realm.json
COPY themes /opt/keycloak/themes

ENV KC_HEALTH_ENABLED=true \
    KC_HTTP_ENABLED=true \
    KC_DB=postgres

RUN /opt/keycloak/bin/kc.sh build

FROM ${KEYCLOAK_IMAGE}

COPY --from=builder /opt/keycloak/ /opt/keycloak/
COPY sebt-realm.preview.json /opt/keycloak/data/import/sebt-realm.json
COPY themes /opt/keycloak/themes

# Runtime DB/hostname/admin settings come from ECS task environment.
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized", "--import-realm", "--http-enabled=true", "--proxy-headers=xforwarded", "--hostname-strict=false"]
