import { useState, useMemo } from "react";
import Pagination from "../../../../components/Pagination/Pagination";
import { CheckCircle2, XCircle, Clock, AlertCircle, Search, X, BarChart3, ArrowRight } from "lucide-react";
import { REVIEWER_ACCENT as ACCENT } from "../../../../constants/theme";
import { REVIEWED_PROPOSALS as TEXT_HISTORY } from "../../../../mocks/reviewer/history";

// Reviewer đang đăng nhập (mock) - dùng để lấy quyết định "của bạn" trong 3 phiếu
const ME = "R1";

// 3 reviewer -> trạng thái tổng: >=2 từ chối = Rejected, >=2 duyệt = Approved, còn lại Pending.
// Giống hệt logic bên Lịch sử Ghi âm - đóng góp câu giờ cũng do 3 người kiểm duyệt.
function resolveStatus(reviews) {
  const rejected = reviews.filter((r) => r.decision === "reject").length;
  const approved = reviews.filter((r) => r.decision === "approve").length;
  if (rejected >= 2) return "Rejected";
  if (approved >= 2) return "Approved";
  return "Pending";
}

// Số phiếu THỰC SỰ đã bỏ (approve/reject) - loại cả "pending" (chưa tới lượt) lẫn
// "not_needed" (R1+R2 đã đồng thuận nên R3 không cần đánh giá nữa).
function votedCountOf(reviews) {
  return reviews.filter((r) => r.decision === "approve" || r.decision === "reject").length;
}

// Nội dung hiện ở bảng: câu đóng góp, hoặc câu speaker đề xuất / câu bị báo lỗi
const contentOf = (it) => (it.type === "issue" ? it.proposed || it.original : it);

// Nhãn loại câu - cùng màu với hàng chờ "Đề xuất câu"
const TYPE_TAG = {
  contribution: { label: "Đóng góp", bg: "#E6F0FE", text: "#1E40AF" },
  edit: { label: "Đề xuất sửa", bg: "#EAF7EF", text: "#1F5C3F" },
  report: { label: "Báo lỗi", bg: "#FFF1DE", text: "#A85E12" },
};
const tagOf = (it) => TYPE_TAG[it.type === "issue" ? it.kind : "contribution"];

const TYPE_FILTERS = [
  { value: "all", label: "Tất cả" },
  { value: "contribution", label: "Đóng góp" },
  { value: "issue", label: "Có vấn đề" },
];

function InlineLabel({ variant }) {
  return (
    <span className="text-tag font-emphasis w-9 h-4 text-center shrink-0 rounded inline-flex items-center justify-center text-white" style={{ background: variant === "cs" ? ACCENT : "#8B8D95", letterSpacing: "0.02em" }}>
      {variant === "cs" ? "VI-EN" : "VI"}
    </span>
  );
}

// Màu phân loại (bộ A) - xanh dương / hổ phách / hồng magenta
const CATEGORY_COLORS = {
  "Hội thoại hàng ngày": { bg: "#E6F0FE", text: "#1E40AF", border: "#C9DEFB" },
  "Công nghệ thông tin": { bg: "#FBF0DA", text: "#92600A", border: "#F3E0B5" },
  "Giáo dục": { bg: "#FCE7F0", text: "#9D2662", border: "#F8CFE0" },
};
const catStyle = (c) => CATEGORY_COLORS[c] || { bg: "#F7F5EF", text: "#6E7078", border: "#E5E2D8" };

function StatCard({ icon: Icon, label, value, pct, accent, bg }) {
  return (
    <div className="bg-white rounded-2xl px-3.5 py-2.5 flex items-center gap-3 border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)]">
      <span className="w-8 h-8 rounded-full flex items-center justify-center shrink-0" style={{ background: bg }}>
        <Icon className="w-4 h-4" style={{ color: accent }} />
      </span>
      <div className="min-w-0">
        <p className="type-label text-[#6E7078]">{label}</p>
        <p className="type-stat text-[#16171C]">
          {value}
          {pct !== undefined && <span className="type-label ml-1" style={{ color: accent }}>{pct}%</span>}
        </p>
      </div>
    </div>
  );
}

