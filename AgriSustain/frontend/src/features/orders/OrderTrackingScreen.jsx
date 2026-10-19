import React, { useState } from 'react';
import { money, localTime, Notice, Modal, Field } from '../../components/ui/Common';

/**
 * UC-RC-04: Track Retail Order Screen (SCR-16)
 * Includes Order History, Order Details, QR Batch Link, Review & Dispute Actions
 */
export default function OrderTrackingScreen({ fixture = 'retail-customer', navigate }) {
  const [activeTab, setActiveTab] = useState('All');
  const [keyword, setKeyword] = useState('');
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [reviewModalOpen, setReviewModalOpen] = useState(false);
  const [complaintModalOpen, setComplaintModalOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState(null);

  // Mock sample orders conforming to OrderSummaryDto and OrderDetailDto in Class Diagram
  const [orders, setOrders] = useState([
    {
      orderId: 101,
      orderCode: 'ORD-2026-9812',
      orderDate: '2026-10-18T09:30:00Z',
      finalAmount: 325000,
      orderStatus: 'Shipping', // Đang vận chuyển
      paymentStatus: 'Paid',
      recipientName: 'Nguyễn Văn A',
      recipientPhone: '0912345678',
      shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
      trackingNumber: 'AGRI-EXPRESS-VN8823',
      sellerOrders: [
        {
          sellerOrderId: 201,
          farmName: 'HTX Nông Nghiệp Lâm Đồng',
          items: [
            {
              orderItemId: 301,
              productName: 'Bơ Sáp 034 Hữu Cơ (Chứng chỉ VietGAP)',
              harvestBatchCode: 'LOT-BO-2026-08',
              quantity: 5,
              unit: 'kg',
              unitPriceSnapshot: 65000,
              isReviewed: false,
            },
          ],
        },
      ],
    },
    {
      orderId: 102,
      orderCode: 'ORD-2026-8940',
      orderDate: '2026-10-12T14:15:00Z',
      finalAmount: 450000,
      orderStatus: 'Completed', // Đã hoàn thành
      paymentStatus: 'Paid',
      recipientName: 'Nguyễn Văn A',
      recipientPhone: '0912345678',
      shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
      trackingNumber: 'AGRI-EXPRESS-VN7719',
      sellerOrders: [
        {
          sellerOrderId: 202,
          farmName: 'Nông trại Xanh Đồng Tháp',
          items: [
            {
              orderItemId: 302,
              productName: 'Xoài Cát Chu Cao Lãnh Đồng Tháp',
              harvestBatchCode: 'LOT-XOAI-09',
              quantity: 10,
              unit: 'kg',
              unitPriceSnapshot: 45000,
              isReviewed: false,
            },
          ],
        },
      ],
    },
    {
      orderId: 103,
      orderCode: 'ORD-2026-7731',
      orderDate: '2026-10-05T08:00:00Z',
      finalAmount: 180000,
      orderStatus: 'Cancelled', // Đã hủy
      paymentStatus: 'Refunded',
      recipientName: 'Nguyễn Văn A',
      recipientPhone: '0912345678',
      shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
      trackingNumber: '—',
      sellerOrders: [
        {
          sellerOrderId: 203,
          farmName: 'Nông Hộ Rau Sạch An Phú',
          items: [
            {
              orderItemId: 303,
              productName: 'Cải thìa sạch Bắc Ninh',
              harvestBatchCode: 'BN-20260920-01',
              quantity: 4,
              unit: 'kg',
              unitPriceSnapshot: 45000,
              isReviewed: true,
            },
          ],
        },
      ],
    },
  ]);

  // Status mapping
  const statusBadges = {
    PendingPayment: { label: 'Chờ thanh toán', class: 'Pending' },
    Paid: { label: 'Đã thanh toán', class: 'Active' },
    Processing: { label: 'Đang chuẩn bị', class: 'neutral' },
    Shipping: { label: 'Đang giao hàng', class: 'neutral' },
    Delivered: { label: 'Đã giao hàng', class: 'Active' },
    Completed: { label: 'Hoàn thành', class: 'Active' },
    Cancelled: { label: 'Đã hủy', class: 'Suspended' },
  };

  // Filter orders by tab and search
  const filteredOrders = orders.filter((order) => {
    if (activeTab === 'Completed' && order.orderStatus !== 'Completed') return false;
    if (activeTab === 'Cancelled' && order.orderStatus !== 'Cancelled') return false;
    if (activeTab === 'Shipping' && order.orderStatus !== 'Shipping') return false;
    if (keyword.trim()) {
      const matchCode = order.orderCode.toLowerCase().includes(keyword.toLowerCase());
      const matchProduct = order.sellerOrders.some((so) =>
        so.items.some((item) => item.productName.toLowerCase().includes(keyword.toLowerCase()))
      );
      return matchCode || matchProduct;
    }
    return true;
  });

  return (
    <div className="order-tracking-container" style={{ maxWidth: 1040, margin: '0 auto' }}>
      <div className="section-heading">
        <div>
          <p className="eyebrow">THEO DÕI ĐƠN HÀNG (SCR-16 / UC-RC-04)</p>
          <h1>Lịch sử & Theo dõi chi tiết đơn hàng</h1>
          <p className="muted">Kiểm tra hành trình vận chuyển bưu kiện, tra cứu mã lô sản phẩm và gửi đánh giá.</p>
        </div>
      </div>

      {toastMessage && <Notice>{toastMessage}</Notice>}

      {/* Tabs lọc trạng thái theo SRS 6.3.1 */}
      <div className="filters" style={{ padding: '0 0 16px', borderBottom: '2px solid #e0e7df' }}>
        <div style={{ display: 'flex', gap: 8 }}>
          {['All', 'Shipping', 'Completed', 'Cancelled'].map((tab) => {
            const labels = {
              All: 'Tất cả đơn',
              Shipping: 'Đang giao hàng',
              Completed: 'Đã hoàn thành',
              Cancelled: 'Đã hủy',
            };
            return (
              <button
                key={tab}
                type="button"
                className={activeTab === tab ? 'selected' : ''}
                onClick={() => setActiveTab(tab)}
              >
                {labels[tab]}
              </button>
            );
          })}
        </div>

        <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
          <input
            type="search"
            placeholder="Tìm theo mã đơn hoặc sản phẩm..."
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            style={{ width: 280 }}
          />
        </div>
      </div>

      {/* Danh sách đơn hàng */}
      <div style={{ marginTop: 24, display: 'grid', gap: 20 }}>
        {filteredOrders.length === 0 ? (
          <div className="panel padded" style={{ textAlign: 'center', padding: '40px 20px' }}>
            <p style={{ fontSize: 16, fontWeight: 600, color: '#617469' }}>
              MSG23: Bạn chưa có đơn hàng nào trong mục này.
            </p>
            <button
              type="button"
              className="primary"
              style={{ marginTop: 12 }}
              onClick={() => { if (navigate) navigate('/checkout'); }}
            >
              Tiếp tục mua sắm nông sản
            </button>
          </div>
        ) : (
          filteredOrders.map((order) => {
            const badge = statusBadges[order.orderStatus] || { label: order.orderStatus, class: 'neutral' };
            return (
              <div key={order.orderId} className="panel padded">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #eef1ec', paddingBottom: 12, marginBottom: 16 }}>
                  <div>
                    <span style={{ fontWeight: 700, fontSize: 15, color: '#1e6546' }}>#{order.orderCode}</span>
                    <span className="muted" style={{ fontSize: 12, marginLeft: 12 }}>
                      Ngày đặt: {localTime(order.orderDate)}
                    </span>
                  </div>
                  <div>
                    <span className={`badge ${badge.class}`}>{badge.label}</span>
                  </div>
                </div>

                {/* Danh sách sản phẩm của các nhà vườn */}
                {order.sellerOrders.map((so) => (
                  <div key={so.sellerOrderId} style={{ marginBottom: 14 }}>
                    <div className="muted" style={{ fontSize: 12, marginBottom: 8 }}>
                      🌱 Nhà cung cấp: <strong>{so.farmName}</strong>
                    </div>

                    {so.items.map((item) => (
                      <div
                        key={item.orderItemId}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          padding: '8px 0',
                          borderTop: '1px dashed #f0f2ef',
                        }}
                      >
                        <div>
                          <div style={{ fontWeight: 600, fontSize: 14 }}>{item.productName}</div>
                          <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>
                            Mã lô thu hoạch: <strong style={{ color: '#1e6546' }}>{item.harvestBatchCode}</strong> | Số lượng: {item.quantity} {item.unit}
                          </div>
                        </div>

                        <div style={{ textAlign: 'right' }}>
                          <span style={{ fontWeight: 700, color: '#1e6546' }}>
                            {money(item.quantity * item.unitPriceSnapshot)}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                ))}

                {/* Tracking & Hàng nút hành động theo Flow của Sequence Diagram */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: '1px solid #eef1ec', paddingTop: 14, marginTop: 12 }}>
                  <div style={{ fontSize: 13 }}>
                    Tổng thanh toán: <strong style={{ fontSize: 17, color: '#1e6546' }}>{money(order.finalAmount)}</strong>
                    {order.trackingNumber !== '—' && (
                      <span className="muted" style={{ marginLeft: 16 }}>
                        Mã bưu gửi: <strong>{order.trackingNumber}</strong>
                      </span>
                    )}
                  </div>

                  <div style={{ display: 'flex', gap: 10 }}>
                    <button type="button" onClick={() => setSelectedOrder(order)}>
                      Xem chi tiết đơn
                    </button>

                    {/* Mở liên kết SCR-TRC-01 truy xuất nguồn gốc lô */}
                    <button
                      type="button"
                      style={{ background: '#e8f1e9', borderColor: '#72e48a', color: '#1e6546', fontWeight: 600 }}
                      onClick={() => alert(`Chuyển đến màn hình Tra cứu nguồn gốc (SCR-TRC-01) cho mã lô: ${order.sellerOrders[0].items[0].harvestBatchCode}`)}
                    >
                      🔍 Tra cứu nguồn gốc
                    </button>

                    {/* UC-RC-05: Đánh giá sản phẩm khi hoàn thành */}
                    {order.orderStatus === 'Completed' && (
                      <button
                        type="button"
                        style={{ background: '#fff9e6', borderColor: '#c28e00', color: '#c28e00', fontWeight: 600 }}
                        onClick={() => { setSelectedOrder(order); setReviewModalOpen(true); }}
                      >
                        ⭐ Đánh giá (UC-RC-05)
                      </button>
                    )}

                    {/* UC-RC-06: Khiếu nại khi có sự cố */}
                    {order.orderStatus === 'Completed' && (
                      <button
                        type="button"
                        onClick={() => { setSelectedOrder(order); setComplaintModalOpen(true); }}
                      >
                        Khiếu nại (UC-RC-06)
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Modal Chi tiết đơn hàng */}
      {selectedOrder && !reviewModalOpen && !complaintModalOpen && (
        <Modal title={`Chi tiết đơn hàng #${selectedOrder.orderCode}`} onClose={() => setSelectedOrder(null)}>
          <div style={{ padding: '10px 0' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
              <div>
                <p className="muted" style={{ fontSize: 12 }}>THÔNG TIN NGƯỜI NHẬN</p>
                <p><strong>{selectedOrder.recipientName}</strong> - {selectedOrder.recipientPhone}</p>
                <p className="muted" style={{ fontSize: 13 }}>{selectedOrder.shippingAddress}</p>
              </div>
              <div>
                <p className="muted" style={{ fontSize: 12 }}>TRẠNG THÁI THANH TOÁN</p>
                <p>Phương thức: <strong>VietQR / PayOS</strong></p>
                <p>Trạng thái: <span className="badge Active">{selectedOrder.paymentStatus}</span></p>
              </div>
            </div>

            <div style={{ borderTop: '1px solid #e0e7df', paddingTop: 14 }}>
              <p className="muted" style={{ fontSize: 12, marginBottom: 8 }}>CHI TIẾT SẢN PHẨM & LÔ THU HOẠCH</p>
              {selectedOrder.sellerOrders.map((so) =>
                so.items.map((item) => (
                  <div key={item.orderItemId} style={{ display: 'flex', justifyContent: 'space-between', padding: '6px 0', fontSize: 13 }}>
                    <span>{item.productName} (Mã lô: <strong>{item.harvestBatchCode}</strong>) × {item.quantity}</span>
                    <span style={{ fontWeight: 600 }}>{money(item.quantity * item.unitPriceSnapshot)}</span>
                  </div>
                ))
              )}
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 20, paddingTop: 14, borderTop: '2px solid #e0e7df' }}>
              <span style={{ fontSize: 15, fontWeight: 700 }}>Tổng cộng:</span>
              <span style={{ fontSize: 19, fontWeight: 700, color: '#1e6546' }}>{money(selectedOrder.finalAmount)}</span>
            </div>
          </div>
        </Modal>
      )}

      {/* Modal Đánh giá sản phẩm (UC-RC-05) */}
      {reviewModalOpen && (
        <Modal title="Đánh giá sản phẩm (UC-RC-05)" onClose={() => setReviewModalOpen(false)}>
          <div style={{ padding: '10px 0' }}>
            <p>Đánh giá chất lượng nông sản cho đơn hàng <strong>#{selectedOrder?.orderCode}</strong></p>
            <div style={{ margin: '16px 0' }}>
              <label className="nav-label" style={{ display: 'block', margin: '0 0 6px' }}>CHỌN SỐ SAO:</label>
              <div style={{ fontSize: 24, cursor: 'pointer', color: '#e68a00' }}>★★★★★</div>
            </div>
            <Field label="Nhận xét của bạn *">
              <textarea rows={4} placeholder="Nông sản tươi ngon, đóng gói cẩn thận, giao nhanh..." />
            </Field>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 16 }}>
              <button type="button" onClick={() => setReviewModalOpen(false)}>Hủy</button>
              <button
                type="button"
                className="primary"
                onClick={() => {
                  setReviewModalOpen(false);
                  setToastMessage('Gửi đánh giá thành công! Đánh giá sẽ được hiển thị sau khi qua kiểm duyệt của Content Moderator.');
                }}
              >
                Gửi đánh giá
              </button>
            </div>
          </div>
        </Modal>
      )}

      {/* Modal Khiếu nại đơn hàng (UC-RC-06) */}
      {complaintModalOpen && (
        <Modal title="Gửi khiếu nại đơn hàng (UC-RC-06)" onClose={() => setComplaintModalOpen(false)}>
          <div style={{ padding: '10px 0' }}>
            <p style={{ fontSize: 13 }} className="muted">
              Áp dụng cho đơn hàng giao trong vòng 48h có sự cố về chất lượng, hư hỏng hoặc sai hàng.
            </p>
            <div style={{ marginTop: 12 }}>
              <Field label="Lý do khiếu nại *">
                <select>
                  <option>Sản phẩm bị dập nát, hư hỏng trong quá trình vận chuyển</option>
                  <option>Sản phẩm không đúng với mô tả / sai loại</option>
                  <option>Thiếu số lượng / không đúng khối lượng</option>
                </select>
              </Field>
            </div>
            <div style={{ marginTop: 12 }}>
              <Field label="Mô tả chi tiết bằng chứng">
                <textarea rows={3} placeholder="Mô tả tình trạng hàng khi mở hộp..." />
              </Field>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 16 }}>
              <button type="button" onClick={() => setComplaintModalOpen(false)}>Hủy</button>
              <button
                type="button"
                className="primary"
                onClick={() => {
                  setComplaintModalOpen(false);
                  setToastMessage('Khiếu nại của bạn đã được tiếp nhận. Đội ngũ Customer Support (UC-CS-01) sẽ xử lý trong 24h.');
                }}
              >
                Gửi khiếu nại
              </button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
