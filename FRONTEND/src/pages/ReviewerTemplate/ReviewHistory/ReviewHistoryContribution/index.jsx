import { useEffect, useState } from "react";
import { keepPreviousData, useQueries, useQuery } from "@tanstack/react-query";
import Pagination from "../../../../components/Pagination/Pagination";
import { CheckCircle2, XCircle, PencilLine, Search, X, FileText } from "lucide-react";
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT, AUDIO_PRIMARY } from "../../../../constants/theme"; // ACCENT = xanh lá thương hiệu, AUDIO_PRIMARY = màu phần tiếng Anh
import Loading from "../../../../components/Loading/Loading";
import ErrorState from "../../../../components/ErrorState/ErrorState";
import { getReviewerContributionHistoryApi } from "../../../../services/reviewerApi";
import { getTopicsApi } from "../../../../services/configApi";
import { GET_REVIEWER_CONTRIBUTION_HISTORY_API, GET_TOPICS_API } from "../../../../utils/queryKey";

const ITEMS_PER_PAGE = 10;

// Loại câu = kind của API (suy ra từ quyết định của mình) - cùng màu với hàng chờ "Đề xuất câu"
const TYPE_TAG = {
  contribution: { label: "Đóng góp", bg: "#E6F0FE", text: "#1E40AF" },
  edit: { label: "Đề xuất sửa", bg: "#EAF7EF", text: "#1F5C3F" },
  report: { label: "Báo lỗi", bg: "#FFF1DE", text: "#A85E12" },
};
const tagOf = (kind) => TYPE_TAG[kind] ?? TYPE_TAG.contribution;

const TYPE_FILTERS = [
  { value: "", label: "Tất cả" },
  { value: "contribution", label: "Đóng góp" },
  { value: "edit", label: "Đề xuất sửa" },
  { value: "report", label: "Báo lỗi" },
];

// Quyết định của chính mình (myAction của API)
const MY_ACTION = {
  Accepted: { label: "Đã duyệt", icon: CheckCircle2, className: "bg-[#3FA66B]/10 text-[#1F5C3F] border-[#3FA66B]/25", iconColor: "#3FA66B" },
  Edited: { label: "Đã sửa", icon: PencilLine, className: "bg-[#EAF7EF] text-[#1F5C3F] border-[#3FA66B]/25", iconColor: "#3FA66B" },
  Rejected: { label: "Từ chối", icon: XCircle, className: "bg-[#FDEAEA] text-[#C63B3B] border-[#F3C9C9]", iconColor: "#C63B3B" },
};

function MyDecision({ action }) {
  const a = MY_ACTION[action];
  if (!a) return <span className="text-caption text-[#9A9CA3]">-</span>;
  const Icon = a.icon;
  return (
    <span className={`w-[92px] h-6 inline-flex items-center justify-center gap-1 rounded-full type-caption border whitespace-nowrap ${a.className}`}>
      <Icon className="w-3 h-3" style={{ color: a.iconColor }} /> {a.label}
    </span>
  );
}

function InlineLabel({ variant }) {
  return (
    <span className="text-tag font-emphasis w-9 h-4 text-center shrink-0 rounded inline-flex items-center justify-center" style={variant === "cs" ? { background: `${AUDIO_PRIMARY}1A`, color: AUDIO_PRIMARY, letterSpacing: "0.02em" } : { background: "#F0EEE6", color: "#6E7078", letterSpacing: "0.02em" }}>
      {variant === "cs" ? "VI-EN" : "VI"}
    </span>
  );
}

// Màu chủ đề (bộ A) - theo tên chủ đề API trả về
const CATEGORY_COLORS = {
  "Hội thoại hàng ngày": { bg: "#E6F0FE", text: "#1E40AF", border: "#C9DEFB" },
  "Công nghệ thông tin": { bg: "#FBF0DA", text: "#92600A", border: "#F3E0B5" },
  "Giáo dục": { bg: "#FCE7F0", text: "#9D2662", border: "#F8CFE0" },
};
const catStyle = (c) => CATEGORY_COLORS[c] || { bg: "#F7F5EF", text: "#6E7078", border: "#E5E2D8" };

