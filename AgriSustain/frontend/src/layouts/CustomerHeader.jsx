import React, { useState } from 'react';

export default function CustomerHeader({ currentRoute, navigate, user, onSwitchRole }) {
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [searchKeyword, setSearchKeyword] = useState('');

  const navLinks = [
    { label: 'Trang chủ', route: '#/orders' },
    { label: 'Đơn mua của tôi', route: '#/orders', badge: '3' },
  ];

  return (
    <header className="customer-header">
      <div className="customer-header-container">
        {/* Brand Logo */}
        <div className="brand-group" onClick={() => navigate('/orders')} style={{ cursor: 'pointer' }}>
          <div className="brand-leaf-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M11 20A7 7 0 0 1 9.8 6.1C15.5 5 17 4.48 19 2c1 2 2 4.18 2 8 0 5.5-4.78 10-10 10Z" />
              <path d="M2 21c0-3 1.85-5.36 5.08-6C9.5 14.52 12 13 13 12" />
            </svg>
          </div>
          <div>
            <div className="brand-title">Nông Sản Việt</div>
            <div className="brand-tagline">AGRISUSTAIN · MINH BẠCH NGUỒN GỐC</div>
          </div>
        </div>

        {/* Header Navigation Items */}
        <nav className="customer-nav">
          {navLinks.map((item, index) => {
            const isActive =
              (item.route === '#/orders' && (currentRoute === '/orders' && item.label === 'Đơn mua của tôi')) ||
              (item.route === '#/profile' && currentRoute === '/profile' && item.label === 'Nông trại & HTX');

            return (
              <a
                key={index}
                href={item.route}
                className={`nav-item ${isActive ? 'active' : ''} ${item.highlight ? 'highlight-pill' : ''}`}
                onClick={(e) => {
                  e.preventDefault();
                  navigate(item.route.replace('#', ''));
                }}
              >
                {item.highlight && <span className="qr-icon">🔍</span>}
                {item.label}
                {item.badge && <span className="nav-badge">{item.badge}</span>}
              </a>
            );
          })}
        </nav>

        {/* Right Action Icons & User Account */}
        <div className="header-actions">
          {/* Search box */}
          <div className="header-search">
            <span className="search-icon">🔍</span>
            <input
              type="text"
              placeholder="Tìm kiếm nông sản, mã lô QR..."
              value={searchKeyword}
              onChange={(e) => setSearchKeyword(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && searchKeyword.trim()) {
                  navigate('/orders');
                }
              }}
            />
          </div>

          {/* Cart Icon */}
          <div
            className="cart-button"
            title="Giỏ hàng nông sản"
            onClick={() => navigate('/checkout')}
            style={{ cursor: 'pointer' }}
          >
            <span className="cart-icon">🛒</span>
            <span className="cart-count">2</span>
          </div>

          {/* User Account / Auth buttons */}
          {currentRoute === '/register' ? (
            <div className="auth-buttons">
              <button
                type="button"
                className="secondary-btn"
                onClick={() => navigate('/profile')}
              >
                Đăng nhập
              </button>
              <button
                type="button"
                className="primary-btn active"
                onClick={() => navigate('/register')}
              >
                Đăng ký
              </button>
            </div>
          ) : (
            <div className="user-dropdown-container">
              <button
                type="button"
                className="user-profile-btn"
                onClick={() => setUserMenuOpen(!userMenuOpen)}
              >
                <div className="user-avatar">{user.role === 'Farmer' ? 'F' : 'R'}</div>
                <div className="user-info-text">
                  <span className="user-name">{user.fullName}</span>
                  <span className="user-role-pill">{user.role}</span>
                </div>
                <span className="dropdown-caret">▾</span>
              </button>

              {userMenuOpen && (
                <div className="user-menu-dropdown" onMouseLeave={() => setUserMenuOpen(false)}>
                  <div className="menu-header">
                    <strong>{user.fullName}</strong>
                    <small>{user.email}</small>
                  </div>
                  <div className="menu-divider" />
                  <a
                    href="#/profile"
                    onClick={(e) => {
                      e.preventDefault();
                      setUserMenuOpen(false);
                      navigate('/profile');
                    }}
                  >
                    👤 Hồ sơ & Sổ địa chỉ (SCR-11)
                  </a>
                  <a
                    href="#/orders"
                    onClick={(e) => {
                      e.preventDefault();
                      setUserMenuOpen(false);
                      navigate('/orders');
                    }}
                  >
                    📦 Lịch sử & Đơn mua (SCR-16)
                  </a>
                  <a
                    href="#/register"
                    onClick={(e) => {
                      e.preventDefault();
                      setUserMenuOpen(false);
                      navigate('/register');
                    }}
                  >
                    ✍ Tạo tài khoản mới (SCR-07)
                  </a>
                  <div className="menu-divider" />
                  <div className="menu-role-switch">
                    <span>Vai trò demo:</span>
                    <button
                      type="button"
                      className={`role-switch-btn ${user.role === 'RetailCustomer' ? 'active' : ''}`}
                      onClick={() => onSwitchRole('RetailCustomer')}
                    >
                      Người mua lẻ
                    </button>
                    <button
                      type="button"
                      className={`role-switch-btn ${user.role === 'Farmer' ? 'active' : ''}`}
                      onClick={() => onSwitchRole('Farmer')}
                    >
                      Nông dân
                    </button>
                  </div>
                  <div className="menu-divider" />
                  <a
                    href="#/administration/users"
                    className="admin-link"
                    onClick={(e) => {
                      e.preventDefault();
                      setUserMenuOpen(false);
                      navigate('/administration/users');
                    }}
                  >
                    ⚙️ Chuyển sang Quản trị (Admin)
                  </a>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
