import React, { useEffect, useState } from 'react';
import ProductOffers from '../features/commerce/ProductOffers';
import RegisterScreen from '../features/authentication/RegisterScreen';
import ProfileScreen from '../features/profiles/ProfileScreen';
import OrderTrackingScreen from '../features/orders/OrderTrackingScreen';
import CustomerHeader from '../layouts/CustomerHeader';
import CustomerFooter from '../layouts/CustomerFooter';
import ProductManagement from '../features/commerce/ProductManagement';
import ShoppingCart from '../features/commerce/ShoppingCart';
import Fulfillment from '../features/commerce/Fulfillment';
import CheckoutScreen from '../features/orders/CheckoutScreen';
import '../styles/customer-portal.css';
import { useAuth } from '../hooks/useAuth';

/**
 * CustomerPortalApp (Sàn Nông Sản Việt - AgriSustain)
 * Thiết kế giao diện chuẩn Figma SCR-TRC-01 cho Iteration 1 của Dự (DuND):
 * - SCR-07: Đăng ký tài khoản (UC-AUTH-01 / UC-AUTH-08)
 * - SCR-11: Quản lý hồ sơ & địa chỉ (UC-AUTH-03)
 * - SCR-16: Lịch sử & Theo dõi đơn hàng (UC-RC-04)
 */

function readPortalRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(register|profile|orders|products|cart|checkout|farmer\/products|farmer\/orders|receipts)$/.test(path) ? path : '/orders';
}

function readPortalFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return selected || 'retail-customer';
}

function readStoredUser() {
  try {
    const raw = sessionStorage.getItem('agrisustain:current-user');
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function navigate(path) {
  window.location.hash = path;
}

export default function CustomerPortalApp() {
  const { user: sessionUser, isLoading } = useAuth();
  const [fixture, setFixture] = useState(readPortalFixture);
  const [currentUser, setCurrentUser] = useState(readStoredUser);
  const [route, setRoute] = useState(readPortalRoute);

  const isFarmer = sessionUser ? sessionUser.role === 'Farmer' : currentUser ? currentUser.role === 'Farmer' : fixture === 'farmer';
  const roleName = sessionUser?.role || currentUser?.role || (isFarmer ? 'Farmer' : 'RetailCustomer');
  const userName = sessionUser ? sessionUser.fullName : currentUser ? currentUser.fullName : (isFarmer ? 'Nguyễn Văn Hùng (Farmer)' : 'Lê Thị Mai (RetailBuyer)');
  const userEmail = sessionUser ? sessionUser.email : currentUser ? currentUser.email : (isFarmer ? 'nongtrai.dalat@gmail.com' : 'khachle.hcm@gmail.com');

  const screen = route === '/register' ? 'register'
    : route === '/profile' ? 'profile'
    : route === '/orders' ? 'orders' : 'commerce';

  useEffect(() => {
    const handleRoute = () => setRoute(readPortalRoute());
    const handleUser = () => {
      setCurrentUser(readStoredUser());
      setFixture(readPortalFixture());
    };
    window.addEventListener('hashchange', handleRoute);
    window.addEventListener('agrisustain:user-changed', handleUser);
    return () => {
      window.removeEventListener('hashchange', handleRoute);
      window.removeEventListener('agrisustain:user-changed', handleUser);
    };
  }, []);

  function handleSwitchRole(role) {
    const newFixture = role === 'Farmer' ? 'farmer' : 'retail-customer';
    sessionStorage.removeItem('agrisustain:current-user');
    sessionStorage.setItem('agrisustain:demo-fixture', newFixture);
    setCurrentUser(null);
    setFixture(newFixture);
    if (role === 'RetailCustomer' && route.startsWith('/farmer/')) navigate('/cart');
    if (role === 'Farmer' && ['/cart', '/checkout', '/receipts', '/products'].includes(route)) navigate('/farmer/products');
  }

  const breadcrumbLabels = {
    register: 'Đăng ký tài khoản mới (SCR-07)',
    profile: 'Hồ sơ cá nhân & Sổ địa chỉ (SCR-11)',
    orders: 'Lịch sử & Theo dõi đơn hàng (SCR-16)',
  };
  const commerceBreadcrumbs = {
    '/farmer/products': 'Quản lý sản phẩm',
    '/farmer/orders': 'Xử lý đơn hàng',
    '/cart': 'Giỏ hàng',
    '/checkout': 'Thanh toán',
    '/products': 'Khám phá nông sản',
    '/receipts': 'Xác nhận nhận hàng',
  };
  const commerceLinks = isFarmer
    ? [['/farmer/products', 'Quản lý sản phẩm'], ['/farmer/orders', 'Xử lý đơn hàng']]
    : [['/cart', 'Giỏ hàng'], ['/orders', 'Đơn hàng của tôi'], ['/receipts', 'Xác nhận nhận hàng']];

  if (isLoading) return <p role="status">Đang tải phiên đăng nhập...</p>;

  return (
    <div className="customer-portal-shell">
      {/* 1. Header ngang đầy đủ danh mục nông sản, logo và tài khoản theo SCR-TRC-01 */}
      <CustomerHeader
        currentRoute={route}
        navigate={navigate}
        user={{
          fullName: userName,
          email: userEmail,
          role: roleName,
        }}
        onSwitchRole={sessionUser ? undefined : handleSwitchRole}
      />

      {/* 2. Sub-header Slogan & Breadcrumb Bar */}
      <div className="portal-subbar">
        <div className="portal-subbar-container">
          <div className="portal-slogan">
            <span>🌿</span>
            <span>MINH BẠCH NGUỒN GỐC - AN TÂM LỰA CHỌN · HỆ THỐNG TRUY XUẤT NÔNG SẢN AGRISUSTAIN</span>
          </div>
          <div className="portal-breadcrumb">
            <a href="#/orders" onClick={(e) => { e.preventDefault(); navigate('/orders'); }}>Trang chủ</a>
            <span>›</span>
            <span>{commerceBreadcrumbs[route] || breadcrumbLabels[screen]}</span>
          </div>
        </div>
      </div>

      {/* 3. Vùng nội dung chính của màn hình */}
      <nav className="commerce commerce-actions" aria-label="Mua bán nông sản">{commerceLinks.map(([path, label]) => <button key={path} className={route === path ? 'primary' : undefined} aria-current={route === path ? 'page' : undefined} onClick={() => navigate(path)}>{label}</button>)}</nav>
<main className="customer-portal-main">
        {screen === 'register' && <RegisterScreen navigate={navigate} />}
        {screen === 'profile' && <ProfileScreen fixture={fixture} navigate={navigate} />}
        {screen === 'orders' && <OrderTrackingScreen fixture={fixture} navigate={navigate} />}
        {route === '/cart' && <ShoppingCart fixture={fixture} navigate={navigate} userName={userName} />}
        {route === '/products' && <ProductOffers fixture={fixture} />}
        {route === '/checkout' && <><button onClick={() => navigate('/cart')}>← Quay lại giỏ hàng</button><CheckoutScreen fixture={fixture} /></>}
        {route === '/farmer/products' && <ProductManagement fixture={fixture} />}
        {route === '/farmer/orders' && <Fulfillment fixture={fixture} />}
        {route === '/receipts' && <Fulfillment fixture={fixture} buyer />}
      </main>

      {/* 4. Footer phong cách thương mại nông sản chuẩn */}
      <CustomerFooter />
    </div>
  );
}
