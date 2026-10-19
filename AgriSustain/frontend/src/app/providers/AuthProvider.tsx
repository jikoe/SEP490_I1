import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../../api/authApi';
import { setSessionToken } from '../../api/client';
import type { LoginRequest, UserSession } from '../../types/auth';

interface AuthContextValue {
  user: UserSession | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);
const SESSION_FLAG = 'agrisustain-has-session';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    sessionStorage.removeItem('agrisustain-auth');
    if (sessionStorage.getItem(SESSION_FLAG) !== 'true') {
      setIsLoading(false);
      return;
    }
    let active = true;
    authApi.refresh().then(response => {
      if (active) persistSession(response as UserSession);
    }).catch(() => {
      if (active) sessionStorage.removeItem(SESSION_FLAG);
    }).finally(() => {
      if (active) setIsLoading(false);
    });
    return () => { active = false; };
  }, []);

  const persistSession = (nextUser: UserSession) => {
    setSessionToken(nextUser.accessToken);
    setUser(nextUser);
    sessionStorage.setItem(SESSION_FLAG, 'true');
  };

  const login = async (email: string, password: string) => {
    const payload: LoginRequest = { email, password };
    const response = await authApi.login(payload);

    persistSession({
      accountId: response.accountId,
      email: response.email,
      fullName: response.fullName,
      role: response.role as UserSession['role'],
      accessToken: response.accessToken,
      accessTokenExpiresAt: response.accessTokenExpiresAt,
      refreshTokenExpiresAt: response.refreshTokenExpiresAt
    });
  };

  const refreshSession = async () => {
    const response = await authApi.refresh();
    persistSession({
      accountId: response.accountId,
      email: response.email,
      fullName: response.fullName,
      role: response.role as UserSession['role'],
      accessToken: response.accessToken,
      accessTokenExpiresAt: response.accessTokenExpiresAt,
      refreshTokenExpiresAt: response.refreshTokenExpiresAt
    });
  };

  const logout = async () => {
    try {
      await authApi.logout(user?.accessToken);
    } finally {
      setUser(null);
      setSessionToken(undefined);
      sessionStorage.removeItem(SESSION_FLAG);
    }
  };

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: Boolean(user),
      isLoading,
      login,
      logout,
      refreshSession
    }),
    [user, isLoading]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error('useAuthContext must be used inside AuthProvider');
  }

  return context;
}
