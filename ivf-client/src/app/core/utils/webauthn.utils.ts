/**
 * Convert a base64url or base64 encoded string to an ArrayBuffer.
 */
export function coerceToArrayBuffer(input: string | ArrayBuffer): ArrayBuffer {
  if (input instanceof ArrayBuffer) return input;

  // base64url -> base64
  let base64 = input.replace(/-/g, '+').replace(/_/g, '/');
  // Pad if needed
  while (base64.length % 4 !== 0) base64 += '=';

  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
}

/**
 * Convert a Uint8Array to a base64url encoded string.
 */
export function coerceToBase64Url(bytes: Uint8Array): string {
  let binary = '';
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
