import React, { useEffect, useRef, useState } from 'react';
import { request, getSessionToken } from '../../api/client';
import './farmer-retail.css';

export const money = value => new Intl.NumberFormat('vi-VN').format(value || 0) + ' ₫';
export const date = value => value ? new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' }).format(new Date(value.endsWith('Z') ? value : value + 'Z')) : '—';
export const status = value => ({ Pending: 'Chờ duyệt', Approved: 'Đã duyệt', Rejected: 'Từ chối', PendingPayment: 'Chờ thanh toán', PendingConfirmation: 'Chờ xác nhận', Confirmed: 'Đang chuẩn bị', Preparing: 'Đang chuẩn bị', ReadyToShip: 'Sẵn sàng giao', Shipping: 'Đang giao', InTransit: 'Đang giao', Delivered: 'Đã giao', Received: 'Đã nhận', Cancelled: 'Đã hủy' }[value] || value);
const messages = { MSG71: 'Đã gửi duyệt, sản phẩm chưa hiển thị công khai.', MSG72: 'Đã lưu thay đổi.', MSG74: 'Đã ẩn sản phẩm.', MSG164: 'Đã hiển thị sản phẩm đã duyệt.', MSG81: 'Đã thêm vào giỏ hàng.', MSG82: 'Đã áp dụng giá sỉ.', MSG83: 'Đã áp dụng lại giá lẻ.', MSG85: 'Đã xóa sản phẩm đã xác nhận.', MSG86: 'Số lượng đã điều chỉnh theo tồn kho và bước bán. Kiểm tra lại số lượng, đơn vị và tạm tính trước khi tiếp tục.', MSG87: 'Vui lòng chọn ít nhất một sản phẩm để tiếp tục.', MSG88: 'Có sản phẩm không thể mua. Dòng đó đã bỏ chọn; bạn vẫn có thể xóa hoặc tiếp tục với các dòng còn hợp lệ.', MSG118: 'Giá hoặc chính sách bán đã thay đổi. Kiểm tra giá mới rồi tiến hành thanh toán lần nữa.', MSG168: 'Giỏ hàng đang trống.', MSG64: 'Đã xác nhận đơn hàng.', MSG146: 'Đơn hàng sẵn sàng giao.', MSG66: 'Đã bắt đầu giao mô phỏng và thông báo cho người mua.', MSG67: 'Đã từ chối đơn. Nếu có thu tiền, yêu cầu hoàn tiền đang chờ Finance xử lý.', MSG147: 'Đã ghi nhận xác nhận nhận hàng.', MSG149: 'Đã lưu báo cáo và bằng chứng. Hàng trả chưa được cộng lại tồn bán.', MSG150: 'Đã gửi phản hồi cho hỗ trợ xem xét.' };
export function useFarmerRetailApi(fixture) {
  const [notice, setNotice] = useState(''); const [error, setError] = useState(null); const [busy, setBusy] = useState(false);
  const pending = useRef(0);
  async function run(path, options = {}) {
    pending.current++; setBusy(true); setError(null);
    try { const data = await request(path, { fixture, ...options }); if (data.messageCode) setNotice(messages[data.messageCode] || data.messageCode); return data; }
    catch (e) { setError(e); return null; } finally { pending.current--; if (pending.current === 0) setBusy(false); }
  }
  return { run, busy, notice, error, setNotice, feedback: <>{error && <div className="commerce-alert error" role="alert">{error.code}: {error.message}</div>}{notice && <div className="commerce-alert" role="status">{notice}</div>}</> };
}
// Pending/rejected product media is visible only to its authenticated owner. Never attach
// a session header to external media URLs. Blob URLs are released when the image changes.
export function AuthenticatedImage({ src, ...props }) {
  const [blob, setBlob] = useState(null);
  useEffect(() => {
    if (!src?.startsWith('/api/product-media/')) return;
    const controller = new AbortController(); let url;
    fetch(src, { headers: { Authorization: getSessionToken() ? `Bearer ${getSessionToken()}` : `Demo ${sessionStorage.getItem('agrisustain:demo-fixture') || 'retail-customer'}` }, signal: controller.signal })
      .then(response => { if (!response.ok) throw new Error('Image unavailable'); return response.blob(); })
      .then(value => { url = URL.createObjectURL(value); setBlob(url); }).catch(() => setBlob(null));
    return () => { controller.abort(); if (url) URL.revokeObjectURL(url); };
  }, [src]);
  return <img {...props} src={src?.startsWith('/api/product-media/') ? blob || undefined : src} />;
}
export function Pagination({ page, total, onChange, busy }) { return <div className="commerce-pagination"><span>Hiển thị {total ? (page - 1) * 10 + 1 : 0}–{Math.min(page * 10, total)} trong {total} bản ghi · 10 bản ghi/trang</span><button aria-label="Trang trước" disabled={busy || page <= 1} onClick={() => onChange(page - 1)}>‹</button><button className="primary" aria-current="page" disabled>{page}</button><button aria-label="Trang sau" disabled={busy || page * 10 >= total} onClick={() => onChange(page + 1)}>›</button></div>; }
export function FarmerHeading({ title, verified }) { return <header className="commerce-heading"><div><h1>{title}</h1><p>Quản lý hoạt động của nông trại</p></div>{verified && <span className="commerce-pill">Hồ sơ đã xác minh</span>}</header>; }
export function ViewTabs({ items, active, onChange }) { return <nav className="commerce-tabs" aria-label="Các màn hình">{items.map(([id, title]) => <button key={id} className={active === id ? 'active' : ''} aria-current={active === id ? 'page' : undefined} onClick={() => onChange(id)}>{title}</button>)}</nav>; }
export function Notice({ children }) { return <div className="commerce-alert">{children || 'Thông báo hiển thị tại đây theo kết quả kiểm tra. Dữ liệu hợp lệ được giữ lại khi cần sửa lỗi.'}</div>; }
export function EvidencePicker({ files, onChange, onError }) { return <div className="commerce-evidence"><label>Bằng chứng<input type="file" aria-label="Bằng chứng" accept="image/jpeg,image/png" multiple onChange={async e => { try { const added = await readImages(e.target.files); if (files.length + added.length > 10) throw new Error('Tối đa 10 ảnh.'); onChange([...files, ...added]); } catch (error) { onError(error.message); } e.target.value = ''; }} /></label><small>JPG, JPEG, PNG · tối đa 5 MB/ảnh</small><div className="commerce-gallery">{files.map((file, i) => <div key={i}><img alt={file.name} src={`data:${file.contentType};base64,${file.content}`} /><button type="button" onClick={() => onChange(files.filter((_, index) => index !== i))}>Bỏ tệp</button></div>)}</div></div>; }
export function Confirm({ text, onConfirm, onCancel, busy }) { return <div className="commerce-backdrop"><section className="commerce-dialog" role="dialog" aria-modal="true" aria-label="Xác nhận thao tác"><h2>Xác nhận thao tác</h2><p>{text}</p><div className="commerce-actions"><button onClick={onCancel} disabled={busy}>Hủy</button><button className="primary" onClick={onConfirm} disabled={busy}>Xác nhận</button></div></section></div>; }
export async function readImages(files) {
  if (files.length > 10) throw new Error('Tối đa 10 ảnh.');
  return Promise.all([...files].map(async file => {
    if (!['image/png', 'image/jpeg'].includes(file.type) || !/\.(png|jpe?g)$/i.test(file.name) || file.size > 5 * 1024 * 1024) throw new Error('Chọn JPG/JPEG/PNG tối đa 5 MB mỗi ảnh.');
    const content = await new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(reader.result.split(',')[1]); reader.onerror = reject; reader.readAsDataURL(file); });
    return { name: file.name, contentType: file.type, content, primary: false };
  }));
}
export function ImagePicker({ images, onChange, onError }) { return <div><label>Ảnh JPG/JPEG/PNG, tối đa 5 MB/ảnh<input aria-label="Tải ảnh" type="file" accept="image/jpeg,image/png" multiple onChange={async e => { try { const added = await readImages(e.target.files); if (images.length + added.length > 10) throw new Error('Tối đa 10 ảnh.'); onChange([...images, ...added].map((x, i) => ({ ...x, primary: images.length ? x.primary : i === 0 }))); } catch (err) { onError(err.message); } e.target.value = ''; }} /></label><h3>Chọn đúng một ảnh đại diện</h3><div className="commerce-gallery">{images.map((image, index) => <div key={image.existingUrl || index}><AuthenticatedImage alt={image.name || 'Ảnh sản phẩm'} src={image.content ? `data:${image.contentType};base64,${image.content}` : image.existingUrl} /><label><input type="radio" name="primary-image" checked={image.primary} onChange={() => onChange(images.map((x, i) => ({ ...x, primary: i === index })))} />Ảnh đại diện</label><button type="button" onClick={() => onChange(images.filter((_, i) => i !== index))}>Bỏ ảnh</button></div>)}</div></div>; }
