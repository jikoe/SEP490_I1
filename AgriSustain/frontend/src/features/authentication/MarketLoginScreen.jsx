import React, { useState } from 'react';
import { useAuth } from '../../hooks/useAuth';

export default function MarketLoginScreen({ returnTo = '/marketplace' }) {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  return <main className="password-page"><section className="password-panel market-login-panel">
    <h1>Đăng nhập</h1><p className="password-subtitle">Chào mừng bạn đến với Nông Sản Việt</p>
    <form onSubmit={async event => {
      event.preventDefault(); setPending(true); setError('');
      try { await login(email, password); window.location.hash = returnTo; }
      catch { setError('Email hoặc mật khẩu không chính xác, hoặc tài khoản không còn hoạt động.'); }
      finally { setPending(false); }
    }}><label>Email<input type="email" required value={email} autoComplete="username"
      onChange={event => setEmail(event.target.value)} /></label>
      <label>Mật khẩu<input type="password" required value={password} autoComplete="current-password"
        onChange={event => setPassword(event.target.value)} /></label>
      {error && <p role="alert" className="market-field-error">{error}</p>}
      <button className="market-primary" disabled={pending}>{pending ? 'Đang đăng nhập...' : 'Đăng nhập'}</button>
      <a href="#/marketplace">Tiếp tục khám phá sản phẩm</a>
    </form></section></main>;
}
