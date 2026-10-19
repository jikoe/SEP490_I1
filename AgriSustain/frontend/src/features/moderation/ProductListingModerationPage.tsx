import { useState } from 'react';

type ListingStatus = 'Pending Approval' | 'Approved' | 'Rejected';

interface ProductListing {
  id: string;
  name: string;
  category: string;
  images: string[];
  price: number;
  quantity: number;
  unit: string;
  farmer: {
    name: string;
    email: string;
    phone: string;
  };
  farm: {
    name: string;
    location: string;
    size: string;
  };
  harvest: {
    date: string;
    method: string;
    notes: string;
  };
  traceability: {
    batchId: string;
    plantingDate: string;
    growingPractices: string;
    certificates: string[];
  };
  submittedAt: string;
  status: ListingStatus;
  rejectionReason?: string;
}

const initialListings: ProductListing[] = [
  {
    id: 'PRD-2026-0418',
    name: 'Hữu cơ Đà Lạt',
    category: 'Rau củ hữu cơ',
    images: [
      'https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=1200&q=85',
      'https://images.unsplash.com/photo-1518977676601-b53f82aba655?auto=format&fit=crop&w=1200&q=85',
      'https://images.unsplash.com/photo-1566385101042-1a0aa0c1268c?auto=format&fit=crop&w=1200&q=85'
    ],
    price: 45000,
    quantity: 120,
    unit: 'kg',
    farmer: {
      name: 'Nguyễn Thị Mai',
      email: 'mai.nguyen@example.com',
      phone: '+84 912 345 678'
    },
    farm: {
      name: 'Nông trại Xanh Đà Lạt',
      location: 'Xuân Thọ, Đà Lạt, Lâm Đồng',
      size: '2.5 ha'
    },
    harvest: {
      date: '18/10/2026',
      method: 'Thu hoạch thủ công',
      notes: 'Thu hoạch vào sáng sớm, đóng gói trong ngày.'
    },
    traceability: {
      batchId: 'DL-VEG-261018-A',
      plantingDate: '20/08/2026',
      growingPractices: 'Canh tác theo hướng hữu cơ, không sử dụng thuốc trừ sâu tổng hợp.',
      certificates: ['VietGAP', 'Chứng nhận cơ sở hữu cơ']
    },
    submittedAt: '20/10/2026 08:42',
    status: 'Pending Approval'
  },
  {
    id: 'PRD-2026-0417',
    name: 'Cam sành Vĩnh Long',
    category: 'Trái cây',
    images: [
      'https://images.unsplash.com/photo-1547514701-42782101795e?auto=format&fit=crop&w=1200&q=85',
      'https://images.unsplash.com/photo-1580052614034-c55d20bfee3b?auto=format&fit=crop&w=1200&q=85'
    ],
    price: 38000,
    quantity: 300,
    unit: 'kg',
    farmer: {
      name: 'Trần Văn Hùng',
      email: 'hung.tran@example.com',
      phone: '+84 938 111 222'
    },
    farm: {
      name: 'Vườn cam Tân Phú',
      location: 'Tân Phú, Tam Bình, Vĩnh Long',
      size: '1.8 ha'
    },
    harvest: {
      date: '22/10/2026',
      method: 'Thu hoạch thủ công',
      notes: 'Cam được phân loại theo kích cỡ trước khi đóng thùng.'
    },
    traceability: {
      batchId: 'VL-ORG-261022-B',
      plantingDate: '15/03/2023',
      growingPractices: 'Sử dụng phân bón hữu cơ và quy trình quản lý dịch hại tổng hợp.',
      certificates: ['VietGAP']
    },
    submittedAt: '20/10/2026 07:15',
    status: 'Pending Approval'
  },
  {
    id: 'PRD-2026-0416',
    name: 'Gạo ST25 mùa mới',
    category: 'Ngũ cốc',
    images: [
      'https://images.unsplash.com/photo-1586201375761-83865001e31c?auto=format&fit=crop&w=1200&q=85',
      'https://images.unsplash.com/photo-1536304993881-ff6e9eefa2a6?auto=format&fit=crop&w=1200&q=85'
    ],
    price: 32000,
    quantity: 800,
    unit: 'kg',
    farmer: {
      name: 'Lê Minh Phúc',
      email: 'phuc.le@example.com',
      phone: '+84 979 555 010'
    },
    farm: {
      name: 'Hợp tác xã Lúa thơm Sóc Trăng',
      location: 'Mỹ Xuyên, Sóc Trăng',
      size: '12 ha'
    },
    harvest: {
      date: '15/10/2026',
      method: 'Thu hoạch bằng máy gặt liên hợp',
      notes: 'Lúa được sấy trong vòng 12 giờ sau thu hoạch.'
    },
    traceability: {
      batchId: 'ST25-SOC-261015-C',
      plantingDate: '10/06/2026',
      growingPractices: 'Sản xuất theo quy trình giảm phát thải, ghi nhật ký canh tác.',
      certificates: []
    },
    submittedAt: '19/10/2026 16:30',
    status: 'Pending Approval'
  }
];

