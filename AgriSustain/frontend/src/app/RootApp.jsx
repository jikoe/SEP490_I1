import React, { useEffect, useRef, useState } from 'react';
import { AuthProvider } from './providers/AuthProvider';
import MarketplaceApp from './MarketplaceApp';
import CustomerPortalApp from './CustomerPortalApp';
import AdministrationApp from './App';

export default function RootApp() {
  const [route, setRoute] = useState(() => window.location.hash.slice(1) || '/marketplace');
  const previous = useRef('/marketplace');
  useEffect(() => {
    const handleRoute = () => setRoute(current => {
      if (!current.startsWith('/change-password') && !current.startsWith('/login')) previous.current = current;
      return window.location.hash.slice(1) || '/marketplace';
    });
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);
  return <AuthProvider>{/^\/(register|profile|orders)$/.test(route) ? <CustomerPortalApp />
    : route === '/checkout' || route.startsWith('/administration/') ? <AdministrationApp />
      : <MarketplaceApp route={route} previousRoute={previous.current} />}</AuthProvider>;
}
