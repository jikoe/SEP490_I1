import React, { useEffect, useId, useRef } from 'react';

/**
 * Formats a server-computed decimal amount for Vietnamese display. Display formatting does not
 * calculate discounts or change the order money contract.
 */
export const money = (amount) => new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', maximumFractionDigits: 2,
}).format(amount);

/**
 * Formats an ISO timestamp in the Vietnam business timezone; absent timestamps render as a dash
 * without guessing a legacy database timezone.
 */
export function localTime(time) {
  if (!time) return '—';
  const parts = Object.fromEntries(new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).formatToParts(new Date(time)).map((part) => [part.type, part.value]));
  return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
}

export const statusLabel = {
  Active: 'Hoạt động', Suspended: 'Tạm ngưng', Pending: 'Chờ duyệt', Rejected: 'Từ chối',
  SystemAdmin: 'Quản trị hệ thống', CustomerSupport: 'Hỗ trợ khách hàng',
  RetailCustomer: 'Người mua lẻ', Farmer: 'Nông dân / HTX',
  WholesaleBuyer: 'Người mua B2B',
};

/**
 * Exposes user feedback as an accessible status or alert region, keeping server messages as text
 * rather than rendering untrusted HTML.
 */
export function Notice({ children, error = false }) {
  return children ? <div className={`notice ${error ? 'error' : ''}`}
    role={error ? 'alert' : 'status'}>{children}</div> : null;
}

/**
 * Computes bounded previous/next navigation from the server total and asks the parent to change
 * page; it does not filter a hidden client-side dataset.
 */
export function Pager({ total, page, pageSize = 10, onPage }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return <div className="pager"><span>{total} bản ghi · Trang {page}/{pages}</span>
    <div className="actions"><button disabled={page <= 1} onClick={() => onPage(page - 1)}>
      Trước</button><button disabled={page >= pages} onClick={() => onPage(page + 1)}>
      Sau</button></div></div>;
}

/**
 * Uses a native modal dialog for focus trapping and restores previous focus on cleanup. Escape
 * delegates cancellation so busy operations can retain their confirmation state.
 */
export function Modal({ title, children, onClose }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previousFocus = document.activeElement;
    dialog.showModal();
    return () => { dialog.close(); previousFocus?.focus(); };
  }, []);
  return <dialog ref={ref} onCancel={(event) => { event.preventDefault(); onClose(); }}>
    <div className="modal-heading"><h2>{title}</h2><button aria-label="Đóng"
      onClick={onClose}>×</button></div>{children}</dialog>;
}

/**
 * Links a stable generated label ID to its single child control, preserving an explicit child ID
 * when supplied so forms and accessibility locators remain consistent.
 */
export function Field({ label, children, ...props }) {
  const generatedId = useId();
  const id = children.props.id || generatedId;
  return <div className="field" {...props}><label htmlFor={id}>{label}</label>
    {React.cloneElement(children, { id })}</div>;
}