const formatPrice = (amount: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(amount);

function SectionTitle({ children }: { children: string }) {
  return <h3 className="mb-4 text-base font-semibold text-slate-900">{children}</h3>;
}

export function ProductListingModerationPage({ onBack }: { onBack: () => void }) {
  const [listings, setListings] = useState(initialListings);
  const [selectedId, setSelectedId] = useState(initialListings[0].id);
  const [activeTab, setActiveTab] = useState<'Pending Approval' | 'Reviewed'>('Pending Approval');
  const [rejectionReason, setRejectionReason] = useState('');
  const [actionError, setActionError] = useState('');
  const [notification, setNotification] = useState('');
  const [activeImage, setActiveImage] = useState(0);

  const visibleListings = listings.filter((listing) =>
    activeTab === 'Pending Approval' ? listing.status === activeTab : listing.status !== 'Pending Approval'
  );
  const selectedListing = visibleListings.find((listing) => listing.id === selectedId) ?? visibleListings[0];
  const pendingCount = listings.filter((listing) => listing.status === 'Pending Approval').length;

  const updateListing = (status: Exclude<ListingStatus, 'Pending Approval'>, reason?: string) => {
    if (!selectedListing) return;
    if (status === 'Rejected' && !reason?.trim()) {
      setActionError('Vui lòng nhập lý do từ chối trước khi tiếp tục.');
      return;
    }

    setListings((current) =>
      current.map((listing) =>
        listing.id === selectedListing.id
          ? { ...listing, status, rejectionReason: reason?.trim() }
          : listing
      )
    );
    setActionError('');
    setRejectionReason('');
    setNotification(
      status === 'Approved'
        ? `Đã duyệt "${selectedListing.name}". (Demo) Thông báo kết quả được mô phỏng cho farmer ${selectedListing.farmer.name}.`
        : `Đã từ chối "${selectedListing.name}" với lý do: ${reason?.trim()}. (Demo) Thông báo kết quả được mô phỏng cho farmer ${selectedListing.farmer.name}.`
    );
  };

  const tabs: Array<{ label: string; value: 'Pending Approval' | 'Reviewed' }> = [
    { label: 'Chờ duyệt', value: 'Pending Approval' },
    { label: 'Đã xử lý', value: 'Reviewed' }
  ];

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={onBack}
              className="rounded-xl border border-slate-200 px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-50"
            >
              ← Dashboard
            </button>
            <div className="hidden h-9 w-px bg-slate-200 sm:block" />
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-700">AgriSustain</p>
              <p className="text-sm font-semibold text-slate-900">Administration / Moderation</p>
            </div>
          </div>
          <span className="rounded-full bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700">
            Content Moderator
          </span>
        </div>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <div className="mb-7 flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
          <div>
            <p className="text-sm font-medium text-emerald-700">Quản trị nội dung</p>
            <h1 className="mt-1 text-3xl font-bold tracking-tight text-slate-900">Duyệt tin đăng sản phẩm</h1>
            <p className="mt-2 text-sm text-slate-500">
              Xem thông tin sản phẩm, nguồn gốc và chứng nhận trước khi đưa tin đăng lên sàn.
            </p>
          </div>
          <div className="rounded-2xl border border-amber-200 bg-amber-50 px-5 py-3">
            <p className="text-xs font-medium text-amber-800">Đang chờ duyệt</p>
            <p className="mt-0.5 text-2xl font-bold text-amber-900">{pendingCount}</p>
          </div>
        </div>

        {notification ? (
          <div
            role="status"
            className="mb-6 flex items-start justify-between gap-3 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900"
          >
            <span>{notification}</span>
            <button
              type="button"
              aria-label="Đóng thông báo"
              onClick={() => setNotification('')}
              className="font-semibold text-emerald-800"
            >
              ×
            </button>
          </div>
        ) : null}

        <div className="mb-5 flex gap-2 border-b border-slate-200">
          {tabs.map((tab) => {
            const count =
              tab.value === 'Pending Approval'
                ? pendingCount
                : listings.filter((listing) => listing.status !== 'Pending Approval').length;
            return (
              <button
                key={tab.value}
                type="button"
                onClick={() => setActiveTab(tab.value)}
                className={`border-b-2 px-4 py-3 text-sm font-semibold transition ${
                  activeTab === tab.value
                    ? 'border-emerald-600 text-emerald-700'
                    : 'border-transparent text-slate-500 hover:text-slate-800'
                }`}
              >
                {tab.label}
                <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{count}</span>
              </button>
            );
          })}
        </div>

        {visibleListings.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-16 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-50 text-2xl text-emerald-700">
              ✓
            </div>
            <h2 className="mt-4 text-lg font-semibold text-slate-900">Không có tin đăng cần xử lý</h2>
            <p className="mt-1 text-sm text-slate-500">Tất cả sản phẩm trong danh sách này đã được xem xét.</p>
          </div>
        ) : (
          <div className="grid items-start gap-6 lg:grid-cols-[340px_minmax(0,1fr)]">
            <aside className="space-y-3">
              {visibleListings.map((listing) => (
                <button
                  key={listing.id}
                  type="button"
                  onClick={() => {
                    setSelectedId(listing.id);
                    setRejectionReason('');
                    setActionError('');
                    setActiveImage(0);
                  }}
                  className={`w-full rounded-2xl border bg-white p-4 text-left transition ${
                    selectedListing?.id === listing.id
                      ? 'border-emerald-500 ring-2 ring-emerald-100'
                      : 'border-slate-200 hover:border-emerald-300'
                  }`}
                >
                  <div className="flex gap-3">
                    <img
                      src={listing.images[0]}
                      alt=""
                      className="h-20 w-20 shrink-0 rounded-xl object-cover"
                    />
                    <div className="min-w-0 flex-1">
                      <span
                        className={`inline-flex rounded-full px-2 py-1 text-[11px] font-semibold ${
                          listing.status === 'Pending Approval'
                            ? 'bg-amber-50 text-amber-800'
                            : listing.status === 'Approved'
                              ? 'bg-emerald-50 text-emerald-800'
                              : 'bg-rose-50 text-rose-800'
                        }`}
                      >
                        {listing.status === 'Pending Approval'
                          ? 'Chờ duyệt'
                          : listing.status === 'Approved'
                            ? 'Đã duyệt'
                            : 'Đã từ chối'}
                      </span>
                      <h2 className="mt-2 truncate font-semibold text-slate-900">{listing.name}</h2>
                      <p className="mt-0.5 truncate text-xs text-slate-500">{listing.farmer.name}</p>
                      <p className="mt-1 text-xs text-slate-400">{listing.submittedAt}</p>
                    </div>
                  </div>
                </button>
              ))}
            </aside>

            {selectedListing ? (
              <section className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                <div className="border-b border-slate-200 px-5 py-5 sm:px-7">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className="text-xs font-semibold uppercase tracking-wider text-slate-400">
                        {selectedListing.id}
                      </p>
                      <h2 className="mt-1 text-2xl font-bold text-slate-900">{selectedListing.name}</h2>
                      <p className="mt-1 text-sm text-slate-500">
                        Gửi duyệt {selectedListing.submittedAt} · {selectedListing.category}
                      </p>
                    </div>
                    <span
                      className={`rounded-full px-3 py-1.5 text-xs font-semibold ${
                        selectedListing.status === 'Pending Approval'
                          ? 'bg-amber-50 text-amber-800'
                          : selectedListing.status === 'Approved'
                            ? 'bg-emerald-50 text-emerald-800'
                            : 'bg-rose-50 text-rose-800'
                      }`}
                    >
                      {selectedListing.status === 'Pending Approval'
                        ? 'Chờ duyệt'
                        : selectedListing.status === 'Approved'
                          ? 'Đã duyệt'
                          : 'Đã từ chối'}
                    </span>
                  </div>
                </div>

                <div className="space-y-7 p-5 sm:p-7">
                  <section>
                    <SectionTitle>Hình ảnh sản phẩm</SectionTitle>
                    <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_90px]">
                      <img
                        src={selectedListing.images[activeImage] ?? selectedListing.images[0]}
                        alt={`${selectedListing.name} - ảnh ${activeImage + 1}`}
                        className="h-64 w-full rounded-xl bg-slate-100 object-cover sm:h-72"
                      />
                      <div className="flex gap-2 overflow-x-auto sm:flex-col sm:overflow-y-auto">
                        {selectedListing.images.map((image, index) => (
                          <button
                            key={image}
                            type="button"
                            onClick={() => setActiveImage(index)}
                            aria-label={`Xem ảnh ${index + 1}`}
                            className={`shrink-0 overflow-hidden rounded-lg border-2 ${
                              activeImage === index ? 'border-emerald-500' : 'border-transparent'
                            }`}
                          >
                            <img src={image} alt="" className="h-16 w-20 object-cover sm:h-20 sm:w-full" />
                          </button>
                        ))}
                      </div>
                    </div>
                  </section>

                  <section>
                    <SectionTitle>Thông tin sản phẩm</SectionTitle>
                    <div className="grid gap-4 rounded-xl bg-slate-50 p-4 sm:grid-cols-3">
                      <div>
                        <p className="text-xs text-slate-500">Danh mục</p>
                        <p className="mt-1 font-medium text-slate-900">{selectedListing.category}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-500">Giá bán</p>
                        <p className="mt-1 font-semibold text-emerald-700">{formatPrice(selectedListing.price)} / {selectedListing.unit}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-500">Sản lượng</p>
                        <p className="mt-1 font-medium text-slate-900">
                          {selectedListing.quantity.toLocaleString('vi-VN')} {selectedListing.unit}
                        </p>
                      </div>
                    </div>
                  </section>

                  <section>
                    <SectionTitle>Thông tin farmer và nông trại</SectionTitle>
                    <div className="grid gap-4 sm:grid-cols-2">
                      <div className="rounded-xl border border-slate-200 p-4">
                        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Farmer</p>
                        <p className="mt-2 font-semibold text-slate-900">{selectedListing.farmer.name}</p>
                        <p className="mt-1 break-all text-sm text-slate-600">{selectedListing.farmer.email}</p>
                        <p className="mt-1 text-sm text-slate-600">{selectedListing.farmer.phone}</p>
                      </div>
                      <div className="rounded-xl border border-slate-200 p-4">
                        <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Nông trại</p>
                        <p className="mt-2 font-semibold text-slate-900">{selectedListing.farm.name}</p>
                        <p className="mt-1 text-sm text-slate-600">{selectedListing.farm.location}</p>
                        <p className="mt-1 text-sm text-slate-600">Diện tích: {selectedListing.farm.size}</p>
                      </div>
                    </div>
                  </section>

                  <section>
                    <SectionTitle>Thông tin thu hoạch</SectionTitle>
                    <div className="grid gap-4 rounded-xl bg-slate-50 p-4 sm:grid-cols-3">
                      <div>
                        <p className="text-xs text-slate-500">Ngày thu hoạch dự kiến</p>
                        <p className="mt-1 font-medium text-slate-900">{selectedListing.harvest.date}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-500">Phương thức</p>
                        <p className="mt-1 font-medium text-slate-900">{selectedListing.harvest.method}</p>
                      </div>
                      <div>
                        <p className="text-xs text-slate-500">Ghi chú</p>
                        <p className="mt-1 text-sm text-slate-700">{selectedListing.harvest.notes}</p>
                      </div>
                    </div>
                  </section>

                  <section>
                    <SectionTitle>Truy xuất nguồn gốc & chứng nhận</SectionTitle>
                    <div className="rounded-xl border border-slate-200 p-4">
                      <div className="grid gap-4 sm:grid-cols-2">
                        <div>
                          <p className="text-xs text-slate-500">Mã lô</p>
                          <p className="mt-1 font-mono text-sm font-medium text-slate-900">{selectedListing.traceability.batchId}</p>
                        </div>
                        <div>
                          <p className="text-xs text-slate-500">Ngày gieo trồng</p>
                          <p className="mt-1 font-medium text-slate-900">{selectedListing.traceability.plantingDate}</p>
                        </div>
                      </div>
                      <div className="mt-4">
                        <p className="text-xs text-slate-500">Quy trình canh tác</p>
                        <p className="mt-1 text-sm leading-6 text-slate-700">{selectedListing.traceability.growingPractices}</p>
                      </div>
                      <div className="mt-4">
                        <p className="text-xs text-slate-500">Chứng nhận</p>
                        {selectedListing.traceability.certificates.length ? (
                          <div className="mt-2 flex flex-wrap gap-2">
                            {selectedListing.traceability.certificates.map((certificate) => (
                              <span
                                key={certificate}
                                className="rounded-full border border-emerald-200 bg-emerald-50 px-3 py-1 text-xs font-medium text-emerald-800"
                              >
                                ✓ {certificate}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <p className="mt-1 text-sm text-slate-500">Chưa cung cấp chứng nhận.</p>
                        )}
                      </div>
                    </div>
                  </section>

                  {selectedListing.status === 'Rejected' && selectedListing.rejectionReason ? (
                    <div className="rounded-xl border border-rose-200 bg-rose-50 p-4">
                      <p className="text-sm font-semibold text-rose-900">Lý do từ chối</p>
                      <p className="mt-1 text-sm text-rose-800">{selectedListing.rejectionReason}</p>
                    </div>
                  ) : null}

                  {selectedListing.status === 'Pending Approval' ? (
                    <section className="border-t border-slate-200 pt-6">
                      <SectionTitle>Quyết định kiểm duyệt</SectionTitle>
                      <label htmlFor="rejection-reason" className="mb-2 block text-sm font-medium text-slate-700">
                        Lý do từ chối <span className="font-normal text-slate-400">(bắt buộc nếu từ chối)</span>
                      </label>
                      <textarea
                        id="rejection-reason"
                        value={rejectionReason}
                        onChange={(event) => setRejectionReason(event.target.value)}
                        rows={3}
                        maxLength={500}
                        placeholder="Nêu rõ thông tin cần farmer bổ sung hoặc chỉnh sửa..."
                        className="w-full resize-y rounded-xl border border-slate-300 px-4 py-3 text-sm text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-100"
                      />
                      {actionError ? <p role="alert" className="mt-2 text-sm text-rose-600">{actionError}</p> : null}
                      <div className="mt-4 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                        <button
                          type="button"
                          onClick={() => updateListing('Rejected', rejectionReason)}
                          className="rounded-xl border border-rose-300 px-5 py-2.5 text-sm font-semibold text-rose-700 transition hover:bg-rose-50"
                        >
                          Từ chối sản phẩm
                        </button>
                        <button
                          type="button"
                          onClick={() => updateListing('Approved')}
                          className="rounded-xl bg-emerald-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-emerald-700"
                        >
                          Duyệt sản phẩm
                        </button>
                      </div>
                      <p className="mt-3 text-right text-xs text-slate-400">
                        {rejectionReason.length}/500 ký tự · Farmer sẽ được thông báo sau khi có quyết định.
                      </p>
                    </section>
                  ) : null}
                </div>
              </section>
            ) : null}
          </div>
        )}
        <p className="mt-6 text-center text-xs text-slate-400">
          Bản xem trước giao diện: dữ liệu mẫu, quyết định và thông báo chỉ tồn tại trong phiên hiện tại.
        </p>
      </main>
    </div>
  );
}
