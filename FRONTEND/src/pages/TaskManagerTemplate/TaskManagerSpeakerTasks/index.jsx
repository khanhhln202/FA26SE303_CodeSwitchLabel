import { useState, useMemo, useEffect } from "react";
import { toast } from "sonner";
import { 
  FolderKanban,
  Plus, 
  Search, 
  Filter, 
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

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };

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

export default function TaskManagerSpeakerTasks() {
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [topicFilter, setTopicFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [taskToDelete, setTaskToDelete] = useState(null);

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
      const [taskRes, campaignRes] = await Promise.all([
        taskService.getTasks({ taskType: "Recording" }).catch(() => ({ items: [] })),
        taskService.getCampaigns().catch(() => ({ items: [] }))
      ]);

      const campaignList = campaignRes?.items || campaignRes?.data || [];
      const campaignTopicMap = {};
      campaignList.forEach(c => {
        const cId = c.campaignId || c.id;
        if (cId) campaignTopicMap[cId] = c.domain || c.topic || c.campaignName;
      });

      const items = taskRes?.items || taskRes?.data || [];
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
          title: t.description || t.title || "Nhiệm vụ thu âm",
          topic: topicName,
          rawDomain: t.domain,
          createdAt: t.createdAt || t.createdDate,
          deadline: t.deadline || t.dueDate || t.endDate
        };
      });
      setTasks(mapped);
    } catch (e) {
      console.error("Lỗi lấy danh sách task speaker:", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTasksFromApi();
  }, []);

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
    if (!formData.title.trim()) return;

    try {
      const selectedDate = new Date(formData.deadline);
      selectedDate.setHours(23, 59, 59, 999);
      const isoDeadline = selectedDate.toISOString();

      const domainMap = {
        "Công nghệ thông tin": "ItTechnology",
        "Giáo dục": "Education",
        "Hội thoại hàng ngày": "DailyLife"
      };

      const selectedDomainCode = domainMap[formData.topic] || "ItTechnology";

      if (editingTask) {
        await taskService.updateTask(editingTask.id, {
          taskId: Number(editingTask.id),
          title: formData.title,
          description: formData.title,
          targetQty: 10,
          deadline: isoDeadline
        });
        toast.success(`Đã cập nhật nhiệm vụ "${formData.title}"!`);
      } else {
        // 1. Tạo Task
        const newCreatedTask = await taskService.createTask({
          title: formData.title,
          description: formData.title,
          taskType: "Recording",
          domain: selectedDomainCode,
          topic: formData.topic,
          campaignId: null,
          targetQty: 10,
          deadline: isoDeadline
        });

        const createdTaskId = newCreatedTask?.summary?.taskId || newCreatedTask?.taskId || newCreatedTask?.id;

        // 2. Tự động lấp các câu đúng chủ đề (domain) được chọn vào Task
        if (createdTaskId) {
          await taskService.addTaskItems?.(createdTaskId, {
            autoFill: {
              count: 10,
              domain: selectedDomainCode
            }
          }).catch(err => console.warn("Lỗi autoFill domain:", err));
        }

        toast.success(`Đã tạo thành công nhiệm vụ "${formData.title}"!`);
      }
      setIsModalOpen(false);
      await loadTasksFromApi();
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
      toast.error(errorMsg);
    }
  };

  const handleConfirmDelete = async () => {
    if (!taskToDelete) return;
    const targetId = Number(taskToDelete.id || taskToDelete.taskId);

    try {
      // 1. Thử gọi API xóa cứng trước (dành cho task rảnh)
      await taskService.deleteTask(targetId);
      
      // Nếu BE xóa thành công trong DB -> mới cập nhật lại UI
      setTasks((prev) => prev.filter((t) => t.id !== targetId));
      toast.success(`Đã xóa thành công nhiệm vụ "${taskToDelete.title}"!`);
      setTaskToDelete(null);
    } catch (deleteError) {
      // 2. Nếu không xóa cứng được (do task đã có dữ liệu/phân công), chuyển sang gọi API Hủy (cancelTask)
      try {
        await taskService.cancelTask(targetId);
        
        // Hủy thành công -> tải lại danh sách từ BE để lấy trạng thái mới nhất
        toast.success(`Đã hủy thành công nhiệm vụ "${taskToDelete.title}"!`);
        setTaskToDelete(null);
        await loadTasksFromApi();
      } catch (cancelError) {
        // 3. Nếu cả 2 API đều thất bại -> Báo lỗi chính xác từ BE và KHÔNG xóa khỏi UI
        console.error("Lỗi xóa/hủy task từ BE:", cancelError?.response?.data || cancelError);
        
        const responseData = cancelError?.response?.data || deleteError?.response?.data;
        const errorMsg = responseData?.title || responseData?.message || "Không thể xóa/hủy nhiệm vụ này do ràng buộc dữ liệu!";
        
        toast.error(errorMsg);
        setTaskToDelete(null);
      }
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
      
      {/* Filter Bar + Nút Tạo Nhiệm Vụ */}
      <div className="shrink-0 space-y-2">
        <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 p-2.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-2.5 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div 
              className="relative w-full flex items-center bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl border border-[#E5E2D8] dark:border-gray-700 transition-all p-1"
              onFocus={(e) => {
                e.currentTarget.style.borderColor = TASK_MANAGER_ACCENT;
                e.currentTarget.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = '';
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA3] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm nhiệm vụ..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg type-button hover:opacity-90 transition-all cursor-pointer z-10"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2.5 w-full md:w-auto shrink-0 flex-wrap md:flex-nowrap justify-end">
            <div className="flex items-center gap-2">
              <Filter className="w-4 h-4 text-[#6E7078] dark:text-gray-400 shrink-0" />
              <select
                value={topicFilter}
                onChange={(e) => {
                  setTopicFilter(e.target.value);
                  setCurrentPage(1);
                }}
                className="min-w-[150px] w-auto px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = TASK_MANAGER_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả chủ đề</option>
                <option value="Công nghệ thông tin" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                <option value="Giáo dục" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                <option value="Hội thoại hàng ngày" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
              </select>
            </div>

            <button
              onClick={handleOpenAddModal}
              style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Plus className="w-4 h-4" />
              <span>Tạo nhiệm vụ</span>
            </button>
          </div>
        </div>
      </div>

      {/* Khung chứa bảng */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0 overflow-auto">
          <div className="min-w-[850px] h-full flex flex-col">
            <div className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center shrink-0 h-8">
              <div className="px-3 text-center whitespace-nowrap w-[50px]">STT</div>
              <div className="px-4 text-left flex-1">Nhiệm vụ</div>
              <div className="px-3 text-left w-[170px] whitespace-nowrap">Chủ đề</div>
              <div className="px-3 text-center w-[120px] whitespace-nowrap">Ngày tạo</div>
              <div className="px-3 text-center w-[120px] whitespace-nowrap">Hạn chót</div>
              <div className="px-4 text-center w-[90px] whitespace-nowrap">Thao tác</div>
            </div>

            <div className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui flex-1 grid grid-rows-10">
              {loading ? (
                <div className="row-span-10 flex items-center justify-center">
                  <Loader2 className="w-6 h-6 animate-spin text-[#16A34A]" />
                </div>
              ) : paginatedTasks.length > 0 ? (
                paginatedTasks.map((task, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  const style = getCatStyle(task.topic);

                  return (
                    <div 
                      key={task.id} 
                      className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full bg-white dark:bg-[#1C1D22]"
                    >
                      <div className="px-3 text-center font-sans whitespace-nowrap w-[50px] text-[#9A9CA3] dark:text-gray-500">
                        {stt}
                      </div>

                      <div className="px-4 font-bold flex-1 truncate text-[#16171C] dark:text-gray-100" title={task.title}>
                        {task.title}
                      </div>

                      <div className="px-3 w-[170px] whitespace-nowrap flex items-center">
                        <span
                          className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                          style={{
                            backgroundColor: style.bg,
                            color: style.text,
                            borderColor: style.border
                          }}
                        >
                          {task.topic}
                        </span>
                      </div>

                      <div className="px-3 w-[120px] text-center font-semibold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                        {formatDate(task.createdAt)}
                      </div>

                      <div className="px-3 w-[120px] text-center font-semibold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                        {formatDate(task.deadline)}
                      </div>

                      <div className="px-4 text-center w-[90px] whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            onClick={() => handleOpenEditModal(task)}
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                            title="Sửa nhiệm vụ"
                          >
                            <Edit3 className="w-3.5 h-3.5" />
                          </button>
                          <button
                            onClick={() => setTaskToDelete(task)}
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
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
                <div className="row-span-10 flex items-center justify-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                  Không tìm thấy nhiệm vụ nào phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0 bg-white dark:bg-[#1C1D22]">
          <Pagination 
            currentPage={currentPage} 
            totalPages={totalPages} 
            onPageChange={(p) => setCurrentPage(p)} 
            accent={TASK_MANAGER_ACCENT} 
            variant="soft"
          />
        </div>
      </div>

      {/* Modal Tạo / Sửa Nhiệm vụ */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setIsModalOpen(false)}>
          <div 
            className="rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full" style={{ backgroundColor: TASK_MANAGER_ACCENT }} />
            <form onSubmit={handleSubmitForm} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section truncate text-[#16171C] dark:text-gray-100">
                  {editingTask ? `Chỉnh sửa nhiệm vụ` : "Tạo nhiệm vụ mới"}
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>
              
              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Mô tả nhiệm vụ</label>
                <input
                  type="text"
                  required
                  value={formData.title}
                  onChange={(e) => setFormData({ ...formData, title: e.target.value })}
                  placeholder="Ví dụ: Thu âm mô hình trí tuệ nhân tạo thế hệ mới"
                  className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none transition-all font-sans placeholder:text-[#9A9CA3]"
                  onFocus={(e) => {
                    e.target.style.borderColor = TASK_MANAGER_ACCENT;
                    e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = '';
                    e.target.style.boxShadow = 'none';
                  }}
                />
              </div>

              {/* Ô CHỌN CHỦ ĐỀ: Khóa khi sửa nhiệm vụ */}
              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Chủ đề (Topic)
                </label>
                <select
                  disabled={!!editingTask}
                  value={formData.topic}
                  onChange={(e) => setFormData({ ...formData, topic: e.target.value })}
                  className={`w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans ${
                    editingTask
                      ? "bg-[#F7F5EF] dark:bg-[#18191D] cursor-not-allowed opacity-80 appearance-none"
                      : "bg-white dark:bg-[#25272E] cursor-pointer"
                  }`}
                  onFocus={(e) => {
                    if (!editingTask) {
                      e.target.style.borderColor = TASK_MANAGER_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                    }
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = '';
                    e.target.style.boxShadow = 'none';
                  }}
                >
                  <option value="Công nghệ thông tin" className="dark:bg-[#1C1D22]">Công nghệ thông tin</option>
                  <option value="Giáo dục" className="dark:bg-[#1C1D22]">Giáo dục</option>
                  <option value="Hội thoại hàng ngày" className="dark:bg-[#1C1D22]">Hội thoại hàng ngày</option>
                </select>
              </div>

              {/* Hàng chứa 2 ô chọn ngày tạo và ngày hạn chót */}
              <div className="grid grid-cols-2 gap-3.5">
                {/* Ngày tạo */}
                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Ngày tạo</label>
                  <div className="relative">
                    <div className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] text-[#6E7078] dark:text-gray-400 rounded-xl font-medium flex items-center justify-between pointer-events-none font-sans">
                      <span>{formatDate(formData.createdAt)}</span>
                      <Calendar className="w-4 h-4 text-[#9A9CA3]" />
                    </div>
                    <input
                      type="date"
                      disabled
                      value={formData.createdAt}
                      className="absolute inset-0 opacity-0 cursor-not-allowed w-full h-full"
                    />
                  </div>
                </div>

                {/* Hạn chót */}
                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Hạn chót (Deadline)</label>
                  <div className="relative">
                    <div 
                      className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl font-medium flex items-center justify-between pointer-events-none font-sans transition-all"
                    >
                      <span>{formatDate(formData.deadline)}</span>
                      <Calendar className="w-4 h-4 text-[#9A9CA3]" />
                    </div>
                    <input
                      type="date"
                      required
                      min={todayStr}
                      value={formData.deadline}
                      onChange={(e) => setFormData({ ...formData, deadline: e.target.value })}
                      className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
                      onFocus={(e) => {
                        const parent = e.target.previousElementSibling;
                        if (parent) {
                          parent.style.borderColor = TASK_MANAGER_ACCENT;
                          parent.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                        }
                      }}
                      onBlur={(e) => {
                        const parent = e.target.previousElementSibling;
                        if (parent) {
                          parent.style.borderColor = '';
                          parent.style.boxShadow = 'none';
                        }
                      }}
                    />
                  </div>
                </div>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
                  style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                >
                  {editingTask ? "Lưu thay đổi" : "Tạo nhiệm vụ"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Xóa/Hủy */}
      {taskToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setTaskToDelete(null)}>
          <div 
            className="rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border animate-in fade-in zoom-in-95 duration-200 bg-white dark:bg-[#1C1D22] border-[#E5E2D8] dark:border-gray-800 transition-colors"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />
            <div className="p-6 text-center space-y-3.5">
              <div className="w-11 h-11 rounded-full flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}1A`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận hủy nhiệm vụ</h3>
                <p className="type-ui mt-1 line-clamp-2 text-[#6E7078] dark:text-gray-400">
                  Bạn có chắc muốn hủy nhiệm vụ <span className="font-bold text-[#16171C] dark:text-gray-200">"{taskToDelete.title}"</span>?
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  onClick={() => setTaskToDelete(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={handleConfirmDelete}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                >
                  Xác nhận hủy
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}