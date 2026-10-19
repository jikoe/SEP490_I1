import React, { useState } from 'react';
import { useAuth } from '../hooks/useAuth';
import { request } from '../api/client';
import MarketplaceHeader from '../layouts/MarketplaceHeader';
import MarketplaceScreen from '../features/marketplace/MarketplaceScreen';
import ProductDetailScreen from '../features/marketplace/ProductDetailScreen';
import ChangePasswordScreen from '../features/authentication/ChangePasswordScreen';
import MarketLoginScreen from '../features/authentication/MarketLoginScreen';
import { BotanicalLeaves } from '../features/marketplace/MarketIcons';
import '../styles/marketplace.css';

export default function MarketplaceApp({ route, previousRoute }) {
  const { user, isLoading } = useAuth();
  const [path, query = ''] = route.split('?');
  const criteria = Object.fromEntries(new URLSearchParams(query));
  const [notice, setNotice] = useState('');
  const [adding, setAdding] = useState(null);
  const password = path === '/change-password';
  const login = path === '/login' || (password && !user);
  const productId = path.match(/^\/products\/(\d+)$/)?.[1];
  function onCriteria(values) {
    const query = new URLSearchParams(Object.entries(values).filter(([, value]) => value !== '' && value != null));
    window.location.hash = `/marketplace${query.size ? `?${query}` : ''}`;
    setNotice('');
  }
  async function addToCart(product, quantity = 1) {
    if (!user) {
      window.location.hash = `/login?returnTo=${encodeURIComponent(route)}`;
      return;
    }
    if (user.role !== 'RetailCustomer') {
      setNotice('Giỏ hàng bán lẻ dành cho tài khoản người mua lẻ.');
      return;
    }
    setAdding(product.id); setNotice('');
    try {
      await request('/carts/current/items', { token: user.accessToken, method: 'POST', body: { productId: product.id, quantity } });
      setNotice(`Đã thêm ${quantity} ${product.unit} ${product.name} vào giỏ hàng.`);
    } catch (error) { setNotice(error.message); }
    finally { setAdding(null); }
  }
  if (isLoading) return <p role="status">Đang tải phiên đăng nhập...</p>;
  return <div className={`market-shell ${password || login ? 'market-auth-shell' : ''}`}>
    <MarketplaceHeader key={password || login ? 'auth' : path} simple={password || login}
      keyword={criteria.keyword || ''} onSearch={keyword => onCriteria({ ...criteria, keyword, page: 1 })} />
    <BotanicalLeaves />
    <BotanicalLeaves right />
    {login ? <MarketLoginScreen returnTo={password ? '/change-password'
      : criteria.returnTo?.startsWith('/') && !criteria.returnTo.startsWith('//') ? criteria.returnTo : '/marketplace'} />
      : password ? <ChangePasswordScreen onCancel={() => { window.location.hash = previousRoute || '/marketplace'; }} />
        : productId ? <ProductDetailScreen key={productId} id={productId} onAdd={addToCart} adding={adding} notice={notice} />
          : <MarketplaceScreen criteria={criteria} onCriteria={onCriteria} onAdd={addToCart} adding={adding} notice={notice} />}
  </div>;
}
