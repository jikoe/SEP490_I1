import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/administration/UsersScreen';
import CategoriesScreen from '../features/administration/CategoriesScreen';
import CheckoutScreen from '../features/orders/CheckoutScreen';

/**
 * Renders only the three assigned use cases and an explicit local fixture selector. The selector
 * is not login; real session/router integration belongs to the team modules.
 */
export default function App() {
  const [fixture, setFixture] = useState('admin');
  const [screen, setScreen] = useState('users');
  const admin = fixture === 'admin';
  const [persistence, setPersistence] = useState(null);
  useEffect(() => {
    const controller = new AbortController();
    fetch('/health', { signal: controller.signal }).then((response) => response.json())
      .then((health) => setPersistence(health.persistence)).catch(() => {});
    return () => controller.abort();
  }, []);
  /**
   * Switches the public demo identity and resets the visible screen so administrator and
   * RetailCustomer contexts do not share stale component state.
   */
  function selectFixture(value) {
    setFixture(value); setScreen(value === 'admin' ? 'users' : 'checkout');
  }
  return <div className="app-shell"><aside className="sidebar"><a className="brand" href="/">
    <span className="brand-icon">a</span><span>AgriSustain<small>Nông nghiệp bền vững</small></span>
  </a><p className="nav-label">KHÔNG GIAN LÀM VIỆC</p><nav aria-label="Chức năng">
    {admin && <><button className={screen === 'users' ? 'selected' : ''}
      onClick={() => setScreen('users')}><span>♧</span>Quản lý tài khoản</button>
    <button className={screen === 'categories' ? 'selected' : ''}
      onClick={() => setScreen('categories')}><span>▦</span>Danh mục sản phẩm</button></>}
    {!admin && <button className="selected" onClick={() => setScreen('checkout')}>
      <span>▣</span>Đặt đơn bán lẻ</button>}
  </nav><div className="sidebar-bottom"><span className="status-dot" />
    Môi trường phát triển<small>Chỉ ba UC được giao</small></div></aside>
    <div className="workspace"><header><span>AgriSustain <span className="muted">/ {
      admin ? 'Quản trị nền tảng' : 'Mua hàng'}</span></span><label className="session-selector">
      Phiên giả lập<select aria-label="Phiên giả lập" value={fixture}
        onChange={(event) => selectFixture(event.target.value)}><option value="admin">SystemAdmin demo</option>
        <option value="retail-customer">RetailCustomer demo</option>
        <option value="other-retail-customer">RetailCustomer đối chứng</option></select></label></header>
      <div className="demo-strip"><strong>{persistence === 'SqlDemo' ? 'Dữ liệu SQL Server' : persistence === 'InMemory'
        ? 'Dữ liệu giả lập' : 'Đang kiểm tra dữ liệu'}</strong><span>{persistence === 'SqlDemo'
        ? 'Tài khoản, danh mục, giỏ và đơn được lưu SQL. Đăng nhập, điểm, phí giao và retry key vẫn giả lập.'
        : persistence === 'InMemory' ? 'Dữ liệu trong bộ nhớ mất khi khởi động lại API; các phụ thuộc đang giả lập.'
          : 'Chờ API xác nhận chế độ lưu trữ.'}</span></div>
      <main key={`${fixture}-${screen}`}>{screen === 'users' && <UsersScreen fixture={fixture} />}
        {screen === 'categories' && <CategoriesScreen fixture={fixture} />}
        {screen === 'checkout' && <CheckoutScreen fixture={fixture} />}</main>
      <footer>AgriSustain · Administration & Consumer Ordering</footer>
    </div></div>;
}
