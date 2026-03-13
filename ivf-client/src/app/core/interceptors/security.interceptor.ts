import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';

/**
 * Zero Trust Security Interceptor.
 *
 * Adds security context headers to every outgoing request:
 * - X-Device-Fingerprint: Browser-generated device identity
 * - X-Session-Id: Current session identifier
 * - X-Correlation-Id: Request tracing identifier
 * - X-Requested-With: CSRF protection header
 *
 * Inspired by:
 * - Google BeyondCorp: Device-aware access control
 * - Microsoft Conditional Access: Session binding
 * - AWS Zero Trust: Request context enrichment
 */
export const securityInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
) => {
  const headers: Record<string, string> = {
    'X-Requested-With': 'XMLHttpRequest', // CSRF protection — blocks non-AJAX requests
    'X-Correlation-Id': generateCorrelationId(),
  };

  // Device fingerprint
  const fingerprint = getDeviceFingerprint();
  if (fingerprint) {
    headers['X-Device-Fingerprint'] = fingerprint;
  }

  // Session ID
  const sessionId = sessionStorage.getItem('zt_session_id');
  if (sessionId) {
    headers['X-Session-Id'] = sessionId;
  }

  const secureReq = req.clone({ setHeaders: headers });
  return next(secureReq);
};

/**
 * Generates a deterministic device fingerprint from browser signals.
 * Uses SHA-256 hash via Web Crypto API for collision resistance.
 * NO PII is collected — only hardware/software characteristics.
 */
function getDeviceFingerprint(): string | null {
  try {
    const cached = sessionStorage.getItem('zt_device_fingerprint');
    if (cached) return cached;

    const components = [
      navigator.userAgent,
      navigator.language,
      navigator.platform,
      Intl.DateTimeFormat().resolvedOptions().timeZone,
      `${screen.width}x${screen.height}`,
    ].join('|');

    // Synchronous SHA-256 using SubtleCrypto is not possible,
    // so compute async and cache. Use a temporary sync hash for the first request.
    computeSha256Fingerprint(components);

    // For the initial request, use a sync fallback (will be upgraded on next request)
    const fingerprint = syncSha256Fallback(components);
    return fingerprint;
  } catch {
    return null;
  }
}

/**
 * SHA-256 fingerprint via Web Crypto API (async).
 * Result is cached in sessionStorage for subsequent requests.
 */
async function computeSha256Fingerprint(input: string): Promise<void> {
  try {
    const encoder = new TextEncoder();
    const data = encoder.encode(input);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    const hashHex = hashArray.map((b) => b.toString(16).padStart(2, '0')).join('');
    sessionStorage.setItem('zt_device_fingerprint', hashHex);
  } catch {
    // Web Crypto unavailable (e.g., non-secure context) — keep sync fallback
  }
}

/**
 * Synchronous SHA-256-like hash for the initial request before async completes.
 * Uses a stronger 64-bit hash to reduce collision probability vs the old 32-bit DJB2.
 */
function syncSha256Fallback(str: string): string {
  let h1 = 0xdeadbeef;
  let h2 = 0x41c6ce57;
  for (let i = 0; i < str.length; i++) {
    const ch = str.charCodeAt(i);
    h1 = Math.imul(h1 ^ ch, 2654435761);
    h2 = Math.imul(h2 ^ ch, 1597334677);
  }
  h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507);
  h1 ^= Math.imul(h2 ^ (h2 >>> 13), 3266489909);
  h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507);
  h2 ^= Math.imul(h1 ^ (h1 >>> 13), 3266489909);
  const combined = 4294967296 * (2097151 & h2) + (h1 >>> 0);
  const hex = combined.toString(16).padStart(16, '0');
  sessionStorage.setItem('zt_device_fingerprint', hex);
  return hex;
}

/**
 * Generates a unique correlation ID for request tracing.
 */
function generateCorrelationId(): string {
  const timestamp = Date.now().toString(36);
  const random = Math.random().toString(36).substring(2, 8);
  return `${timestamp}-${random}`;
}
