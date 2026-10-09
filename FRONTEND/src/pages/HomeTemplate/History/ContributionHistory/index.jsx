import { useState, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import Pagination from "../../../../components/Pagination/Pagination";
import { CheckCircle2, XCircle, Clock, AlertCircle, X, BarChart3, Search, ArrowRight } from "lucide-react";
import { SPEAKER_ACCENT as ACCENT, SUCCESS, DANGER, WARNING, AUDIO_PRIMARY } from "../../../../constants/theme";
import CodeSwitchText from "../../../../components/CodeSwitchText/CodeSwitchText";
import { stripTags } from "../../../../utils/codeSwitch";
import Loading from "../../../../components/Loading/Loading";
import ErrorState from "../../../../components/ErrorState/ErrorState";
import EmptyState from "../../../../components/EmptyState/EmptyState";
import { getSpeakerContributionHistoryApi } from "../../../../services/speakerApi";
import { GET_SPEAKER_CONTRIBUTION_HISTORY_API } from "../../../../utils/queryKey";

// "2026-09-05T07:20:00Z" -> "05/09/2026 - 14:20" (giờ máy người dùng)
function formatDateTime(iso) {
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, "0");
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} - ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// Trạng thái câu của backend -> 3 trạng thái của bảng
const STATUS_OF = {
  Validated: "Approved",
  Rejected: "Rejected",
  Deactivated: "Rejected",
  PendingValidation: "Pending",
};

/** Đổi 1 câu đóng góp từ API sang dạng bảng đang dùng (giữ nguyên tên trường cũ để không phải sửa giao diện). */
function toRow(item) {
  return {
    id: item.scriptId,
    category: item.category,
    cs_transcript: item.csTranscript,
    vi_equivalent: item.viEquivalent,
    alignment: item.alignment ?? [],
    date: formatDateTime(item.createdAt),
    status: STATUS_OF[item.status] ?? "Pending",
    // Lượt duyệt nội dung: Accepted / Edited / Rejected (kèm nhận xét)
    reviews: (item.reviews ?? []).map((r, i) => ({
      reviewer: `R${i + 1}`,
      decision: r.decision === "Rejected" ? "reject" : "approve",
      reason: r.decision === "Edited" ? `Đã sửa câu${r.reason ? `: ${r.reason}` : ""}` : r.reason,
    })),
  };
}

