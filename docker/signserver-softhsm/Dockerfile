# =====================================================
# SignServer CE + SoftHSM2 — PKCS#11 Crypto Token Support
# =====================================================
# Extends official SignServer CE (AlmaLinux 9) with SoftHSM2 for
# FIPS 140-2 Level 1 software-based HSM. Provides PKCS#11 key
# storage that prevents key extraction (unlike P12CryptoToken).
#
# Usage:
#   docker compose --profile softhsm up -d
#   docker exec ivf-signserver bash /opt/keyfactor/persistent/init-softhsm.sh
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

# Set SOFTHSM2_CONF environment variable so SignServer/Java can find it
ENV SOFTHSM2_CONF=/etc/softhsm2.conf

# Detect SoftHSM library path (AlmaLinux puts it in /usr/lib64)
ENV PKCS11_LIBRARY_PATH=/usr/lib64/pkcs11/libsofthsm2.so

# Switch back to SignServer user
USER 10001

LABEL org.opencontainers.image.title="SignServer CE + SoftHSM2"
LABEL org.opencontainers.image.description="SignServer CE with SoftHSM2 PKCS#11 provider for FIPS 140-2 Level 1 compliance"
LABEL ivf.security.phase="4"
