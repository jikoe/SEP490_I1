import React, { useState } from 'react';
import { request } from '../../api/client';
import { useAuth } from '../../hooks/useAuth';
import { Icon } from '../marketplace/MarketIcons';

const fields = [
  ['currentPassword', 'Mật khẩu hiện tại', 'Nhập mật khẩu hiện tại'],
  ['newPassword', 'Mật khẩu mới', 'Nhập mật khẩu mới'],
  ['confirmNewPassword', 'Xác nhận mật khẩu mới', 'Nhập lại mật khẩu mới'],
];

export default function ChangePasswordScreen({ onCancel }) {
  const { user } = useAuth();
  const [values, setValues] = useState({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
  const [visible, setVisible] = useState({});
  const [errors, setErrors] = useState({});
  const [notice, setNotice] = useState('');
  const [pending, setPending] = useState(false);
  async function submit(event) {
    event.preventDefault();
    const required = Object.fromEntries(fields.filter(([key]) => !values[key].trim())
      .map(([key]) => [key, 'Vui lòng nhập trường này.']));
    setErrors(required);
    setNotice('');
    if (Object.keys(required).length) return;
    setPending(true);
    try {
      const message = await request('/passwords/change', {
        method: 'POST', token: user.accessToken, body: values,
      });
      setNotice(message);
      setValues({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    } catch (error) {
      const key = error.code === 'MSG04' ? 'currentPassword'
        : error.code === 'MSG05' ? 'confirmNewPassword'
          : ['MSG06', 'MSG12'].includes(error.code) ? 'newPassword' : 'form';
      setErrors({ [key]: error.status >= 500
        ? 'Không thể đổi mật khẩu. Vui lòng thử lại sau.' : error.message });
    } finally { setPending(false); }
  }
  return <main className="password-page"><section className="password-panel">
    <h1>Đổi mật khẩu</h1><p className="password-subtitle">Cập nhật mật khẩu để bảo vệ tài khoản của bạn</p>
    <form noValidate onSubmit={submit} aria-busy={pending}>
      {fields.map(([key, label, placeholder]) => <div className="password-field" key={key}>
        <label htmlFor={key}>{label}</label><div className="password-input-wrap">
          <input id={key} type={visible[key] ? 'text' : 'password'} placeholder={placeholder}
            value={values[key]} required maxLength={1024} disabled={pending}
            autoComplete={key === 'currentPassword' ? 'current-password' : 'new-password'}
            aria-invalid={Boolean(errors[key])} aria-describedby={errors[key] ? `${key}-error` : undefined}
            onChange={event => setValues({ ...values, [key]: event.target.value })} />
          <button type="button" aria-label={`${visible[key] ? 'Ẩn' : 'Hiện'} ${label.toLowerCase()}`}
            aria-pressed={Boolean(visible[key])} onClick={() => setVisible({ ...visible, [key]: !visible[key] })}>
            <Icon name="eye" /></button></div>
        {errors[key] && <p id={`${key}-error`} className="market-field-error" role="alert">{errors[key]}</p>}
      </div>)}
      {errors.form && <p className="market-field-error" role="alert">{errors.form}</p>}
      {notice && <p className="market-success" role="status">{notice}</p>}
      <div className="password-actions"><button className="market-primary" disabled={pending}>
        {pending ? 'Đang cập nhật...' : 'Đổi mật khẩu'}</button>
        <button type="button" className="market-secondary" disabled={pending} onClick={onCancel}>Hủy</button>
      </div>
    </form>
  </section></main>;
}
