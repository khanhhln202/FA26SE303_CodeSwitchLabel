import { useState, useMemo, useEffect } from "react";
import { 
  FileText, 
  Plus, 
  Search, 
  Filter, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  Clock, 
  X,
  AlertTriangle
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  SUCCESS,
  DANGER,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT
} from "../../../constants/theme";

// Màu phân loại (bộ A) - đồng bộ chuẩn với ReviewerContribution
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};
const catStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F3F4F6', text: '#374151', border: '#E5E7EB' };

const countWords = (text) => {
  if (!text || !text.trim()) return 0;
  const words = text.trim().match(/\S+/g);
  return words ? words.length : 0;
};

const INITIAL_TEXTS = [
  { id: "TXT-001", content: "Em nhớ upload tài liệu lên hệ thống trước deadline ngày mai nhé.", topic: "Công nghệ thông tin", status: "active" },
  { id: "TXT-002", content: "Sáng nay team mình có cuộc meeting trực tiếp lúc two PM tại phòng họp.", topic: "Giáo dục", status: "active" },
  { id: "TXT-003", content: "Vui lòng check kỹ pull request trên Github trước khi merge mã nguồn.", topic: "Công nghệ thông tin", status: "active" },
  { id: "TXT-004", content: "Cuối tuần này bạn có rảnh đi coffee và chill với nhóm chúng mình không.", topic: "Hội thoại hàng ngày", status: "active" },
  { id: "TXT-005", content: "Thầy giáo vừa gởi email nhắc nhở về bài thuyết trình tuần tới.", topic: "Giáo dục", status: "active" },
  { id: "TXT-006", content: "Hệ thống đang bảo trì server, vui lòng thử lại sau vài phút.", topic: "Công nghệ thông tin", status: "active" },
  { id: "TXT-007", content: "Bạn có thể share file slide bài giảng cho lớp cùng xem không.", topic: "Giáo dục", status: "active" },
  { id: "TXT-008", content: "Dự án này cần hoàn thành đúng schedule đã đề ra ban đầu.", topic: "Công nghệ thông tin", status: "active" },
  { id: "TXT-009", content: "Tối nay mình đi shopping sắm vài đồ decor phòng ngủ nhé.", topic: "Hội thoại hàng ngày", status: "active" },
  { id: "TXT-010", content: "Mọi người cùng nhau brainstorm ý tưởng mới cho đợt workshop này.", topic: "Giáo dục", status: "active" },
  { id: "TXT-011", content: "Nhớ backup dữ liệu database trước khi thực hiện nâng cấp hệ thống.", topic: "Công nghệ thông tin", status: "pending" },
  { id: "TXT-012", content: "Thầy hướng dẫn bảo mình làm lại phần research methodology.", topic: "Giáo dục", status: "pending" },
  { id: "TXT-013", content: "Cuối tuần mình đi camping ở ngoại thành để relax một chút.", topic: "Hội thoại hàng ngày", status: "pending" },
  { id: "TXT-014", content: "Bạn tạo thêm một task mới trên Jira để tracking tiến độ công việc.", topic: "Công nghệ thông tin", status: "pending" },
  { id: "TXT-015", content: "Nhớ check inbox email xem có thông báo mới từ trường không.", topic: "Giáo dục", status: "pending" },
  { id: "TXT-016", content: "Quán cafe này có không gian yên tĩnh thích hợp để học study online.", topic: "Hội thoại hàng ngày", status: "pending" },
  { id: "TXT-017", content: "Đội ngũ tech vừa fix lỗi xong tính năng thanh toán trực tuyến.", topic: "Công nghệ thông tin", status: "pending" },
  { id: "TXT-018", content: "Em muốn đăng ký tham gia khóa học IELTS speaking vào tháng sau.", topic: "Giáo dục", status: "pending" },
  { id: "TXT-019", content: "Chiều nay tụi mình ra sân tập workout để rèn luyện sức khỏe.", topic: "Hội thoại hàng ngày", status: "pending" },
  { id: "TXT-020", content: "Cảm ơn bạn đã hỗ trợ feedback bài làm của mình rất chi tiết.", topic: "Giáo dục", status: "pending" }
];

const LOCAL_STORAGE_KEY = "admin_text_dataset_v3";

