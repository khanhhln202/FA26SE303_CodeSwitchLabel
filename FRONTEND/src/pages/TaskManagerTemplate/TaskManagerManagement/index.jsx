import React, { useState, useEffect, useMemo } from "react";
import {
  ListTodo,
  Filter,
  CheckCircle2,
  X,
  Plus,
  Layers,
  Eye,
  Trash2,
  Edit3,
  AlertTriangle,
  ChevronDown,
  Users,
  Search,
  Clock,
  UserCheck,
  Calendar,
  Target,
  Tag
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import { 
  TASK_MANAGER_ACCENT, 
  SUCCESS, 
  DANGER,
  SPEAKER_ACCENT, 
  REVIEWER_ACCENT,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT
} from "../../../constants/theme";

// Helper format ngày
const formatDateToVN = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("/")) return dateStr;
  const [year, month, day] = dateStr.split("-");
  return `${day}/${month}/${year}`;
};

const formatDateToISO = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("-")) return dateStr;
  const [day, month, year] = dateStr.split("/");
  return `${year}-${month}-${day}`;
};

// Component Custom Date Picker hiển thị chuẩn DD/MM/YYYY
const VNFormatDatePicker = ({ label, value, onChange, minDateIso }) => {
  const handleInputChange = (e) => {
    let input = e.target.value.replace(/\D/g, "");
    if (input.length > 8) input = input.slice(0, 8);

    if (input.length >= 5) {
      input = `${input.slice(0, 2)}/${input.slice(2, 4)}/${input.slice(4)}`;
    } else if (input.length >= 3) {
      input = `${input.slice(0, 2)}/${input.slice(2)}`;
    }

    if (input.length === 10) {
      const iso = formatDateToISO(input);
      onChange(iso);
    } else {
      onChange(input);
    }
  };

  const handleNativePickerChange = (e) => {
    const selectedIso = e.target.value;
    if (selectedIso) {
      onChange(selectedIso);
    }
  };

  return (
    <div className="space-y-1">
      <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase flex items-center gap-1">
        <Calendar className="w-3 h-3" /> {label}
      </label>

      <div className="relative flex items-center">
        <input
          type="text"
          placeholder="dd/mm/yyyy"
          value={value.includes("-") ? formatDateToVN(value) : value}
          onChange={handleInputChange}
          maxLength={10}
          className="w-full pl-3 pr-8 py-1.5 text-xs bg-gray-50 dark:bg-[#25272E] border-gray-200 dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-lg outline-none font-sans text-gray-900 dark:text-gray-100 transition-all font-medium border"
        />

        <div className="absolute right-2 flex items-center justify-center cursor-pointer">
          <Calendar className="w-3.5 h-3.5 text-gray-400 dark:text-gray-400 pointer-events-none" />
          <input
            type="date"
            min={minDateIso}
            value={value.includes("-") ? value : formatDateToISO(value)}
            onChange={handleNativePickerChange}
            className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
          />
        </div>
      </div>
    </div>
  );
};

// Dữ liệu mẫu & Khóa Storage
const ADMIN_USERS_STORAGE_KEY = "admin_users_list_v2";
const ASSIGN_TASKS_STORAGE_KEY = "task_manager_assign_v8";
const BATCH_LIST_STORAGE_KEY = "task_manager_management_batches_v4";
const BATCH_ASSIGNMENT_STORAGE_KEY = "task_manager_management_batch_assignments_v4";

