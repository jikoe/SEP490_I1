import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/administration/UsersScreen';
import CategoriesScreen from '../features/administration/CategoriesScreen';
import CheckoutScreen from '../features/orders/CheckoutScreen';
import RegisterScreen from '../features/authentication/RegisterScreen';
import ProfileScreen from '../features/profiles/ProfileScreen';
import OrderTrackingScreen from '../features/orders/OrderTrackingScreen';
import CustomerHeader from '../layouts/CustomerHeader';
import CustomerFooter from '../layouts/CustomerFooter';

/** Reads assigned screen paths; unknown hashes default to orders (customer portal) */
function readRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(administration\/(users|categories)(\/(new|\d+))?|checkout|register|profile|orders)$/.test(path)
    ? path : '/orders';
}

/** Restores development fixture selector */
function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return ['admin', 'retail-customer', 'other-retail-customer', 'farmer'].includes(selected)
    ? selected
    : window.location.hash.startsWith('#/administration') ? 'admin' : 'retail-customer';
}

/** Navigates between screens */
function navigate(path) {
  window.location.hash = path;
}

export default function App() {
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);

  const isAdminRoute = route.startsWith('/administration');
  const roleName = fixture === 'farmer' ? 'Farmer' : fixture === 'admin' ? 'SystemAdmin' : 'RetailCustomer';
  const userName = fixture === 'farmer' ? 'Trần Văn Nông (HTX)' : fixture === 'admin' ? 'Quản trị viên' : 'Nguyễn Văn A';
  const userEmail = fixture === 'farmer' ? 'farmer@agrisustain.vn' : fixture === 'admin' ? 'admin@agrisustain.vn' : 'nguyenvana@gmail.com';

  const screen = route === '/register' ? 'register'
    : route === '/profile' ? 'profile'
    : route === '/orders' ? 'orders'
    : route.startsWith('/administration/categories') ? 'categories'
    : route === '/checkout' ? 'checkout' : 'users';

  useEffect(() => {
    const handleRoute = () => setRoute(readRoute());
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    fetch('/health', { signal: controller.signal })
      .then((response) => response.json())
      .then(async (health) => {
        setPersistence(health.persistence);
        if (health.persistence === 'SqlServer') {
          const response = await fetch('/health/database', { signal: controller.signal });
          const readiness = await response.json();
          setDatabaseReady(response.ok && readiness.connected === true);
        }
      })
      .catch(() => setDatabaseReady(false));
    return () => controller.abort();
  }, []);

  function handleSwitchRole(role) {
    const newFixture = role === 'Farmer' ? 'farmer' : 'retail-customer';
    sessionStorage.setItem('agrisustain:demo-fixture', newFixture);
    setFixture(newFixture);
  }

  // 1. GIAO DIỆN QUẢN TRỊ ADMIN (Nếu route thuộc /administration)
  if (isAdminRoute) {
    return (
      <div className="app-shell">
        <aside className="sidebar">
          <a className="brand" href="#/administration/users">
            <span className="brand-icon">✦</span>
            <span>AgriSustain<small>ADMIN CONSOLE</small></span>
          </a>

          <p className="nav-label">QUẢN TRỊ HỆ THỐNG</p>
          <nav className="main-navigation" aria-label="Chức năng">
            <button
              className={screen === 'users' ? 'selected' : ''}
              onClick={() => navigate('/administration/users')}
            >
              <span>♙</span>Quản lý người dùng
            </button>
            <button
              className={screen === 'categories' ? 'selected' : ''}
              onClick={() => navigate('/administration/categories')}
            >
              <span>▦</span>Danh mục sản phẩm
            </button>
            <div style={{ margin: '16px 0', borderTop: '1px solid #e0e7e0' }} />
            <button
              style={{ color: '#2d8a5e', fontWeight: 600 }}
              onClick={() => navigate('/orders')}
            >
              <span>🌱</span>Xem Sàn Nông Sản Việt (FE)
            </button>
          </nav>

          <div className="sidebar-bottom">
            <div className="sidebar-profile">
              <span className="profile-avatar">A</span>
              <div>
                <strong>SystemAdmin</strong>
                <small>Phiên phát triển</small>
              </div>
            </div>
            <p>
              <span className="status-dot" />
              {databaseReady ? 'Đã kết nối SQL · EF Core' : 'Môi trường phát triển'}
            </p>
          </div>
        </aside>

        <div className="workspace">
          <header>
            <span className="header-path">
              Administration<span> / {screen === 'users' ? 'Người dùng' : 'Danh mục sản phẩm'}</span>
            </span>
            <button
              className="primary"
              style={{ padding: '6px 14px', fontSize: 12 }}
              onClick={() => navigate('/orders')}
            >
              ← Về Sàn Khách Hàng (SCR-TRC-01)
            </button>
          </header>
          <main key={`${fixture}-${screen}`}>
            {screen === 'users' && <UsersScreen fixture={fixture} route={route} navigate={navigate} />}
            {screen === 'categories' && <CategoriesScreen fixture={fixture} route={route} navigate={navigate} />}
          </main>
          <footer>
            AgriSustain Administration Module · Quản lý bởi SystemAdmin
          </footer>
        </div>
      </div>
    );
  }

  // 2. GIAO DIỆN SÀN THƯƠNG MẠI NÔNG SẢN VIỆT (PHONG CÁCH SCR-TRC-01 TRÊN FIGMA)
  const breadcrumbLabels = {
    register: 'Đăng ký tài khoản (SCR-07)',
    profile: 'Hồ sơ cá nhân & Sổ địa chỉ (SCR-11)',
    orders: 'Lịch sử & Theo dõi đơn hàng (SCR-16)',
    checkout: 'Thanh toán đơn hàng (SCR-14)',
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
            <span>{breadcrumbLabels[screen] || 'Khám phá'}</span>
          </div>
        </div>
      </div>

      {/* 3. Vùng nội dung chính (Container rộng rãi, không bị gò bó sidebar) */}
      <main className="customer-portal-main">
        {screen === 'register' && <RegisterScreen navigate={navigate} />}
        {screen === 'profile' && <ProfileScreen fixture={fixture} navigate={navigate} />}
        {screen === 'orders' && <OrderTrackingScreen fixture={fixture} navigate={navigate} />}
        {screen === 'checkout' && <CheckoutScreen fixture={fixture} />}
      </main>

      {/* 4. Footer phong cách thương mại nông sản chuẩn */}
      <CustomerFooter />
    </div>
  );
}