export default function AdminTextData() {
  const [textList, setTextList] = useState(() => {
    const saved = localStorage.getItem(LOCAL_STORAGE_KEY);
    if (saved) {
      try { 
        return JSON.parse(saved); 
      } catch (e) { 
        console.error(e); 
      }
    }
    return INITIAL_TEXTS;
  });

  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("all");
  
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  const [toast, setToast] = useState({ show: false, message: "" });
  const [formData, setFormData] = useState({ content: "", topic: "Công nghệ thông tin", status: "active" });

  useEffect(() => {
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(textList));
  }, [textList]);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => {
        setToast((prev) => ({ ...prev, show: false }));
      }, 3500);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  const handleSearch = () => {
    setActiveSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setFormData({ content: "", topic: "Công nghệ thông tin", status: "active" });
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setFormData({ content: item.content, topic: item.topic, status: item.status });
    setShowModal(true);
  };

  const handleSubmitForm = (e) => {
    e.preventDefault();
    if (!formData.content.trim()) return;

    if (editingItem) {
      setTextList((prev) =>
        prev.map((item) =>
          item.id === editingItem.id
            ? {
                ...item,
                content: formData.content,
                topic: formData.topic,
                status: formData.status,
              }
            : item
        )
      );
      showNotification(`Đã cập nhật thành công văn bản ${editingItem.id}!`);
    } else {
      const newItem = {
        id: `TXT-${String(textList.length + 1).padStart(3, '0')}`,
        content: formData.content,
        topic: formData.topic,
        status: formData.status,
      };
      setTextList((prev) => [...prev, newItem]);
      showNotification("Đã thêm văn bản mới!");
    }

    setShowModal(false);
  };

  const handleConfirmDelete = () => {
    if (!itemToDelete) return;
    setTextList((prev) => prev.filter((t) => t.id !== itemToDelete.id));
    showNotification(`Đã xóa thành công văn bản ${itemToDelete.id}!`);
    setItemToDelete(null);
  };

  const filteredTexts = useMemo(() => {
    return textList.filter((item) => {
      const matchesSearch = 
        item.content.toLowerCase().includes(activeSearchTerm.toLowerCase()) || 
        item.id.toLowerCase().includes(activeSearchTerm.toLowerCase());
      const matchesStatus = selectedStatus === "all" || item.status === selectedStatus;
      
      return matchesSearch && matchesStatus;
    });
  }, [textList, activeSearchTerm, selectedStatus]);

  const totalPages = Math.ceil(filteredTexts.length / pageSize) || 1;
  const paginatedTexts = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredTexts.slice(start, start + pageSize);
  }, [filteredTexts, currentPage, pageSize]);

  return (
    <div className="max-w-6xl mx-auto h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
      {/* Toast Notification */}
      <div 
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-3.5 py-2 rounded-xl shadow-xl border border-[#3FA66B]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-4 h-4 shrink-0" style={{ color: SUCCESS }} />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-1"
        >
          <X className="w-3.5 h-3.5 text-[#9A9CA3] dark:text-[#6E7078]" />
        </button>
      </div>

      {/* Header & Filter */}
      <div className="shrink-0 space-y-2">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1.5">
          <div>
            <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
              <FileText className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
              Quản lý dữ liệu văn bản
            </h2>
            <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
              Kho câu mẫu dùng cho Speaker thu âm và Reviewer kiểm duyệt
            </p>
          </div>

          <button
            onClick={handleOpenAddModal}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="px-3 py-1.5 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 hover:opacity-90 transition-all cursor-pointer shadow-xs shrink-0"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Thêm văn bản mới</span>
          </button>
        </div>

        {/* Toolbar & Filter Bar */}
        <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div className="relative w-full flex items-center bg-[#F9FAFB] dark:bg-[#25272E] rounded-xl border border-[#E5E7EB] dark:border-gray-700 focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA3] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm văn bản..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium text-[#16171C] dark:text-white outline-none transition-all placeholder:text-gray-400"
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
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value);
                setCurrentPage(1);
              }}
              className="w-[150px] px-2.5 py-2 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold text-[#16171C] dark:text-white outline-none cursor-pointer transition-all font-sans"
            >
              <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
              <option value="active" className="bg-white dark:bg-[#25272E]">Sẵn sàng</option>
              <option value="pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
            </select>
          </div>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0">
          <div className="min-w-[700px] h-full flex flex-col">
            {/* Header Table */}
            <div className="bg-[#F9FAFB] dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-[#6E7078] dark:text-gray-400 border-b border-[#E5E7EB] dark:border-gray-800 font-bold flex items-center shrink-0 h-9">
              <div className="px-3 text-center whitespace-nowrap w-[80px]">Mã text</div>
              <div className="px-4 text-left flex-1">Nội dung</div>
              <div className="px-3 text-left w-[170px] whitespace-nowrap">Chủ đề</div>
              <div className="px-3 text-left w-[90px] whitespace-nowrap">Số từ</div>
              <div className="px-3 text-left w-[120px] whitespace-nowrap">Trạng thái</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#E5E7EB] dark:divide-gray-800 text-[11px] font-medium flex-1 grid grid-rows-10">
              {paginatedTexts.length > 0 ? (
                paginatedTexts.map((item) => (
                  <div key={item.id} className="hover:bg-[#F3F4F6] dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full">
                    <div className="px-3 text-center font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap w-[80px]">
                      {item.id}
                    </div>
                    
                    <div className="px-4 flex-1 truncate pr-6 text-[#16171C] dark:text-gray-200 font-medium">
                      {item.content}
                    </div>
                    
                    <div className="px-3 w-[170px] whitespace-nowrap">
                      <span className="px-2.5 py-1 rounded-md text-[11px] font-bold border" style={{ background: catStyle(item.topic).bg, color: catStyle(item.topic).text, borderColor: catStyle(item.topic).border }}>
                        {item.topic}
                      </span>
                    </div>
                    
                    <div className="px-3 w-[90px] font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap">
                      {countWords(item.content)} từ
                    </div>
                    
                    <div className="px-3 w-[120px] whitespace-nowrap">
                      {item.status === "active" ? (
                        <span 
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                          style={{
                            backgroundColor: CHIP_SUCCESS_BG,
                            borderColor: CHIP_SUCCESS_BORDER,
                            color: CHIP_SUCCESS_TEXT
                          }}
                        >
                          <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Sẵn sàng
                        </span>
                      ) : (
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
                    </div>

                    <div className="px-4 text-center w-[100px] whitespace-nowrap">
                      <div className="flex items-center justify-center gap-1">
                        <button 
                          onClick={() => handleOpenEditModal(item)}
                          className="p-1 rounded-md text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F3F4F6] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                          title="Chỉnh sửa văn bản"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>
                        <button 
                          onClick={() => setItemToDelete(item)}
                          className="p-1 rounded-md text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                          title="Xóa văn bản"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  </div>
                ))
              ) : (
                <div className="row-span-10 flex items-center justify-center text-[#9A9CA3] dark:text-gray-500 text-xs">
                  Không tìm thấy dữ liệu văn bản phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Pagination */}
        <div className="px-2 py-1.5 border-t border-[#E5E7EB] dark:border-gray-800 shrink-0">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
          />
        </div>
      </div>

      {/* Modal Thêm & Chỉnh Sửa */}
      {showModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-xs flex items-center justify-center p-4 z-50" onClick={() => setShowModal(false)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-[#E5E7EB] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full" style={{ backgroundColor: ADMIN_ACCENT }} />
            <form onSubmit={handleSubmitForm} className="p-5 space-y-3">
              <h3 className="text-sm font-bold text-[#16171C] dark:text-gray-100">
                {editingItem ? `Chỉnh sửa Văn bản (${editingItem.id})` : "Thêm văn bản mới"}
              </h3>

              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">
                  Chủ đề
                </label>
                <select
                  value={formData.topic}
                  onChange={(e) => setFormData({ ...formData, topic: e.target.value })}
                  className="w-full px-3 py-1.5 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-lg text-xs font-bold text-[#16171C] dark:text-white outline-none transition-all cursor-pointer font-sans"
                >
                  <option value="Công nghệ thông tin" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                  <option value="Giáo dục" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                  <option value="Hội thoại hàng ngày" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">
                  Nội dung câu mẫu
                </label>
                <textarea
                  rows="4"
                  required
                  placeholder="Nhập câu mẫu..."
                  value={formData.content}
                  onChange={(e) => setFormData({ ...formData, content: e.target.value })}
                  className="w-full p-2.5 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-lg text-xs font-medium text-[#16171C] dark:text-white outline-none transition-all resize-none"
                ></textarea>
                <div className="flex justify-between items-center mt-0.5 text-[10px] text-[#6E7078] dark:text-gray-400">
                  <span>Không giới hạn độ dài câu</span>
                  <span className="font-bold text-[#16171C] dark:text-gray-200">
                    Số từ: {countWords(formData.content)} từ
                  </span>
                </div>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">
                  Trạng thái
                </label>
                <select
                  value={formData.status}
                  onChange={(e) => setFormData({ ...formData, status: e.target.value })}
                  className="w-full px-3 py-1.5 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-lg text-xs font-bold text-[#16171C] dark:text-white outline-none transition-all cursor-pointer font-sans"
                >
                  <option value="active" className="bg-white dark:bg-[#25272E]">Sẵn sàng</option>
                  <option value="pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
                </select>
              </div>

              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2 rounded-lg border border-[#E5E7EB] dark:border-gray-700 text-xs font-bold text-[#6E7078] dark:text-gray-300 hover:bg-[#F9FAFB] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 cursor-pointer transition-all"
                >
                  {editingItem ? "Lưu thay đổi" : "Lưu Văn Bản"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Xóa */}
      {itemToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs" onClick={() => setItemToDelete(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden border border-[#E5E7EB] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full" style={{ backgroundColor: DANGER }} />
            <div className="p-5 text-center space-y-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}20`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-[#2B2C31] dark:text-gray-100">Xác nhận xóa văn bản</h3>
                <p className="text-[11px] text-[#6E7078] dark:text-gray-400 mt-1 break-words">
                  Bạn có chắc chắn muốn xóa mã <span className="font-bold text-[#2B2C31] dark:text-gray-200">"{itemToDelete.id}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  onClick={() => setItemToDelete(null)}
                  className="flex-1 py-2 rounded-lg border border-[#E5E7EB] dark:border-gray-700 text-xs font-bold text-[#6E7078] dark:text-gray-300 hover:bg-[#F9FAFB] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={handleConfirmDelete}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold transition-colors cursor-pointer hover:opacity-90"
                  style={{ backgroundColor: DANGER }}
                >
                  Xóa ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}