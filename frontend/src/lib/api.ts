// Lightweight API client using EXPO_PUBLIC_BACKEND_URL.
const BASE = process.env.EXPO_PUBLIC_BACKEND_URL;

export type ApiOptions = {
  token?: string | null;
  body?: any;
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE';
};

export async function api<T = any>(path: string, opts: ApiOptions = {}): Promise<T> {
  const url = `${BASE}/api${path}`;
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (opts.token) headers['Authorization'] = `Bearer ${opts.token}`;
  const res = await fetch(url, {
    method: opts.method ?? (opts.body ? 'POST' : 'GET'),
    headers,
    body: opts.body ? JSON.stringify(opts.body) : undefined,
  });
  const text = await res.text();
  let data: any = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!res.ok) {
    const msg = (data && (data.detail || data.message)) || `Request failed (${res.status})`;
    throw new Error(typeof msg === 'string' ? msg : 'Request failed');
  }
  return data as T;
}
