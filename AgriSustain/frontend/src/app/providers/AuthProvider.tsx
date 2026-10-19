import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../../api/authApi';
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
const AUTH_STORAGE_KEY = 'agrisustain-auth';

function readStoredSession(): UserSession | null {
  const storedValue = sessionStorage.getItem(AUTH_STORAGE_KEY);
  if (!storedValue) {
    return null;
  }

  try {
    return JSON.parse(storedValue) as UserSession;
  } catch {
    sessionStorage.removeItem(AUTH_STORAGE_KEY);
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSession | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const savedSession = readStoredSession();
    setUser(savedSession);
    setIsLoading(false);
  }, []);

  const persistSession = (nextUser: UserSession) => {
    setUser(nextUser);
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(nextUser));
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
      await authApi.logout();
    } finally {
      setUser(null);
      sessionStorage.removeItem(AUTH_STORAGE_KEY);
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
