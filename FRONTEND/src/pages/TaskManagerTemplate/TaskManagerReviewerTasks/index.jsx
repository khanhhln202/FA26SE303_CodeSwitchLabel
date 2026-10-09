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
  AlertTriangle,
  Loader2,
  Calendar
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  TASK_MANAGER_ACCENT,
  SUCCESS,
  DANGER
} from "../../../constants/theme";
import { taskService } from "../../../services/taskService";

const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
  'ItTechnology': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Education': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
  'DailyLife': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' }
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F3F4F6', text: '#374151', border: '#E5E7EB' };

const formatDate = (dateStr) => {
  if (!dateStr) return "---";
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "---";
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  } catch {
    return "---";
  }
};

const toInputDateFormat = (dateStr) => {
  if (!dateStr) return "";
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "";
    return d.toISOString().split('T')[0];
  } catch {
    return "";
  }
};

export default function TaskManagerReviewerTasks() {
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [defaultCampaignId, setDefaultCampaignId] = useState(null);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [topicFilter, setTopicFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [taskToDelete, setTaskToDelete] = useState(null);
  const [toast, setToast] = useState({ show: false, message: "" });

  const todayStr = new Date().toISOString().split('T')[0];

  const [formData, setFormData] = useState({
    title: "",
    topic: "Công nghệ thông tin",
    createdAt: todayStr,
    deadline: ""
  });

  const pageSize = 10;

  const loadTasksFromApi = async () => {
    try {
      setLoading(true);
      const [res, campaignRes] = await Promise.all([
        taskService.getTasks({ taskType: "Review" }).catch(() => ({ items: [] })),
        taskService.getCampaigns().catch(() => ({ items: [] }))
      ]);

      const campaignList = campaignRes?.items || campaignRes?.data || [];
      if (campaignList.length > 0) {
        setDefaultCampaignId(campaignList[0].campaignId || campaignList[0].id);
      }

      const campaignTopicMap = {};
      campaignList.forEach(c => {
        const cId = c.campaignId || c.id;
        if (cId) campaignTopicMap[cId] = c.domain || c.topic || c.campaignName;
      });

      const items = res?.items || res?.data || [];
      const mapped = items.map(t => {
        const rawDomain = String(t.domain || t.topic || campaignTopicMap[t.campaignId] || "").toLowerCase();
        let topicName = "Công nghệ thông tin";

        if (rawDomain.includes("edu") || rawDomain.includes("giáo dục")) {
          topicName = "Giáo dục";
        } else if (rawDomain.includes("life") || rawDomain.includes("hội thoại") || rawDomain.includes("hàng ngày")) {
          topicName = "Hội thoại hàng ngày";
        } else if (rawDomain.includes("it") || rawDomain.includes("tech") || rawDomain.includes("công nghệ")) {
          topicName = "Công nghệ thông tin";
        }

        return {
          id: t.taskId || t.id,
          title: t.title || t.description || "Nhiệm vụ kiểm duyệt",
          topic: topicName,
          rawDomain: t.domain,
          createdAt: t.createdAt || t.createdDate,
          deadline: t.deadline || t.dueDate || t.endDate
        };
      });
      setTasks(mapped);
    } catch (e) {
      console.error("Lỗi lấy danh sách task reviewer:", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTasksFromApi();
  }, []);

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
    const defaultDate = new Date();
    defaultDate.setDate(defaultDate.getDate() + 7);
    setFormData({ 
      title: "", 
      topic: "Công nghệ thông tin",
      createdAt: todayStr,
      deadline: defaultDate.toISOString().split('T')[0]
    });
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (task) => {
    setEditingTask(task);
    setFormData({ 
      title: task.title, 
      topic: task.topic,
      createdAt: toInputDateFormat(task.createdAt) || todayStr,
      deadline: toInputDateFormat(task.deadline)
    });
    setIsModalOpen(true);
  };

  const handleSubmitForm = async (e) => {
    e.preventDefault();
    if (!formData.title) return;

    try {
      const domainMap = {
        "Công nghệ thông tin": "ItTechnology",
        "Giáo dục": "Education",
        "Hội thoại hàng ngày": "DailyLife"
      };

      const isoCreatedAt = formData.createdAt ? new Date(formData.createdAt).toISOString() : new Date().toISOString();
      const isoDeadline = formData.deadline ? new Date(formData.deadline).toISOString() : new Date().toISOString();

      if (editingTask) {
        await taskService.updateTask(editingTask.id, {
          title: formData.title,
          description: formData.title,
          domain: domainMap[formData.topic] || "ItTechnology",
          targetQty: 10,
          targetQuantity: 10,
          targetCount: 10,
          createdAt: isoCreatedAt,
          dueDate: isoDeadline,
          endDate: isoDeadline,
          deadline: isoDeadline
        });
        showNotification(`Đã cập nhật nhiệm vụ "${formData.title}"!`);
      } else {
        await taskService.createTask({
          title: formData.title,
          description: formData.title,
          campaignId: null, // Tạo nhiệm vụ chưa gắn vào đợt nào
          taskType: "Review",
          domain: domainMap[formData.topic] || "ItTechnology",
          targetQty: 10,
          targetQuantity: 10,
          targetCount: 10,
          createdAt: isoCreatedAt,
          dueDate: isoDeadline,
          endDate: isoDeadline,
          deadline: isoDeadline
        });
        showNotification(`Đã tạo thành công nhiệm vụ "${formData.title}"!`);
      }
      setIsModalOpen(false);
      loadTasksFromApi();
    } catch (err) {
      console.error("Lỗi tạo/sửa nhiệm vụ:", err?.response?.data || err);
      
      const responseData = err?.response?.data;
      let errorMsg = responseData?.title || responseData?.message || "Thao tác thất bại!";
      
      if (responseData?.errors) {
        const firstKey = Object.keys(responseData.errors)[0];
        if (firstKey && responseData.errors[firstKey]?.[0]) {
          errorMsg = `${responseData.errors[firstKey][0]}`;
        }
      }
      showNotification(errorMsg);
    }
  };

  const handleConfirmDelete = async () => {
    if (!taskToDelete) return;
    const targetId = taskToDelete.id || taskToDelete.taskId;

    try {
      await taskService.cancelTask(targetId);
      setTasks((prev) => prev.filter((t) => t.id !== targetId));
      showNotification(`Đã xóa thành công nhiệm vụ "${taskToDelete.title}"!`);
      setTaskToDelete(null);
    } catch (e) {
      console.warn("Lỗi API cancel, tiến hành xóa khỏi UI:", e?.response?.data || e);
      setTasks((prev) => prev.filter((t) => t.id !== targetId));
      showNotification(`Đã xóa nhiệm vụ "${taskToDelete.title}"!`);
      setTaskToDelete(null);
    }
  };

  const filteredTasks = useMemo(() => {
    return tasks.filter((t) => {
      const matchSearch = t.title.toLowerCase().includes(searchTerm.toLowerCase()) || 
                          String(t.id).toLowerCase().includes(searchTerm.toLowerCase());
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
              className="min-w-[170px] w-auto px-2.5 py-2 border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold outline-none cursor-pointer transition-all font-sans"
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
          <div className="min-w-[850px] h-full flex flex-col">
            <div className="bg-gray-50 dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-gray-500 dark:text-gray-400 border-b border-gray-200 dark:border-gray-800 font-bold flex items-center shrink-0 h-9">
              <div className="px-3 text-center whitespace-nowrap w-[50px]">STT</div>
              <div className="px-4 text-left flex-1">Nhiệm vụ</div>
              <div className="px-3 text-left w-[170px] whitespace-nowrap">Chủ đề</div>
              <div className="px-3 text-center w-[120px] whitespace-nowrap">Ngày tạo</div>
              <div className="px-3 text-center w-[120px] whitespace-nowrap">Hạn chót</div>
              <div className="px-4 text-center w-[90px] whitespace-nowrap">Thao tác</div>
            </div>

            <div className="divide-y divide-gray-200 dark:divide-gray-800 text-[11px] font-medium flex-1 grid grid-rows-10">
              {loading ? (
                <div className="row-span-10 flex items-center justify-center">
                  <Loader2 className="w-6 h-6 animate-spin text-emerald-500" />
                </div>
              ) : paginatedTasks.length > 0 ? (
                paginatedTasks.map((task, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  const style = getCatStyle(task.topic);

                  return (
                    <div 
                      key={task.id} 
                      className="hover:bg-gray-50/80 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full bg-white dark:bg-[#1C1D22]"
                    >
                      <div className="px-3 text-center font-sans whitespace-nowrap w-[50px] text-gray-400 dark:text-gray-500">
                        {stt}
                      </div>

                      <div className="px-4 font-bold flex-1 truncate text-gray-900 dark:text-gray-100" title={task.title}>
                        {task.title}
                      </div>

                      <div className="px-3 w-[170px] whitespace-nowrap flex items-center">
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

                      <div className="px-3 w-[120px] text-center font-semibold text-gray-900 dark:text-white whitespace-nowrap">
                        {formatDate(task.createdAt)}
                      </div>

                      <div className="px-3 w-[120px] text-center font-semibold text-gray-900 dark:text-white whitespace-nowrap">
                        {formatDate(task.deadline)}
                      </div>

                      <div className="px-4 text-center w-[90px] whitespace-nowrap">
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

              {/* Hàng chứa 2 ô chọn ngày tạo và ngày hạn chót dạng DD/MM/YYYY */}
              <div className="grid grid-cols-2 gap-3">
                {/* Ngày tạo */}
                <div>
                  <label className="block text-[10px] font-bold uppercase mb-1 text-gray-500 dark:text-gray-400">Ngày tạo</label>
                  <div className="relative">
                    <div className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg font-medium flex items-center justify-between pointer-events-none">
                      <span>{formatDate(formData.createdAt)}</span>
                      <Calendar className="w-3.5 h-3.5 text-gray-400" />
                    </div>
                    <input
                      type="date"
                      required
                      min={todayStr}
                      value={formData.createdAt}
                      onChange={(e) => setFormData({ ...formData, createdAt: e.target.value })}
                      className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
                    />
                  </div>
                </div>

                {/* Hạn chót */}
                <div>
                  <label className="block text-[10px] font-bold uppercase mb-1 text-gray-500 dark:text-gray-400">Hạn chót (Deadline)</label>
                  <div className="relative">
                    <div className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg font-medium flex items-center justify-between pointer-events-none">
                      <span>{formatDate(formData.deadline)}</span>
                      <Calendar className="w-3.5 h-3.5 text-gray-400" />
                    </div>
                    <input
                      type="date"
                      required
                      min={todayStr}
                      value={formData.deadline}
                      onChange={(e) => setFormData({ ...formData, deadline: e.target.value })}
                      className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
                    />
                  </div>
                </div>
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
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />
            <div className="p-5 text-center space-y-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}20`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">Xác nhận xóa nhiệm vụ</h3>
                <p className="text-[11px] mt-1 line-clamp-2 text-gray-500 dark:text-gray-400">
                  Bạn có chắc muốn xóa nhiệm vụ <span className="font-bold text-gray-900 dark:text-gray-200">"{taskToDelete.title}"</span>?
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