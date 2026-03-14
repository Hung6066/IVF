# =====================================================
# SignServer CE + SoftHSM2 — PKCS#11 Crypto Token Support
# =====================================================
# Extends official SignServer CE (AlmaLinux 9) with SoftHSM2 for
# FIPS 140-2 Level 1 software-based HSM. Provides PKCS#11 key
# storage that prevents key extraction (unlike P12CryptoToken).
#
# Usage:
#   docker compose --profile softhsm up -d
#   bash scripts/setup-enterprise-pki.sh
#
# PKCS#11 advantages over P12:
#   - Keys cannot be exported/copied (non-extractable flag)
#   - FIPS 140-2 Level 1 compliance
#   - PKCS#11 standard — easy migration to hardware HSM
#   - PIN-protected access
# =====================================================

FROM keyfactor/signserver-ce:latest

USER root

# Install SoftHSM2 + PKCS#11 tools (AlmaLinux 9 minimal — uses microdnf)
RUN microdnf install -y \
    softhsm \
    opensc \
    p11-kit && \
    microdnf clean all && \
    rm -rf /var/cache/yum /var/cache/dnf

# Create SoftHSM2 directories with correct permissions
RUN mkdir -p /opt/keyfactor/persistent/softhsm/tokens && \
    chown -R 10001:root /opt/keyfactor/persistent/softhsm && \
    chmod 700 /opt/keyfactor/persistent/softhsm

# SoftHSM2 configuration
RUN printf 'directories.tokendir = /opt/keyfactor/persistent/softhsm/tokens\nobjectstore.backend = file\nlog.level = INFO\nslots.removable = false\nslots.mechanisms = ALL\n' > /etc/softhsm2.conf

# Register SoftHSM2 PKCS#11 library in SignServer deploy config.
# CRITICAL: This must be done at build time — SignServer CE only loads
# SHAREDLIBRARYNAME entries that were present during deployment.
RUN for f in \
      /opt/keyfactor/signserver-custom/conf/signserver_deploy.properties \
      /opt/keyfactor/signserver/conf/signserver_deploy.properties; do \
    if [ -f "$f" ] && ! grep -q 'SoftHSM' "$f" 2>/dev/null; then \
      printf '\n# SoftHSM2 PKCS#11 provider (added at build time)\ncryptotoken.p11.lib.83.name = SoftHSM\ncryptotoken.p11.lib.83.file = /usr/lib64/pkcs11/libsofthsm2.so\n' >> "$f"; \
    fi; \
    done

# Re-deploy SignServer to pick up the new PKCS#11 library registration.
# SignServer CE reads signserver_deploy.properties only during 'ant deploy'.
RUN cd /opt/keyfactor/signserver && \
    if [ -f bin/ant ]; then \
      bin/ant deploy 2>/dev/null || true; \
    elif command -v ant >/dev/null 2>&1; then \
      ant deploy 2>/dev/null || true; \
    fi

# Set environment variables
ENV SOFTHSM2_CONF=/etc/softhsm2.conf
ENV PKCS11_LIBRARY_PATH=/usr/lib64/pkcs11/libsofthsm2.so

# Switch back to SignServer user
USER 10001

LABEL org.opencontainers.image.title="SignServer CE + SoftHSM2"
LABEL org.opencontainers.image.description="SignServer CE with SoftHSM2 PKCS#11 provider for FIPS 140-2 Level 1 compliance"
LABEL ivf.security.phase="4"
