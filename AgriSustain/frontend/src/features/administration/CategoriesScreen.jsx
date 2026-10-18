import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager } from '../../components/ui/Common';

const blank = { name: '', parentId: '', description: '', priority: 0, isActive: true };

/**
 * Coordinates category paging, editing, hide/activate and confirmed physical deletion. Product
 * counts guide the UI; the server independently enforces all product and other references.
 */
export default function CategoriesScreen({ fixture }) {
  const [filters, setFilters] = useState({ keyword: '', isActive: '', parentId: '',
    rootOnly: '', page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [parents, setParents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refresh, setRefresh] = useState(0);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [form, setForm] = useState(null);
  const [pending, setPending] = useState(null);
  const [busy, setBusy] = useState(false);
  // Abort stale reads when the fixture/filter changes, so an older response cannot win.
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    Promise.all([
      request(`/categories?${queryString(filters)}`, { fixture, signal: controller.signal }),
      request('/categories?pageSize=100', { fixture, signal: controller.signal }),
    ]).then(([data, options]) => { setResult(data); setParents(options.items); })
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [fixture, filters, refresh]);

  /**
   * Updates one draft field without writing to the API. Confirmation and backend validation
   * happen only when the form is submitted.
   */
  const change = (key, value) => setForm((old) => ({ ...old, [key]: value }));
  /**
   * Applies a category filter and resets to the first page so a narrower result cannot strand
   * the user on a later page.
   */
  const filter = (key, value) => setFilters((old) => ({ ...old, [key]: value, page: 1 }));

  /**
   * Submits only the approved category fields and confirms a status change. Images, archive
   * metadata and Version are not part of the revised request.
   */
  async function save(event) {
    event.preventDefault(); setError('');
    const statusChanged = Boolean(form.id) && form.originalIsActive !== form.isActive;
    if (statusChanged && !window.confirm('Xác nhận thay đổi trạng thái danh mục?')) return;
    setBusy(true);
    try {
      await request(`/categories${form.id ? `/${form.id}` : ''}`, {
        fixture, method: form.id ? 'PUT' : 'POST', body: {
          name: form.name.trim(), parentId: form.parentId ? Number(form.parentId) : null,
          description: form.description, priority: Number(form.priority),
          isActive: form.isActive, confirmed: statusChanged,
        },
      });
      setMessage(form.id ? 'Đã cập nhật danh mục.' : 'Đã tạo danh mục.');
      setForm(null); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }

  /**
   * Submits the confirmed hide/activate/delete action, refreshes the list on success and
   * preserves the dialog/error on a rejected reference check.
   */
  async function execute() {
    setBusy(true); setError('');
    try {
      await request(`/categories/${pending.item.id}/${pending.action}`, {
        fixture, method: 'POST', body: { confirmed: true } });
      setMessage(pending.action === 'delete' ? 'Đã xóa danh mục.' : 'Đã cập nhật danh mục.');
      setPending(null); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }

  return <section><div className="section-heading"><div><p className="eyebrow">UC-ADM-05</p>
    <h1>Danh mục sản phẩm</h1><p className="muted">Quản lý phân loại và trạng thái sử dụng.</p>
  </div><button className="primary" onClick={() => { setForm({ ...blank }); setError(''); }}>
    + Thêm danh mục</button></div><Notice>{message}</Notice><Notice error>{error}</Notice>
    <div className="panel"><div className="filters">
      <Field label="Tìm danh mục"><input placeholder="Nhập tên danh mục" maxLength={100}
        value={filters.keyword} onChange={(event) => filter('keyword', event.target.value)} /></Field>
      <Field label="Trạng thái"><select value={filters.isActive}
        onChange={(event) => filter('isActive', event.target.value)}><option value="">Tất cả</option>
        <option value="true">Hoạt động</option><option value="false">Ẩn / khóa</option>
      </select></Field><Field label="Danh mục cha"><select value={filters.rootOnly ? 'root'
        : filters.parentId} onChange={(event) => setFilters((old) => ({ ...old, page: 1,
          parentId: event.target.value === 'root' ? '' : event.target.value,
          rootOnly: event.target.value === 'root' ? true : '' }))}>
        <option value="">Tất cả</option><option value="root">Chỉ danh mục gốc</option>
        {parents.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
      </select></Field></div><div className="table-wrap"><table><thead><tr><th>Danh mục</th>
      <th>Danh mục cha</th><th>Ưu tiên</th><th>Sản phẩm</th><th>Trạng thái</th><th>Thao tác</th>
    </tr></thead><tbody>{!loading && result.items.map((item) => <tr key={item.id}>
      <td><strong>{item.name}</strong><small>#{item.id}</small></td>
      <td>{item.parentName || 'Danh mục gốc'}</td><td>{item.priority}</td><td>{item.productCount}</td>
      <td><span className={`badge ${item.isActive ? 'Active' : 'Suspended'}`}>
        {item.isActive ? 'Hoạt động' : 'Ẩn / khóa'}</span></td><td><div className="actions">
        <button onClick={() => { setForm({ ...item, originalIsActive: item.isActive,
          parentId: item.parentId || '', description: item.description || '' }); setError(''); }}>
          Sửa</button><button onClick={() => { setPending({ item,
            action: item.isActive ? 'deactivate' : 'activate' }); setError(''); }}>
          {item.isActive ? 'Ẩn / khóa' : 'Kích hoạt'}</button>
        <button className="danger-link" disabled={item.productCount > 0}
          title={item.productCount > 0 ? 'Có sản phẩm: chỉ được ẩn hoặc khóa' : 'Xóa danh mục'}
          onClick={() => { setPending({ item, action: 'delete' }); setError(''); }}>Xóa</button>
      </div></td></tr>)}</tbody></table>
      {loading && <p className="empty" role="status">Đang tải danh mục…</p>}
      {!loading && !result.items.length && !error && <p className="empty">Không tìm thấy danh mục.</p>}
    </div><Pager total={result.total} page={filters.page}
      onPage={(page) => setFilters((old) => ({ ...old, page }))} /></div>
    {form && <Modal title={form.id ? 'Chi tiết danh mục' : 'Thêm danh mục'}
      onClose={() => { if (!busy) setForm(null); }}><form onSubmit={save}><div className="form-grid">
      <Field label="Tên danh mục *"><input autoFocus required minLength={2} maxLength={100}
        value={form.name} onChange={(event) => change('name', event.target.value)} /></Field>
      <Field label="Danh mục cha"><select value={form.parentId}
        onChange={(event) => change('parentId', event.target.value)}>
        <option value="">Không có (danh mục gốc)</option>{parents.filter((item) =>
          item.isActive && item.id !== form.id).map((item) =>
            <option key={item.id} value={item.id}>{item.name}</option>)}
      </select></Field><Field label="Ưu tiên hiển thị"><input type="number" min={0}
        max={2147483647} step={1} required value={form.priority}
        onChange={(event) => change('priority', event.target.value)} /></Field>
      <Field label="Trạng thái"><select value={String(form.isActive)}
        onChange={(event) => change('isActive', event.target.value === 'true')}>
        <option value="true">Hoạt động</option><option value="false">Ẩn / khóa</option>
      </select></Field></div><Field label="Mô tả"><textarea maxLength={500}
        value={form.description} onChange={(event) => change('description', event.target.value)} />
      </Field><Notice error>{error}</Notice><div className="modal-actions">
        <button type="button" disabled={busy} onClick={() => setForm(null)}>Hủy</button>
        <button className="primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu danh mục'}</button>
      </div></form></Modal>}
    {pending && <Modal title="Xác nhận thay đổi danh mục"
      onClose={() => { if (!busy) setPending(null); }}><p>{pending.action === 'delete'
        ? 'Xóa vĩnh viễn danh mục chưa có sản phẩm? Liên kết khác sẽ được kiểm tra trước khi xóa.'
        : `Xác nhận ${pending.action === 'activate' ? 'kích hoạt' : 'ẩn / khóa'} danh mục?`}</p>
      <p><strong>{pending.item.name}</strong></p><Notice error>{error}</Notice>
      <div className="modal-actions"><button disabled={busy} onClick={() => setPending(null)}>Hủy</button>
        <button className="primary" disabled={busy} onClick={execute}>Xác nhận</button></div>
    </Modal>}
  </section>;
}
