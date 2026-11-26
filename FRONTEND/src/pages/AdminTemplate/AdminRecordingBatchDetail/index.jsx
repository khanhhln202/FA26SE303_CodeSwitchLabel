import { Fragment, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { toast } from "sonner";
import { ArrowLeft, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Download, Lock, Play, Search } from "lucide-react";
import { ADMIN_ACCENT } from "../../../constants/theme";
import CodeSwitchText from "../../../components/CodeSwitchText/CodeSwitchText";
import { MOCK_BATCHES, SENTENCE_STATUS, buildMockSentences, isRecording, topicName } from "../AdminRecordingBatches/mockData";

/**
 * Chi tiết 1 đợt nhập: thanh tồn kho kiêm bộ lọc, danh sách câu, bấm dòng để xem câu tiếng Việt, audio và lịch sử.
 * CHỈ LÀM UI với dữ liệu minh hoạ - backend chưa có API danh sách câu theo đợt và lịch sử từng câu.
 */

const CARD = "bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)]";
const PAGE_SIZE = 50;
const fmt = (n) => Number(n || 0).toLocaleString("vi-VN");

// Cùng màu với thanh tồn kho ở trang kho
const GROUPS = [
  { key: "recorded", label: "Đã ghi xong", color: "#2E9E6B" },
  { key: "inTask", label: "Trong task", color: "#7FA7E6" },
  { key: "locked", label: "Bị khoá", color: "#E0A23B" },
  { key: "available", label: "Sẵn sàng", color: "#C9CCD3" },
];

const CHIP = {
  waiting: "bg-[#0052CC]/[0.08] text-[#0B4FB3] dark:text-[#9DBDF2]",
  pendingReview: "bg-[#FFF1D6] text-[#8B5E0F] dark:bg-[#3A2E14] dark:text-[#F0C46A]",
  recorded: "bg-[#E3F4EA] text-[#1E7A4F] dark:bg-[#173326] dark:text-[#7FD3A6]",
  locked: "bg-[#FFF1D6] text-[#8B5E0F] dark:bg-[#3A2E14] dark:text-[#F0C46A]",
  available: "bg-[#F0EEE7] text-[#6E7078] dark:bg-[#25272E] dark:text-gray-400",
};
const TONE = { danger: "#C2412D", success: "#1E7A4F", warning: "#8B5E0F" };

function batchStatus(b) {
  if (isRecording(b)) return { label: "Đang ghi âm", cls: CHIP.waiting };
  if (b.mergedInto) return { label: "Đã gộp", cls: CHIP.available };
  if (b.counts.recorded === 0) return { label: "Sẵn sàng", cls: CHIP.recorded };
  return { label: "Đã ghi âm xong", cls: CHIP.available };
}

const fakePlay = (e) => {
  e.stopPropagation();
  toast.info("Giao diện mẫu: chưa có audio thật.");
};

function PersonCell({ s }) {
  const muted = "block type-caption text-[#9A9CA3]";
  switch (s.status) {
    case "waiting":
      return (
        <span>
          Task {s.task}
          <span className={muted}>{s.speaker ? `${s.speaker} đã nhận` : "chưa có người nhận"}</span>
        </span>
      );
    case "pendingReview":
      return (
        <span>
          {s.speaker} · {s.task}
          <span className={muted}>{s.attempt > 1 ? `ghi lại lần ${s.attempt}` : "ghi lần 1"}</span>
        </span>
      );
    case "recorded":
      return (
        <span>
          {s.speaker}
          <span className={muted}>duyệt bởi {s.reviewer}</span>
        </span>
      );
    case "locked":
      return (
        <span>
          Báo lỗi: {s.reason.toLowerCase()}
          <span className={muted}>chờ reviewer duyệt</span>
        </span>
      );
    default:
      return <span className="text-[#9A9CA3]">—</span>;
  }
}

function Expanded({ s }) {
  const hasAudio = s.status === "recorded" || s.status === "pendingReview";
  return (
    <div className="pl-[88px] pr-4 pb-4 pt-1 grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
      <div className="space-y-3">
        <div>
          <p className="type-caption font-label text-[#9A9CA3]">Tiếng Việt tương đương</p>
          <p className="mt-0.5 type-ui text-[#16171C] dark:text-gray-100">{s.vi}</p>
        </div>
        {hasAudio && (
          <button
            type="button"
            onClick={fakePlay}
            className="w-full max-w-[360px] flex items-center gap-2.5 px-3 py-2 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#1C1D22] cursor-pointer hover:border-[#0052CC]"
          >
            <span className="w-7 h-7 rounded-full flex items-center justify-center text-white shrink-0" style={{ backgroundColor: ADMIN_ACCENT }}>
              <Play className="w-3.5 h-3.5 ml-0.5" />
            </span>
            <span className="flex-1 h-1 rounded-full bg-[#E5E2D8] dark:bg-gray-700" />
            <span className="type-caption tabular-nums text-[#9A9CA3]">0:04</span>
          </button>
        )}
      </div>
      <div>
        <p className="type-caption font-label text-[#9A9CA3]">Lịch sử câu</p>
        <ol className="mt-1.5 border-l-2 border-[#E5E2D8] dark:border-gray-700 pl-3 space-y-1.5">
          {s.history.map((h, i) => (
            <li key={i} className="type-caption text-[#4A4C53] dark:text-gray-300" style={h.tone ? { color: TONE[h.tone] } : undefined}>
              {h.at && <span className="tabular-nums text-[#9A9CA3] mr-1.5">{h.at}</span>}
              {h.text}
            </li>
          ))}
        </ol>
      </div>
    </div>
  );
}

export default function AdminRecordingBatchDetail() {
  const { batchId } = useParams();
  const batch = MOCK_BATCHES.find((b) => String(b.batchId) === batchId);
  const sentences = useMemo(() => (batch ? buildMockSentences(batch) : []), [batch]);

  const [group, setGroup] = useState("all");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState(null);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return sentences.filter((s) => {
      if (group !== "all" && SENTENCE_STATUS[s.status].group !== group) return false;
      if (!q) return true;
      return [String(s.id), s.tagged.replace(/\[(vi|en)\]/g, ""), s.vi, s.speaker || "", s.task || ""].some((v) => v.toLowerCase().includes(q));
    });
  }, [sentences, group, query]);

  if (!batch) {
    return (
      <div className="p-6 type-ui text-[#6E7078]">
        Không tìm thấy đợt nhập này.{" "}
        <Link to="/admin/recording-batches" className="underline" style={{ color: ADMIN_ACCENT }}>
          Về kho câu
        </Link>
      </div>
    );
  }

  const total = sentences.length;
  const pages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const current = Math.min(page, pages);
  const rows = filtered.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);
  const status = batchStatus(batch);

  const chooseGroup = (key) => {
    setGroup((g) => (g === key ? "all" : key));
    setPage(1);
    setOpenId(null);
  };

  const tabCls = (active) =>
    `h-8 px-3 rounded-lg inline-flex items-center gap-1.5 type-meta whitespace-nowrap cursor-pointer transition-colors ${
      active ? "bg-[#0052CC]/[0.08] text-[#0B4FB3] dark:text-[#9DBDF2] font-label" : "text-[#6E7078] dark:text-gray-400 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
    }`;

  return (
    <div className="w-full h-full flex flex-col gap-2.5 text-left font-sans p-1 overflow-auto">

      {/* Đầu trang */}
      <div className="shrink-0 flex flex-wrap items-end justify-between gap-3 px-1">
        <div className="min-w-0">
          <Link to="/admin/recording-batches" className="inline-flex items-center gap-1 type-meta text-[#6E7078] dark:text-gray-400 hover:underline">
            <ArrowLeft className="w-3.5 h-3.5" /> Kho câu theo đợt nhập
          </Link>
          <div className="mt-1 flex items-center gap-2 flex-wrap">
            <h1 className="type-section text-[#16171C] dark:text-gray-100">{batch.batchName}</h1>
            <span className={`px-2 py-0.5 rounded-md type-caption font-label ${status.cls}`}>{status.label}</span>
          </div>
          <p className="mt-0.5 type-caption text-[#9A9CA3]">
            {batch.fileName} · {fmt(total)} câu · {topicName(batch.domain)} · nhập {batch.createdAt.slice(0, 10).split("-").reverse().join("/")}
            {batch.mergedInto && ` · đã gộp vào "${batch.mergedInto}"`}
          </p>
        </div>
        <button
          type="button"
          onClick={() => toast.info("Giao diện mẫu: chưa nối API xuất CSV.")}
          className="h-9 px-4 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-gray-800 inline-flex items-center gap-1.5 cursor-pointer"
        >
          <Download className="w-4 h-4" /> Xuất CSV
        </button>
      </div>

      {/* Thanh tồn kho kiêm bộ lọc */}
      <div className={`${CARD} shrink-0 px-4 py-3`}>
        <div className="flex h-2.5 rounded-full overflow-hidden gap-[2px]" role="group" aria-label="Lọc theo phần tồn kho">
          {GROUPS.filter((g) => batch.counts[g.key] > 0).map((g) => (
            <button
              key={g.key}
              type="button"
              onClick={() => chooseGroup(g.key)}
              title={`${g.label}: ${fmt(batch.counts[g.key])} câu`}
              aria-label={`Lọc ${g.label}`}
              className="h-full cursor-pointer transition-opacity min-w-[4px]"
              style={{ flex: batch.counts[g.key], backgroundColor: g.color, opacity: group === "all" || group === g.key ? 1 : 0.3 }}
            />
          ))}
        </div>
        <div className="mt-2.5 flex flex-wrap gap-1" role="tablist">
          <button type="button" role="tab" aria-selected={group === "all"} onClick={() => chooseGroup("all")} className={tabCls(group === "all")}>
            Tất cả <span className="tabular-nums">{fmt(total)}</span>
          </button>
          {GROUPS.map((g) => (
            <button key={g.key} type="button" role="tab" aria-selected={group === g.key} onClick={() => chooseGroup(g.key)} className={tabCls(group === g.key)}>
              <span className="w-2 h-2 rounded-full" style={{ backgroundColor: g.color }} />
              {g.label} <span className="tabular-nums">{fmt(batch.counts[g.key])}</span>
            </button>
          ))}
        </div>
      </div>

      {/* Danh sách câu */}
      <div className={`${CARD} flex-1 min-h-[360px] flex flex-col overflow-hidden`}>
        <div className="shrink-0 px-4 py-3 border-b border-[#E5E2D8] dark:border-gray-800">
          <label className="relative block max-w-md">
            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-[#9A9CA3]" />
            <input
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setPage(1);
              }}
              placeholder="Tìm câu, mã câu, speaker hoặc task"
              className="w-full h-9 pl-9 pr-3 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none placeholder:text-[#9A9CA3] focus:border-[#0052CC] focus:shadow-[0_0_0_4px_rgba(0,82,204,0.1)]"
            />
          </label>
        </div>

        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full min-w-[860px] text-left border-collapse type-ui">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 sticky top-0 z-10">
                <th className="pl-4 pr-2 py-2 w-[88px]">Mã</th>
                <th className="px-2 py-2">Câu</th>
                <th className="px-2 py-2 w-[140px]">Trạng thái</th>
                <th className="px-2 py-2 w-[200px]">Người / task</th>
                <th className="pr-4 py-2 w-[48px]" aria-label="Thao tác" />
              </tr>
            </thead>
            <tbody>
              {rows.map((s) => {
                const open = openId === s.id;
                const playable = s.status === "recorded";
                return (
                  <Fragment key={s.id}>
                    <tr
                      onClick={() => setOpenId(open ? null : s.id)}
                      aria-expanded={open}
                      className={`border-b border-[#EFEDE6] dark:border-gray-800 cursor-pointer transition-colors ${
                        open ? "bg-[#F7F5EF] dark:bg-[#25272E] border-b-0" : "hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50"
                      }`}
                    >
                      <td className="pl-4 pr-2 py-2.5 tabular-nums text-[#9A9CA3] type-meta">#{s.id}</td>
                      <td className="px-2 py-2.5 text-[#16171C] dark:text-gray-100">
                        <CodeSwitchText transcript={s.tagged} accent={ADMIN_ACCENT} />
                      </td>
                      <td className="px-2 py-2.5">
                        <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-md type-caption font-label whitespace-nowrap ${CHIP[s.status]}`}>
                          {s.status === "locked" && <Lock className="w-3 h-3" />}
                          {SENTENCE_STATUS[s.status].label}
                        </span>
                      </td>
                      <td className="px-2 py-2.5 type-meta text-[#4A4C53] dark:text-gray-300">
                        <PersonCell s={s} />
                      </td>
                      <td className="pr-4 py-2.5 text-right">
                        {playable && !open ? (
                          <button
                            type="button"
                            onClick={fakePlay}
                            aria-label="Nghe bản ghi"
                            className="w-7 h-7 rounded-full border border-[#E5E2D8] dark:border-gray-700 inline-flex items-center justify-center text-[#6E7078] hover:border-[#0052CC] hover:text-[#0052CC] cursor-pointer"
                          >
                            <Play className="w-3 h-3 ml-0.5" />
                          </button>
                        ) : open ? (
                          <ChevronUp className="w-4 h-4 inline text-[#9A9CA3]" />
                        ) : (
                          <ChevronDown className="w-4 h-4 inline text-[#9A9CA3]" />
                        )}
                      </td>
                    </tr>
                    {open && (
                      <tr className="bg-[#F7F5EF] dark:bg-[#25272E] border-b border-[#EFEDE6] dark:border-gray-800">
                        <td colSpan={5}>
                          <Expanded s={s} />
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center type-ui text-[#9A9CA3]">
                    Không có câu nào khớp bộ lọc.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="shrink-0 px-4 py-2.5 flex items-center justify-between border-t border-[#E5E2D8] dark:border-gray-800 type-meta text-[#6E7078] dark:text-gray-400">
          <span className="tabular-nums">
            {filtered.length === 0 ? "0" : `${fmt((current - 1) * PAGE_SIZE + 1)}–${fmt(Math.min(current * PAGE_SIZE, filtered.length))}`} / {fmt(filtered.length)} câu
          </span>
          <div className="flex items-center gap-1">
            <button
              type="button"
              disabled={current <= 1}
              onClick={() => setPage(current - 1)}
              aria-label="Trang trước"
              className="w-8 h-8 rounded-lg inline-flex items-center justify-center hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] disabled:opacity-40 cursor-pointer disabled:cursor-default"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="tabular-nums px-1">
              Trang {current} / {pages}
            </span>
            <button
              type="button"
              disabled={current >= pages}
              onClick={() => setPage(current + 1)}
              aria-label="Trang sau"
              className="w-8 h-8 rounded-lg inline-flex items-center justify-center hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] disabled:opacity-40 cursor-pointer disabled:cursor-default"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