const DEFAULT_USERS = [
  { id: "USR-001", name: "Quản Lý", email: "manager@fpt.edu.vn", role: "Task Manager", status: "Active", createdAt: "15/01/2026" },
  { id: "USR-002", name: "Trần Minh Tâm", email: "tam.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "20/01/2026" },
  { id: "USR-005", name: "Nguyễn Văn Anh", email: "anh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "06/02/2026" },
  { id: "USR-006", name: "Trần Thị Bình", email: "binh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "07/02/2026" },
  { id: "USR-007", name: "Lê Văn Cường", email: "cuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "08/02/2026" },
  { id: "USR-008", name: "Phạm Thị Dung", email: "dung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "09/02/2026" },
  { id: "USR-009", name: "Hoàng Văn Em", email: "em.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "10/02/2026" },
  { id: "USR-010", name: "Vũ Thị Phương", email: "phuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "11/02/2026" },
  { id: "USR-011", name: "Đặng Văn Giang", email: "giang.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "12/02/2026" },
  { id: "USR-012", name: "Bùi Thị Hải", email: "hai.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "13/02/2026" },
  { id: "USR-013", name: "Đinh Văn Hùng", email: "hung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "14/02/2026" },
  { id: "USR-003", name: "Phạm Thu Thảo", email: "thao.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "01/02/2026" },
  { id: "USR-004", name: "Lê Hoàng Nam", email: "nam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "05/02/2026" },
  { id: "USR-014", name: "Đỗ Thị Khánh", email: "khanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "15/02/2026" },
  { id: "USR-015", name: "Hoàng Văn Lâm", email: "lam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "16/02/2026" },
  { id: "USR-016", name: "Ngô Thị Minh", email: "minh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "17/02/2026" },
  { id: "USR-017", name: "Dương Văn Nghĩa", email: "nghia.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "18/02/2026" },
  { id: "USR-018", name: "Lý Thị Oanh", email: "oanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "19/02/2026" },
  { id: "USR-019", name: "Võ Văn Phong", email: "phong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "20/02/2026" },
  { id: "USR-020", name: "Đoàn Thị Quỳnh", email: "quynh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "21/02/2026" },
  { id: "USR-021", name: "Trịnh Văn Rồng", email: "rong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "22/02/2026" },
];

const FULL_SPEAKER_TASKS = [
  { id: "TSK-001", title: "Nhiệm vụ ghi âm thuật ngữ công nghệ", topic: "Công nghệ thông tin", target: 100, reviewed: 65, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-002", title: "Nhiệm vụ ghi âm hội thoại giáo dục phổ thông", topic: "Giáo dục", target: 80, reviewed: 80, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-003", title: "Thu âm giao tiếp đời sống hàng ngày", topic: "Hội thoại hàng ngày", target: 150, reviewed: 135, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-004", title: "Đọc ngữ liệu lệnh thoại nhà thông minh", topic: "Công nghệ thông tin", target: 120, reviewed: 40, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-005", title: "Thu âm kịch bản hỏi đáp y tế cơ bản", topic: "Hội thoại hàng ngày", target: 90, reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-006", title: "Ghi âm bài giảng toán học trực tuyến", topic: "Giáo dục", target: 110, reviewed: 110, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-007", title: "Đọc tin tức kinh tế và thị trường tài chính", topic: "Hội thoại hàng ngày", target: 70, reviewed: 20, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-008", title: "Thu âm dữ liệu hội thoại bán hàng tự động", topic: "Công nghệ thông tin", target: 130, reviewed: 130, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-009", title: "Ghi âm phát âm bảng chữ cái Tiếng Việt cho trẻ em", topic: "Giáo dục", target: 60, reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-010", title: "Đọc tài liệu hướng dẫn lập trình Python", topic: "Công nghệ thông tin", target: 100, reviewed: 85, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-011", title: "Thu âm mẫu hội thoại đặt xe trực tuyến", topic: "Hội thoại hàng ngày", target: 85, reviewed: 85, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-012", title: "Đọc thuật ngữ trí tuệ nhân tạo nâng cao", topic: "Công nghệ thông tin", target: 140, reviewed: 30, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-013", title: "Ghi âm bài luyện nói Tiếng Anh giao tiếp", topic: "Giáo dục", target: 95, reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-014", title: "Thu âm các đoạn hội thoại tư vấn tài chính", topic: "Hội thoại hàng ngày", target: 110, reviewed: 110, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-015", title: "Đọc lệnh điều khiển thiết bị IoT trong nhà", topic: "Công nghệ thông tin", target: 75, reviewed: 50, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-016", title: "Ghi âm truyện đọc phát triển trí tuệ trẻ em", topic: "Giáo dục", target: 100, reviewed: 20, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-017", title: "Thu âm kịch bản hỏi đáp dịch vụ khách sạn", topic: "Hội thoại hàng ngày", target: 90, reviewed: 0, status: "Chưa bắt đầu", statusType: "pending" },
  { id: "TSK-018", title: "Đọc tài liệu về an ninh mạng và bảo mật", topic: "Công nghệ thông tin", target: 125, reviewed: 125, status: "Hoàn thành", statusType: "completed" },
  { id: "TSK-019", title: "Ghi âm bài giảng môn Lịch Sử phổ thông", topic: "Giáo dục", target: 80, reviewed: 45, status: "Đang thực hiện", statusType: "in-progress" },
  { id: "TSK-020", title: "Thu âm giao tiếp tại sân bay và ga tàu", topic: "Hội thoại hàng ngày", target: 105, reviewed: 105, status: "Hoàn thành", statusType: "completed" }
];

export default function TaskManagerManagement() {
  const [searchQuery, setSearchQuery] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [toast, setToast] = useState({ show: false, message: "" });

  const todayIso = useMemo(() => new Date().toISOString().split("T")[0], []);

  // 1. Trạng thái Modal Tạo/Cập nhật Đợt
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState("CREATE"); // "CREATE" hoặc "UPDATE"
  const [modalTargetBatchId, setModalTargetBatchId] = useState(null);

  // Form mới cho Modal Đợt
  const [batchFormData, setBatchFormData] = useState({
    name: "",
    target: 50,
    speakerCount: 1,
    reviewerCount: 1,
    topic: "Công nghệ thông tin",
    startDate: todayIso,
    endDate: todayIso,
    assignedSpeakers: [],
    assignedReviewers: []
  });

  // 2. Trạng thái Modal Phân công Nhiệm vụ
  const [isAssignModalOpen, setIsAssignModalOpen] = useState(false);
  const [editingAssignment, setEditingAssignment] = useState(null);
  const [allAdminUsers, setAllAdminUsers] = useState([]);
  const [speakerTasksList, setSpeakerTasksList] = useState([]);
  const [reviewerTasksList, setReviewerTasksList] = useState([]);

  const [assignFormData, setAssignFormData] = useState({
    selectedBatchId: "",
    selectedUniqueKey: "",
    assignedUsers: []
  });

  // Modal Xem danh sách người thực hiện & Modal Xóa Đợt
  const [viewingAssignedTask, setViewingAssignedTask] = useState(null);
  const [deletingBatch, setDeletingBatch] = useState(null);

  // 3. Modal SỬA / XÓA NHIỆM VỤ TRONG BẢNG (MỚI THÊM)
  const [isEditTaskModalOpen, setIsEditTaskModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [editTaskFormData, setEditTaskFormData] = useState({
    title: "",
    role: "Speaker",
    assignedUsers: []
  });
  const [deletingTask, setDeletingTask] = useState(null);

  // Danh sách Đợt
  const [batches, setBatches] = useState(() => {
    const saved = localStorage.getItem(BATCH_LIST_STORAGE_KEY);
    if (saved) {
      try { return JSON.parse(saved); } catch (e) { console.error(e); }
    }
    return [
      { id: "BATCH-01", name: "Đợt 1", target: 100, speakerCount: 5, reviewerCount: 2, topic: "Công nghệ thông tin", startDate: "01/03/2026", endDate: "15/03/2026", assignedSpeakers: [], assignedReviewers: [] },
      { id: "BATCH-02", name: "Đợt 2", target: 80, speakerCount: 4, reviewerCount: 2, topic: "Giáo dục", startDate: "01/03/2026", endDate: "15/03/2026", assignedSpeakers: [], assignedReviewers: [] },
      { id: "BATCH-03", name: "Đợt 3", target: 150, speakerCount: 6, reviewerCount: 3, topic: "Hội thoại hàng ngày", startDate: "01/03/2026", endDate: "15/03/2026", assignedSpeakers: [], assignedReviewers: [] }
    ];
  });

  const [selectedBatchId, setSelectedBatchId] = useState(() => {
    const saved = localStorage.getItem(BATCH_LIST_STORAGE_KEY);
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (parsed && parsed.length > 0) return parsed[0].id;
      } catch (e) {}
    }
    return "BATCH-01";
  });

  // Danh sách nhiệm vụ đã phân công
  const [assignTasks, setAssignTasks] = useState([]);

  // Mapping phân công Đợt cho các Nhiệm vụ
  const [batchMapping, setBatchMapping] = useState(() => {
    const saved = localStorage.getItem(BATCH_ASSIGNMENT_STORAGE_KEY);
    if (saved) {
      try { return JSON.parse(saved); } catch (e) { console.error(e); }
    }
    return {};
  });

  // Tải danh sách Admin Users & Task Lists
  const reloadAdminUsers = () => {
    const savedUsers = localStorage.getItem(ADMIN_USERS_STORAGE_KEY);
    if (savedUsers) {
      try { setAllAdminUsers(JSON.parse(savedUsers)); } catch (e) { setAllAdminUsers(DEFAULT_USERS); }
    } else {
      setAllAdminUsers(DEFAULT_USERS);
    }
  };

  const reloadTaskLists = () => {
    const savedSpeaker = localStorage.getItem("speaker_tasks_v1") || localStorage.getItem("task_manager_dataset_v3");
    if (savedSpeaker) {
      try { setSpeakerTasksList(JSON.parse(savedSpeaker)); } catch (e) { setSpeakerTasksList(FULL_SPEAKER_TASKS); }
    } else {
      setSpeakerTasksList(FULL_SPEAKER_TASKS);
    }

    const savedReviewer = localStorage.getItem("task_manager_custom_dataset_v1") || localStorage.getItem("reviewer_tasks_dataset_v1");
    if (savedReviewer) {
      try { setReviewerTasksList(JSON.parse(savedReviewer)); } catch (e) { setReviewerTasksList(FULL_SPEAKER_TASKS); }
    } else {
      setReviewerTasksList(FULL_SPEAKER_TASKS);
    }
  };

  // Tải danh sách nhiệm vụ từ Storage
  const loadAssignTasks = () => {
    const saved = localStorage.getItem(ASSIGN_TASKS_STORAGE_KEY);
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed) && parsed.length > 0) {
          const normalized = parsed.map((item) => ({
            ...item,
            id: item.id || item.taskId || "N/A",
            title: item.taskTitle || item.title || "Chưa có tên nhiệm vụ",
            assignedUsers: Array.isArray(item.assignedUsers) ? item.assignedUsers : (item.assignedUsers ? [item.assignedUsers] : []),
            status: item.status || "Đang thực hiện",
            startDate: item.startDate || "01/03/2026",
            endDate: item.endDate || "15/03/2026"
          }));
          setAssignTasks(normalized);
          return;
        }
      } catch (e) {
        console.error("Lỗi đọc TaskManagerAssign data:", e);
      }
    }
    setAssignTasks([]);
  };

  useEffect(() => {
    loadAssignTasks();
    reloadAdminUsers();
    reloadTaskLists();

    const handleStorageChange = () => {
      loadAssignTasks();
      reloadAdminUsers();
      reloadTaskLists();
    };

    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("assign_tasks_updated", handleStorageChange);
    window.addEventListener("admin_users_updated", handleStorageChange);
    window.addEventListener("reviewer_tasks_updated", handleStorageChange);
    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("assign_tasks_updated", handleStorageChange);
      window.removeEventListener("admin_users_updated", handleStorageChange);
      window.removeEventListener("reviewer_tasks_updated", handleStorageChange);
    };
  }, []);

  useEffect(() => {
    localStorage.setItem(BATCH_LIST_STORAGE_KEY, JSON.stringify(batches));
  }, [batches]);

  useEffect(() => {
    localStorage.setItem(BATCH_ASSIGNMENT_STORAGE_KEY, JSON.stringify(batchMapping));
  }, [batchMapping]);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => setToast((prev) => ({ ...prev, show: false })), 3000);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  // Cập nhật Trạng thái trực tiếp của Nhiệm vụ
  const handleUpdateTaskStatus = (taskId, newStatus) => {
    const updatedTasks = assignTasks.map((t) =>
      t.id === taskId ? { ...t, status: newStatus } : t
    );
    setAssignTasks(updatedTasks);
    localStorage.setItem(ASSIGN_TASKS_STORAGE_KEY, JSON.stringify(updatedTasks));
    window.dispatchEvent(new Event("assign_tasks_updated"));
    showNotification(`Đã cập nhật trạng thái thành "${newStatus}"`);
  };

  // Lấy danh sách Speaker & Reviewer Active
  const activeSpeakers = useMemo(() => {
    return allAdminUsers.filter(u => u.role === "Speaker" && u.status === "Active");
  }, [allAdminUsers]);

  const activeReviewers = useMemo(() => {
    return allAdminUsers.filter(u => u.role === "Reviewer" && u.status === "Active");
  }, [allAdminUsers]);

  // Tổng hợp tất cả nhiệm vụ từ TaskManagerSpeakerTasks và TaskManagerReviewerTasks
  const allTasksOptions = useMemo(() => [
    ...speakerTasksList.map(t => ({ 
      ...t, 
      role: "Speaker", 
      uniqueKey: `Speaker-${t.id}`,
      label: `[Speaker] ${t.title}` 
    })),
    ...reviewerTasksList.map(t => ({ 
      ...t, 
      role: "Reviewer", 
      uniqueKey: `Reviewer-${t.id}`,
      label: `[Reviewer] ${t.title}` 
    }))
  ], [speakerTasksList, reviewerTasksList]);

  // Đợt đang chọn trong Modal Phân công
  const assignModalSelectedBatch = useMemo(() => {
    return batches.find(b => b.id === assignFormData.selectedBatchId) || batches[0] || null;
  }, [batches, assignFormData.selectedBatchId]);

  // Lọc danh sách Nhiệm vụ theo Topic của Đợt được chọn trong Modal Phân công
  const filteredTasksForAssignModal = useMemo(() => {
    if (!assignModalSelectedBatch) return allTasksOptions;
    return allTasksOptions.filter(t => t.topic === assignModalSelectedBatch.topic);
  }, [allTasksOptions, assignModalSelectedBatch]);

  const currentSelectedTask = useMemo(() => {
    return filteredTasksForAssignModal.find(t => t.uniqueKey === assignFormData.selectedUniqueKey) || filteredTasksForAssignModal[0];
  }, [assignFormData.selectedUniqueKey, filteredTasksForAssignModal]);

  const targetUserList = useMemo(() => {
    return currentSelectedTask?.role === "Speaker" ? activeSpeakers : activeReviewers;
  }, [currentSelectedTask, activeSpeakers, activeReviewers]);

  // Giới hạn số lượng tối đa theo yêu cầu của Đợt
  const maxAllowedUsers = useMemo(() => {
    if (!assignModalSelectedBatch || !currentSelectedTask) return Infinity;
    return currentSelectedTask.role === "Speaker"
      ? Number(assignModalSelectedBatch.speakerCount || 0)
      : Number(assignModalSelectedBatch.reviewerCount || 0);
  }, [assignModalSelectedBatch, currentSelectedTask]);

  // Mở Modal PHÂN CÔNG NHIỆM VỤ MỚI (Đã thêm kiểm tra đợt trước khi vào phân công)
  const handleOpenAssignModal = () => {
    if (batches.length === 0) {
      showNotification("Vui lòng tạo đợt trước khi thực hiện phân công nhiệm vụ!");
      return;
    }

    reloadAdminUsers();
    reloadTaskLists();
    setEditingAssignment(null);
    const initialBatch = batches.find(b => b.id === selectedBatchId) || batches[0] || null;
    const initialBatchTopic = initialBatch?.topic;
    const availableTasks = initialBatchTopic 
      ? allTasksOptions.filter(t => t.topic === initialBatchTopic) 
      : allTasksOptions;

    const defaultTask = availableTasks[0] || allTasksOptions[0];

    setAssignFormData({
      selectedBatchId: initialBatch ? initialBatch.id : "",
      selectedUniqueKey: defaultTask ? defaultTask.uniqueKey : "",
      assignedUsers: []
    });
    setIsAssignModalOpen(true);
  };

  const handleBatchChangeInAssignModal = (newBatchId) => {
    const nextBatch = batches.find(b => b.id === newBatchId);
    const nextAvailableTasks = nextBatch 
      ? allTasksOptions.filter(t => t.topic === nextBatch.topic)
      : allTasksOptions;
    
    const defaultNextTask = nextAvailableTasks[0];

    setAssignFormData({
      selectedBatchId: newBatchId,
      selectedUniqueKey: defaultNextTask ? defaultNextTask.uniqueKey : "",
      assignedUsers: []
    });
  };

  const handleTaskChangeInAssignModal = (newUniqueKey) => {
    const newSelectedTask = filteredTasksForAssignModal.find(t => t.uniqueKey === newUniqueKey);
    const isRoleChanged = newSelectedTask?.role !== currentSelectedTask?.role;

    setAssignFormData(prev => ({
      ...prev,
      selectedUniqueKey: newUniqueKey,
      assignedUsers: isRoleChanged ? [] : prev.assignedUsers
    }));
  };

  const handleUserCheckboxToggle = (userName) => {
    setAssignFormData(prev => {
      const exists = prev.assignedUsers.includes(userName);
      if (exists) {
        return { ...prev, assignedUsers: prev.assignedUsers.filter(u => u !== userName) };
      } else {
        if (prev.assignedUsers.length >= maxAllowedUsers) {
          alert(`Số lượng ${currentSelectedTask?.role} chọn không được vượt quá số lượng đợt yêu cầu (${maxAllowedUsers} người)!`);
          return prev;
        }
        return { ...prev, assignedUsers: [...prev.assignedUsers, userName] };
      }
    });
  };

  // Xác nhận lưu Modal Phân Công
  const handleSubmitAssignForm = (e) => {
    e.preventDefault();

    if (assignFormData.assignedUsers.length === 0) {
      alert(`Vui lòng chọn ít nhất 1 ${currentSelectedTask?.role || 'người'} để thực hiện nhiệm vụ!`);
      return;
    }

    if (assignFormData.assignedUsers.length > maxAllowedUsers) {
      alert(`Số lượng người chọn (${assignFormData.assignedUsers.length}) lớn hơn số lượng đợt yêu cầu (${maxAllowedUsers})!`);
      return;
    }

    const taskObj = currentSelectedTask;
    const batchObj = assignModalSelectedBatch;

    const createdAssignment = {
      id: `ASN-${String(Date.now()).slice(-4)}`,
      taskId: taskObj.id,
      taskTitle: taskObj.title,
      title: taskObj.title,
      role: taskObj.role,
      assignedUsers: assignFormData.assignedUsers,
      startDate: batchObj?.startDate || formatDateToVN(todayIso),
      endDate: batchObj?.endDate || formatDateToVN(todayIso),
      status: "Đang thực hiện"
    };

    const updatedTasks = [...assignTasks, createdAssignment];
    setAssignTasks(updatedTasks);
    localStorage.setItem(ASSIGN_TASKS_STORAGE_KEY, JSON.stringify(updatedTasks));

    if (batchObj) {
      setBatchMapping((prev) => {
        const existingList = prev[batchObj.id] || [];
        if (!existingList.includes(createdAssignment.id)) {
          return {
            ...prev,
            [batchObj.id]: [...existingList, createdAssignment.id]
          };
        }
        return prev;
      });
    }

    window.dispatchEvent(new Event("assign_tasks_updated"));

    showNotification(`Đã phân công thành công nhiệm vụ "${taskObj.title}"!`);
    setIsAssignModalOpen(false);
  };

  // Mở Modal TẠO ĐỢT MỚI
  const handleOpenCreateModal = () => {
    reloadAdminUsers();
    setModalMode("CREATE");
    setModalTargetBatchId(null);
    setBatchFormData({
      name: `Đợt ${batches.length + 1}`,
      target: 50,
      speakerCount: activeSpeakers.length || 1,
      reviewerCount: activeReviewers.length || 1,
      topic: "Công nghệ thông tin",
      startDate: todayIso,
      endDate: todayIso,
      assignedSpeakers: [],
      assignedReviewers: []
    });
    setIsModalOpen(true);
  };

  // Mở Modal CẬP NHẬT ĐỢT
  const handleOpenUpdateModal = (e, batch) => {
    e.stopPropagation();
    reloadAdminUsers();
    setModalMode("UPDATE");
    setModalTargetBatchId(batch.id);
    setSelectedBatchId(batch.id);
    setBatchFormData({
      name: batch.name || "",
      target: batch.target || 50,
      speakerCount: batch.speakerCount ?? activeSpeakers.length ?? 1,
      reviewerCount: batch.reviewerCount ?? activeReviewers.length ?? 1,
      topic: batch.topic || "Công nghệ thông tin",
      startDate: batch.startDate ? formatDateToISO(batch.startDate) : todayIso,
      endDate: batch.endDate ? formatDateToISO(batch.endDate) : todayIso,
      assignedSpeakers: batch.assignedSpeakers || [],
      assignedReviewers: batch.assignedReviewers || []
    });
    setIsModalOpen(true);
  };

  // Xác nhận lưu Đợt
  const handleConfirmSaveModal = (e) => {
    e.preventDefault();

    if (!batchFormData.name.trim()) {
      alert("Vui lòng nhập tên đợt!");
      return;
    }

    const startIso = batchFormData.startDate.includes("/") ? formatDateToISO(batchFormData.startDate) : batchFormData.startDate;
    const endIso = batchFormData.endDate.includes("/") ? formatDateToISO(batchFormData.endDate) : batchFormData.endDate;

    if (endIso < startIso) {
      alert("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu!");
      return;
    }

    if (modalMode === "CREATE") {
      const newId = `BATCH-${String(Date.now()).slice(-4)}`;
      const newBatchObj = {
        id: newId,
        name: batchFormData.name,
        target: Number(batchFormData.target),
        speakerCount: Number(batchFormData.speakerCount),
        reviewerCount: Number(batchFormData.reviewerCount),
        topic: batchFormData.topic,
        startDate: formatDateToVN(batchFormData.startDate),
        endDate: formatDateToVN(batchFormData.endDate),
        assignedSpeakers: batchFormData.assignedSpeakers,
        assignedReviewers: batchFormData.assignedReviewers
      };

      setBatches((prev) => [...prev, newBatchObj]);
      setSelectedBatchId(newId);
      showNotification(`Đã tạo thành công ${batchFormData.name}!`);
    } else {
      setBatches((prev) =>
        prev.map((b) =>
          b.id === modalTargetBatchId
            ? {
                ...b,
                name: batchFormData.name,
                target: Number(batchFormData.target),
                speakerCount: Number(batchFormData.speakerCount),
                reviewerCount: Number(batchFormData.reviewerCount),
                topic: batchFormData.topic,
                startDate: formatDateToVN(batchFormData.startDate),
                endDate: formatDateToVN(batchFormData.endDate),
                assignedSpeakers: batchFormData.assignedSpeakers,
                assignedReviewers: batchFormData.assignedReviewers
              }
            : b
        )
      );
      showNotification(`Đã cập nhật ${batchFormData.name}!`);
    }

    setIsModalOpen(false);
  };

  // Chuẩn bị xóa Đợt
  const handleRequestDeleteBatch = (e, batchToDelete) => {
    e.stopPropagation();
    setDeletingBatch(batchToDelete);
  };

  const handleConfirmDeleteBatch = () => {
    if (!deletingBatch) return;

    const remainingBatches = batches.filter((b) => b.id !== deletingBatch.id);
    setBatches(remainingBatches);

    setBatchMapping((prev) => {
      const nextMap = { ...prev };
      delete nextMap[deletingBatch.id];
      return nextMap;
    });

    if (selectedBatchId === deletingBatch.id) {
      setSelectedBatchId(remainingBatches.length > 0 ? remainingBatches[0].id : null);
    }

    showNotification(`Đã xóa thành công ${deletingBatch.name}!`);
    setDeletingBatch(null);
  };

  // -------------------------------------------------------------
  // THAO TÁC XÓA & SỬA NHIỆM VỤ TRONG TABLE
  // -------------------------------------------------------------
  const handleOpenEditTaskModal = (task) => {
    reloadAdminUsers();
    setEditingTask(task);
    setEditTaskFormData({
      title: task.title || "",
      role: task.role || "Speaker",
      assignedUsers: task.assignedUsers || []
    });
    setIsEditTaskModalOpen(true);
  };

  const handleRoleChangeInEditTask = (newRole) => {
    setEditTaskFormData((prev) => ({
      ...prev,
      role: newRole,
      assignedUsers: [] // Reset danh sách nếu đổi vai trò
    }));
  };

  const handleToggleUserInEditTask = (userName) => {
    const editBatch = batches.find(b => b.id === selectedBatchId);
    const maxUsers = editTaskFormData.role === "Speaker" 
      ? Number(editBatch?.speakerCount || 999) 
      : Number(editBatch?.reviewerCount || 999);

    setEditTaskFormData((prev) => {
      const exists = prev.assignedUsers.includes(userName);
      if (exists) {
        return { ...prev, assignedUsers: prev.assignedUsers.filter((u) => u !== userName) };
      } else {
        if (prev.assignedUsers.length >= maxUsers) {
          alert(`Số lượng không được vượt quá số lượng đợt yêu cầu (${maxUsers} người)!`);
          return prev;
        }
        return { ...prev, assignedUsers: [...prev.assignedUsers, userName] };
      }
    });
  };

  const handleSaveEditTask = (e) => {
    e.preventDefault();

    if (!editTaskFormData.title.trim()) {
      alert("Tên nhiệm vụ không được để trống!");
      return;
    }

    if (editTaskFormData.assignedUsers.length === 0) {
      alert("Vui lòng chọn ít nhất 1 người thực hiện!");
      return;
    }

    const updatedTasks = assignTasks.map((t) =>
      t.id === editingTask.id
        ? {
            ...t,
            title: editTaskFormData.title,
            taskTitle: editTaskFormData.title,
            role: editTaskFormData.role,
            assignedUsers: editTaskFormData.assignedUsers
          }
        : t
    );

    setAssignTasks(updatedTasks);
    localStorage.setItem(ASSIGN_TASKS_STORAGE_KEY, JSON.stringify(updatedTasks));
    window.dispatchEvent(new Event("assign_tasks_updated"));

    showNotification(`Đã cập nhật nhiệm vụ "${editTaskFormData.title}"!`);
    setIsEditTaskModalOpen(false);
  };

  const handleRequestDeleteTask = (task) => {
    setDeletingTask(task);
  };

  const handleConfirmDeleteTask = () => {
    if (!deletingTask) return;

    // Xóa khỏi assignTasks
    const updatedTasks = assignTasks.filter((t) => t.id !== deletingTask.id);
    setAssignTasks(updatedTasks);
    localStorage.setItem(ASSIGN_TASKS_STORAGE_KEY, JSON.stringify(updatedTasks));

    // Xóa khỏi mapping của đợt hiện tại
    setBatchMapping((prev) => {
      const currentList = prev[selectedBatchId] || [];
      return {
        ...prev,
        [selectedBatchId]: currentList.filter((id) => id !== deletingTask.id)
      };
    });

    window.dispatchEvent(new Event("assign_tasks_updated"));
    showNotification(`Đã xóa nhiệm vụ "${deletingTask.title}"!`);
    setDeletingTask(null);
  };
  // -------------------------------------------------------------

  // Lấy danh sách nhiệm vụ thuộc đợt đang chọn
  const currentBatchTaskIds = useMemo(() => {
    if (!selectedBatchId) return [];
    return batchMapping[selectedBatchId] || [];
  }, [batchMapping, selectedBatchId]);

  const currentTasks = useMemo(() => {
    return assignTasks.filter((t) => currentBatchTaskIds.includes(t.id));
  }, [assignTasks, currentBatchTaskIds]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setSearchQuery(searchInput);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      e.preventDefault();
      setSearchQuery(searchInput);
    }
  };

  // Lọc nhiệm vụ
  const filteredTasks = useMemo(() => {
    const q = (searchQuery || "").toLowerCase();
    return currentTasks.filter((task) => {
      const titleStr = (task?.title || "").toLowerCase();
      const matchSearch = titleStr.includes(q);
      const matchStatus = statusFilter === "ALL" || task.status === statusFilter;
      return matchSearch && matchStatus;
    });
  }, [currentTasks, searchQuery, statusFilter]);

  const selectedBatchObj = batches.find((b) => b.id === selectedBatchId) || null;
  const targetModalBatchObj = batches.find((b) => b.id === modalTargetBatchId) || selectedBatchObj;

  return (
    <div className="w-full h-full flex flex-col gap-3 text-left font-sans p-1 overflow-y-auto relative">
      
      {/* Toast Notification */}
      <div 
        style={{ borderColor: SUCCESS }}
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-3.5 py-2 rounded-xl shadow-xl border transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 style={{ color: SUCCESS }} className="w-4 h-4 shrink-0" />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          type="button"
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-1"
        >
          <X className="w-3.5 h-3.5 text-gray-400 dark:text-gray-500" />
        </button>
      </div>

      {/* MODAL TẠO / CẬP NHẬT ĐỢT */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setIsModalOpen(false)}>
          <div 
            className="rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <form onSubmit={handleConfirmSaveModal} className="p-5 space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">
                  {modalMode === "CREATE" ? "Tạo đợt nhiệm vụ mới" : `Chỉnh sửa ${targetModalBatchObj?.name || "đợt"}`}
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="p-1 hover:bg-gray-100 dark:hover:bg-[#25272E] rounded-lg transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-gray-500 dark:text-gray-400" />
                </button>
              </div>

              {/* Tên Đợt */}
              <div>
                <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1">
                  Tên đợt
                </label>
                <input
                  type="text"
                  required
                  value={batchFormData.name}
                  onChange={(e) => setBatchFormData({ ...batchFormData, name: e.target.value })}
                  placeholder="Ví dụ: Đợt 1, Đợt 2..."
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-medium transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                />
              </div>

              {/* Chủ đề & Mục tiêu */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1 flex items-center justify-between">
                    <span>Chủ đề (Topic)</span>
                  </label>
                  <select
                    disabled={modalMode === "UPDATE"}
                    value={batchFormData.topic}
                    onChange={(e) => setBatchFormData({ ...batchFormData, topic: e.target.value })}
                    className={`w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 ${
                      modalMode === "UPDATE"
                        ? "bg-gray-100 dark:bg-[#18191D] cursor-not-allowed opacity-80 appearance-none"
                        : "bg-gray-50 dark:bg-[#25272E] cursor-pointer"
                    }`}
                  >
                    <option value="Công nghệ thông tin" className="dark:bg-[#1C1D22]">Công nghệ thông tin</option>
                    <option value="Giáo dục" className="dark:bg-[#1C1D22]">Giáo dục</option>
                    <option value="Hội thoại hàng ngày" className="dark:bg-[#1C1D22]">Hội thoại hàng ngày</option>
                  </select>
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1">
                    Mục tiêu (Số câu)
                  </label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={batchFormData.target}
                    onChange={(e) => setBatchFormData({ ...batchFormData, target: e.target.value })}
                    className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                  />
                </div>
              </div>

              {/* Ô NHẬP SỐ LƯỢNG SPEAKERS VÀ REVIEWERS */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1">
                    Số lượng Speakers
                  </label>
                  <input
                    type="number"
                    min="0"
                    required
                    value={batchFormData.speakerCount}
                    onChange={(e) => setBatchFormData({ ...batchFormData, speakerCount: e.target.value })}
                    className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1">
                    Số lượng Reviewers
                  </label>
                  <input
                    type="number"
                    min="0"
                    required
                    value={batchFormData.reviewerCount}
                    onChange={(e) => setBatchFormData({ ...batchFormData, reviewerCount: e.target.value })}
                    className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
                  />
                </div>
              </div>

              {/* Ngày Bắt đầu & Kết thúc */}
              <div className="grid grid-cols-2 gap-3">
                <VNFormatDatePicker
                  label="Ngày bắt đầu"
                  value={batchFormData.startDate}
                  minDateIso={todayIso}
                  onChange={(newVal) => setBatchFormData({ ...batchFormData, startDate: newVal })}
                />

                <VNFormatDatePicker
                  label="Ngày kết thúc"
                  value={batchFormData.endDate}
                  minDateIso={batchFormData.startDate.includes("-") ? batchFormData.startDate : todayIso}
                  onChange={(newVal) => setBatchFormData({ ...batchFormData, endDate: newVal })}
                />
              </div>

              <div className="flex gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer"
                >
                  {modalMode === "CREATE" ? "Tạo đợt" : "Lưu thay đổi"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL PHÂN CÔNG NHIỆM VỤ MỚI */}
      {isAssignModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setIsAssignModalOpen(false)}>
          <div 
            className="rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            <form onSubmit={handleSubmitAssignForm} className="p-5 space-y-3">
              <h3 className="text-sm font-bold truncate text-gray-900 dark:text-gray-100">
                Phân công nhiệm vụ mới
              </h3>

              {/* Select Chọn Đợt */}
              <div>
                <label className="block text-[10px] font-bold uppercase mb-1 text-gray-500 dark:text-gray-400">
                  Chọn đợt
                </label>
                <div className="relative flex items-center">
                  <select
                    value={assignFormData.selectedBatchId}
                    onChange={(e) => handleBatchChangeInAssignModal(e.target.value)}
                    className="w-full text-xs appearance-none border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans truncate pl-3 pr-8 py-1.5 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 cursor-pointer"
                  >
                    {batches.map((b) => (
                      <option key={b.id} value={b.id} className="bg-white text-gray-900 dark:bg-[#1C1D22] dark:text-white">
                        {b.name} - Số lượng(tối đa): {b.speakerCount} speakers, {b.reviewerCount} reviewers
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="w-3.5 h-3.5 absolute right-2.5 pointer-events-none text-gray-400 dark:text-gray-400" />
                </div>
              </div>
              
              {/* Select Chọn Nhiệm Vụ (đã lọc theo Topic của Đợt) */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="block text-[10px] font-bold uppercase text-gray-500 dark:text-gray-400">
                    Chọn nhiệm vụ (Thuộc chủ đề: {assignModalSelectedBatch?.topic || "Tất cả"})
                  </label>
                </div>
                <div className="relative flex items-center">
                  <select
                    value={assignFormData.selectedUniqueKey}
                    onChange={(e) => handleTaskChangeInAssignModal(e.target.value)}
                    className="w-full text-xs appearance-none border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-bold transition-all font-sans truncate pl-3 pr-8 py-1.5 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 cursor-pointer"
                  >
                    {filteredTasksForAssignModal.length > 0 ? (
                      filteredTasksForAssignModal.map((t) => (
                        <option key={t.uniqueKey} value={t.uniqueKey} className="bg-white text-gray-900 dark:bg-[#1C1D22] dark:text-white">
                          {t.label}
                        </option>
                      ))
                    ) : (
                      <option value="" disabled className="bg-white text-gray-900 dark:bg-[#1C1D22] dark:text-white">
                        Không có nhiệm vụ nào thuộc chủ đề {assignModalSelectedBatch?.topic}
                      </option>
                    )}
                  </select>
                  <ChevronDown className="w-3.5 h-3.5 absolute right-2.5 pointer-events-none text-gray-400 dark:text-gray-400" />
                </div>
              </div>

              {/* Danh sách SPEAKER / REVIEWER */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="block text-[10px] font-bold uppercase text-gray-500 dark:text-gray-400">
                    {currentSelectedTask?.role} đang hoạt động ({assignFormData.assignedUsers.length}/{maxAllowedUsers} tối đa)
                  </label>
                  <span 
                    style={{ backgroundColor: currentSelectedTask?.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                    className="text-[9px] text-white px-1.5 py-0.2 rounded font-bold"
                  >
                    {currentSelectedTask?.role}
                  </span>
                </div>

                <div className="max-h-48 overflow-y-auto border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] rounded-lg p-2 space-y-1">
                  {targetUserList.length > 0 ? (
                    targetUserList.map((u) => {
                      const isChecked = assignFormData.assignedUsers.includes(u.name);
                      const isDisableUnchecked = !isChecked && assignFormData.assignedUsers.length >= maxAllowedUsers;

                      return (
                        <label 
                          key={u.id} 
                          className={`flex items-center justify-between p-1.5 rounded-md text-xs transition-colors ${
                            isDisableUnchecked 
                              ? "opacity-50 cursor-not-allowed text-gray-400 dark:text-gray-500" 
                              : "cursor-pointer"
                          } ${
                            isChecked 
                              ? "bg-white dark:bg-[#1C1D22] shadow-2xs font-bold text-gray-900 dark:text-white" 
                              : "hover:bg-black/5 dark:hover:bg-white/5 text-gray-600 dark:text-gray-300"
                          }`}
                        >
                          <div className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              disabled={isDisableUnchecked}
                              onChange={() => handleUserCheckboxToggle(u.name)}
                              className="rounded text-gray-900 focus:ring-0 cursor-pointer disabled:cursor-not-allowed"
                            />
                            <span>{u.name}</span>
                          </div>
                          <span className="text-[10px] font-normal text-gray-400 dark:text-gray-400">
                            {u.email}
                          </span>
                        </label>
                      );
                    })
                  ) : (
                    <div className="p-3 text-center text-xs font-medium text-gray-500 dark:text-gray-400">
                      Không có {currentSelectedTask?.role} nào đang hoạt động (Active).
                    </div>
                  )}
                </div>
              </div>

              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setIsAssignModalOpen(false)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={filteredTasksForAssignModal.length === 0}
                  style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Lưu phân công
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL XÓA ĐỢT */}
      {deletingBatch && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setDeletingBatch(null)}>
          <div 
            className="border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: DANGER }} className="h-1.5 w-full shrink-0" />
            <div className="p-5 text-center space-y-3">
              <div 
                style={{ backgroundColor: "rgba(243, 114, 127, 0.15)", color: DANGER }}
                className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto"
              >
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">Xác nhận xóa đợt</h3>
                <p className="text-[11px] mt-1 break-words text-gray-500 dark:text-gray-400">
                  Bạn có chắc muốn xóa <span className="font-bold text-gray-900 dark:text-gray-200">"{deletingBatch.name}"</span>? Các nhiệm vụ trong đợt này sẽ trở về trạng thái chưa được phân đợt.
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setDeletingBatch(null)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDeleteBatch}
                  style={{ backgroundColor: DANGER }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer"
                >
                  Xóa ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL SỬA NHIỆM VỤ */}
      {isEditTaskModalOpen && editingTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setIsEditTaskModalOpen(false)}>
          <div 
            className="rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <form onSubmit={handleSaveEditTask} className="p-5 space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">
                  Chỉnh sửa nhiệm vụ
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsEditTaskModalOpen(false)}
                  className="p-1 hover:bg-gray-100 dark:hover:bg-[#25272E] rounded-lg transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-gray-500 dark:text-gray-400" />
                </button>
              </div>

              {/* Tên nhiệm vụ */}
              <div>
                <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1 flex items-center justify-between">
                  <span>Tên nhiệm vụ</span>
                </label>
                <input
                  type="text"
                  disabled
                  value={editTaskFormData.title}
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-100 dark:bg-[#18191D] text-gray-500 dark:text-gray-400 rounded-lg outline-none font-medium transition-all font-sans cursor-not-allowed opacity-80"
                />
              </div>

              {/* Vai trò */}
              <div>
                <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1 flex items-center justify-between">
                  <span>Vai trò thực hiện</span>
                </label>
                <select
                  disabled
                  value={editTaskFormData.role}
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-100 dark:bg-[#18191D] text-gray-500 dark:text-gray-400 rounded-lg outline-none font-bold transition-all cursor-not-allowed opacity-80 appearance-none"
                >
                  <option value="Speaker" className="dark:bg-[#1C1D22]">Speaker</option>
                  <option value="Reviewer" className="dark:bg-[#1C1D22]">Reviewer</option>
                </select>
              </div>

              {/* Danh sách người thực hiện */}
              <div>
                {(() => {
                  const editBatch = batches.find(b => b.id === selectedBatchId);
                  const maxUsers = editTaskFormData.role === "Speaker" 
                    ? Number(editBatch?.speakerCount || 999) 
                    : Number(editBatch?.reviewerCount || 999);

                  return (
                    <>
                      <div className="flex items-center justify-between mb-1">
                        <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase">
                          Danh sách người thực hiện ({editTaskFormData.assignedUsers.length}/{maxUsers} tối đa)
                        </label>
                        <span 
                          style={{ backgroundColor: editTaskFormData.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                          className="text-[9px] text-white px-1.5 py-0.2 rounded font-bold"
                        >
                          {editTaskFormData.role}
                        </span>
                      </div>

                      <div className="max-h-44 overflow-y-auto border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] rounded-lg p-2 space-y-1">
                        {(editTaskFormData.role === "Speaker" ? activeSpeakers : activeReviewers).map((u) => {
                          const isChecked = editTaskFormData.assignedUsers.includes(u.name);
                          const isDisableUnchecked = !isChecked && editTaskFormData.assignedUsers.length >= maxUsers;

                          return (
                            <label 
                              key={u.id}
                              className={`flex items-center justify-between p-1.5 rounded-md text-xs transition-colors ${
                                isDisableUnchecked 
                                  ? "opacity-50 cursor-not-allowed text-gray-400 dark:text-gray-500" 
                                  : "cursor-pointer"
                              } ${
                                isChecked 
                                  ? "bg-white dark:bg-[#1C1D22] shadow-2xs font-bold text-gray-900 dark:text-white" 
                                  : "hover:bg-black/5 dark:hover:bg-white/5 text-gray-600 dark:text-gray-300"
                              }`}
                            >
                              <div className="flex items-center gap-2">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  disabled={isDisableUnchecked}
                                  onChange={() => handleToggleUserInEditTask(u.name)}
                                  className="rounded text-gray-900 focus:ring-0 cursor-pointer disabled:cursor-not-allowed"
                                />
                                <span>{u.name}</span>
                              </div>
                              <span className="text-[10px] font-normal text-gray-400">{u.email}</span>
                            </label>
                          );
                        })}
                      </div>
                    </>
                  );
                })()}
              </div>

              <div className="flex gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setIsEditTaskModalOpen(false)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer"
                >
                  Lưu thay đổi
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL XÓA NHIỆM VỤ */}
      {deletingTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setDeletingTask(null)}>
          <div 
            className="border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: DANGER }} className="h-1.5 w-full shrink-0" />
            <div className="p-5 text-center space-y-3">
              <div 
                style={{ backgroundColor: "rgba(243, 114, 127, 0.15)", color: DANGER }}
                className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto"
              >
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100">Xác nhận xóa nhiệm vụ</h3>
                <p className="text-[11px] mt-1 break-words text-gray-500 dark:text-gray-400">
                  Bạn có chắc muốn xóa nhiệm vụ <span className="font-bold text-gray-900 dark:text-gray-200">"{deletingTask.title}"</span> khỏi đợt này?
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setDeletingTask(null)}
                  className="flex-1 py-2 rounded-lg border border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-300 text-xs font-bold hover:bg-gray-100 dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDeleteTask}
                  style={{ backgroundColor: DANGER }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer"
                >
                  Xóa ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL XEM DANH SÁCH NGƯỜI ĐƯỢC PHÂN CÔNG */}
      {viewingAssignedTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setViewingAssignedTask(null)}>
          <div 
            className="rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            <div className="p-4 border-b border-gray-200 dark:border-gray-800 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Users style={{ color: TASK_MANAGER_ACCENT }} className="w-4 h-4" />
                <h3 className="text-xs font-bold text-gray-900 dark:text-gray-100">
                  Danh sách phân công ({viewingAssignedTask.role || "Người thực hiện"})
                </h3>
              </div>
              <button 
                onClick={() => setViewingAssignedTask(null)}
                className="p-1 hover:bg-gray-100 dark:hover:bg-[#25272E] rounded-lg transition-colors cursor-pointer"
              >
                <X className="w-4 h-4 text-gray-500 dark:text-gray-400" />
              </button>
            </div>

            <div className="p-4 space-y-3">
              <p className="text-xs font-bold break-words text-gray-900 dark:text-gray-200">
                {viewingAssignedTask.title}
              </p>

              <div className="max-h-60 overflow-y-auto space-y-1.5 pr-1">
                {viewingAssignedTask.assignedUsers && viewingAssignedTask.assignedUsers.length > 0 ? (
                  viewingAssignedTask.assignedUsers.map((userName, idx) => (
                    <div 
                      key={idx} 
                      className="flex items-center justify-between p-2 rounded-xl text-xs font-semibold bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-gray-200"
                    >
                      <div className="flex items-center gap-2">
                        <div 
                          style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                          className="w-6 h-6 rounded-full text-white flex items-center justify-center text-[10px] font-bold"
                        >
                          {userName.charAt(0)}
                        </div>
                        <span>{userName}</span>
                      </div>
                      <span 
                        style={{ backgroundColor: viewingAssignedTask.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                        className="text-[9px] text-white px-2 py-0.5 rounded-md font-bold"
                      >
                        {viewingAssignedTask.role || "Thực hiện"}
                      </span>
                    </div>
                  ))
                ) : (
                  <p className="text-xs text-center py-2 text-gray-500 dark:text-gray-400">Chưa phân công người nào</p>
                )}
              </div>
            </div>

            <div className="p-3 border-t border-gray-200 dark:border-gray-800 text-right bg-gray-50/50 dark:bg-[#25272E]/50">
              <button
                type="button"
                onClick={() => setViewingAssignedTask(null)}
                style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                className="px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-opacity cursor-pointer"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header Page + Cụm Nút Thao Tác */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 shrink-0">
        <div>
          <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
            <ListTodo className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
            Quản lý nhiệm vụ
          </h2>
          <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-0.5">
            Quản lý và tạo các nhiệm vụ thực hiện theo từng đợt
          </p>
        </div>

        <div className="flex items-center gap-2 self-start md:self-auto shrink-0">
          <button
            type="button"
            onClick={handleOpenCreateModal}
            style={{ backgroundColor: TASK_MANAGER_ACCENT }}
            className="inline-flex items-center gap-1.5 py-1.5 px-3 text-xs font-bold text-white rounded-lg shadow-2xs hover:opacity-90 transition-all cursor-pointer"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Tạo đợt</span>
          </button>

          <button
            type="button"
            onClick={handleOpenAssignModal}
            style={{ backgroundColor: TASK_MANAGER_ACCENT }}
            className="inline-flex items-center gap-1.5 py-1.5 px-3 text-xs font-bold text-white rounded-lg shadow-2xs hover:opacity-90 transition-all cursor-pointer"
          >
            <UserCheck className="w-3.5 h-3.5" />
            <span>Phân công</span>
          </button>
        </div>
      </div>

      {/* Grid Layout chính */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-3 flex-1 min-h-0">
        
        {/* Cột trái: Danh sách các Đợt */}
        <div className="lg:col-span-1 rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-3 shadow-xs flex flex-col gap-2 min-h-0">
          
          <div className="flex items-center justify-between px-1 pb-1 border-b border-gray-100 dark:border-gray-800">
            <h3 className="text-xs font-bold uppercase tracking-wider text-gray-900 dark:text-white flex items-center gap-1.5">
              <Layers style={{ color: TASK_MANAGER_ACCENT }} className="w-3.5 h-3.5" />
              DANH SÁCH ĐỢT
            </h3>
          </div>

          <div className="flex flex-col gap-2 overflow-y-auto max-h-[580px] pr-1">
            {batches.length === 0 ? (
              <div className="p-3 text-center text-xs font-medium text-gray-400 dark:text-gray-500">
                Chưa có đợt nào. Bấm nút tạo đợt ở trên để tạo.
              </div>
            ) : (
              batches.map((batch) => {
                const active = batch.id === selectedBatchId;
                
                const mappedIds = batchMapping[batch.id] || [];
                const batchTasks = assignTasks.filter((t) => mappedIds.includes(t.id));

                const isBatchCompleted = batchTasks.length > 0 && batchTasks.every((t) => t.status === "Hoàn thành");

                return (
                  <div
                    key={batch.id}
                    onClick={() => setSelectedBatchId(batch.id)}
                    style={active ? { 
                      backgroundColor: `${TASK_MANAGER_ACCENT}15`, 
                      borderColor: `${TASK_MANAGER_ACCENT}50` 
                    } : {}}
                    className={`relative w-full text-left p-3 rounded-xl transition-all border cursor-pointer flex flex-col gap-2 ${
                      active 
                        ? "shadow-xs" 
                        : "bg-gray-50/50 dark:bg-[#25272E]/50 border-transparent hover:bg-gray-100 dark:hover:bg-[#25272E]"
                    }`}
                  >
                    {/* Dòng Header Đợt & Nút thao tác */}
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0 flex-1">
                        <span className="text-xs font-bold text-gray-900 dark:text-white truncate">
                          {batch.name}
                        </span>
                        
                        <span 
                          style={{
                            backgroundColor: isBatchCompleted ? CHIP_SUCCESS_BG : CHIP_WARNING_BG,
                            borderColor: isBatchCompleted ? CHIP_SUCCESS_BORDER : CHIP_WARNING_BORDER,
                            color: isBatchCompleted ? CHIP_SUCCESS_TEXT : CHIP_WARNING_TEXT
                          }}
                          className="inline-flex items-center gap-1 pl-1.5 pr-2 py-0.5 rounded-full text-[9px] font-bold border shrink-0 leading-tight"
                        >
                          {isBatchCompleted ? (
                            <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} />
                          ) : (
                            <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_WARNING_TEXT }} />
                          )}
                          <span>{isBatchCompleted ? "Hoàn thành" : "Đang thực hiện"}</span>
                        </span>
                      </div>

                      <div className="flex items-center gap-0.5 shrink-0">
                        <button
                          type="button"
                          onClick={(e) => handleOpenUpdateModal(e, batch)}
                          title={`Sửa ${batch.name}`}
                          className="p-1 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md hover:text-gray-900 dark:hover:text-gray-100 transition-colors cursor-pointer dark:text-gray-400"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>

                        <button
                          type="button"
                          onClick={(e) => handleRequestDeleteBatch(e, batch)}
                          title={`Xóa ${batch.name}`}
                          className="p-1 text-gray-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-md hover:text-red-600 dark:hover:text-[#E55353] transition-colors cursor-pointer dark:text-gray-400"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    {/* THÔNG TIN CHỦ ĐỀ, MỤC TIÊU, SPEAKERS/REVIEWERS, NGÀY BẮT ĐẦU VÀ KẾT THÚC */}
                    <div className="text-[10px] space-y-1 text-gray-500 dark:text-gray-400 border-t border-gray-100 dark:border-gray-800/60 pt-1.5">
                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Tag className="w-3 h-3 text-emerald-500 shrink-0" />
                        <span className="truncate">Chủ đề: <strong className="text-gray-900 dark:text-white font-bold">{batch.topic || "Công nghệ thông tin"}</strong></span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Target className="w-3 h-3 text-blue-500 shrink-0" />
                        <span>Mục tiêu: <strong className="text-gray-900 dark:text-white font-bold">{batch.target || 0}</strong> câu</span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Users className="w-3 h-3 text-amber-500 shrink-0" />
                        <span>Số lượng(tối đa): <strong className="text-gray-900 dark:text-white font-bold">{batch.speakerCount || 0}</strong> Speakers, <strong className="text-gray-800 dark:text-gray-200">{batch.reviewerCount || 0}</strong> Reviewers</span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Calendar className="w-3 h-3 text-purple-500 shrink-0" />
                        <span>Thời gian: <strong className="text-gray-900 dark:text-white font-bold">{batch.startDate || "N/A"} - {batch.endDate || "N/A"}</strong></span>
                      </div>
                    </div>

                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Cột phải: Bảng danh sách nhiệm vụ */}
        <div className="lg:col-span-3 flex flex-col gap-3 min-h-0">
          
          {/* Filter Bar */}
          <div className="rounded-xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
            <div className="w-full md:w-auto flex-1 max-w-xl">
              <div className="relative w-full flex items-center rounded-xl border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
                <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none z-10 text-gray-400 dark:text-gray-500" />
                <input
                  type="text"
                  placeholder="Tìm theo tên nhiệm vụ..."
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium outline-none transition-all placeholder:text-gray-400 text-gray-900 dark:text-white"
                />
                <button
                  type="button"
                  onClick={handleSearchSubmit}
                  style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                  className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-all cursor-pointer z-10 shadow-xs"
                >
                  Tìm kiếm
                </button>
              </div>
            </div>

            <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
              <Filter className="w-3.5 h-3.5 text-gray-500 dark:text-gray-400 shrink-0" />
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value)}
                className="w-[160px] px-2.5 py-2 border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold outline-none cursor-pointer transition-all font-sans"
              >
                <option value="ALL" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
                <option value="Đang thực hiện" className="bg-white dark:bg-[#25272E]">Đang thực hiện</option>
                <option value="Hoàn thành" className="bg-white dark:bg-[#25272E]">Hoàn thành</option>
              </select>
            </div>
          </div>

          {/* BẢNG CHUẨN */}
          <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] shadow-xs overflow-hidden flex-1 flex flex-col">
            <div className="overflow-x-auto flex-1">
              <table className="w-full text-left text-xs">
                <thead>
                  <tr className="border-b border-gray-200 dark:border-gray-800 text-gray-500 dark:text-gray-400 text-[10px] uppercase font-bold bg-gray-50/80 dark:bg-[#25272E]">
                    <th className="py-3 px-3 text-center w-[50px]">STT</th>
                    <th className="py-3 px-3 min-w-[200px]">NHIỆM VỤ</th>
                    <th className="py-3 px-3 text-center w-[100px]">VAI TRÒ</th>
                    <th className="py-3 px-3 text-center w-[200px]">NGƯỜI ĐƯỢC PHÂN CÔNG</th>
                    <th className="py-3 px-3 text-center w-[140px]">TRẠNG THÁI</th>
                    <th className="py-3 px-3 text-center w-[100px]">THAO TÁC</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                  {!selectedBatchObj || filteredTasks.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-gray-400 dark:text-gray-500 font-medium">
                        {!selectedBatchObj 
                          ? "Hãy chọn hoặc tạo một đợt để xem danh sách nhiệm vụ." 
                          : `Không tìm thấy nhiệm vụ nào trong ${selectedBatchObj.name}.`}
                      </td>
                    </tr>
                  ) : (
                    filteredTasks.map((task, index) => {
                      const isCompleted = task.status === "Hoàn thành";
                      const countUsers = task.assignedUsers ? task.assignedUsers.length : 0;

                      return (
                        <tr key={task.id} className="hover:bg-gray-50/80 dark:hover:bg-[#25272E]/50 transition-colors">
                          <td className="py-3.5 px-3 text-center text-gray-400 font-medium text-xs">
                            {index + 1}
                          </td>
                          <td className="py-3.5 px-3">
                            <p className="font-bold text-gray-900 dark:text-white text-xs leading-snug">{task.title}</p>
                          </td>
                          <td className="py-3.5 px-3 text-center">
                            <span 
                              style={{ backgroundColor: task.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                              className="text-[10px] font-bold text-white px-2.5 py-1 rounded-md"
                            >
                              {task.role}
                            </span>
                          </td>

                          <td className="py-3.5 px-3 text-center whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() => setViewingAssignedTask(task)}
                              className="inline-flex items-center justify-center gap-1.5 px-2.5 py-1 rounded-lg bg-[#F9FAFB] dark:bg-[#25272E] hover:bg-[#E5E7EB] dark:hover:bg-gray-700 text-[#2B2C31] dark:text-gray-200 text-[11px] font-bold border border-[#E5E7EB] dark:border-gray-700 transition-colors cursor-pointer"
                            >
                              <Eye className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
                              <span>Xem danh sách ({countUsers})</span>
                            </button>
                          </td>
                          
                          <td className="py-3.5 px-3 text-center">
                            <div className="relative inline-flex items-center justify-center">
                              {isCompleted ? (
                                <CheckCircle2 className="w-2.5 h-2.5 shrink-0 absolute left-2 pointer-events-none z-10" style={{ color: CHIP_SUCCESS_TEXT }} />
                              ) : (
                                <Clock className="w-2.5 h-2.5 shrink-0 absolute left-2 pointer-events-none z-10" style={{ color: CHIP_WARNING_TEXT }} />
                              )}

                              <select
                                value={task.status || "Đang thực hiện"}
                                onChange={(e) => handleUpdateTaskStatus(task.id, e.target.value)}
                                style={{
                                  backgroundColor: isCompleted ? CHIP_SUCCESS_BG : CHIP_WARNING_BG,
                                  borderColor: isCompleted ? CHIP_SUCCESS_BORDER : CHIP_WARNING_BORDER,
                                  color: isCompleted ? CHIP_SUCCESS_TEXT : CHIP_WARNING_TEXT
                                }}
                                className="appearance-none inline-flex items-center pl-5 pr-5 py-0.5 rounded-full text-[9.5px] font-bold border cursor-pointer focus:outline-none transition-all leading-tight"
                              >
                                <option value="Đang thực hiện" className="bg-white dark:bg-[#1C1D22] text-amber-600 font-bold">
                                  Đang thực hiện
                                </option>
                                <option value="Hoàn thành" className="bg-white dark:bg-[#1C1D22] text-emerald-600 font-bold">
                                  Hoàn thành
                                </option>
                              </select>

                              <ChevronDown 
                                style={{ color: isCompleted ? CHIP_SUCCESS_TEXT : CHIP_WARNING_TEXT }}
                                className="w-2.5 h-2.5 absolute right-1.5 pointer-events-none z-10" 
                              />
                            </div>
                          </td>

                          {/* CỘT THAO TÁC SỬA / XÓA NHIỆM VỤ */}
                          <td className="py-3.5 px-3 text-center">
                            <div className="flex items-center justify-center gap-1">
                              <button
                                type="button"
                                onClick={() => handleOpenEditTaskModal(task)}
                                title="Sửa nhiệm vụ"
                                className="p-1 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md hover:text-gray-900 dark:hover:text-gray-100 transition-colors cursor-pointer dark:text-gray-400"
                              >
                                <Edit3 className="w-3.5 h-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={() => handleRequestDeleteTask(task)}
                                title="Xóa nhiệm vụ"
                                className="p-1 text-gray-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-md hover:text-red-600 dark:hover:text-[#E55353] transition-colors cursor-pointer dark:text-gray-400"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>

        </div>

      </div>
    </div>
  );
}