function SentenceContent({ item }) {
  return (
    <>
      {[{ label: "VI-EN", text: item.cs_transcript, color: ACCENT }, { label: "VI", text: item.vi_equivalent, color: "#8B8D95" }].map(({ label, text, color }) => (
        <div key={label} className="bg-[#F7F5EF] p-3.5 rounded-xl border border-[#E5E2D8] mb-2.5 flex items-center gap-2.5">
          <span className="text-tag font-emphasis w-9 h-4 shrink-0 rounded inline-flex items-center justify-center text-white" style={{ background: color }}>{label}</span>
          <p className="type-body text-[#16171C] break-words min-w-0"><CodeSwitchText transcript={text} accent={AUDIO_PRIMARY} /></p>
        </div>
      ))}
      {item.alignment.length > 0 && (
        <div className="mt-4 mb-4">
          <p className="type-label text-[#9A9CA3] mb-2">Nghĩa từ tiếng Anh</p>
          <div className="flex flex-wrap gap-2">
            {item.alignment.map((a, i) => (
              <div key={i} className="flex items-center gap-1.5 bg-[#F0EEE6] border border-[#E5E2D8] rounded-lg px-2.5 py-1.5">
                <span className="type-label font-mono" style={{ color: ACCENT }}>{a.source}</span>
                <ArrowRight className="w-3 h-3 text-[#B7B4A9] shrink-0" />
                <span className="type-meta text-[#16171C]">{a.target}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

// Màu phân loại (bộ A) - xanh dương / hổ phách / hồng magenta, giống bên Reviewer
const CATEGORY_COLORS = {
  "Hội thoại hàng ngày": { bg: "#E6F0FE", text: "#1E40AF", border: "#C9DEFB" },
  "Công nghệ thông tin": { bg: "#FBF0DA", text: "#92600A", border: "#F3E0B5" },
  "Giáo dục": { bg: "#FCE7F0", text: "#9D2662", border: "#F8CFE0" },
};
const catStyle = (cat) => CATEGORY_COLORS[cat] || { bg: "#F7F5EF", text: "#6E7078", border: "#E5E2D8" };

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

// Kết quả (trạng thái + số phiếu đã bỏ) - gộp "Trạng thái" và "Phản hồi" cũ, bấm để xem chi tiết
function ResultButton({ status, votedCount, onClick, ariaLabel }) {
  const s = {
    Approved: { bg: "#EAF7EF", border: "rgba(63,166,107,0.25)", text: "#1F5C3F", icon: CheckCircle2, label: "Đã duyệt" },
    Rejected: { bg: "#FDEAEA", border: "#F3C9C9", text: "#C63B3B", icon: XCircle, label: "Từ chối" },
    Pending: { bg: "#FFF1DE", border: "#F5DFC0", text: "#A85E12", icon: Clock, label: "Chờ duyệt" },
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

const FILTER_OPTIONS = [
  { value: "all", label: "Tất cả trạng thái" },
  { value: "Approved", label: "Đã duyệt" },
  { value: "Rejected", label: "Từ chối" },
  { value: "Pending", label: "Chờ duyệt" },
];

export default function ContributionHistory() {
  const [currentPage, setCurrentPage] = useState(1);
  const [detailItem, setDetailItem] = useState(null);
  const [statusFilter, setStatusFilter] = useState("all");
  const [categoryFilter, setCategoryFilter] = useState("all");
  const [searchTerm, setSearchTerm] = useState("");

  // Lấy hết lịch sử (mỗi lần tối đa 100 câu) - lọc, tìm, phân trang làm ở FE như bảng cũ
  const historyQuery = useQuery({
    queryKey: [GET_SPEAKER_CONTRIBUTION_HISTORY_API],
    queryFn: async () => {
      const all = [];
      for (let page = 1; ; page += 1) {
        const result = await getSpeakerContributionHistoryApi({ page, pageSize: 100 });
        all.push(...result.items);
        if (!result.hasNext) break;
      }
      return all;
    },
  });

  const withStatus = useMemo(() => (historyQuery.data ?? []).map(toRow), [historyQuery.data]);

  const stats = useMemo(() => {
    const total = withStatus.length;
    const approved = withStatus.filter((i) => i.status === "Approved").length;
    const rejected = withStatus.filter((i) => i.status === "Rejected").length;
    const pending = withStatus.filter((i) => i.status === "Pending").length;
    const pct = (n) => (total ? Math.round((n / total) * 100) : 0);
    return { total, approved, rejected, pending, approvedPct: pct(approved), rejectedPct: pct(rejected), pendingPct: pct(pending) };
  }, [withStatus]);

  const categories = [...new Set(withStatus.map((item) => item.category))];
  const filteredData = useMemo(() => {
    const query = searchTerm.trim().toLocaleLowerCase("vi");
    return withStatus.filter((item) =>
      (statusFilter === "all" || item.status === statusFilter) &&
      (categoryFilter === "all" || item.category === categoryFilter) &&
      [item.category, stripTags(item.cs_transcript), stripTags(item.vi_equivalent)].some((value) =>
        value.toLocaleLowerCase("vi").includes(query)
      )
    );
  }, [withStatus, statusFilter, categoryFilter, searchTerm]);
  // Cố định 10 câu mỗi trang; trang đầy thì 10 dòng giãn đều hết chiều cao khung (màn thấp thì bảng cuộn bên trong)
  const itemsPerPage = 10;
  const totalPages = Math.max(1, Math.ceil(filteredData.length / itemsPerPage));
  const pageItems = filteredData.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  if (historyQuery.isLoading) return <Loading />;
  if (historyQuery.isError) {
    return <ErrorState message={historyQuery.error.message} onRetry={historyQuery.refetch} />;
  }

  return (
    <div className="-mt-2 h-full min-h-0 flex flex-col gap-3 text-left">

      {/* THẺ THỐNG KÊ - ô cứng (không bấm để lọc) */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-2.5 sm:gap-3">
        <StatCard icon={BarChart3} label="Tổng cộng" value={stats.total} accent="#16171C" bg="#F0EEE6" />
        <StatCard icon={CheckCircle2} label="Đã duyệt" value={stats.approved} pct={stats.approvedPct} accent={SUCCESS} bg="#EAF7EF" />
        <StatCard icon={XCircle} label="Từ chối" value={stats.rejected} pct={stats.rejectedPct} accent={DANGER} bg="#FDEAEA" />
        <StatCard icon={Clock} label="Chờ duyệt" value={stats.pending} pct={stats.pendingPct} accent={WARNING} bg="#FFF1DE" />
      </div>

      {/* BẢNG */}
      <div className="flex-1 min-h-0 bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col overflow-hidden">
        <div className="px-4 sm:px-5 py-2 border-b border-[#F0EEE6] space-y-3">
          <div className="flex flex-col md:flex-row gap-3">
            <div className="relative flex-1 min-w-0">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#9A9CA3]" />
              <input
                aria-label="Tìm câu đóng góp"
                value={searchTerm}
                onChange={(event) => {
                  setSearchTerm(event.target.value);
                  setCurrentPage(1);
                }}
                placeholder="Tìm phân loại, nội dung..."
                className="w-full pl-9 pr-3 py-1.5 rounded-lg border border-[#E5E2D8] text-ui leading-4 focus:outline-none focus:border-blue-500"
              />
            </div>
            <select
              aria-label="Lọc phân loại"
              value={categoryFilter}
              onChange={(event) => {
                setCategoryFilter(event.target.value);
                setCurrentPage(1);
              }}
              className="min-w-0 rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]"
            >
              <option value="all">Tất cả phân loại</option>
              {categories.map((category) => (
                <option key={category} value={category}>{category}</option>
              ))}
            </select>
            <select
              aria-label="Lọc trạng thái"
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value);
                setCurrentPage(1);
              }}
              className="rounded-lg border border-[#E5E2D8] px-3 py-1.5 bg-white text-meta text-[#16171C]"
            >
              {FILTER_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
        </div>

        <div className="relative flex-1 min-h-0 overflow-auto">
          <table className={`w-full min-w-[1000px] table-fixed border-collapse ${pageItems.length === itemsPerPage ? "h-full" : ""}`}>
            <thead>
              <tr className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8]">
                <th className="py-2 px-3 text-center w-[5%]">STT</th>
                <th className="py-2 px-3 text-left w-[50%]">Nội dung</th>
                <th className="py-2 px-3 text-left w-[17%]">Phân loại</th>
                <th className="py-2 px-3 text-left w-[14%]">Ngày nộp</th>
                <th className="py-2 px-3 text-left w-[14%]">Kết quả</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] type-ui">
              {filteredData.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    {withStatus.length === 0 ? (
                      <EmptyState title="Chưa có câu đóng góp nào" description="Đóng góp câu đầu tiên ở mục Đóng góp văn bản." />
                    ) : (
                      <EmptyState title="Không có câu phù hợp bộ lọc" description="Thử đổi từ khoá hoặc bộ lọc." />
                    )}
                  </td>
                </tr>
              ) : pageItems.map((item, idx) => {
                const votedCount = item.reviews.filter((r) => r.decision === "approve" || r.decision === "reject").length;
                return (
                  <tr key={item.id} onClick={() => setDetailItem(item)} className="hover:bg-[#F7F5EF]/70 transition-colors group h-11 cursor-pointer">
                    <td className="px-3 text-center whitespace-nowrap align-middle">
                      <span className="text-[#9A9CA3] type-meta">{(currentPage - 1) * itemsPerPage + idx + 1}</span>
                    </td>
                    <td className="px-3 text-left align-middle">
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5"><CodeSwitchText transcript={item.cs_transcript} accent={AUDIO_PRIMARY} /></p>
                      <p className="text-meta font-regular text-[#16171C] truncate leading-5 mt-0.5"><CodeSwitchText transcript={item.vi_equivalent} accent={AUDIO_PRIMARY} /></p>
                    </td>
                    <td className="px-3 text-left align-middle">
                      <span className="w-[136px] h-6 inline-flex items-center justify-center rounded-md type-caption whitespace-nowrap border" style={{ background: catStyle(item.category).bg, color: catStyle(item.category).text, borderColor: catStyle(item.category).border }}>{item.category}</span>
                    </td>
                    <td className="px-3 text-left whitespace-nowrap align-middle">
                      <span className="text-[#6E7078] type-meta">{item.date}</span>
                    </td>
                    <td className="px-3 text-left align-middle">
                      <ResultButton status={item.status} votedCount={votedCount} onClick={(e) => { e.stopPropagation(); setDetailItem(item); }} ariaLabel={`Xem kết quả kiểm duyệt, ${item.id}`} />
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

      {/* POPUP CHI TIẾT - 3 reviewer, giống RecordingHistory */}
      {detailItem && (() => {
        const ResultIcon = detailItem.status === "Approved" ? CheckCircle2 : detailItem.status === "Rejected" ? AlertCircle : Clock;
        const accent = detailItem.status === "Rejected" ? "#C63B3B" : detailItem.status === "Approved" ? "#3FA66B" : "#A85E12";
        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 lg:pl-64">
            <div className="absolute inset-0" style={{ background: "rgba(22,23,28,0.55)", backdropFilter: "blur(2px)", WebkitBackdropFilter: "blur(2px)" }} onClick={() => setDetailItem(null)} />
            <div className="bg-white rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] relative z-10 overflow-hidden max-h-[85vh] flex flex-col">
              <div className="h-1.5 w-full shrink-0" style={{ background: accent }} />
              <div className="p-6 overflow-y-auto">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-full flex items-center justify-center flex-shrink-0" style={{ background: `${accent}1A` }}>
                      <ResultIcon className="w-[18px] h-[18px]" style={{ color: accent }} />
                    </div>
                    <span className="type-section text-[#16171C]">Kết quả kiểm duyệt</span>
                  </div>
                  <button onClick={() => setDetailItem(null)} aria-label="Đóng" className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] transition-colors flex-shrink-0">
                    <X className="w-[18px] h-[18px] text-[#6E7078]" />
                  </button>
                </div>

                <div className="flex items-center gap-2 mb-3">
                  <span className="px-2.5 py-1 rounded-md text-caption font-label border" style={{ background: catStyle(detailItem.category).bg, color: catStyle(detailItem.category).text, borderColor: catStyle(detailItem.category).border }}>{detailItem.category}</span>
                  <span className="type-meta text-[#9A9CA3]">{detailItem.date}</span>
                </div>

                <SentenceContent item={detailItem} />

                {detailItem.reviews.length === 0 && (
                  <p className="type-meta text-[#6E7078]">Câu đang chờ duyệt nội dung. Kết quả hiện khi có người duyệt.</p>
                )}
                {/* 3 reviewer - giống RecordingHistory */}
                <div className="flex flex-col gap-2">
                  {detailItem.reviews.filter((r) => r.decision !== "not_needed" && (detailItem.status === "Pending" || r.decision === "approve" || r.decision === "reject")).map((r, i) => {
                    const isReject = r.decision === "reject";
                    const isApprove = r.decision === "approve";
                    const c = isReject ? "#C63B3B" : isApprove ? "#3FA66B" : "#A85E12";
                    const rowBg = isReject ? "#FDEAEA" : isApprove ? "#EAF7EF" : "#FFF1DE";
                    const rowBorder = isReject ? "#F3C9C9" : isApprove ? "rgba(63,166,107,0.2)" : "#F5DFC0";
                    return (
                      <div key={i} className="flex gap-2.5 px-3 py-2.5 rounded-xl border" style={{ background: rowBg, borderColor: rowBorder }}>
                        <span className="w-6 h-6 rounded-full text-white text-caption font-label flex items-center justify-center flex-shrink-0" style={{ background: c }}>{r.reviewer}</span>
                        <div className="min-w-0">
                          <p className="text-meta font-label" style={{ color: isApprove ? "#1F5C3F" : c }}>
                            {isReject ? `Từ chối${r.errorCategory ? " · " + r.errorCategory : ""}` : isApprove ? "Đã duyệt" : "Chưa đánh giá"}
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
