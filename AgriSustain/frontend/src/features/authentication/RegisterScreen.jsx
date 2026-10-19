import React, { useState } from 'react';
import { Notice, Field, Modal } from '../../components/ui/Common';

/**
 * UC-AUTH-01: Register RetailCustomer or Farmer Account (SCR-07)
 * Includes UC-AUTH-08: Verify Email OTP modal
 */
export default function RegisterScreen({ navigate }) {
  const [role, setRole] = useState('RetailCustomer');
  const [form, setForm] = useState({
    fullName: '',
    phoneNumber: '',
    email: '',
    password: '',
    confirmPassword: '',
    agreeTerms: false,
  });
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [otpModalOpen, setOtpModalOpen] = useState(false);
  const [otpDigits, setOtpDigits] = useState(['', '', '', '', '', '']);
  const [otpError, setOtpError] = useState(null);
  const [otpCountdown, setOtpCountdown] = useState(300);
  const [successNotice, setSuccessNotice] = useState(null);

  function handleChange(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    setError(null);
  }

  function handleRegisterSubmit(e) {
    e.preventDefault();
    if (!form.fullName.trim()) {
      setError('Vui lòng nhập Họ và tên (tối đa 50 ký tự).');
      return;
    }
    if (!/^0\d{9}$/.test(form.phoneNumber.trim())) {
      setError('Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và đủ 10 chữ số).');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
      setError('Địa chỉ email không đúng định dạng.');
      return;
    }
    if (form.password.length < 8) {
      setError('Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số.');
      return;
    }
    if (form.password !== form.confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }
    if (!form.agreeTerms) {
      setError('Bạn cần đồng ý với Điều khoản dịch vụ & Chính sách bảo mật.');
      return;
    }

    setLoading(true);
    // Simulate API call to POST /api/auth/register (InitiateRegistrationAsync)
    setTimeout(() => {
      setLoading(false);
      setOtpModalOpen(true);
      setOtpDigits(['', '', '', '', '', '']);
      setOtpError(null);
    }, 500);
  }

  function handleOtpDigitChange(index, value) {
    if (!/^\d*$/.test(value)) return;
    const newDigits = [...otpDigits];
    newDigits[index] = value.slice(-1);
    setOtpDigits(newDigits);
    setOtpError(null);

    // Auto focus next input
    if (value && index < 5) {
      const nextInput = document.getElementById(`otp-input-${index + 1}`);
      nextInput?.focus();
    }
  }

  function handleOtpKeyDown(index, e) {
    if (e.key === 'Backspace' && !otpDigits[index] && index > 0) {
      const prevInput = document.getElementById(`otp-input-${index - 1}`);
      prevInput?.focus();
    }
  }

  function handleVerifyOtp(e) {
    e.preventDefault();
    const code = otpDigits.join('');
    if (code.length < 6) {
      setOtpError('Vui lòng nhập đủ 6 chữ số mã OTP.');
      return;
    }

    setLoading(true);
    // Simulate POST /api/auth/verify-otp (VerifyOtpAndCompleteRegistrationAsync)
    setTimeout(() => {
      setLoading(false);
      setOtpModalOpen(false);
      setSuccessNotice('MSG56: Đăng ký tài khoản thành công! Bạn có thể đăng nhập ngay bây giờ.');
      setTimeout(() => {
        if (navigate) navigate('/administration/users');
      }, 2500);
    }, 600);
  }

  return (
    <div className="register-container" style={{ maxWidth: 640, margin: '20px auto' }}>
      <div className="section-heading">
        <div>
          <p className="eyebrow">XÁC THỰC VÀ TÀI KHOẢN (SCR-07 / UC-AUTH-01)</p>
          <h1>Đăng ký tài khoản mới</h1>
          <p className="muted">Tạo tài khoản để tham gia chuỗi cung ứng nông sản an toàn AgriSustain.</p>
        </div>
      </div>

      <Notice error={Boolean(error)}>{error}</Notice>
      {successNotice && <Notice>{successNotice}</Notice>}

      <div className="panel padded">
        <form onSubmit={handleRegisterSubmit}>
          <div style={{ marginBottom: 20 }}>
            <label className="nav-label" style={{ margin: '0 0 10px', display: 'block' }}>BẠN ĐĂNG KÝ VỚI VAI TRÒ NÀO?</label>
            <div style={{ display: 'flex', gap: 16 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontWeight: role === 'RetailCustomer' ? 650 : 400 }}>
                <input
                  type="radio"
                  name="userRole"
                  value="RetailCustomer"
                  checked={role === 'RetailCustomer'}
                  onChange={() => setRole('RetailCustomer')}
                />
                RetailCustomer (Người mua lẻ)
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontWeight: role === 'Farmer' ? 650 : 400 }}>
                <input
                  type="radio"
                  name="userRole"
                  value="Farmer"
                  checked={role === 'Farmer'}
                  onChange={() => setRole('Farmer')}
                />
                Farmer (Nông dân / Hợp tác xã)
              </label>
            </div>
          </div>

          <Field label="Họ và tên *">
            <input
              type="text"
              placeholder="Ví dụ: Nguyễn Văn A"
              value={form.fullName}
              onChange={(e) => handleChange('fullName', e.target.value)}
              disabled={loading}
              maxLength={50}
            />
          </Field>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Field label="Số điện thoại *">
              <input
                type="tel"
                placeholder="0912345678"
                value={form.phoneNumber}
                onChange={(e) => handleChange('phoneNumber', e.target.value)}
                disabled={loading}
                maxLength={10}
              />
            </Field>

            <Field label="Email *">
              <input
                type="email"
                placeholder="nguyenvana@gmail.com"
                value={form.email}
                onChange={(e) => handleChange('email', e.target.value)}
                disabled={loading}
              />
            </Field>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
            <Field label="Mật khẩu *">
              <input
                type="password"
                placeholder="Ít nhất 8 ký tự"
                value={form.password}
                onChange={(e) => handleChange('password', e.target.value)}
                disabled={loading}
              />
            </Field>

            <Field label="Xác nhận mật khẩu *">
              <input
                type="password"
                placeholder="Nhập lại mật khẩu"
                value={form.confirmPassword}
                onChange={(e) => handleChange('confirmPassword', e.target.value)}
                disabled={loading}
              />
            </Field>
          </div>

          <div style={{ margin: '18px 0' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, cursor: 'pointer' }}>
              <input
                type="checkbox"
                checked={form.agreeTerms}
                onChange={(e) => handleChange('agreeTerms', e.target.checked)}
                disabled={loading}
              />
              Tôi đồng ý với <strong>Điều khoản dịch vụ</strong> & <strong>Chính sách bảo mật</strong> của AgriSustain.
            </label>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 24, paddingTop: 16, borderTop: '1px solid #e0e7df' }}>
            <span className="muted" style={{ fontSize: 13 }}>
              Đã có tài khoản? <a href="#/login" style={{ color: '#1e6546', fontWeight: 600 }}>Đăng nhập</a>
            </span>
            <div style={{ display: 'flex', gap: 12 }}>
              <button type="button" onClick={() => setForm({ fullName: '', phoneNumber: '', email: '', password: '', confirmPassword: '', agreeTerms: false })}>
                Hủy
              </button>
              <button type="submit" className="primary" disabled={loading}>
                {loading ? 'Đang xử lý...' : 'Đăng ký tài khoản'}
              </button>
            </div>
          </div>
        </form>
      </div>

      {/* UC-AUTH-08: Modal Xác thực Email OTP */}
      {otpModalOpen && (
        <Modal title="Xác thực mã OTP (UC-AUTH-08)" onClose={() => setOtpModalOpen(false)}>
          <div style={{ textAlign: 'center', padding: '10px 0 20px' }}>
            <p style={{ margin: '0 0 12px' }}>
              Mã xác thực 6 chữ số đã được gửi qua email đến:
              <br />
              <strong style={{ color: '#1e6546' }}>{form.email}</strong>
            </p>

            <Notice error={Boolean(otpError)}>{otpError}</Notice>

            <div style={{ display: 'flex', justifyContent: 'center', gap: 8, margin: '20px 0' }}>
              {otpDigits.map((digit, idx) => (
                <input
                  key={idx}
                  id={`otp-input-${idx}`}
                  type="text"
                  maxLength={1}
                  value={digit}
                  onChange={(e) => handleOtpDigitChange(idx, e.target.value)}
                  onKeyDown={(e) => handleOtpKeyDown(idx, e)}
                  style={{
                    width: 44,
                    height: 52,
                    fontSize: 22,
                    fontWeight: 700,
                    textAlign: 'center',
                    border: '2px solid #d4dfd7',
                    borderRadius: 8,
                    outline: 'none',
                  }}
                />
              ))}
            </div>

            <p className="muted" style={{ fontSize: 12, marginBottom: 20 }}>
              Mã có hiệu lực trong 5 phút (Demo OTP: <strong>123456</strong>)
            </p>

            <div style={{ display: 'flex', justifyContent: 'center', gap: 12 }}>
              <button type="button" onClick={() => setOtpModalOpen(false)}>Hủy</button>
              <button type="button" className="primary" onClick={handleVerifyOtp} disabled={loading}>
                {loading ? 'Đang xác minh...' : 'Xác minh OTP'}
              </button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
