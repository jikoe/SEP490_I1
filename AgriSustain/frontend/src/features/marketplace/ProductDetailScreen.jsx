import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { localTime, money } from '../../components/ui/Common';
import { useAuth } from '../../hooks/useAuth';
import ProductCard, { ProductPhoto, Rating } from './ProductCard';
import { BrandMark, Icon } from './MarketIcons';

export default function ProductDetailScreen({ id, onAdd, adding, notice }) {
  const { user } = useAuth();
  const [detail, setDetail] = useState(null);
  const [similar, setSimilar] = useState([]);
  const [image, setImage] = useState(0);
  const [quantity, setQuantity] = useState(1);
  const [tab, setTab] = useState('info');
  const [error, setError] = useState('');
  const [shared, setShared] = useState('');
  const [farmOpen, setFarmOpen] = useState(false);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setDetail(null); setError(''); setSimilar([]); setImage(0); setQuantity(1); setTab('info');
    request(`/products/${id}`, { token: user?.accessToken, signal: controller.signal })
      .then(result => {
        setDetail(result);
        setQuantity(Math.min(1, result.product.availableQuantity) || 1);
        return request(`/products?categoryId=${result.product.categoryId}&pageSize=4`,
          { signal: controller.signal });
      }).then(result => setSimilar(result.items.filter(item => String(item.id) !== String(id)).slice(0, 3)))
      .catch(error => { if (error.name !== 'AbortError') setError(error.message); });
    return () => controller.abort();
  }, [id, user?.accessToken, retry]);
  if (error && !detail) return <main className="market-empty" role="alert"><h1>{error}</h1>
    <a href="#/marketplace">Quay lại Marketplace</a><button className="market-secondary"
      onClick={() => setRetry(retry + 1)}>Thử lại</button></main>;
  if (!detail) return <main className="market-empty" role="status">Đang tải thông tin sản phẩm...</main>;
  const product = detail.product;
  const images = detail.images.length ? detail.images : [{ url: product.imageUrl, altText: product.name }];
  const inStock = product.availableQuantity > 0;
  const validQuantity = Number.isFinite(quantity) && quantity > 0 && quantity <= product.availableQuantity
    && Math.round(quantity * 1000) / 1000 === quantity;
  return <main className="market-detail">
    <nav className="market-breadcrumb" aria-label="Đường dẫn"><a href="#/marketplace"><Icon name="home" size={18} />Trang chủ</a>
      <span>›</span><a href="#/marketplace">Nông sản</a><span>›</span>
      <a href={`#/marketplace?categoryId=${product.categoryId}`}>{product.categoryName}</a><span>›</span>
      <span aria-current="page">{product.name}</span></nav>
    <div className="market-detail-grid">
      <section className="market-gallery" aria-label="Hình ảnh sản phẩm">
        <div className="market-main-photo"><ProductPhoto url={images[image].url}
          name={images[image].altText || product.name} />
          {product.certifications[0] && <span className="product-cert-badge"><Icon name="shield" size={15} />{product.certifications[0]}</span>}
          {images.length > 1 && <><button className="gallery-prev" aria-label="Ảnh trước"
            onClick={() => setImage((image + images.length - 1) % images.length)}>‹</button>
            <button className="gallery-next" aria-label="Ảnh sau"
              onClick={() => setImage((image + 1) % images.length)}>›</button></>}
        </div><div className="market-thumbnails">{images.map((item, index) =>
          <button key={`${item.url}-${index}`} className={index === image ? 'selected' : ''}
            aria-label={`Xem ảnh ${index + 1}`} aria-pressed={index === image} onClick={() => setImage(index)}>
            <ProductPhoto url={item.url} name={item.altText || product.name} /></button>)}</div>
      </section>
      <section className="market-product-intro"><span className="market-category-pill">{product.categoryName}</span>
        <h1>{product.name}</h1><p className="market-short-description">
          {product.description || detail.description?.split('\n')[0] || `Nông sản từ ${product.supplierName}`}</p>
        <Rating rating={product.rating} count={product.reviewCount} />
        <p className="detail-price">{money(product.price)}<small> / {product.unit}</small></p>
        <ul className="market-benefits"><li><Icon name="leaf" />Xuất xứ: {product.region || 'Chưa cập nhật'}</li>
          {product.certifications.length > 0 && <li><Icon name="shield" />Chứng nhận: {product.certifications.join(', ')}</li>}
          <li><Icon name="truck" />Phương thức giao hàng được xác nhận khi thanh toán</li>
          <li><Icon name="check" />{inStock ? `Còn ${product.availableQuantity} ${product.unit}` : 'Sản phẩm hiện đã hết hàng'}</li></ul>
        <div className="market-purchase"><div className="market-quantity">
          <button aria-label="Giảm số lượng" disabled={quantity <= 1} onClick={() => setQuantity(Math.max(1, quantity - 1))}>−</button>
          <input type="number" aria-label="Số lượng" min="0.001" step="0.001" max={product.availableQuantity}
            value={quantity} onChange={event => setQuantity(event.target.value === '' ? '' : Number(event.target.value))} />
          <button aria-label="Tăng số lượng" disabled={quantity >= product.availableQuantity}
            onClick={() => setQuantity(Math.min(product.availableQuantity, Number(quantity) + 1))}>+</button>
        </div><span>({product.unit})</span><button className="market-primary" disabled={!inStock || !validQuantity || adding === product.id
            || Boolean(user && user.role !== 'RetailCustomer')}
          onClick={() => onAdd(product, quantity)}><Icon name="cart" />
          {!inStock ? 'Hết hàng' : adding === product.id ? 'Đang thêm...' : 'Thêm vào giỏ hàng'}</button></div>
        {inStock && !validQuantity && <p role="alert" className="market-field-error">Số lượng phải lớn hơn 0, tối đa 3 số lẻ và không vượt tồn kho.</p>}
        {user && user.role !== 'RetailCustomer' && <p className="market-muted">Giỏ hàng bán lẻ dành cho tài khoản người mua lẻ.</p>}
        {detail.canStartB2B && <p className="market-b2b">Giá sỉ: {money(detail.wholesalePrice)} / {product.unit},
          tối thiểu {detail.wholesaleMinQuantity} {product.unit}. Liên hệ nhà cung cấp để đặt sỉ.</p>}
        <div className="market-product-actions"><button className="market-secondary" onClick={async () => {
          try { await navigator.clipboard.writeText(window.location.href); setShared('Đã sao chép liên kết sản phẩm.'); }
          catch { setShared(`Liên kết sản phẩm: ${window.location.href}`); }
        }}><Icon name="share" size={18} />Chia sẻ</button></div>
        {(notice || shared) && <p role="status" className="market-success">{notice || shared}</p>}
      </section>
      <aside className="market-supplier-column"><section className="market-farm-card">
        <div className="market-farm-heading"><span className="market-farm-avatar"><BrandMark /></span>
          <div><h2>{product.supplierName}</h2><p><Icon name="shield" size={16} />Đã xác thực</p></div></div>
        <p><span className="market-star">★</span> Uy tín nhà cung cấp: {detail.farmRating}</p>
        <button className="market-secondary" onClick={() => setFarmOpen(!farmOpen)} aria-expanded={farmOpen}>
          Xem gian hàng <span>›</span></button>
        {farmOpen && <div className="market-farm-public"><p>Địa phương: {product.region || 'Chưa cập nhật'}</p>
          <p>Trạng thái: {detail.farmVerification === 'Approved' ? 'Đã xác thực' : detail.farmVerification}</p>
          <a href={`#/marketplace?supplierId=${product.supplierId}`}>Tất cả sản phẩm của nhà cung cấp →</a></div>}
      </section><section className="market-service-card">
        {[['truck', 'Vận chuyển', 'Phạm vi, phí vận chuyển và thời gian giao hàng được xác nhận tại thanh toán.'],
          ['card', 'Thanh toán', 'Các phương thức thanh toán khả dụng được hiển thị tại bước thanh toán.'],
          ['shield', 'Cam kết', 'Thông tin sản phẩm, chứng nhận và nguồn gốc được hiển thị theo hồ sơ đã duyệt.']].map(([icon, title, text]) =>
          <div key={icon}><Icon name={icon} size={28} /><div><h3>{title}</h3><p>{text}</p></div></div>)}
      </section></aside>
      <section className="market-info-panel"><div role="tablist" aria-label="Thông tin chi tiết sản phẩm" className="market-tabs">
        {[['info', 'Thông tin sản phẩm'], ['reviews', `Đánh giá (${product.reviewCount})`], ['shipping', 'Chính sách giao hàng']].map(([key, label]) =>
          <button key={key} id={`tab-${key}`} role="tab" aria-selected={tab === key} aria-controls="product-information-panel"
            tabIndex={tab === key ? 0 : -1} onKeyDown={event => {
              if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
              const keys = ['info', 'reviews', 'shipping'];
              const next = keys[(keys.indexOf(key) + (event.key === 'ArrowRight' ? 1 : 2)) % 3];
              setTab(next); document.getElementById(`tab-${next}`)?.focus();
            }} onClick={() => setTab(key)}>{label}</button>)}</div>
        <div role="tabpanel" id="product-information-panel" aria-labelledby={`tab-${tab}`} className="market-tab-content">
          {tab === 'info' && <><div className="market-description"><div><h3>Mô tả sản phẩm</h3>
            <p className="market-description-text">{detail.description || 'Nhà cung cấp chưa bổ sung mô tả sản phẩm.'}</p>
            <h3>Chứng nhận chất lượng</h3>{detail.certificates.length ? detail.certificates.map(certificate =>
              <p key={certificate.number}><Icon name="check" size={17} />{certificate.type} · {certificate.issuedBy}
                {certificate.expiresOn ? ` · Hết hạn: ${certificate.expiresOn}` : ''}</p>) : <p>Chưa có chứng nhận còn hiệu lực.</p>}
          </div><dl className="market-specifications"><dt className="market-spec-title"><Icon name="grid" size={17} />Thông số sản phẩm</dt><dd />
            <dt>Loại sản phẩm</dt><dd>{product.categoryName}</dd><dt>Xuất xứ</dt><dd>{product.region || 'Chưa cập nhật'}</dd>
            <dt>Đơn vị</dt><dd>{product.unit}</dd><dt>Tồn kho</dt><dd>{product.availableQuantity} {product.unit}</dd>
            <dt>Nhà cung cấp</dt><dd>{product.supplierName}</dd></dl></div>
            <h3>Lô hàng & nguồn gốc</h3>{detail.batches.length ? <div className="market-batches">{detail.batches.map(batch =>
              <article key={batch.id}><strong>{batch.code}</strong><span>{batch.season} · {batch.availableQuantity} {product.unit}</span>
                {batch.harvestedOn && <span>Thu hoạch: {batch.harvestedOn}</span>}
                {batch.expiresOn && <span>Hạn sử dụng: {batch.expiresOn}</span>}
                {batch.traceabilityUrl && /^(https?:\/\/|\/(?!\/))/.test(batch.traceabilityUrl) &&
                  <a href={batch.traceabilityUrl} target="_blank" rel="noreferrer">Xem truy xuất nguồn gốc ↗</a>}</article>)}</div>
              : <p>Chưa có lô hàng khả dụng.</p>}</>}
          {tab === 'reviews' && (detail.reviews.length ? detail.reviews.map((review, index) =>
            <article className="market-review" key={index}><span className="market-star">{'★'.repeat(review.rating)}</span>
              <small>Người mua đã xác thực · {localTime(review.createdAt)}</small><p>{review.comment || 'Không có nhận xét.'}</p></article>)
            : <p>Chưa có đánh giá từ đơn hàng hoàn tất.</p>)}
          {tab === 'shipping' && <><h3>Thông tin giao hàng</h3><p>Chọn địa chỉ nhận hàng tại bước thanh toán để xem phương thức và phí vận chuyển áp dụng.</p>
            <p>Vui lòng kiểm tra thông tin sản phẩm, lô hàng và điều kiện giao nhận trước khi đặt hàng.</p></>}
        </div>
      </section>
      <aside className="market-similar"><div><h2>Sản phẩm tương tự</h2>
        <a href={`#/marketplace?categoryId=${product.categoryId}`}>Xem tất cả</a></div>
        {similar.length ? similar.map(item => <ProductCard key={item.id} product={item} compact />)
          : <p className="market-muted">Chưa có sản phẩm tương tự.</p>}</aside>
    </div>
  </main>;
}
