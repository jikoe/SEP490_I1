import React from 'react';
import { createRoot } from 'react-dom/client';
import App from './app/CustomerPortalApp';
import './styles/app.css';
import './styles/customer-portal.css';

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
