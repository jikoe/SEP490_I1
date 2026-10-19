import React, { useState } from 'react';
import { Notice, Field, initials, VerificationBadge } from '../../components/ui/Common';

/**
 * UC-AUTH-03: Manage Profile Screen (SCR-11)
 * Supports RetailCustomer, Farmer, and WholesaleBuyer profiles
 */
export default function ProfileScreen({ fixture = 'retail-customer' }) {
  const isFarmer = fixture === 'farmer';
  const roleName = isFarmer ? 'Farmer' : 'RetailCustomer';

  // Demo state loaded according to SRS specs
  const [profile, setProfile] = useState({
    fullName: isFarmer ? 'Trần Văn Nông' : 'Nguyễn Văn A',
    phoneNumber: '0912345678',
    email: isFarmer ? 'farmer@agrisustain.vn' : 'nguyenvana@gmail.com',
    gender: 'Nam',
    birthDate: '1990-05-15',
    // Delivery address (for RetailCustomer)
    provinceCity: 'Hà Nội',
    district: 'Quận Cầu Giấy',
    ward: 'Phường Dịch Vọng Hậu',
    addressDetail: 'Số 18, Phố Duy Tân',
    rewardPoints: 1250,
    // Farmer specific fields
    farmName: isFarmer ? 'Hợp tác xã Nông nghiệp Xanh' : '',
    representativeName: isFarmer ? 'Trần Văn Nông' : '',
    productionScale: isFarmer ? '5.2 ha' : '',
    cultivationZone: isFarmer ? 'Đồng bằng Sông Hồng' : '',
    businessLicenseNumber: isFarmer ? 'BL-2026-HN-991' : '',
    verificationStatus: 'Approved',
    reputationScore: 4.85,
  });

  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);

  function handleChange(field, value) {
    setProfile((prev) => ({ ...prev, [field]: value }));
    setNotice(null);
    setError(null);
  }

  function handleSave(e) {
    e.preventDefault();
    if (!profile.fullName.trim()) {
      setError('Họ và tên không được để trống.');
      return;
    }
    if (!profile.phoneNumber.trim()) {
      setError('Số điện thoại không được để trống.');
      return;
    }

    setSaving(true);
    // Simulate API call to PUT /api/profile
    setTimeout(() => {
      setSaving(false);
      setNotice('MSG13: Cập nhật thông tin hồ sơ thành công!');
    }, 400);
  }

  function handleSubmitKyc() {
    if (!confirm('Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC (UC-VO-01) không?')) return;
    setSaving(true);
    setTimeout(() => {
      setSaving(false);
      setProfile((prev) => ({ ...prev, verificationStatus: 'Pending' }));
      setNotice('Hồ sơ của bạn đã được chuyển vào hàng đợi duyệt KYC của Verification Officer (UC-VO-01).');
    }, 400);
  }

  return (
    <div className="profile-container" style={{ maxWidth: 960, margin: '0 auto' }}>
      <div className="section-heading">
        <div>
          <p className="eyebrow">QUẢN LÝ THÔNG TIN TÀI KHOẢN (SCR-11 / UC-AUTH-03)</p>
          <h1>Hồ sơ cá nhân & Thông tin địa chỉ</h1>
          <p className="muted">Cập nhật hồ sơ thành viên, thông tin liên hệ và sổ địa chỉ nhận hàng giao hàng.</p>
        </div>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <span className="count">Điểm thưởng: {profile.rewardPoints} điểm</span>
          <VerificationBadge value={profile.verificationStatus} />
        </div>
      </div>

      <Notice error={Boolean(error)}>{error}</Notice>
      {notice && <Notice>{notice}</Notice>}

      <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 24 }}>
        {/* Cột trái: Tóm tắt thông tin tài khoản */}
        <aside className="panel padded" style={{ textAlign: 'center', height: 'fit-content' }}>
          <div
            style={{
              width: 80,
              height: 80,
              borderRadius: '50%',
              background: '#e8f1e9',
              color: '#1e6546',
              fontSize: 28,
              fontWeight: 700,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
              border: '2px solid #1e6546',
            }}
          >
            {initials(profile.fullName)}
          </div>
          <h3 style={{ margin: '0 0 4px', fontSize: 16 }}>{profile.fullName}</h3>
          <p className="muted" style={{ fontSize: 12, margin: '0 0 12px' }}>
            {profile.email}
          </p>
          <span className="badge Active" style={{ marginBottom: 20, display: 'inline-block' }}>
            {roleName}
          </span>

          <div style={{ borderTop: '1px solid #e0e7df', paddingTop: 16, textAlign: 'left', fontSize: 13 }}>
            <p style={{ margin: '6px 0' }}>
              <strong>Trạng thái KYC:</strong> <VerificationBadge value={profile.verificationStatus} />
            </p>
            {isFarmer && (
              <p style={{ margin: '6px 0' }}>
                <strong>Điểm uy tín:</strong> ⭐ {profile.reputationScore} / 5.0
              </p>
            )}
          </div>

          {profile.verificationStatus === 'NotVerified' && (
            <button
              type="button"
              className="primary"
              style={{ width: '100%', marginTop: 16 }}
              onClick={handleSubmitKyc}
              disabled={saving}
            >
              Gửi duyệt KYC (VO)
            </button>
          )}
        </aside>

        {/* Cột phải: Form cập nhật thông tin */}
        <main className="panel padded">
          <form onSubmit={handleSave}>
            <h2>1. Thông tin cơ bản</h2>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Họ và tên *">
                <input
                  type="text"
                  value={profile.fullName}
                  onChange={(e) => handleChange('fullName', e.target.value)}
                  disabled={saving}
                  maxLength={50}
                />
              </Field>

              <Field label="Số điện thoại *">
                <input
                  type="tel"
                  value={profile.phoneNumber}
                  onChange={(e) => handleChange('phoneNumber', e.target.value)}
                  disabled={saving}
                  maxLength={10}
                />
              </Field>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginTop: 12 }}>
              <Field label="Email đăng nhập (Cố định)">
                <input type="email" value={profile.email} readOnly style={{ background: '#f5f7f4' }} />
              </Field>

              <Field label="Vai trò hệ thống">
                <input type="text" value={roleName} readOnly style={{ background: '#f5f7f4' }} />
              </Field>
            </div>

            {/* Thông tin dành riêng cho Farmer nếu có */}
            {isFarmer && (
              <>
                <h2 style={{ marginTop: 28 }}>2. Thông tin nông trại & Giấy phép</h2>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Tên nông trại / HTX *">
                    <input
                      type="text"
                      value={profile.farmName}
                      onChange={(e) => handleChange('farmName', e.target.value)}
                      disabled={saving}
                    />
                  </Field>
                  <Field label="Số giấy phép kinh doanh">
                    <input
                      type="text"
                      value={profile.businessLicenseNumber}
                      onChange={(e) => handleChange('businessLicenseNumber', e.target.value)}
                      disabled={saving}
                    />
                  </Field>
                </div>
              </>
            )}

            {/* Sổ địa chỉ mặc định DeliveryAddress */}
            <h2 style={{ marginTop: 28 }}>{isFarmer ? '3. Địa chỉ nông trại' : '2. Sổ địa chỉ nhận hàng mặc định'}</h2>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Tỉnh / Thành phố *">
                <select value={profile.provinceCity} onChange={(e) => handleChange('provinceCity', e.target.value)}>
                  <option value="Hà Nội">Hà Nội</option>
                  <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                  <option value="Lâm Đồng">Lâm Đồng</option>
                  <option value="Đồng Tháp">Đồng Tháp</option>
                </select>
              </Field>

              <Field label="Quận / Huyện *">
                <input
                  type="text"
                  value={profile.district}
                  onChange={(e) => handleChange('district', e.target.value)}
                  disabled={saving}
                />
              </Field>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginTop: 12 }}>
              <Field label="Phường / Xã *">
                <input
                  type="text"
                  value={profile.ward}
                  onChange={(e) => handleChange('ward', e.target.value)}
                  disabled={saving}
                />
              </Field>

              <Field label="Địa chỉ chi tiết (Số nhà, đường) *">
                <input
                  type="text"
                  value={profile.addressDetail}
                  onChange={(e) => handleChange('addressDetail', e.target.value)}
                  disabled={saving}
                />
              </Field>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 28, paddingTop: 16, borderTop: '1px solid #e0e7df' }}>
              <button type="button" onClick={() => setNotice(null)}>Hủy bỏ</button>
              <button type="submit" className="primary" disabled={saving}>
                {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
              </button>
            </div>
          </form>
        </main>
      </div>
    </div>
  );
}
