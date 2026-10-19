import React, { useEffect, useState } from 'react';
import { money } from '../../components/ui/Common';
import { Icon } from './MarketIcons';

export function ProductPhoto({ url, name, className = '' }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [url]);
  const crop = url?.split('#')[1];
  const positions = { lettuce: '29.2% 64.8%', mango: '51.6% 64.8%', shrimp: '96.1% 64.8%' };
  if (positions[crop]) return <div role="img" aria-label={name}
    className={`product-photo product-photo-sprite ${className}`}
    style={{ backgroundImage: `url(${url.split('#')[0]})`, backgroundPosition: positions[crop] }} />;
  return url && !failed ? <img className={`product-photo ${className}`} src={url} alt={name} loading="lazy"
    onError={() => setFailed(true)} />
    : <div className={`product-photo product-photo-empty ${className}`} role="img" aria-label="Chưa có ảnh">
      <Icon name="leaf" size={50} /></div>;
}

export function Rating({ rating, count }) {
  return <span className="product-rating"><span aria-hidden="true">★</span>
    {rating == null ? 'Chưa có đánh giá' : `${Number(rating).toFixed(1)} `}
    {count > 0 && <small>({count} đánh giá)</small>}</span>;
}

export default function ProductCard({ product, onAdd, compact = false, busy = false }) {
  return <article className={`market-product-card ${compact ? 'market-product-compact' : ''}`}>
    <a href={`#/products/${product.id}`} className="product-image-link" aria-label={`Xem ${product.name}`}>
      <ProductPhoto url={product.imageUrl} name={product.name} />
      {product.certifications[0] && !compact && <span className="product-cert-badge">
        <Icon name="shield" size={13} />{product.certifications[0]}</span>}
    </a><div className="product-card-content">
      <a href={`#/products/${product.id}`} className="product-name">{product.name}</a>
      {!compact && <p className="product-supplier">{product.supplierName}</p>}
      <Rating rating={product.rating} count={product.reviewCount} />
      {!compact && <p className="product-region"><Icon name="pin" size={14} />{product.region || 'Chưa cập nhật xuất xứ'}</p>}
      <p className="product-price">{money(product.price)}<small> / {product.unit}</small></p>
      {!compact && <button className="market-primary product-add" disabled={product.availableQuantity <= 0 || busy}
        onClick={() => onAdd(product)}><Icon name="cart" size={18} />
        {product.availableQuantity <= 0 ? 'Hết hàng' : busy ? 'Đang thêm...' : 'Thêm vào giỏ'}</button>}
    </div>
  </article>;
}
