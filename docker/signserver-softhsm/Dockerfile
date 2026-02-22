# =====================================================
# SignServer CE + SoftHSM2 — PKCS#11 Crypto Token Support
# =====================================================
# Extends official SignServer CE with SoftHSM2 for FIPS 140-2 Level 1
# software-based HSM. Provides PKCS#11 key storage that prevents
# key extraction (unlike P12CryptoToken where keys are files).
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

# Install SoftHSM2 + PKCS#11 tools
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    softhsm2 \
    opensc \
    libengine-pkcs11-openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create SoftHSM2 directories with correct permissions
RUN mkdir -p /opt/keyfactor/persistent/softhsm/tokens && \
    chown -R 10001:root /opt/keyfactor/persistent/softhsm && \
    chmod 700 /opt/keyfactor/persistent/softhsm

# SoftHSM2 configuration
RUN echo "directories.tokendir = /opt/keyfactor/persistent/softhsm/tokens\n\
    objectstore.backend = file\n\
    log.level = INFO\n\
    slots.removable = false\n\
    slots.mechanisms = ALL" > /etc/softhsm2.conf

# Set SOFTHSM2_CONF environment variable so SignServer/Java can find it
ENV SOFTHSM2_CONF=/etc/softhsm2.conf

# Add shared library reference for SignServer PKCS#11
# SignServer looks for shared libraries in its configuration
ENV PKCS11_LIBRARY_PATH=/usr/lib/softhsm/libsofthsm2.so

# Switch back to SignServer user
USER 10001

LABEL org.opencontainers.image.title="SignServer CE + SoftHSM2"
LABEL org.opencontainers.image.description="SignServer CE with SoftHSM2 PKCS#11 provider for FIPS 140-2 Level 1 compliance"
LABEL ivf.security.phase="4"
