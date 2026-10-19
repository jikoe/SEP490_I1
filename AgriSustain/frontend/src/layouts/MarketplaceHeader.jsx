import React, { useEffect, useState } from 'react';
import { useAuth } from '../hooks/useAuth';
import { BrandMark, Icon } from '../features/marketplace/MarketIcons';

export default function MarketplaceHeader({ simple = false, keyword = '', onSearch }) {
  const { user, logout } = useAuth();
  const [search, setSearch] = useState(keyword);
  const [open, setOpen] = useState(false);
  useEffect(() => setSearch(keyword), [keyword]);
  return <header className={`market-header ${simple ? 'market-header-simple' : ''}`}>
    <a className="market-brand" href="#/marketplace"><BrandMark /><span>Nông Sản Việt</span></a>
    <p className="market-slogan">Nền tảng nông sản Việt – Kết nối nông dân, bền vững tương lai</p>
    {!simple && <><form className="market-search" role="search" onSubmit={event => {
      event.preventDefault();
      if (onSearch) onSearch(search);
      else window.location.hash = `/marketplace?keyword=${encodeURIComponent(search)}`;
    }}><button aria-label="Tìm kiếm" type="submit"><Icon name="search" /></button>
      <input aria-label="Tìm kiếm sản phẩm, danh mục" placeholder="Tìm kiếm sản phẩm, danh mục..."
        value={search} maxLength={150} onChange={event => setSearch(event.target.value)} /></form>
    <a className="market-cart" href={user ? '#/cart' : '#/login'} aria-label="Giỏ hàng">
      <Icon name="cart" size={30} /></a>
    {user ? <div className="market-account"><button className="market-account-trigger"
      aria-expanded={open} onClick={() => setOpen(!open)}><Icon name="user" size={28} />
      <span>{user.fullName}</span><span aria-hidden="true">⌄</span></button>
      {open && <nav aria-label="Tài khoản" className="market-account-menu">
        <a href="#/profile">Hồ sơ tài khoản</a>
        <a href="#/change-password" onClick={() => setOpen(false)}>Đổi mật khẩu</a>
        <a href="#/orders">Đơn mua</a>
        {user.role === 'Farmer' && <><a href="#/farmer/products">Quản lý sản phẩm</a><a href="#/farmer/orders">Xử lý đơn hàng</a></>}
        <button onClick={async () => { await logout(); setOpen(false); }}>Đăng xuất</button>
    </nav>}</div> : <a className="market-login-link" href="#/login" aria-label="Đăng nhập">
      <Icon name="user" /><span>Đăng nhập</span></a>}
    </>}
  </header>;
}
