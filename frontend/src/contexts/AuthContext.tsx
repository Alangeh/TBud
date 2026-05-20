import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { storage } from '@/src/utils/storage';
import { api } from '@/src/lib/api';

export type User = {
  user_id: string;
  email: string;
  name: string;
  picture?: string | null;
  bio?: string;
  verified: boolean;
  review_count: number;
  follower_count: number;
  following_count: number;
  countries_visited: string[];
  created_at?: string;
};

type AuthState = {
  user: User | null;
  token: string | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<User>;
  register: (email: string, password: string, name: string) => Promise<User>;
  googleSession: (sessionToken: string) => Promise<User>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  setUser: (u: User) => void;
};

const Ctx = createContext<AuthState | undefined>(undefined);
const TOKEN_KEY = 'tr_token';

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUserState] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const persistToken = async (t: string | null) => {
    setToken(t);
    if (t) await storage.secureSet(TOKEN_KEY, t);
    else await storage.secureRemove(TOKEN_KEY);
  };

  const refresh = useCallback(async () => {
    const t = await storage.secureGet<string>(TOKEN_KEY, '');
    if (!t) { setUserState(null); setToken(null); setLoading(false); return; }
    try {
      const res = await api<{ user: User }>('/auth/me', { token: t });
      setUserState(res.user);
      setToken(t);
    } catch {
      await storage.secureRemove(TOKEN_KEY);
      setUserState(null);
      setToken(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { refresh(); }, [refresh]);

  const login = async (email: string, password: string) => {
    const res = await api<{ token: string; user: User }>('/auth/login', { body: { email, password } });
    await persistToken(res.token);
    setUserState(res.user);
    return res.user;
  };

  const register = async (email: string, password: string, name: string) => {
    const res = await api<{ token: string; user: User }>('/auth/register', { body: { email, password, name } });
    await persistToken(res.token);
    setUserState(res.user);
    return res.user;
  };

  const googleSession = async (sessionToken: string) => {
    const res = await api<{ token: string; user: User }>('/auth/google/session', { body: { session_token: sessionToken } });
    await persistToken(res.token);
    setUserState(res.user);
    return res.user;
  };

  const logout = async () => {
    try { if (token) await api('/auth/logout', { token, method: 'POST' }); } catch {}
    await persistToken(null);
    setUserState(null);
  };

  return (
    <Ctx.Provider value={{ user, token, loading, login, register, googleSession, logout, refresh, setUser: setUserState }}>
      {children}
    </Ctx.Provider>
  );
}

export function useAuth() {
  const v = useContext(Ctx);
  if (!v) throw new Error('useAuth must be inside AuthProvider');
  return v;
}
