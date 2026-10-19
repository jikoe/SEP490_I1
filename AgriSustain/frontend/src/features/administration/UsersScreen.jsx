import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager, localTime, statusLabel } from '../../components/ui/Common';

const actionLabel = { suspend: 'Tạm ngưng', activate: 'Kích hoạt lại', unlock: 'Mở khóa' };

/**
 * Coordinates administrator account filters, detail and confirmed status/unlock actions for UC-
 * ADM-03; the authenticated server still enforces self-action and role restrictions.
 */
export default function UsersScreen({ fixture }) {
  const [filters, setFilters] = useState({ keyword: '', role: '', status: '', page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [detail, setDetail] = useState(null);
  const [pending, setPending] = useState(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/users?${queryString(filters)}`, { fixture, signal: controller.signal })
      .then(setResult).catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
      }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [fixture, filters, refresh]);

  /**
   * Applies a user-directory filter and returns to page one before the next abortable list
   * request.
   */
  function update(key, value) { setFilters((old) => ({ ...old, [key]: value, page: 1 })); }
  /**
   * Opens a fresh confirmation draft for one account/action and clears the previous reason/error
   * without mutating server state.
   */
  function prepare(user, action) { setReason(''); setPending({ user, action }); setError(''); }
  /**
   * Submits the confirmed account action with its reason, updates displayed details and reloads
   * the directory only after success. No removed Version field is sent.
   */
  async function change(event) {
    event.preventDefault(); setBusy(true); setError('');
    try {
      const updated = await request(`/users/${pending.user.id}/status-actions`, {
        fixture, method: 'POST', body: {
          action: pending.action, reason, confirmed: true,
        },
      });
      setMessage(`${actionLabel[pending.action]} tài khoản thành công.`);
      setDetail((old) => old?.id === updated.id ? updated : old);
      setPending(null); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }

  /**
   * Shows state-eligible action buttons and hides self-action affordances. These controls
   * support usability; server-side actor/target checks remain authoritative.
   */
  const buttons = (user) => <div className="actions">
    {user.id !== result.currentActorId && user.status === 'Active' && <button className="danger-link"
      onClick={() => prepare(user, 'suspend')}>Tạm ngưng</button>}
    {user.id !== result.currentActorId && user.status === 'Suspended' && <button
      onClick={() => prepare(user, 'activate')}>Kích hoạt lại</button>}
    {user.id !== result.currentActorId && user.isLocked && <button
      onClick={() => prepare(user, 'unlock')}>Mở khóa</button>}
  </div>;

  return <section><div className="section-heading"><div><p className="eyebrow">UC-ADM-03</p>
    <h1>Quản lý tài khoản</h1><p className="muted">Tra cứu và quản lý quyền truy cập nền tảng.</p>
  </div><span className="count">{result.total} tài khoản</span></div>
    <Notice>{message}</Notice><Notice error>{error}</Notice>
    <div className="panel"><div className="filters">
      <Field label="Tìm tài khoản"><input maxLength={100} value={filters.keyword}
        placeholder="Tên, email, số điện thoại hoặc mã"
        onChange={(event) => update('keyword', event.target.value)} /></Field>
      <Field label="Vai trò"><select value={filters.role}
        onChange={(event) => update('role', event.target.value)}>
        <option value="">Tất cả vai trò</option>{['SystemAdmin', 'CustomerSupport', 'RetailCustomer', 'Farmer',
          'WholesaleBuyer'].map((role) => <option key={role} value={role}>
          {statusLabel[role]}</option>)}</select></Field>
      <Field label="Trạng thái"><select value={filters.status}
        onChange={(event) => update('status', event.target.value)}><option value="">Tất cả</option>
        {['Active', 'Suspended', 'Pending', 'Rejected'].map((status) =>
          <option value={status} key={status}>{statusLabel[status]}</option>)}
      </select></Field><button onClick={() => setRefresh((value) => value + 1)}>Tải lại</button>
    </div><div className="table-wrap"><table><thead><tr><th>Tài khoản</th><th>Liên hệ</th>
      <th>Vai trò</th><th>Trạng thái</th><th>Ngày tham gia</th><th>Thao tác</th></tr></thead>
      <tbody>{!loading && result.items.map((user) => <tr key={user.id}><td>
        <button className="text-button" onClick={() => { setDetail(user); setError(''); }}>
          {user.organizationName || user.name}</button><small>#{user.id}</small></td>
        <td>{user.email}<small>{user.phone || 'Chưa có số điện thoại'}</small></td>
        <td>{statusLabel[user.role] || user.role}</td><td><span className={`badge ${user.status}`}>
          {statusLabel[user.status]}</span>{user.isLocked && <small>Đang khóa tạm</small>}</td>
        <td>{localTime(user.createdAt)}</td><td>{buttons(user)}</td></tr>)}</tbody></table>
      {loading && <p className="empty" role="status">Đang tải tài khoản…</p>}
      {!loading && !result.items.length && !error && <p className="empty">
        Không tìm thấy tài khoản phù hợp. Hãy điều chỉnh bộ lọc.</p>}
    </div><Pager total={result.total} page={filters.page}
      onPage={(page) => setFilters((old) => ({ ...old, page }))} /></div>
    {detail && !pending && <Modal title="Chi tiết tài khoản" onClose={() => setDetail(null)}>
      <dl className="details"><dt>Họ tên / đơn vị</dt><dd>{detail.name}</dd><dt>Mã tài khoản</dt>
        <dd>#{detail.id}</dd><dt>Email</dt><dd>{detail.email}</dd><dt>Vai trò</dt>
        <dd>{statusLabel[detail.role]}</dd><dt>Trạng thái</dt><dd>{statusLabel[detail.status]}</dd>
        <dt>Xác minh</dt><dd>{detail.verificationStatus || 'Không áp dụng'}</dd>
        <dt>Khóa tạm đến</dt><dd>{localTime(detail.lockedUntil)}</dd>
        <dt>Số lần đăng nhập lỗi</dt><dd>{detail.failedLoginCount}</dd></dl>
      {buttons(detail)}<button onClick={() => setDetail(null)}>Quay lại danh sách</button>
    </Modal>}
    {pending && <Modal title={`${actionLabel[pending.action]} tài khoản`}
      onClose={() => { if (!busy) setPending(null); }}><form onSubmit={change}>
      <p>Xác nhận {actionLabel[pending.action].toLowerCase()} tài khoản <strong>
        {pending.user.name}</strong>?</p><Field label={pending.action === 'unlock'
        ? 'Ghi chú (không bắt buộc)' : 'Lý do *'}><textarea autoFocus maxLength={500}
        required={pending.action !== 'unlock'} value={reason}
        onChange={(event) => setReason(event.target.value)} /></Field>
      <Notice error>{error}</Notice><div className="modal-actions"><button type="button"
        disabled={busy} onClick={() => setPending(null)}>Hủy</button>
        <button className="primary" disabled={busy}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button>
      </div></form></Modal>}
  </section>;
}
