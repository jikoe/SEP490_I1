// Bearer sessions take precedence; legacy Demo selectors are Development-only fixtures.
/**
 * Sends JSON to the same-origin API with the current Bearer session or local fixture and optional
 * cancellation. Non-success responses retain the stable error code/status for screen recovery;
 * keeps authentication credentials out of persistent browser storage.
 */
let sessionToken;

export function getSessionToken() { return sessionToken; }

export function setSessionToken(token) { sessionToken = token; }

export async function request(path, { fixture, token = sessionToken, signal, method = 'GET', body } = {}) {
  const response = await fetch(`/api${path}`, {
    method,
    signal,
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` }
        : fixture ? { Authorization: `Demo ${fixture}` } : {}),
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.title || 'Không thể tải dữ liệu. Vui lòng thử lại.');
    error.code = data.code;
    error.status = response.status;
    throw error;
  }
  if (data && typeof data === 'object' && !Array.isArray(data)) {
    data.currentActorId = Number(response.headers.get('X-Demo-Actor-Id')) || null;
  }
  return data;
}

/**
 * Serializes supplied filters while omitting empty/unset values, preserving explicit false and
 * zero rather than changing their meaning.
 */
export function queryString(values) {
  return new URLSearchParams(Object.entries(values).filter(([, value]) =>
    value !== '' && value !== null && value !== undefined)).toString();
}