// Quyết định của chính reviewer đang đăng nhập
function MyDecision({ review }) {
  if (review?.decision === "approve") {
    return <span className="w-[92px] h-6 inline-flex items-center justify-center gap-1 rounded-full type-caption bg-[#3FA66B]/10 text-[#1F5C3F] border border-[#3FA66B]/25 whitespace-nowrap"><CheckCircle2 className="w-3 h-3 text-[#3FA66B]" /> Đã duyệt</span>;
  }
  if (review?.decision === "reject") {
    return <span className="w-[92px] h-6 inline-flex items-center justify-center gap-1 rounded-full type-caption bg-[#FDEAEA] text-[#C63B3B] border border-[#F3C9C9] whitespace-nowrap"><XCircle className="w-3 h-3 text-[#C63B3B]" /> Từ chối</span>;
  }
  return <span className="text-caption text-[#9A9CA3]">-</span>;
}

// Kết quả chung (trạng thái + số phiếu đã bỏ) - gộp "Trạng thái" và "Phản hồi" cũ, bấm để xem chi tiết
function ResultButton({ status, votedCount, onClick, ariaLabel }) {
  const s = {
    Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", icon: CheckCircle2, label: "Đã duyệt" },
    Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", icon: XCircle, label: "Từ chối" },
    Pending: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", icon: Clock, label: "Chờ" },
  }[status];
  const Icon = s.icon;
  return (
    <button
      onClick={onClick}
      aria-label={ariaLabel}
      className="w-[128px] h-6 inline-flex items-center justify-center gap-1 rounded-full border type-caption transition-colors cursor-pointer whitespace-nowrap hover:opacity-80"
      style={{ background: s.bg, borderColor: s.border, color: s.text }}
    >
      <Icon className="w-3 h-3 shrink-0" />
      <span>{s.label} · {votedCount}/3</span>
    </button>
  );
}

// Danh sách "từ tiếng Anh → nghĩa" dạng thô
function AlignmentList({ alignment, muted }) {
  return (
    <ul className={`space-y-0.5 leading-5 ${muted ? "text-[#6E7078]" : "text-[#16171C]"}`}>
      {alignment.map((a, i) => (
        <li key={i} className="flex items-center gap-1.5 break-words">
          <span className="font-mono">{a.source}</span>
          <ArrowRight className="w-3 h-3 text-[#B7B4A9] shrink-0" />
          <span>{a.target}</span>
        </li>
      ))}
    </ul>
  );
}

// Bảng câu chỉ xem: 1 cột (câu đóng góp / câu bị báo lỗi) hoặc 2 cột (bản gốc - bản sau)
function SentenceTable({ columns }) {
  const cols = columns.length === 2 ? "grid-cols-[64px_1fr_1fr]" : "grid-cols-[64px_1fr]";
  return (
    <div className="rounded-xl border border-[#E5E2D8] overflow-hidden text-meta">
      <div className={`grid ${cols} bg-[#F7F5EF] text-caption font-label text-[#9A9CA3]`}>
        <span className="px-2.5 py-1.5" />
        {columns.map((c) => <span key={c.title} className="px-2.5 py-1.5">{c.title}</span>)}
      </div>
      <div className={`grid ${cols} border-t border-[#F0EEE6]`}>
        <span className="px-2.5 py-2"><InlineLabel variant="cs" /></span>
        {columns.map((c) => <p key={c.title} className={`px-2.5 py-2 type-body break-words min-w-0 ${c.muted ? "text-[#6E7078]" : "text-[#16171C]"}`}>{c.data.cs_transcript}</p>)}
      </div>
      <div className={`grid ${cols} border-t border-[#F0EEE6]`}>
        <span className="px-2.5 py-2"><InlineLabel variant="vi" /></span>
        {columns.map((c) => <p key={c.title} className={`px-2.5 py-2 type-body break-words min-w-0 ${c.muted ? "text-[#6E7078]" : "text-[#16171C]"}`}>{c.data.vi_equivalent}</p>)}
      </div>
      {columns.some((c) => c.data.alignment.length > 0) && (
        <>
          <p className="px-2.5 py-1.5 border-t border-[#F0EEE6] bg-[#F7F5EF] text-caption font-label text-[#9A9CA3]">Nghĩa tiếng Việt của các từ tiếng Anh</p>
          <div className={`grid ${cols} border-t border-[#F0EEE6]`}>
            <span />
            {columns.map((c) => <div key={c.title} className="px-2.5 py-2 min-w-0"><AlignmentList alignment={c.data.alignment} muted={c.muted} /></div>)}
          </div>
        </>
      )}
    </div>
  );
}