// "2026-10-13T01:26:46Z" -> "13/10/2026 - 08:26" (giờ máy người dùng)
function formatDateTime(iso) {
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, "0");
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} - ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function ReviewHistoryContribution() {
  const [currentPage, setCurrentPage] = useState(1);
  const [detailItem, setDetailItem] = useState(null);
  const [typeFilter, setTypeFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [search, setSearch] = useState("");

  // Gõ xong 400ms mới gửi từ khoá lên API, tránh gọi mỗi lần bấm phím
  useEffect(() => {
    const timer = setTimeout(() => setSearch(searchTerm.trim()), 400);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Call API lấy 1 trang lịch sử (lọc loại / chủ đề / từ khoá ở phía server)
  const { data: history, isLoading, isError, error, refetch } = useQuery({
    queryKey: [GET_REVIEWER_CONTRIBUTION_HISTORY_API, typeFilter, categoryFilter, search, currentPage],
    queryFn: () => getReviewerContributionHistoryApi({ kind: typeFilter, category: categoryFilter, search, page: currentPage, pageSize: ITEMS_PER_PAGE }),
    placeholderData: keepPreviousData,
  });

  // Call API lấy số câu của từng loại cho các nút lọc (dùng total của API, chỉ lấy 1 dòng)
  const typeCountQueries = useQueries({
    queries: TYPE_FILTERS.map((f) => ({
      queryKey: [GET_REVIEWER_CONTRIBUTION_HISTORY_API, "count", f.value],
      queryFn: () => getReviewerContributionHistoryApi({ kind: f.value, page: 1, pageSize: 1 }),
    })),
  });

  // Call API lấy danh sách chủ đề cho ô lọc (hiện name, gửi code)
  const { data: topics = [] } = useQuery({
    queryKey: [GET_TOPICS_API],
    queryFn: getTopicsApi,
    staleTime: Infinity,
  });

  const items = history?.items ?? [];
  const totalPages = Math.max(1, history?.totalPages ?? 1);
  const changeFilter = (setter) => (value) => { setter(value); setCurrentPage(1); };

  if (isLoading) return <Loading />;
  if (isError) return <ErrorState message={error.message} onRetry={refetch} />;

  return (
    <div className="-mt-2 h-full min-h-0 flex flex-col gap-3 text-left">
      {/* BẢNG */}
      <div className="flex-1 min-h-0 bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col overflow-hidden">
        <div className="px-4 sm:px-5 py-2 border-b border-[#F0EEE6]">
          <div className="flex flex-col md:flex-row md:items-center gap-3">
            {/* Lọc loại câu theo kind của API */}
            <div className="flex items-center gap-1.5 shrink-0 flex-wrap">
              {TYPE_FILTERS.map((f, i) => {
                const active = typeFilter === f.value;
                const count = typeCountQueries[i].data?.total;
                return (
                  <button
                    key={f.value || "all"}
                    type="button"
                    onClick={() => changeFilter(setTypeFilter)(f.value)}
                    className={`h-8 px-3 rounded-full text-meta font-label border transition-colors cursor-pointer whitespace-nowrap ${active ? "text-white border-transparent" : "bg-white border-[#E5E2D8] text-[#6E7078] hover:bg-[#F7F5EF]"}`}
                    style={active ? { background: ACCENT } : undefined}
                  >
                    {f.label} <span className={`ml-1 tabular-nums ${active ? "text-white/80" : "text-[#9A9CA3]"}`}>{count ?? "·"}</span>
                  </button>
                );
              })}
            </div>
            <div className="relative flex-1 min-w-0">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#9A9CA3]" />
              <input aria-label="Tìm câu" value={searchTerm} onChange={(event) => { setSearchTerm(event.target.value); setCurrentPage(1); }} placeholder="Tìm nội dung, mã câu..." className="w-full pl-9 pr-3 py-1.5 rounded-lg border border-[#E5E2D8] text-ui leading-4 focus:outline-none focus:border-[#16A34A]" />
            </div>
            <select aria-label="Lọc chủ đề" value={categoryFilter} onChange={(event) => changeFilter(setCategoryFilter)(event.target.value)} className="min-w-0 rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]">
              <option value="">Tất cả chủ đề</option>
              {topics.map((t) => <option key={t.code} value={t.code}>{t.name}</option>)}
            </select>
          </div>
        </div>

        <div className="relative flex-1 min-h-0 overflow-auto">
          <table className={`w-full min-w-[1000px] border-collapse table-fixed ${items.length === ITEMS_PER_PAGE ? "h-full" : ""}`}>
            <colgroup>
              <col className="w-[5%]" />
              <col className="w-[12%]" />
              <col className="w-[55%]" />
              <col className="w-[15%]" />
              <col className="w-[13%]" />
            </colgroup>
            <thead>
              <tr className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8]">
                <th className="py-2 px-3 text-center">STT</th>
                <th className="py-2 px-3 text-left">Loại</th>
                <th className="py-2 px-3 text-left">Nội dung</th>
                <th className="py-2 px-3 text-left">Speaker / Thời gian</th>
                <th className="py-2 px-3 text-left">Bạn</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] type-ui">
              {items.length === 0 ? (
                <tr><td colSpan={5} className="py-10 text-center text-[#9A9CA3] text-body">Không có mục nào phù hợp bộ lọc.</td></tr>
              ) : items.map((item, idx) => {
                const tag = tagOf(item.kind);
                return (
                  <tr key={`${item.scriptId}-${item.reviewedAt}`} onClick={() => setDetailItem(item)} className="hover:bg-[#F7F5EF]/70 transition-colors h-11 cursor-pointer">
                    <td className="px-3 text-center"><span className="text-[#9A9CA3] type-meta">{(currentPage - 1) * ITEMS_PER_PAGE + idx + 1}</span></td>
                    <td className="px-3 text-left">
                      <span className="w-[84px] h-6 inline-flex items-center justify-center rounded-md type-caption whitespace-nowrap" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                    </td>
                    <td className="px-3 text-left">
                      {/* Hiện nguyên văn API trả về, giữ nhãn [vi] / [en] */}
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5">{item.csTranscript}</p>
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5 mt-0.5">{item.viEquivalent}</p>
                    </td>
                    <td className="px-3 text-left">
                      {/* API chưa trả người gửi - chờ backend bổ sung */}
                      <p className="font-label text-[#9A9CA3] truncate">—</p>
                      <p className="text-[#9A9CA3] type-meta mt-0.5 tabular-nums whitespace-nowrap">{formatDateTime(item.reviewedAt)}</p>
                    </td>
                    <td className="px-3 text-left"><MyDecision action={item.myAction} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <div className="px-3 border-t border-[#E5E2D8] bg-white shrink-0">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(page) => setCurrentPage(page)} accent={ACCENT} variant="soft" />
        </div>
      </div>

      {/* POPUP CHI TIẾT */}
      {detailItem && (() => {
        const tag = tagOf(detailItem.kind);
        const cat = catStyle(detailItem.category);
        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64" style={{ background: "rgba(22,23,28,0.55)", backdropFilter: "blur(2px)", WebkitBackdropFilter: "blur(2px)" }} onClick={() => setDetailItem(null)}>
            <div className="bg-white rounded-[24px] w-full max-w-xl shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[88vh] flex flex-col" onClick={(e) => e.stopPropagation()}>
              <div className="h-1.5 w-full shrink-0" style={{ background: ACCENT }} />
              <div className="p-6 overflow-y-auto">
                <div className="flex items-center justify-between mb-1.5">
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: `${ACCENT}1A` }}>
                      <FileText className="w-[18px] h-[18px]" style={{ color: ACCENT }} />
                    </div>
                    <span className="px-2 py-1 rounded-md text-caption font-label" style={{ background: tag.bg, color: tag.text }}>{tag.label}</span>
                    <span className="type-section text-[#16171C]">Chi tiết</span>
                  </div>
                  <button onClick={() => setDetailItem(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0 cursor-pointer">
                    <X className="w-[18px] h-[18px] text-[#6E7078]" />
                  </button>
                </div>

                <div className="flex items-center gap-2 flex-wrap mb-4 type-meta text-[#6E7078]">
                  <span className="px-2 py-0.5 rounded-md text-caption font-label border" style={{ background: cat.bg, color: cat.text, borderColor: cat.border }}>{detailItem.category}</span>
                  <span className="text-[#9A9CA3]">{formatDateTime(detailItem.reviewedAt)}</span>
                  <span className="text-[#9A9CA3]">· {detailItem.scriptId}</span>
                </div>

                <div className="rounded-xl border border-[#E5E2D8] overflow-hidden">
                  <div className="flex items-start gap-2.5 px-3 py-2.5">
                    <InlineLabel variant="cs" />
                    <p className="type-body text-[#16171C] break-words min-w-0">{detailItem.csTranscript}</p>
                  </div>
                  <div className="flex items-start gap-2.5 px-3 py-2.5 border-t border-[#F0EEE6]">
                    <InlineLabel variant="vi" />
                    <p className="type-body text-[#16171C] break-words min-w-0">{detailItem.viEquivalent}</p>
                  </div>
                </div>

                <div className="flex items-center gap-2 mt-4">
                  <span className="text-meta font-label text-[#16171C]">Quyết định của bạn:</span>
                  <MyDecision action={detailItem.myAction} />
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
