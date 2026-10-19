import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/administration/UsersScreen';
import CategoriesScreen from '../features/administration/CategoriesScreen';
import CheckoutScreen from '../features/orders/CheckoutScreen';
import RegisterScreen from '../features/authentication/RegisterScreen';
import ProfileScreen from '../features/profiles/ProfileScreen';
import OrderTrackingScreen from '../features/orders/OrderTrackingScreen';

/** Reads assigned screen paths; unknown hashes return to appropriate screen. */
function readRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(administration\/(users|categories)(\/(new|\d+))?|checkout|register|profile|orders)$/.test(path)
    ? path : '/administration/users';
}

/** Restores development fixture selector */
function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return ['admin', 'retail-customer', 'other-retail-customer', 'farmer'].includes(selected) ? selected
    : window.location.hash === '#/checkout' ? 'retail-customer' : 'admin';
}

/** Navigates between screens */
function navigate(path) { window.location.hash = path; }

export default function App() {
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);
  const admin = fixture === 'admin';

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
    fetch('/health', { signal: controller.signal }).then((response) => response.json())
      .then(async (health) => {
        setPersistence(health.persistence);
        if (health.persistence === 'SqlServer') {
          const response = await fetch('/health/database', { signal: controller.signal });
          const readiness = await response.json();
          setDatabaseReady(response.ok && readiness.connected === true);
        }
      }).catch(() => setDatabaseReady(false));
    return () => controller.abort();
  }, []);

  function selectFixture(value) {
    sessionStorage.setItem('agrisustain:demo-fixture', value);
    setFixture(value);
    navigate(value === 'admin' ? '/administration/users' : '/orders');
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href={admin ? '#/administration/users' : '#/orders'}>
          <span className="brand-icon">✦</span>
          <span>AgriSustain<small>{admin ? 'ADMIN CONSOLE' : 'RETAIL MARKET'}</small></span>
        </a>

        <p className="nav-label">{admin ? 'QUẢN TRỊ HỆ THỐNG' : 'CHỨC NĂNG NGƯỜI DÙNG'}</p>
        <nav className="main-navigation" aria-label="Chức năng">
          {admin ? (
            <>
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
            </>
          ) : (
            <>
              <button
                className={screen === 'orders' ? 'selected' : ''}
                onClick={() => navigate('/orders')}
              >
                <span>📦</span>Đơn hàng của tôi (SCR-16)
              </button>
              <button
                className={screen === 'profile' ? 'selected' : ''}
                onClick={() => navigate('/profile')}
              >
                <span>👤</span>Hồ sơ cá nhân (SCR-11)
              </button>
              <button
                className={screen === 'checkout' ? 'selected' : ''}
                onClick={() => navigate('/checkout')}
              >
                <span>▣</span>Thanh toán đơn hàng (SCR-14)
              </button>
              <button
                className={screen === 'register' ? 'selected' : ''}
                onClick={() => navigate('/register')}
              >
                <span>✍</span>Đăng ký tài khoản (SCR-07)
              </button>
            </>
          )}
        </nav>

        <div className="sidebar-bottom">
          <div className="sidebar-profile">
            <span className="profile-avatar">{admin ? 'A' : fixture === 'farmer' ? 'F' : 'R'}</span>
            <div>
              <strong>{admin ? 'SystemAdmin' : fixture === 'farmer' ? 'Farmer / HTX' : 'RetailCustomer'}</strong>
              <small>Phiên phát triển</small>
            </div>
          </div>
          <p>
            <span className="status-dot" />
            {databaseReady
              ? 'Đã kết nối SQL · EF Core'
              : persistence === 'SqlServer'
              ? 'Chưa xác nhận kết nối SQL'
              : persistence === 'InMemory'
              ? 'Dữ liệu trong bộ nhớ'
              : 'Đang kết nối API'}
          </p>
        </div>
      </aside>

      <div className="workspace">
        <header>
          <span className="header-path">
            {admin ? 'Administration' : 'AgriSustain Retail'}
            <span>
              {' / '}
              {screen === 'users'
                ? 'Người dùng'
                : screen === 'categories'
                ? 'Danh mục sản phẩm'
                : screen === 'checkout'
                ? 'Checkout'
                : screen === 'profile'
                ? 'Hồ sơ cá nhân (SCR-11)'
                : screen === 'orders'
                ? 'Đơn hàng của tôi (SCR-16)'
                : 'Đăng ký tài khoản (SCR-07)'}
            </span>
          </span>
          <label className="session-selector">
            Phiên giả lập
            <select
              aria-label="Phiên giả lập"
              value={fixture}
              onChange={(event) => selectFixture(event.target.value)}
            >
              <option value="admin">SystemAdmin demo</option>
              <option value="retail-customer">RetailCustomer demo</option>
              <option value="farmer">Farmer / HTX demo</option>
              <option value="other-retail-customer">RetailCustomer đối chứng</option>
            </select>
          </label>
        </header>

        <main key={`${fixture}-${screen}`}>
          {screen === 'users' && <UsersScreen fixture={fixture} route={route} navigate={navigate} />}
          {screen === 'categories' && <CategoriesScreen fixture={fixture} route={route} navigate={navigate} />}
          {screen === 'checkout' && <CheckoutScreen fixture={fixture} />}
          {screen === 'register' && <RegisterScreen navigate={navigate} />}
          {screen === 'profile' && <ProfileScreen fixture={fixture} />}
          {screen === 'orders' && <OrderTrackingScreen fixture={fixture} navigate={navigate} />}
        </main>

        <footer>
          {databaseReady ? 'SQL Server · EF Core' : 'Môi trường phát triển'} · Đầy đủ 3 màn hình ITE 1 của Dự: SCR-07 (Register + OTP), SCR-11 (Profile), SCR-16 (Order Tracking).
        </footer>
      </div>
    </div>
  );
}

