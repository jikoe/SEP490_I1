import type { ReactNode } from 'react';
import { AuthProvider } from './providers/AuthProvider';

interface AppRootProps {
  children: ReactNode;
}

export function AppRoot({ children }: AppRootProps) {
  return <AuthProvider>{children}</AuthProvider>;
}
