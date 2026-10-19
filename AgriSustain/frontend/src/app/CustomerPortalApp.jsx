import React, { useEffect, useState } from 'react';
import RegisterScreen from '../features/authentication/RegisterScreen';
import ProfileScreen from '../features/profiles/ProfileScreen';
import OrderTrackingScreen from '../features/orders/OrderTrackingScreen';
import CustomerHeader from '../layouts/CustomerHeader';
import CustomerFooter from '../layouts/CustomerFooter';
import '../styles/customer-portal.css';

/**
 * CustomerPortalApp (Sàn Nông Sản Việt - AgriSustain)
 * Thiết kế giao diện chuẩn Figma SCR-TRC-01 cho Iteration 1 của Dự (DuND):
 * - SCR-07: Đăng ký tài khoản (UC-AUTH-01 / UC-AUTH-08)
 * - SCR-11: Quản lý hồ sơ & địa chỉ (UC-AUTH-03)
 * - SCR-16: Lịch sử & Theo dõi đơn hàng (UC-RC-04)
 */

function readPortalRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(register|profile|orders)$/.test(path) ? path : '/orders';
}

function readPortalFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return ['farmer', 'retail-customer'].includes(selected) ? selected : 'retail-customer';
}

function navigate(path) {
  window.location.hash = path;
}

export default function CustomerPortalApp() {
  const [fixture, setFixture] = useState(readPortalFixture);
  const [route, setRoute] = useState(readPortalRoute);

  const isFarmer = fixture === 'farmer';
  const roleName = isFarmer ? 'Farmer' : 'RetailCustomer';
  const userName = isFarmer ? 'Trần Văn Nông (HTX)' : 'Nguyễn Văn A';
  const userEmail = isFarmer ? 'farmer@agrisustain.vn' : 'nguyenvana@gmail.com';

  const screen = route === '/register' ? 'register'
    : route === '/profile' ? 'profile'
    : 'orders';

  useEffect(() => {
    const handleRoute = () => setRoute(readPortalRoute());
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);

  function handleSwitchRole(role) {
    const newFixture = role === 'Farmer' ? 'farmer' : 'retail-customer';
    sessionStorage.setItem('agrisustain:demo-fixture', newFixture);
    setFixture(newFixture);
  }

  const breadcrumbLabels = {
    register: 'Đăng ký tài khoản mới (SCR-07)',
    profile: 'Hồ sơ cá nhân & Sổ địa chỉ (SCR-11)',
    orders: 'Lịch sử & Theo dõi đơn hàng (SCR-16)',
  };

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
        onSwitchRole={handleSwitchRole}
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
            <span>{breadcrumbLabels[screen]}</span>
          </div>
        </div>
      </div>

      {/* 3. Vùng nội dung chính của màn hình */}
      <main className="customer-portal-main">
        {screen === 'register' && <RegisterScreen navigate={navigate} />}
        {screen === 'profile' && <ProfileScreen fixture={fixture} navigate={navigate} />}
        {screen === 'orders' && <OrderTrackingScreen fixture={fixture} navigate={navigate} />}
      </main>

      {/* 4. Footer phong cách thương mại nông sản chuẩn */}
      <CustomerFooter />
    </div>
  );
}
