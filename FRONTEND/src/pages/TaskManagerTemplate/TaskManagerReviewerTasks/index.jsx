import { useState, useMemo, useEffect } from "react";
import { 
  FolderKanban,
  Plus, 
  Search, 
  Filter, 
  CheckCircle2, 
  Edit3, 
  Trash2, 
  X, 
  AlertTriangle
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  TASK_MANAGER_ACCENT,
  SUCCESS,
  DANGER
} from "../../../constants/theme";

// Bộ màu badge chủ đề chuẩn thiết kế giao diện nhẹ nhàng
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { 
  bg: '#F3F4F6', 
  text: '#374151', 
  border: '#E5E7EB' 
};

// Danh sách dữ liệu mẫu đã được Việt hóa chủ đề
const INITIAL_REVIEWER_TASKS = [
  { id: "TSK-001", title: "Nhiệm vụ kiểm thử thuật ngữ công nghệ", topic: "Công nghệ thông tin", reviewed: 65, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-002", title: "Nhiệm vụ đánh giá hội thoại giáo dục phổ thông", topic: "Giáo dục", reviewed: 80, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-003", title: "Rà soát dữ liệu giao tiếp đời sống hàng ngày", topic: "Hội thoại hàng ngày", reviewed: 135, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-004", title: "Thẩm định ngữ liệu lệnh thoại nhà thông minh", topic: "Công nghệ thông tin", reviewed: 40, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-005", title: "Kiểm duyệt kịch bản hỏi đáp y tế cơ bản", topic: "Hội thoại hàng ngày", reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-006", title: "Đánh giá bài giảng toán học trực tuyến", topic: "Giáo dục", reviewed: 110, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-007", title: "Kiểm tra tin tức kinh tế và thị trường tài chính", topic: "Hội thoại hàng ngày", reviewed: 20, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-008", title: "Thẩm định dữ liệu hội thoại bán hàng tự động", topic: "Công nghệ thông tin", reviewed: 130, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-009", title: "Rà soát phát âm bảng chữ cái Tiếng Việt cho trẻ em", topic: "Giáo dục", reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-010", title: "Kiểm duyệt tài liệu hướng dẫn lập trình Python", topic: "Công nghệ thông tin", reviewed: 85, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-011", title: "Đánh giá mẫu hội thoại đặt xe trực tuyến", topic: "Hội thoại hàng ngày", reviewed: 85, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-012", title: "Thẩm định thuật ngữ trí tuệ nhân tạo nâng cao", topic: "Công nghệ thông tin", reviewed: 30, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-013", title: "Rà soát bài luyện nói Tiếng Anh giao tiếp", topic: "Giáo dục", reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-014", title: "Kiểm duyệt các đoạn hội thoại tư vấn tài chính", topic: "Hội thoại hàng ngày", reviewed: 110, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-015", title: "Đánh giá lệnh điều khiển thiết bị IoT trong nhà", topic: "Công nghệ thông tin", reviewed: 50, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-016", title: "Thẩm định truyện đọc phát triển trí tuệ trẻ em", topic: "Giáo dục", reviewed: 20, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-017", title: "Rà soát kịch bản hỏi đáp dịch vụ khách sạn", topic: "Hội thoại hàng ngày", reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-018", title: "Kiểm duyệt tài liệu về an ninh mạng và bảo mật", topic: "Công nghệ thông tin", reviewed: 125, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-019", title: "Đánh giá bài giảng môn Lịch Sử phổ thông", topic: "Giáo dục", reviewed: 45, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-020", title: "Thẩm định giao tiếp tại sân bay và ga tàu", topic: "Hội thoại hàng ngày", reviewed: 105, status: "Hoàn thành", statusType: "completed" }
];

const TASK_STORAGE_KEY = "task_manager_custom_dataset_v1";

export default function TaskManagerCustomPage() {
  const [tasks, setTasks] = useState(() => {
    const saved = localStorage.getItem(TASK_STORAGE_KEY);
    if (saved) {
      try { 
        const parsed = JSON.parse(saved);
        return parsed.map(t => ({
          ...t,
          topic: t.topic === "IT/Technology" ? "Công nghệ thông tin" :
                 t.topic === "Education" ? "Giáo dục" :
                 t.topic === "Daily Life" ? "Hội thoại hàng ngày" : t.topic
        }));
      } catch (e) { console.error(e); }
    }
    return INITIAL_REVIEWER_TASKS;
  });

  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [topicFilter, setTopicFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [taskToDelete, setTaskToDelete] = useState(null);
  const [toast, setToast] = useState({ show: false, message: "" });

  const [formData, setFormData] = useState({
    title: "",
    topic: "Công nghệ thông tin"
  });

  const pageSize = 10;

  useEffect(() => {
    localStorage.setItem(TASK_STORAGE_KEY, JSON.stringify(tasks));
  }, [tasks]);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => setToast((prev) => ({ ...prev, show: false })), 3500);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  const handleSearch = () => {
    setSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const handleOpenAddModal = () => {
    setEditingTask(null);
    setFormData({
      title: "",
      topic: "Công nghệ thông tin"
    });
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (task) => {
    setEditingTask(task);
    setFormData({
      title: task.title,
      topic: task.topic
    });
    setIsModalOpen(true);
  };

  const handleSubmitForm = (e) => {
    e.preventDefault();
    if (!formData.title) return;

    if (editingTask) {
      setTasks((prev) =>
        prev.map((t) =>
          t.id === editingTask.id
            ? {
                ...t,
                title: formData.title,
                topic: formData.topic
              }
            : t
        )
      );
      showNotification(`Đã cập nhật nhiệm vụ "${formData.title}"!`);
    } else {
      const created = {
        id: `TSK-00${tasks.length + 1}`,
        title: formData.title,
        topic: formData.topic,
        reviewed: 0,
        status: "Chưa bắt đầu",
        statusType: "pending",
      };
      setTasks((prev) => [...prev, created]);
      showNotification(`Đã tạo thành công nhiệm vụ "${formData.title}"!`);
    }

    setIsModalOpen(false);
  };

  const handleConfirmDelete = () => {
    if (!taskToDelete) return;
    setTasks((prev) => prev.filter((t) => t.id !== taskToDelete.id));
    showNotification(`Đã xóa thành công nhiệm vụ "${taskToDelete.title}"!`);
    setTaskToDelete(null);
  };

  const filteredTasks = useMemo(() => {
    return tasks.filter((t) => {
      const matchSearch = t.title.toLowerCase().includes(searchTerm.toLowerCase()) || 
                          t.id.toLowerCase().includes(searchTerm.toLowerCase());
      const matchTopic = topicFilter === "all" || t.topic === topicFilter;
      return matchSearch && matchTopic;
    });
  }, [tasks, searchTerm, topicFilter]);

  const totalPages = Math.ceil(filteredTasks.length / pageSize) || 1;
  const paginatedTasks = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredTasks.slice(start, start + pageSize);
  }, [filteredTasks, currentPage, pageSize]);

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
      {/* Toast Notification */}
      <div 
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-3.5 py-2 rounded-xl shadow-xl border border-[${SUCCESS}]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-4 h-4 shrink-0" style={{ color: SUCCESS }} />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          type="button"
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
              <FolderKanban className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
              Nhiệm vụ Reviewer
            </h2>
            <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
              Quản lý danh sách nhiệm vụ kiểm duyệt cho các Reviewer
            </p>
          </div>

          <button
            onClick={handleOpenAddModal}
            className="px-3 py-1.5 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer shadow-xs shrink-0 hover:opacity-90"
            style={{ backgroundColor: TASK_MANAGER_ACCENT }}
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Tạo nhiệm vụ</span>
          </button>
        </div>

        {/* Filter Bar */}
        <div className="rounded-xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div className="relative w-full flex items-center rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none z-10 text-gray-400 dark:text-gray-500" />
              <input
                type="text"
                placeholder="Tìm kiếm nhiệm vụ..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium outline-none transition-all placeholder:text-gray-400 text-gray-900 dark:text-white"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-all cursor-pointer z-10 shadow-xs"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
            <Filter className="w-3.5 h-3.5 shrink-0 text-gray-500 dark:text-gray-400" />
            <select
              value={topicFilter}
              onChange={(e) => {
                setTopicFilter(e.target.value);
                setCurrentPage(1);
              }}
              className="w-[150px] px-2.5 py-2 border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold outline-none cursor-pointer transition-all font-sans"
            >
              <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả chủ đề</option>
              <option value="Công nghệ thông tin" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
              <option value="Giáo dục" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
              <option value="Hội thoại hàng ngày" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
            </select>
          </div>
        </div>
      </div>

      {/* Khung chứa bảng */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0">
          <div className="min-w-[700px] h-full flex flex-col">
            {/* Header Bảng */}
            <div className="bg-gray-50 dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-gray-500 dark:text-gray-400 border-b border-gray-200 dark:border-gray-800 font-bold flex items-center shrink-0 h-9">
              <div className="px-3 text-center whitespace-nowrap w-[60px]">STT</div>
              <div className="px-4 text-left flex-1">Nhiệm vụ</div>
              <div className="px-3 text-left w-[200px] whitespace-nowrap">Chủ đề</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Thân Bảng */}
            <div className="divide-y divide-gray-200 dark:divide-gray-800 text-[11px] font-medium flex-1 grid grid-rows-10">
              {paginatedTasks.length > 0 ? (
                paginatedTasks.map((task, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  const style = getCatStyle(task.topic);

                  return (
                    <div 
                      key={task.id} 
                      className="hover:bg-gray-50/80 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full bg-white dark:bg-[#1C1D22]"
                    >
                      {/* STT */}
                      <div className="px-3 text-center font-sans whitespace-nowrap w-[60px] text-gray-400 dark:text-gray-500">
                        {stt}
                      </div>

                      {/* Nhiệm vụ */}
                      <div className="px-4 font-bold flex-1 truncate text-gray-900 dark:text-gray-100" title={task.title}>
                        {task.title}
                      </div>

                      {/* Chủ đề Badge */}
                      <div className="px-3 w-[200px] whitespace-nowrap flex items-center">
                        <span
                          className="px-2.5 py-1 rounded-md text-[11px] font-bold border inline-block whitespace-nowrap"
                          style={{
                            backgroundColor: style.bg,
                            color: style.text,
                            borderColor: style.border
                          }}
                        >
                          {task.topic}
                        </span>
                      </div>

                      {/* Thao tác */}
                      <div className="px-4 text-center w-[100px] whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            onClick={() => handleOpenEditModal(task)}
                            className="p-1 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md transition-colors cursor-pointer text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100"
                            title="Sửa nhiệm vụ"
                          >
                            <Edit3 className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => setTaskToDelete(task)}
                            className="p-1 text-gray-500 dark:text-gray-400 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-md hover:text-red-600 dark:hover:text-red-400 transition-colors cursor-pointer"
                            title="Xóa nhiệm vụ"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div className="row-span-10 flex items-center justify-center text-xs font-medium text-gray-400 dark:text-gray-500">
                  Không tìm thấy nhiệm vụ nào phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Pagination */}
        <div className="px-2 py-1.5 border-t shrink-0 bg-white dark:bg-[#1C1D22] border-gray-200 dark:border-gray-800">
          <Pagination 
            currentPage={currentPage} 
            totalPages={totalPages} 
            onPageChange={(p) => setCurrentPage(p)} 
            accent={TASK_MANAGER_ACCENT} 
          />
        </div>
      </div>

      {/* Modal Tạo / Sửa Nhiệm vụ */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setIsModalOpen(false)}>
          <div 
            className="rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-gray-100 dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full" style={{ backgroundColor: TASK_MANAGER_ACCENT }} />
            <form onSubmit={handleSubmitForm} className="p-5 space-y-3">
              <h3 className="text-sm font-bold truncate text-gray-900 dark:text-gray-100">
                {editingTask ? `Chỉnh sửa nhiệm vụ` : "Tạo nhiệm vụ mới"}
              </h3>
              
              <div>
                <label className="block text-[10px] font-bold uppercase mb-1 text-gray-500 dark:text-gray-400">Tên nhiệm vụ</label>
                <input
                  type="text"
                  required
                  value={formData.title}
                  onChange={(e) => setFormData({ ...formData, title: e.target.value })}
                  placeholder="Ví dụ: Kiểm duyệt mô hình trí tuệ nhân tạo thế hệ mới"
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-medium transition-all font-sans truncate focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold uppercase mb-1 text-gray-500 dark:text-gray-400">Chủ đề (Topic)</label>
                <select
                  value={formData.topic}
                  onChange={(e) => setFormData({ ...formData, topic: e.target.value })}
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans truncate cursor-pointer focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                >
                  <option value="Công nghệ thông tin" className="dark:bg-[#1C1D22]">Công nghệ thông tin</option>
                  <option value="Giáo dục" className="dark:bg-[#1C1D22]">Giáo dục</option>
                  <option value="Hội thoại hàng ngày" className="dark:bg-[#1C1D22]">Hội thoại hàng ngày</option>
                </select>
              </div>

              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-colors cursor-pointer"
                  style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                >
                  {editingTask ? "Lưu thay đổi" : "Tạo nhiệm vụ"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Xóa */}
      {taskToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setTaskToDelete(null)}>
          <div 
            className="rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden border animate-in fade-in zoom-in-95 duration-200 bg-white dark:bg-[#1C1D22] border-gray-100 dark:border-gray-800 transition-colors"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Đường kẻ vạch màu DANGER trang trí trên đỉnh */}
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />

            <div className="p-5 text-center space-y-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}20`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">Xác nhận xóa nhiệm vụ</h3>
                <p className="text-[11px] mt-1 line-clamp-2 text-gray-500 dark:text-gray-400">
                  Bạn có chắc muốn xóa nhiệm vụ <span className="font-bold text-gray-900 dark:text-gray-200">"{taskToDelete.title}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  onClick={() => setTaskToDelete(null)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={handleConfirmDelete}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-colors cursor-pointer"
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