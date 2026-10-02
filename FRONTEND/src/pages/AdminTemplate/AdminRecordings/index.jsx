import { useState, useMemo } from "react";
import { Search, Download, CheckCircle2, XCircle, Play, Pause, Headphones, Filter, Eye, Clock, X, AlertCircle } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  BORDER_LIGHT,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT,
  CHIP_DANGER_BG,
  CHIP_DANGER_BORDER,
  CHIP_DANGER_TEXT
} from "../../../constants/theme";

// Danh sách 10 Reviewers và 10 Speakers
const DEFAULT_REVIEWERS = [
  "Trần Minh Tâm", "Nguyễn Văn Anh", "Trần Thị Bình", "Lê Văn Cường", "Phạm Thị Dung",
  "Hoàng Văn Em", "Vũ Thị Phương", "Đặng Văn Giang", "Bùi Thị Hải", "Đinh Văn Hùng"
];

const DEFAULT_SPEAKERS = [
  "Phạm Thu Thảo", "Lê Hoàng Nam", "Đỗ Thị Khánh", "Hoàng Văn Lâm", "Ngô Thị Minh",
  "Dương Văn Nghĩa", "Lý Thị Oanh", "Võ Văn Phong", "Đoàn Thị Quỳnh", "Trịnh Văn Rồng"
];

// Bộ màu chủ đề
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F9FAFB', text: '#2B2C31', border: '#E5E7EB' };

// Chuyển đổi số giây nguyên sang dạng 00:SS
const formatDurationText = (sec) => {
  const integerSec = Math.round(Number(sec));
  const s = integerSec % 60;
  return `00:${s < 10 ? '0' : ''}${s}`;
};