// Cột nội dung trong popup chi tiết theo loại câu và quyết định của bạn
function detailColumns(it, myReview) {
  if (it.type === "contribution") return [{ title: "Nội dung", data: it }];
  const after = myReview?.decision === "approve" && it.final
    ? { title: "Bản bạn đã duyệt", data: it.final }
    : it.proposed ? { title: "Bản đề xuất", data: it.proposed } : null;
  const before = { title: it.kind === "edit" ? "Bản gốc trong kho" : "Câu bị báo lỗi", data: it.original, muted: !!after };
  return after ? [before, after] : [before];
}

const FILTER_OPTIONS = [
  { value: "all", label: "Tất cả kết quả" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Từ chối" },
  { value: "Pending", label: "Chờ kết quả" },
];

export default function ReviewHistoryContribution() {
  const [currentPage, setCurrentPage] = useState(1);
  const [detailItem, setDetailItem] = useState(null);
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [searchTerm, setSearchTerm] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("all");

  const withStatus = useMemo(() => TEXT_HISTORY.map((it) => ({ ...it, status: resolveStatus(it.reviews) })), []);

  const stats = useMemo(() => {
    const total = withStatus.length;
    const approved = withStatus.filter((i) => i.status === "Approved").length;
    const rejected = withStatus.filter((i) => i.status === "Rejected").length;
    const pending = withStatus.filter((i) => i.status === "Pending").length;
    const pct = (n) => (total ? Math.round((n / total) * 100) : 0);
    return { total, approved, rejected, pending, approvedPct: pct(approved), rejectedPct: pct(rejected), pendingPct: pct(pending) };
  }, [withStatus]);

  const typeCounts = {
    all: withStatus.length,
    contribution: withStatus.filter((it) => it.type === "contribution").length,
    issue: withStatus.filter((it) => it.type === "issue").length,
  };

  const filteredData = useMemo(() => {
    const query = searchTerm.trim().toLocaleLowerCase("vi");
    return withStatus.filter((item) => {
      const c = contentOf(item);
      return (typeFilter === "all" || item.type === typeFilter) &&
        (statusFilter === "all" || item.status === statusFilter) &&
        (typeFilter !== "contribution" || categoryFilter === "all" || item.category === categoryFilter) &&
        [item.author, item.reason || "", c.cs_transcript, c.vi_equivalent].some((text) => text.toLocaleLowerCase("vi").includes(query));
    });
  }, [withStatus, typeFilter, statusFilter, categoryFilter, searchTerm]);
  // Cố định 10 mục mỗi trang; trang đầy thì các dòng giãn đều hết khung (màn thấp thì bảng cuộn bên trong)
  const itemsPerPage = 10;
  const totalPages = Math.max(1, Math.ceil(filteredData.length / itemsPerPage));
  const pageItems = filteredData.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  const changeFilter = (setter) => (value) => { setter(value); setCurrentPage(1); };

  return (
    <div className="-mt-2 h-full min-h-0 flex flex-col gap-3 text-left">

      {/* THẺ THỐNG KÊ */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-2.5 sm:gap-3">
        <StatCard icon={BarChart3} label="Tổng đã xử lý" value={stats.total} accent="#16171C" bg="#F0EEE6" />
        <StatCard icon={CheckCircle2} label="Đã duyệt" value={stats.approved} pct={stats.approvedPct} accent="#3FA66B" bg="#EAF7EF" />
        <StatCard icon={XCircle} label="Từ chối" value={stats.rejected} pct={stats.rejectedPct} accent="#C63B3B" bg="#FDEAEA" />
        <StatCard icon={Clock} label="Chờ kết quả" value={stats.pending} pct={stats.pendingPct} accent="#A85E12" bg="#FFF1DE" />
      </div>

      {/* BẢNG */}
      <div className="flex-1 min-h-0 bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col overflow-hidden">
        <div className="px-4 sm:px-5 py-2 border-b border-[#F0EEE6]">
          <div className="flex flex-col md:flex-row md:items-center gap-3">
            {/* Lọc loại câu - giống hàng chờ "Đề xuất câu" */}
            <div className="flex items-center gap-1.5 shrink-0">
              {TYPE_FILTERS.map((f) => {
                const active = typeFilter === f.value;
                return (
                  <button
                    key={f.value}
                    type="button"
                    onClick={() => changeFilter(setTypeFilter)(f.value)}
                    className={`h-8 px-3 rounded-full text-meta font-label border transition-colors cursor-pointer whitespace-nowrap ${active ? "text-white border-transparent" : "bg-white border-[#E5E2D8] text-[#6E7078] hover:bg-[#F7F5EF]"}`}
                    style={active ? { background: ACCENT } : undefined}
                  >
                    {f.label} <span className={`ml-1 tabular-nums ${active ? "text-white/80" : "text-[#9A9CA3]"}`}>{typeCounts[f.value]}</span>
                  </button>
                );
              })}
            </div>
            <div className="relative flex-1 min-w-0">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#9A9CA3]" />
              <input aria-label="Tìm câu" value={searchTerm} onChange={(event) => changeFilter(setSearchTerm)(event.target.value)} placeholder="Tìm người gửi, lý do, nội dung..." className="w-full pl-9 pr-3 py-1.5 rounded-lg border border-[#E5E2D8] text-ui leading-4 focus:outline-none focus:border-blue-500" />
            </div>
            {typeFilter === "contribution" && (
              <select aria-label="Lọc phân loại" value={categoryFilter} onChange={(event) => changeFilter(setCategoryFilter)(event.target.value)} className="min-w-0 rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]">
                <option value="all">Tất cả phân loại</option>
                {[...new Set(TEXT_HISTORY.map((item) => item.category))].map((category) => <option key={category} value={category}>{category}</option>)}
              </select>
            )}
            <select aria-label="Lọc kết quả" value={statusFilter} onChange={(event) => changeFilter(setStatusFilter)(event.target.value)} className="rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]">
              {FILTER_OPTIONS.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
          </div>
        </div>

        {/* Chiều cao mỗi dòng cố định; nội dung đầy đủ, nghĩa từ và phiếu 3 reviewer chỉ có trong popup chi tiết */}
        <div className="relative flex-1 min-h-0 overflow-auto">
          <table className={`w-full min-w-[1100px] border-collapse table-fixed ${pageItems.length === itemsPerPage ? "h-full" : ""}`}>
            <colgroup>
              <col className="w-[5%]" />
              <col className="w-[11%]" />
              <col className="w-[40%]" />
              <col className="w-[16%]" />
              <col className="w-[12%]" />
              <col className="w-[16%]" />
            </colgroup>
            <thead>
              <tr className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8]">
                <th className="py-2 px-3 text-center">STT</th>
                <th className="py-2 px-3 text-left">Loại</th>
                <th className="py-2 px-3 text-left">Nội dung</th>
                <th className="py-2 px-3 text-left">Speaker / Thời gian</th>
                <th className="py-2 px-3 text-left">Bạn</th>
                <th className="py-2 px-3 text-left">Kết quả</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] type-ui">
              {filteredData.length === 0 ? (
                <tr><td colSpan={6} className="py-10 text-center text-[#9A9CA3] text-body">Không có mục nào phù hợp bộ lọc.</td></tr>
              ) : pageItems.map((item, idx) => {
                const c = contentOf(item);
                const tag = tagOf(item);
                return (
                  <tr key={item.id} onClick={() => setDetailItem(item)} className="hover:bg-[#F7F5EF]/70 transition-colors h-11 cursor-pointer">
                    <td className="px-3 text-center"><span className="text-[#9A9CA3] type-meta">{(currentPage - 1) * itemsPerPage + idx + 1}</span></td>
                    <td className="px-3 text-left">
                      <span className="w-[84px] h-6 inline-flex items-center justify-center rounded-md type-caption whitespace-nowrap" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                    </td>
                    <td className="px-3 text-left">
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5">{c.cs_transcript}</p>
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5 mt-0.5">{c.vi_equivalent}</p>
                    </td>
                    <td className="px-3 text-left">
                      <p className="font-label text-[#16171C] truncate">{item.author}</p>
                      <p className="text-[#9A9CA3] type-meta mt-0.5">{item.date}</p>
                    </td>
                    <td className="px-3 text-left"><MyDecision review={item.reviews.find((r) => r.reviewer === ME)} /></td>
                    <td className="px-3 text-left">
                      <ResultButton
                        status={item.status}
                        votedCount={votedCountOf(item.reviews)}
                        onClick={(e) => { e.stopPropagation(); setDetailItem(item); }}
                        ariaLabel={`Xem chi tiết câu của ${item.author}, ${item.id}`}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="px-3 border-t border-[#E5E2D8] bg-white shrink-0">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(page) => setCurrentPage(page)} accent={ACCENT} />
        </div>
      </div>

      {/* POPUP CHI TIẾT - gộp popup xem câu và popup phiếu 3 reviewer làm 1 */}
      {detailItem && (() => {
        const ResultIcon = detailItem.status === "Approved" ? CheckCircle2 : detailItem.status === "Rejected" ? AlertCircle : Clock;
        const accent = detailItem.status === "Rejected" ? "#C63B3B" : detailItem.status === "Approved" ? "#3FA66B" : "#A85E12";
        const tag = tagOf(detailItem);
        const myReview = detailItem.reviews.find((r) => r.reviewer === ME);
        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: "rgba(22,23,28,0.55)", backdropFilter: "blur(2px)", WebkitBackdropFilter: "blur(2px)" }} onClick={() => setDetailItem(null)}>
            <div className="bg-white rounded-[24px] w-full max-w-2xl shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[88vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
              <div className="h-1.5 w-full shrink-0" style={{ background: accent }} />
              <div className="p-6 overflow-y-auto">
                <div className="flex items-center justify-between mb-1.5">
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: `${accent}1A` }}>
                      <ResultIcon className="w-[18px] h-[18px]" style={{ color: accent }} />
                    </div>
                    <span className="px-2 py-1 rounded-md text-caption font-label" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                    <span className="type-section text-[#16171C]">Chi tiết</span>
                  </div>
                  <button onClick={() => setDetailItem(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0 cursor-pointer">
                    <X className="w-[18px] h-[18px] text-[#6E7078]" />
                  </button>
                </div>

                <div className="flex items-center gap-2 flex-wrap mb-4 type-meta text-[#6E7078]">
                  <span className="font-label text-[#16171C]">{detailItem.author}</span>
                  {detailItem.reason && <span>· Lý do: {detailItem.reason}</span>}
                  <span className="px-2 py-0.5 rounded-md text-caption font-label border" style={{ background: catStyle(detailItem.category).bg, color: catStyle(detailItem.category).text, borderColor: catStyle(detailItem.category).border }}>{detailItem.category}</span>
                  <span className="type-meta text-[#9A9CA3]">{detailItem.date}</span>
                </div>

                <SentenceTable columns={detailColumns(detailItem, myReview)} />

                <p className="text-meta font-label text-[#16171C] mt-4 mb-2">Phiếu của 3 reviewer</p>
                <div className="flex flex-col gap-2">
                  {detailItem.reviews.map((r) => {
                    const isReject = r.decision === "reject";
                    const isApprove = r.decision === "approve";
                    const idle = !isReject && !isApprove;
                    const c = isReject ? "#C63B3B" : isApprove ? "#3FA66B" : "#9A9CA3";
                    const rowBg = isReject ? "#FDEAEA" : isApprove ? "#EAF7EF" : "#F7F5EF";
                    const rowBorder = isReject ? "#F3C9C9" : isApprove ? "rgba(63,166,107,0.2)" : "#E5E2D8";
                    const label = isReject
                      ? `Từ chối${r.errorCategory ? " · " + r.errorCategory : ""}`
                      : isApprove ? "Duyệt"
                      : r.decision === "not_needed" ? "Không cần đánh giá · 2 reviewer đầu đã đồng thuận" : "Chưa đánh giá";
                    return (
                      <div key={r.reviewer} className="flex gap-2.5 px-3 py-2.5 rounded-xl border" style={{ background: rowBg, borderColor: rowBorder }}>
                        <span className="h-6 px-1.5 min-w-6 rounded-full text-white text-caption font-label flex items-center justify-center flex-shrink-0" style={{ background: c }}>{r.reviewer}</span>
                        <div className="min-w-0">
                          <p className="text-meta font-label" style={{ color: isApprove ? "#1F5C3F" : idle ? "#6E7078" : c }}>
                            {r.reviewer === ME && <span className="mr-1">Bạn ·</span>}{label}
                          </p>
                          {r.reason && <p className="text-meta text-[#6E7078] mt-0.5">{r.reason}</p>}
                        </div>
                      </div>
                    );
                  })}
                </div>

                <button onClick={() => setDetailItem(null)} className="w-full mt-5 py-3 text-white rounded-xl text-body font-label transition-all hover:opacity-90 active:scale-[0.98] cursor-pointer" style={{ background: ACCENT }}>Đóng</button>
              </div>
            </div>
          </div>
        );
      })()}
    </div>
  );
}
