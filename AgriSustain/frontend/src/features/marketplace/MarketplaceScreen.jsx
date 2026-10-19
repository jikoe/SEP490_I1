import React, { useEffect, useState } from 'react';
import { queryString, request } from '../../api/client';
import { Pager } from '../../components/ui/Common';
import ProductCard from './ProductCard';
import { Icon } from './MarketIcons';

export default function MarketplaceScreen({ criteria, onCriteria, onAdd, adding, notice }) {
  const [filters, setFilters] = useState(null);
  const [page, setPage] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const serialized = queryString(criteria);
  useEffect(() => {
    const controller = new AbortController();
    request('/products/filters', { signal: controller.signal }).then(setFilters)
      .catch(error => { if (error.name !== 'AbortError') setError(error.message); });
    return () => controller.abort();
  }, [retry]);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError(''); setPage(null);
    request(`/products?${serialized}`, { signal: controller.signal }).then(setPage)
      .catch(error => { if (error.name !== 'AbortError') setError(error.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [serialized, retry]);
  function update(key, value) { onCriteria({ ...criteria, [key]: value, page: 1 }); }
  const allCount = filters?.categories.reduce((total, category) => total + category.count, 0) || 0;
  const categoryIcons = ['leaf', 'leaf', 'leaf', 'grid', 'leaf', 'leaf', 'grid'];
  return <main className="market-browse">
    <aside className="market-filters" aria-label="Bộ lọc sản phẩm">
      <h2><Icon name="grid" size={19} />Danh mục sản phẩm</h2>
      <button className={`market-category ${!criteria.categoryId ? 'selected' : ''}`}
        onClick={() => update('categoryId', '')}><Icon name="leaf" />Tất cả sản phẩm<span>{allCount}</span></button>
      {filters?.categories.map((category, index) => <button key={category.id}
        className={`market-category ${String(category.id) === criteria.categoryId ? 'selected' : ''}`}
        onClick={() => update('categoryId', String(category.id))}>
        <Icon name={categoryIcons[index % categoryIcons.length]} />{category.name}<span>{category.count}</span></button>)}
      <hr /><h2><Icon name="filter" size={20} />Bộ lọc</h2>
      <fieldset><legend>Khoảng giá</legend>
        {[[0, 49999.99, 'Dưới 50.000đ'], [50000, 100000, '50.000đ – 100.000đ'],
          [100000, 200000, '100.000đ – 200.000đ'], [200000.01, '', 'Trên 200.000đ']].map(([min, max, label]) =>
          <label className="market-check" key={min}><input type="radio" name="price"
            checked={String(criteria.minPrice) === String(min) && String(criteria.maxPrice ?? '') === String(max)}
            onChange={() => onCriteria({ ...criteria, minPrice: min, maxPrice: max, page: 1 })} />{label}</label>)}
        <div className="market-price-range"><input type="number" min="0" aria-label="Giá thấp nhất"
          placeholder="Từ" value={criteria.minPrice ?? ''} onChange={event => update('minPrice', event.target.value)} />
          <span>–</span><input type="number" min="0" aria-label="Giá cao nhất" placeholder="Đến"
            value={criteria.maxPrice ?? ''} onChange={event => update('maxPrice', event.target.value)} /></div>
      </fieldset>
      <label className="market-filter-select">Địa phương<select aria-label="Địa phương" value={criteria.region || ''}
        onChange={event => update('region', event.target.value)}><option value="">Tất cả tỉnh/thành</option>
        {filters?.regions.map(region => <option key={region}>{region}</option>)}</select></label>
      <fieldset><legend>Chứng nhận</legend>{filters?.certifications.map(type =>
        <label key={type} className="market-check"><input type="checkbox"
          checked={criteria.certification === type}
          onChange={event => update('certification', event.target.checked ? type : '')} />{type}</label>)}</fieldset>
      <label className="market-filter-select">Nhà cung cấp<select aria-label="Nhà cung cấp" value={criteria.supplierId || ''}
        onChange={event => update('supplierId', event.target.value)}><option value="">Tất cả nhà cung cấp</option>
        {filters?.suppliers.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <label className="market-filter-select">Mùa vụ<select aria-label="Mùa vụ" value={criteria.cropSeasonId || ''}
        onChange={event => update('cropSeasonId', event.target.value)}><option value="">Tất cả mùa vụ</option>
        {filters?.seasons.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
      <label className="market-filter-select">Đánh giá<select aria-label="Đánh giá" value={criteria.minRating || ''}
        onChange={event => update('minRating', event.target.value)}><option value="">Tất cả đánh giá</option>
        {[4, 3, 2, 1].map(star => <option key={star} value={star}>Từ {star} sao</option>)}</select></label>
      <button className="market-clear" onClick={() => onCriteria({})}>Xóa tất cả bộ lọc</button>
    </aside>
    <div className="market-catalog">
      <section className="market-hero"><div className="market-hero-art" /><div className="market-hero-copy">
        <h1>Nông sản Việt<br />Tươi ngon từ nông trại đến bàn ăn</h1>
        <p>Sản phẩm chất lượng – Người nông dân được hỗ trợ –<br />Cộng đồng phát triển bền vững</p>
        <button className="market-primary" onClick={() => document.getElementById('market-products')
          ?.scrollIntoView({ behavior: 'smooth', block: 'start' })}>Khám phá ngay <Icon name="arrow" size={18} /></button>
      </div></section>
      <nav className="market-category-tiles" aria-label="Khám phá danh mục">{filters?.categories.map((category, index) =>
        <button key={category.id} onClick={() => update('categoryId', String(category.id))}
          className={String(category.id) === criteria.categoryId ? 'selected' : ''}>
          <Icon name={categoryIcons[index % categoryIcons.length]} size={36} /><span>{category.name}</span></button>)}</nav>
      <section id="market-products" aria-busy={loading}>
        <div className="market-products-heading"><h2><Icon name="leaf" size={29} />
          {criteria.keyword ? `Kết quả cho “${criteria.keyword}”` : 'Sản phẩm nổi bật'}</h2>
          <label>Sắp xếp<select aria-label="Sắp xếp sản phẩm" value={criteria.sortBy || 'newest'}
            onChange={event => update('sortBy', event.target.value)}>
            <option value="newest">Mới nhất</option><option value="price-asc">Giá tăng dần</option>
            <option value="price-desc">Giá giảm dần</option><option value="rating">Đánh giá cao nhất</option></select></label></div>
        {notice && <p className="market-success" role="status">{notice}</p>}
        {error && <div className="market-empty" role="alert"><p>{error}</p>
          <button className="market-secondary" onClick={() => setRetry(retry + 1)}>Thử lại</button></div>}
        {loading && <p className="market-empty" role="status">Đang tải sản phẩm...</p>}
        {!loading && !error && page?.total === 0 && <div className="market-empty" role="status">
          <Icon name="search" size={38} /><h3>Không tìm thấy sản phẩm phù hợp</h3>
          <p>Hãy đổi từ khóa hoặc xóa bộ lọc để khám phá thêm nông sản.</p>
          <button className="market-secondary" onClick={() => onCriteria({})}>Xóa bộ lọc</button></div>}
        <div className="market-product-grid">{page?.items.map(product => <ProductCard key={product.id}
          product={product} onAdd={onAdd} busy={adding === product.id} />)}</div>
        {page && <div className="market-pagination"><Pager total={page.total} page={page.page}
          pageSize={page.pageSize} onPage={number => onCriteria({ ...criteria, page: number })} />
          <label>Hiển thị<select aria-label="Số sản phẩm mỗi trang" value={page.pageSize}
            onChange={event => update('pageSize', event.target.value)}>{[12, 24, 48].map(size =>
              <option key={size} value={size}>{size} sản phẩm</option>)}</select></label></div>}
      </section>
    </div>
  </main>;
}