// Danh sách bản ghi âm Code-switching
const UNIQUE_RECORDINGS_DATA = [
  {
    id: "REC-001", textId: "TXT-001",
    content: "Em nhớ upload tài liệu lên hệ thống trước deadline ngày mai nhé.",
    contentVietnamese: "Em nhớ tải tài liệu lên hệ thống trước thời hạn ngày mai nhé.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[0],
    reviewers: [
      { name: DEFAULT_REVIEWERS[0], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[1], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[2], status: "Pending", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-002", textId: "TXT-002",
    content: "Sáng nay team mình có cuộc meeting trực tiếp lúc two PM tại phòng họp.",
    contentVietnamese: "Sáng nay đội mình có cuộc họp trực tiếp lúc hai giờ chiều tại phòng họp.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[0],
    reviewers: [
      { name: DEFAULT_REVIEWERS[0], status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" },
      { name: DEFAULT_REVIEWERS[1], status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: DEFAULT_REVIEWERS[2], status: "Approved", reason: null }
    ],
    durationSec: 6, createdAt: "15/09/2026"
  },
  {
    id: "REC-003", textId: "TXT-003",
    content: "Vui lòng check kỹ pull request trên Github trước khi merge mã nguồn.",
    contentVietnamese: "Vui lòng kiểm tra kỹ yêu cầu hợp nhất trên Github trước khi gộp mã nguồn.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[1],
    reviewers: [
      { name: DEFAULT_REVIEWERS[1], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[3], status: "Approved", reason: null }
    ],
    durationSec: 5, createdAt: "15/09/2026"
  },
  {
    id: "REC-004", textId: "TXT-004",
    content: "Cuối tuần này bạn có rảnh đi coffee và chill với nhóm chúng mình không.",
    contentVietnamese: "Cuối tuần này bạn có rảnh đi cà phê và thư giãn với nhóm chúng mình không.",
    topic: "Hội thoại hàng ngày", speaker: DEFAULT_SPEAKERS[1],
    reviewers: [
      { name: DEFAULT_REVIEWERS[1], status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" },
      { name: DEFAULT_REVIEWERS[4], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[5], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-005", textId: "TXT-005",
    content: "Thầy giáo vừa gởi email nhắc nhở về bài thuyết trình tuần tới.",
    contentVietnamese: "Thầy giáo vừa gởi thư điện tử nhắc nhở về bài thuyết trình tuần tới.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[2],
    reviewers: [
      { name: DEFAULT_REVIEWERS[2], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[6], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-006", textId: "TXT-006",
    content: "Hệ thống đang bảo trì server, vui lòng thử lại sau vài phút.",
    contentVietnamese: "Hệ thống đang bảo trì máy chủ, vui lòng thử lại sau vài phút.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[2],
    reviewers: [
      { name: DEFAULT_REVIEWERS[2], status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" },
      { name: DEFAULT_REVIEWERS[7], status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" }
    ],
    durationSec: 6, createdAt: "15/09/2026"
  },
  {
    id: "REC-007", textId: "TXT-007",
    content: "Bạn có thể share file slide bài giảng cho lớp cùng xem không.",
    contentVietnamese: "Bạn có thể chia sẻ tệp trình chiếu bài giảng cho lớp cùng xem không.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[3],
    reviewers: [
      { name: DEFAULT_REVIEWERS[3], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[8], status: "Approved", reason: null }
    ],
    durationSec: 3, createdAt: "15/09/2026"
  },
  {
    id: "REC-008", textId: "TXT-008",
    content: "Dự án này cần hoàn thành đúng schedule đã đề ra ban đầu.",
    contentVietnamese: "Dự án này cần hoàn thành đúng tiến độ đã đề ra ban đầu.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[3],
    reviewers: [
      { name: DEFAULT_REVIEWERS[3], status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" },
      { name: DEFAULT_REVIEWERS[9], status: "Pending", reason: null }
    ],
    durationSec: 6, createdAt: "15/09/2026"
  },
  {
    id: "REC-009", textId: "TXT-009",
    content: "Tối nay mình đi shopping sắm vài đồ decor phòng ngủ nhé.",
    contentVietnamese: "Tối nay mình đi mua sắm vài đồ trang trí phòng ngủ nhé.",
    topic: "Hội thoại hàng ngày", speaker: DEFAULT_SPEAKERS[4],
    reviewers: [
      { name: DEFAULT_REVIEWERS[4], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[0], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-010", textId: "TXT-010",
    content: "Mọi người cùng nhau brainstorm ý tưởng mới cho đợt workshop này.",
    contentVietnamese: "Mọi người cùng nhau động não ý tưởng mới cho đợt hội thảo này.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[4],
    reviewers: [
      { name: DEFAULT_REVIEWERS[4], status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: DEFAULT_REVIEWERS[1], status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" }
    ],
    durationSec: 6, createdAt: "15/09/2026"
  },
  {
    id: "REC-011", textId: "TXT-011",
    content: "Nhớ backup dữ liệu database trước khi thực hiện nâng cấp hệ thống.",
    contentVietnamese: "Nhớ sao lưu dữ liệu cơ sở dữ liệu trước khi thực hiện nâng cấp hệ thống.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[5],
    reviewers: [
      { name: DEFAULT_REVIEWERS[5], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[2], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-012", textId: "TXT-012",
    content: "Thầy hướng dẫn bảo mình làm lại phần research methodology.",
    contentVietnamese: "Thầy hướng dẫn bảo mình làm lại phần phương pháp nghiên cứu.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[5],
    reviewers: [
      { name: DEFAULT_REVIEWERS[5], status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" },
      { name: DEFAULT_REVIEWERS[3], status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-013", textId: "TXT-013",
    content: "Cuối tuần mình đi camping ở ngoại thành để relax một chút.",
    contentVietnamese: "Cuối tuần mình đi cắm trại ở ngoại thành để thư giãn một chút.",
    topic: "Hội thoại hàng ngày", speaker: DEFAULT_SPEAKERS[6],
    reviewers: [
      { name: DEFAULT_REVIEWERS[6], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[4], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-014", textId: "TXT-014",
    content: "Bạn tạo thêm một task mới trên Jira để tracking tiến độ công việc.",
    contentVietnamese: "Bạn tạo thêm một nhiệm vụ mới trên Jira để theo dõi tiến độ công việc.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[6],
    reviewers: [
      { name: DEFAULT_REVIEWERS[6], status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" },
      { name: DEFAULT_REVIEWERS[5], status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" }
    ],
    durationSec: 5, createdAt: "15/09/2026"
  },
  {
    id: "REC-015", textId: "TXT-015",
    content: "Nhớ check inbox email xem có thông báo mới từ trường không.",
    contentVietnamese: "Nhớ kiểm tra hộp thư đến thư điện tử xem có thông báo mới từ trường không.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[7],
    reviewers: [
      { name: DEFAULT_REVIEWERS[7], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[6], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-016", textId: "TXT-016",
    content: "Quán cafe này có không gian yên tĩnh thích hợp để học study online.",
    contentVietnamese: "Quán cà phê này có không gian yên tĩnh thích hợp để học tập trực tuyến.",
    topic: "Hội thoại hàng ngày", speaker: DEFAULT_SPEAKERS[7],
    reviewers: [
      { name: DEFAULT_REVIEWERS[7], status: "Rejected", reason: "Đọc thiếu từ hoặc ngắt nghỉ không tự nhiên" },
      { name: DEFAULT_REVIEWERS[8], status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-017", textId: "TXT-017",
    content: "Đội ngũ tech vừa fix lỗi xong tính năng thanh toán trực tuyến.",
    contentVietnamese: "Đội ngũ kỹ thuật vừa sửa lỗi xong tính năng thanh toán trực tuyến.",
    topic: "Công nghệ thông tin", speaker: DEFAULT_SPEAKERS[8],
    reviewers: [
      { name: DEFAULT_REVIEWERS[8], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[9], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-018", textId: "TXT-018",
    content: "Em muốn đăng ký tham gia khóa học IELTS speaking vào tháng sau.",
    contentVietnamese: "Em muốn đăng ký tham gia khóa học IELTS kỹ năng nói vào tháng sau.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[8],
    reviewers: [
      { name: DEFAULT_REVIEWERS[8], status: "Rejected", reason: "Tốc độ đọc quá nhanh, khó nhận diện từ" },
      { name: DEFAULT_REVIEWERS[0], status: "Rejected", reason: "Phát âm từ tiếng Anh chưa chuẩn / sai accent" }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-019", textId: "TXT-019",
    content: "Chiều nay tụi mình ra sân tập workout để rèn luyện sức khỏe.",
    contentVietnamese: "Chiều nay tụi mình ra sân tập thể dục để rèn luyện sức khỏe.",
    topic: "Hội thoại hàng ngày", speaker: DEFAULT_SPEAKERS[9],
    reviewers: [
      { name: DEFAULT_REVIEWERS[9], status: "Approved", reason: null },
      { name: DEFAULT_REVIEWERS[1], status: "Approved", reason: null }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  },
  {
    id: "REC-020", textId: "TXT-020",
    content: "Cảm ơn bạn đã hỗ trợ feedback bài làm của mình rất chi tiết.",
    contentVietnamese: "Cảm ơn bạn đã hỗ trợ nhận xét bài làm của mình rất chi tiết.",
    topic: "Giáo dục", speaker: DEFAULT_SPEAKERS[9],
    reviewers: [
      { name: DEFAULT_REVIEWERS[9], status: "Rejected", reason: "Giọng đọc bị rè / âm lượng quá nhỏ" },
      { name: DEFAULT_REVIEWERS[2], status: "Rejected", reason: "Lẫn tiếng ồn môi trường / tạp âm" }
    ],
    durationSec: 4, createdAt: "15/09/2026"
  }
];

// Hàm tính toán trạng thái chung của bản ghi dựa trên danh sách Reviewers:
const calculateRecordingStatus = (reviewers = []) => {
  const hasPending = reviewers.some((rev) => rev.status === "Pending");
  if (hasPending) return "Pending";

  const rejectedCount = reviewers.filter((rev) => rev.status === "Rejected").length;
  if (rejectedCount >= 2) return "Rejected";

  return "Approved";
};

// Hàm lấy tên viết tắt cho Avatar/Icon đại diện reviewer
const getInitials = (name) => {
  if (!name) return "R";
  const parts = name.trim().split(" ");
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[parts.length - 2].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
};

export default function AdminRecordings() {
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  // Modals state
  const [selectedTextContent, setSelectedTextContent] = useState(null);
  const [selectedReviewersList, setSelectedReviewersList] = useState(null);
  
  // Audio Player Modal State
  const [playingRecording, setPlayingRecording] = useState(null);
  const [isPlaying, setIsPlaying] = useState(false);

  const recordings = useMemo(() => UNIQUE_RECORDINGS_DATA, []);

  const handleSearch = () => {
    setSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  // Logic Tìm kiếm
  const filteredRecordings = useMemo(() => {
    const keywords = searchTerm.trim().toLowerCase().split(/\s+/).filter(Boolean);

    return recordings.filter((r) => {
      const recStatus = calculateRecordingStatus(r.reviewers);

      const matchStatus = statusFilter === "all" || recStatus === statusFilter;
      if (!matchStatus) return false;

      if (keywords.length === 0) return true;

      const reviewersText = r.reviewers.map((rev) => rev.name).join(" ");
      const combinedSearchableText = [
        r.id,
        r.textId,
        r.content,
        r.contentVietnamese || "",
        r.speaker,
        r.topic,
        reviewersText
      ].join(" ").toLowerCase();

      return keywords.every((kw) => combinedSearchableText.includes(kw));
    });
  }, [recordings, searchTerm, statusFilter]);

  const totalPages = Math.ceil(filteredRecordings.length / pageSize) || 1;
  const paginatedRecordings = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredRecordings.slice(start, start + pageSize);
  }, [filteredRecordings, currentPage, pageSize]);

  const emptyRowsCount = useMemo(() => {
    return Math.max(0, pageSize - paginatedRecordings.length);
  }, [paginatedRecordings.length, pageSize]);

  const handleExportDataset = () => {
    const approvedCount = recordings.filter((r) => calculateRecordingStatus(r.reviewers) === "Approved").length;
    alert(`Đang đóng gói và tải xuống tập dữ liệu Code-switching gồm ${approvedCount} file Audio (.ZIP)...`);
  };

  const openAudioPlayer = (recording) => {
    setPlayingRecording(recording);
    setIsPlaying(false);
  };

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      {/* Header & Filter */}
      <div className="shrink-0 space-y-1">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1">
          <div>
            <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
              <Headphones className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
              Quản lý bản thu & bộ dữ liệu
            </h2>
            <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
              Quản lý các bản thu âm Code-switching (Anh - Việt) và xuất tập dữ liệu
            </p>
          </div>

          <button
            onClick={handleExportDataset}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="px-3 py-1 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 hover:opacity-90 transition-all cursor-pointer shadow-xs shrink-0"
          >
            <Download className="w-3.5 h-3.5" />
            <span>Tải Dataset chuẩn (.ZIP)</span>
          </button>
        </div>

        {/* Filter Bar */}
        <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div className="relative w-full flex items-center bg-[#F9FAFB] dark:bg-[#25272E] rounded-xl border border-[#E5E7EB] dark:border-gray-700 focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA6] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm bản thu âm, nội dung, speaker..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium text-[#2B2C31] dark:text-white outline-none transition-all placeholder:text-gray-400"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-all cursor-pointer z-10 shadow-xs"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
            <Filter className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setCurrentPage(1);
              }}
              className="w-[150px] px-2.5 py-2 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold text-[#2B2C31] dark:text-white outline-none cursor-pointer transition-all font-sans"
            >
              <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
              <option value="Approved" className="bg-white dark:bg-[#25272E]">Đã phê duyệt</option>
              <option value="Pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
              <option value="Rejected" className="bg-white dark:bg-[#25272E]">Từ chối</option>
            </select>
          </div>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-hidden">
          <table className="w-full h-full text-left border-collapse table-fixed">
            <thead>
              <tr className="bg-[#F9FAFB] dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-[#6E7078] dark:text-gray-400 border-b border-[#E5E7EB] dark:border-gray-800 font-bold h-7">
                <th className="w-[80px] px-1.5 text-center">Mã Audio</th>
                <th className="w-[80px] px-1.5 text-center">Mã Text</th>
                <th className="w-[150px] px-1.5 text-center">Nội dung câu</th>
                <th className="w-[160px] px-1.5">Chủ đề</th>
                <th className="w-[140px] px-1.5">Speaker</th>
                <th className="w-[170px] px-1.5 text-center">Phản hồi</th>
                <th className="w-[100px] px-1 text-center">Nghe</th>
                <th className="w-[100px] px-1 text-center">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E5E7EB] dark:divide-gray-800 text-[11px] font-medium">
              {paginatedRecordings.length > 0 ? (
                <>
                  {paginatedRecordings.map((r) => {
                    const style = getCatStyle(r.topic);
                    const status = calculateRecordingStatus(r.reviewers);
                    const totalRev = r.reviewers.length;
                    const rejectedRevCount = r.reviewers.filter((rev) => rev.status === "Rejected").length;

                    return (
                      <tr key={r.id} className="hover:bg-[#F3F4F6] dark:hover:bg-[#25272E]/50 transition-colors">
                        <td className="px-1.5 text-center font-bold text-[#2B2C31] dark:text-gray-100 align-middle py-0.5">
                          {r.id}
                        </td>

                        <td className="px-1.5 text-center font-bold text-[#2B2C31] dark:text-gray-100 align-middle py-0.5">
                          {r.textId}
                        </td>

                        {/* Nút xem nội dung câu */}
                        <td className="px-1.5 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => setSelectedTextContent(r)}
                            className="inline-flex items-center justify-center gap-1.5 px-2.5 py-1 rounded-lg bg-[#F9FAFB] dark:bg-[#25272E] hover:bg-[#E5E7EB] dark:hover:bg-gray-700 text-[#2B2C31] dark:text-gray-200 text-[11px] font-bold border border-[#E5E7EB] dark:border-gray-700 transition-colors cursor-pointer w-full"
                          >
                            <Eye className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
                            <span>Xem nội dung câu</span>
                          </button>
                        </td>

                        <td className="px-1.5 align-middle py-0.5">
                          <span
                            className="px-2 py-0.5 rounded-md text-[10.5px] font-bold border inline-block whitespace-nowrap"
                            style={{
                              backgroundColor: style.bg,
                              color: style.text,
                              borderColor: style.border
                            }}
                          >
                            {r.topic}
                          </span>
                        </td>

                        <td className="px-1.5 font-bold text-[#2B2C31] dark:text-gray-100 align-middle truncate py-0.5">
                          <span title={r.speaker}>{r.speaker}</span>
                        </td>

                        {/* Cột Phản hồi */}
                        <td className="px-1.5 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => setSelectedReviewersList(r)}
                            className="inline-flex items-center justify-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium border bg-[#F7FAFC] dark:bg-[#25272E] border-[#E2E8F0] dark:border-gray-700 hover:bg-[#EDF2F7] dark:hover:bg-gray-700 transition-all cursor-pointer mx-auto"
                          >
                            <span
                              style={{
                                color: rejectedRevCount > 0 ? CHIP_DANGER_TEXT : undefined
                              }}
                              className={rejectedRevCount > 0 ? "font-semibold" : "text-[#4A5568] dark:text-gray-300"}
                            >
                              {rejectedRevCount > 0
                                ? `${rejectedRevCount}/${totalRev} từ chối`
                                : `${totalRev} reviewers`}
                            </span>
                            <Eye className="w-3 h-3 text-[#A0AEC0] dark:text-gray-400 shrink-0" />
                          </button>
                        </td>

                        {/* Nút Nghe */}
                        <td className="px-1 text-center align-middle py-0.5">
                          <button
                            type="button"
                            onClick={() => openAudioPlayer(r)}
                            style={{ backgroundColor: ADMIN_ACCENT }}
                            className="inline-flex items-center justify-center gap-1.5 w-[76px] py-1 rounded-md text-white text-[11px] font-bold hover:opacity-90 active:scale-95 transition-all cursor-pointer shadow-xs mx-auto"
                          >
                            <Play className="w-3 h-3 fill-white shrink-0" />
                            <span className="tabular-nums">{formatDurationText(r.durationSec)}</span>
                          </button>
                        </td>

                        {/* Trạng thái */}
                        <td className="px-1 text-center align-middle py-0.5">
                          {status === "Approved" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_SUCCESS_BG,
                                borderColor: CHIP_SUCCESS_BORDER,
                                color: CHIP_SUCCESS_TEXT
                              }}
                            >
                              <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Đã duyệt
                            </span>
                          )}

                          {status === "Pending" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_WARNING_BG,
                                borderColor: CHIP_WARNING_BORDER,
                                color: CHIP_WARNING_TEXT
                              }}
                            >
                              <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Chờ duyệt
                            </span>
                          )}

                          {status === "Rejected" && (
                            <span
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                              style={{
                                backgroundColor: CHIP_DANGER_BG,
                                borderColor: CHIP_DANGER_BORDER,
                                color: CHIP_DANGER_TEXT
                              }}
                            >
                              <XCircle className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_DANGER_TEXT }} /> Từ chối
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan="8" className="py-0.5">&nbsp;</td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan="8" className="py-16 text-center text-[#9A9CA6] dark:text-gray-500 text-xs">
                    Chưa có bản ghi âm nào phù hợp.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Pagination */}
        <div className="px-2 py-0.5 border-t border-[#E5E7EB] dark:border-gray-800 shrink-0">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
          />
        </div>
      </div>

      {/* MODAL 1: NGHE BẢN GHI ÂM */}
      {playingRecording && (
        <div className="fixed inset-0 z-50 bg-black/40 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in duration-150">
            
            <div 
              style={{ backgroundColor: ADMIN_ACCENT }} 
              className="h-1.5 w-full rounded-t-3xl shrink-0" 
            />

            <div className="p-6 space-y-5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-[#E6F0FE] dark:bg-blue-900/40 flex items-center justify-center text-[#1E40AF] dark:text-blue-300">
                    <Headphones className="w-4 h-4" />
                  </div>
                  <h3 className="text-sm font-bold text-[#2B2C31] dark:text-gray-100">
                    Nghe bản ghi âm
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => setPlayingRecording(null)}
                  className="p-1.5 rounded-full text-[#9A9CA6] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-4 bg-[#F8F9FA] dark:bg-[#25272E] rounded-2xl space-y-1.5">
                <span className="text-[10px] font-bold uppercase tracking-wider text-[#9A9CA6] dark:text-gray-400 block">
                  NỘI DUNG CÂU
                </span>
                <p className="font-medium text-[#2B2C31] dark:text-gray-100 leading-relaxed text-xs">
                  "{playingRecording.content}"
                </p>
              </div>

              <div className="space-y-1 pt-1">
                <div className="relative w-full h-1 bg-[#E5E7EB] dark:bg-gray-700 rounded-full flex items-center cursor-pointer">
                  <div className="h-full bg-gray-300 dark:bg-gray-500 rounded-full w-0" />
                  <div className="w-3 h-3 bg-white dark:bg-gray-200 border-2 border-gray-400 dark:border-gray-500 rounded-full shadow-xs absolute left-0 -translate-x-1/2 cursor-pointer" />
                </div>
                <div className="flex items-center justify-between text-[11px] font-semibold text-[#9A9CA6] dark:text-gray-400 pt-1">
                  <span>0:00</span>
                  <span>-{formatDurationText(playingRecording.durationSec)}</span>
                </div>
              </div>

              <div className="flex justify-center pt-1 pb-1">
                <button
                  type="button"
                  onClick={() => setIsPlaying(!isPlaying)}
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="w-12 h-12 rounded-full text-white flex items-center justify-center hover:scale-105 active:scale-95 transition-all shadow-md cursor-pointer"
                >
                  {isPlaying ? (
                    <Pause className="w-5 h-5 fill-white" />
                  ) : (
                    <Play className="w-5 h-5 fill-white ml-0.5" />
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: HIỆN CẶP CÂU NỘI DUNG */}
      {selectedTextContent && (
        <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-lg w-full overflow-hidden animate-in fade-in zoom-in duration-150">
            {/* VẠCH MÀU TRANG TRÍ Ở MÉP TRÊN ĐỈNH MODAL MÀU ADMIN */}
            <div 
              style={{ backgroundColor: ADMIN_ACCENT }} 
              className="h-1.5 w-full rounded-t-3xl shrink-0" 
            />

            <div className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E7EB] dark:border-gray-800 pb-3">
                <div className="flex items-center gap-2">
                  <div className="w-8 h-8 rounded-full bg-[#F3F4F6] dark:bg-[#25272E] flex items-center justify-center text-[#2B2C31] dark:text-gray-200">
                    <Eye className="w-4 h-4" />
                  </div>
                  <h3 className="text-sm font-bold text-[#2B2C31] dark:text-gray-100">
                    Chi tiết nội dung câu ({selectedTextContent.textId})
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedTextContent(null)}
                  className="p-1 rounded-full text-[#6E7078] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="space-y-3 text-xs">
                <div className="p-3.5 bg-[#F9FAFB] dark:bg-[#25272E] rounded-2xl border border-[#E5E7EB]/60 dark:border-gray-700/60 space-y-1">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#6E7078] dark:text-gray-400">
                    Câu trộn Ngôn ngữ (Vietnamese - English):
                  </span>
                  <p className="font-semibold text-[#2B2C31] dark:text-gray-100 leading-relaxed text-sm">
                    "{selectedTextContent.content}"
                  </p>
                </div>

                <div className="p-3.5 bg-[#E6F0FE] dark:bg-[#1E293B] rounded-2xl border border-[#C9DEFB] dark:border-blue-900 space-y-1">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#1E40AF] dark:text-blue-300">
                    Câu thuần Tiếng Việt (Nghĩa hoàn toàn tương đồng):
                  </span>
                  <p className="font-semibold text-[#1E40AF] dark:text-blue-200 leading-relaxed text-sm">
                    "{selectedTextContent.contentVietnamese}"
                  </p>
                </div>
              </div>

              <div className="flex justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setSelectedTextContent(null)}
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="w-full py-2.5 text-white rounded-2xl text-xs font-bold hover:opacity-90 transition-all cursor-pointer shadow-sm text-center"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: KẾT QUẢ KIỂM DUYỆT (Đã cố định màu chữ tối text-[#2B2C31] cho reviewer) */}
      {selectedReviewersList && (() => {
        const total = selectedReviewersList.reviewers.length;
        const approvedCount = selectedReviewersList.reviewers.filter(r => r.status === "Approved").length;
        const rejectedCount = selectedReviewersList.reviewers.filter(r => r.status === "Rejected").length;
        const finalStatus = calculateRecordingStatus(selectedReviewersList.reviewers);

        const topBorderBg =
          finalStatus === "Approved"
            ? CHIP_SUCCESS_TEXT
            : finalStatus === "Rejected"
            ? CHIP_DANGER_TEXT
            : CHIP_WARNING_TEXT;

        const iconBg =
          finalStatus === "Approved"
            ? CHIP_SUCCESS_BG
            : finalStatus === "Rejected"
            ? CHIP_DANGER_BG
            : CHIP_WARNING_BG;

        return (
          <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4">
            <div className="bg-white dark:bg-[#1C1D22] rounded-3xl border border-[#E5E7EB] dark:border-gray-800 shadow-2xl max-w-md w-full overflow-hidden animate-in fade-in zoom-in duration-150 text-left">
              
              {/* VẠCH MÀU TRANG TRÍ Ở MÉP TRÊN ĐỈNH MODAL */}
              <div 
                style={{ backgroundColor: topBorderBg }} 
                className="h-1.5 w-full rounded-t-3xl shrink-0" 
              />

              <div className="p-6 space-y-4">
                {/* Header Modal */}
                <div 
                  style={{ borderBottomColor: BORDER_LIGHT }} 
                  className="flex items-start justify-between border-b pb-3"
                >
                  <div className="flex items-start gap-2.5">
                    <div 
                      style={{ backgroundColor: iconBg, color: topBorderBg }}
                      className="w-8 h-8 rounded-full flex items-center justify-center shrink-0 mt-0.5 transition-colors"
                    >
                      <AlertCircle className="w-4 h-4" />
                    </div>
                    <div>
                      <h3 className="text-base font-bold text-[#2B2C31] dark:text-gray-100">
                        Kết quả kiểm duyệt
                      </h3>
                      <p className="text-xs font-semibold text-[#6E7078] dark:text-gray-400 mt-0.5">
                        {approvedCount}/{total} duyệt · {rejectedCount}/{total} từ chối →{" "}
                        <span
                          style={{
                            color: topBorderBg
                          }}
                          className="font-bold"
                        >
                          {finalStatus === "Approved" ? "Đã duyệt" : finalStatus === "Rejected" ? "Từ chối" : "Chờ duyệt"}
                        </span>
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    className="p-1 rounded-full text-[#9A9CA6] hover:bg-[#F3F4F6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                {/* Ô CÂU VĂN */}
                <div className="p-3.5 bg-[#F9FAFB] dark:bg-[#25272E] rounded-2xl space-y-1 border border-[#E5E7EB]/60 dark:border-gray-700/60">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#9A9CA6] dark:text-gray-400 block">
                    CÂU VĂN
                  </span>
                  <p className="font-semibold text-[#2B2C31] dark:text-gray-100 text-xs leading-relaxed">
                    "{selectedReviewersList.content}"
                  </p>
                </div>

                {/* DANH SÁCH REVIEWER KIỂM DUYỆT */}
                <div className="space-y-2 max-h-64 overflow-y-auto pr-1">
                  {selectedReviewersList.reviewers.map((rev, idx) => {
                    const isApp = rev.status === "Approved";
                    const isRej = rev.status === "Rejected";

                    const itemBg = isApp ? CHIP_SUCCESS_BG : isRej ? CHIP_DANGER_BG : CHIP_WARNING_BG;
                    const itemBorder = isApp ? CHIP_SUCCESS_BORDER : isRej ? CHIP_DANGER_BORDER : CHIP_WARNING_BORDER;
                    const itemText = isApp ? CHIP_SUCCESS_TEXT : isRej ? CHIP_DANGER_TEXT : CHIP_WARNING_TEXT;

                    return (
                      <div
                        key={idx}
                        style={{
                          backgroundColor: itemBg,
                          borderColor: itemBorder
                        }}
                        className="p-3 rounded-2xl border transition-colors flex flex-col gap-1"
                      >
                        <div className="flex items-center gap-2">
                          {/* Avatar tròn chứa tên viết tắt */}
                          <div
                            style={{ backgroundColor: itemText }}
                            className="w-6 h-6 rounded-full flex items-center justify-center text-[10px] font-bold text-white shrink-0"
                          >
                            {getInitials(rev.name)}
                          </div>

                          {/* Đã giữ nguyên màu chữ tối text-[#2B2C31] trên cả light và dark mode */}
                          <span className="font-bold text-xs text-[#2B2C31] flex-1 truncate">
                            {rev.name}
                          </span>

                          <span
                            style={{ color: itemText }}
                            className="text-xs font-bold"
                          >
                            {isApp ? "Đã duyệt" : isRej ? "Từ chối" : "Chưa đánh giá"}
                          </span>
                        </div>

                        {/* Lý do từ chối nếu có (sử dụng màu chữ đậm dễ đọc) */}
                        {isRej && rev.reason && (
                          <div className="pl-8 text-[11px] font-semibold text-[#2B2C31]">
                            {rev.reason}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>

                {/* NÚT ĐÓNG */}
                <div className="pt-2">
                  <button
                    type="button"
                    onClick={() => setSelectedReviewersList(null)}
                    style={{ backgroundColor: ADMIN_ACCENT }}
                    className="w-full py-2.5 text-white rounded-2xl text-xs font-bold hover:opacity-90 transition-all cursor-pointer shadow-sm text-center"
                  >
                    Đóng
                  </button>
                </div>
              </div>

            </div>
          </div>
        );
      })()}
    </div>
  );
}