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
 * Uses the same algorithm as the backend DeviceFingerprintService.
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

    // SHA-256 hash via Web Crypto API is async, so we use a simple hash for the header
    // The server will do its own fingerprint calculation for verification
    const fingerprint = simpleHash(components);
    sessionStorage.setItem('zt_device_fingerprint', fingerprint);
    return fingerprint;
  } catch {
    return null;
  }
}

/**
 * Simple deterministic hash for device fingerprinting.
 * The server performs its own SHA-256 fingerprint for verification.
 */
function simpleHash(str: string): string {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    const char = str.charCodeAt(i);
    hash = (hash << 5) - hash + char;
    hash = hash & hash; // Convert to 32-bit integer
  }
  return Math.abs(hash).toString(16).padStart(8, '0');
}

/**
 * Generates a unique correlation ID for request tracing.
 */
function generateCorrelationId(): string {
  const timestamp = Date.now().toString(36);
  const random = Math.random().toString(36).substring(2, 8);
  return `${timestamp}-${random}`;
}
