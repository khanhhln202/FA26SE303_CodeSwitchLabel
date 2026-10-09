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
  Tag,
  Award,
  AlertCircle,
  FolderKanban,
  Ban
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
import { taskService } from "../../../services/taskService";

// Helper format ngày
const formatDateToVN = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("/")) return dateStr;
  const [year, month, day] = dateStr.split("T")[0].split("-");
  return `${day}/${month}/${year}`;
};

const formatDateToISO = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("-")) return dateStr.split("T")[0];
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
    target: 2000,
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

  // 3. Modal SỬA / XÓA NHIỆM VỤ TRONG BẢNG
  const [isEditTaskModalOpen, setIsEditTaskModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [editTaskFormData, setEditTaskFormData] = useState({
    title: "",
    role: "Speaker",
    assignedUsers: []
  });
  const [deletingTask, setDeletingTask] = useState(null);

  // Danh sách Đợt từ API
  const [batches, setBatches] = useState([]);
  const [selectedBatchId, setSelectedBatchId] = useState(null);

  // Danh sách nhiệm vụ từ API
  const [assignTasks, setAssignTasks] = useState([]);
  const [batchMapping, setBatchMapping] = useState({});

  // Gọi API kết nối Backend
  const loadDataFromApi = async () => {
    try {
      // Bắt riêng lỗi 403 của getUsers để không làm ngắt đứt luồng tải Task/Campaign
      const [campaignRes, taskRes, userRes] = await Promise.all([
        taskService.getCampaigns().catch(() => ({ items: [] })),
        taskService.getTasks().catch(() => ({ items: [] })),
        taskService.getUsers().catch((err) => {
          console.warn("Không có quyền lấy danh sách users (403):", err);
          return { items: [] };
        })
      ]);

      const campaignList = (campaignRes?.items || campaignRes?.data || []).map((c) => ({
        id: c.campaignId || c.id,
        name: c.campaignName || c.title || c.name || `Đợt ${c.campaignId}`,
        campaignName: c.campaignName || c.title || c.name || `Đợt ${c.campaignId}`,
        target: c.targetQty || c.targetCount || c.target || 2000,
        speakerCount: c.speakerCount || 5,
        reviewerCount: c.reviewerCount || 2,
        topic: c.domain === "ItTechnology" ? "Công nghệ thông tin" :
               c.domain === "Education" ? "Giáo dục" : "Hội thoại hàng ngày",
        startDate: formatDateToVN(c.startDate || todayIso),
        endDate: formatDateToVN(c.endDate || todayIso),
        status: c.status || "Draft",
        assignedSpeakers: [],
        assignedReviewers: []
      }));

      const rawTasks = taskRes?.items || taskRes?.data || [];

      const mappedTasks = rawTasks.map((t) => {
        const rawStatus = String(t.status || t.taskStatus || t.state || "").toLowerCase();
        
        let calculatedStatus = "Đang thực hiện";
        if (rawStatus === "completed" || rawStatus === "hoàn thành" || rawStatus === "done" || rawStatus === "1") {
          calculatedStatus = "Hoàn thành";
        } else if (rawStatus === "cancelled" || rawStatus === "canceled" || rawStatus === "đã hủy" || rawStatus === "3") {
          calculatedStatus = "Đã hủy";
        }

        return {
          id: t.taskId || t.id,
          taskId: t.taskId || t.id,
          title: t.title || t.description || "Nhiệm vụ",
          role: t.taskType === "Recording" ? "Speaker" : "Reviewer",
          campaignId: t.campaignId,
          assignedUsers: t.assigneeName ? [t.assigneeName] : [],
          status: calculatedStatus
        };
      });

      const rawUsers = userRes?.items || userRes?.data || [];
      const mappedUsers = rawUsers.map((u) => ({
        id: u.userId || u.id,
        name: u.fullName || u.userName || u.name,
        email: u.email,
        role: u.roleName || (u.role === "Recording" ? "Speaker" : u.role),
        status: "Active",
        performanceTag: u.performanceTag || "GOOD"
      }));

      setBatches(campaignList);
      if (campaignList.length > 0 && !selectedBatchId) {
        setSelectedBatchId(campaignList[0].id);
      }

      setAssignTasks(mappedTasks);
      setAllAdminUsers(mappedUsers);

      const mapping = {};
      mappedTasks.forEach((t) => {
        if (t.campaignId) {
          if (!mapping[t.campaignId]) mapping[t.campaignId] = [];
          mapping[t.campaignId].push(t.id);
        }
      });
      setBatchMapping(mapping);

      setSpeakerTasksList(mappedTasks.filter((t) => t.role === "Speaker"));
      setReviewerTasksList(mappedTasks.filter((t) => t.role === "Reviewer"));
    } catch (error) {
      console.error("Lỗi khi kết nối API:", error);
    }
  };

  useEffect(() => {
    loadDataFromApi();
  }, []);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => setToast((prev) => ({ ...prev, show: false })), 3000);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  // Cập nhật Trạng thái nhiệm vụ
  const handleUpdateTaskStatus = (taskId, newStatus) => {
    let backendStatus = "InProgress";
    if (newStatus === "Hoàn thành") backendStatus = "Completed";
    if (newStatus === "Đã hủy") backendStatus = "Cancelled";

    setAssignTasks((prevTasks) =>
      prevTasks.map((t) => (t.id === taskId ? { ...t, status: newStatus } : t))
    );

    showNotification(`Đã cập nhật trạng thái thành "${newStatus}"`);

    const currentTask = assignTasks.find((t) => t.id === taskId);
    taskService.updateTask(taskId, {
      taskId: taskId,
      title: currentTask?.title || "Nhiệm vụ",
      description: currentTask?.title || "Nhiệm vụ",
      status: backendStatus,
      campaignId: currentTask?.campaignId
    }).catch((e) => {
      console.warn("Lỗi API ngầm updateTask:", e?.response?.data || e.message);
    });
  };

  // Lấy danh sách Speaker & Reviewer Active
  const activeSpeakers = useMemo(() => {
    const speakers = allAdminUsers.filter(u => u.role === "Speaker" && u.status === "Active");
    return [...speakers].sort((a, b) => {
      const order = { GOOD: 1, NORMAL: 2, BAD: 3 };
      const rankA = order[a.performanceTag] || 2;
      const rankB = order[b.performanceTag] || 2;
      return rankA - rankB;
    });
  }, [allAdminUsers]);

  const activeReviewers = useMemo(() => {
    return allAdminUsers.filter(u => u.role === "Reviewer" && u.status === "Active");
  }, [allAdminUsers]);

  // Tổng hợp tất cả nhiệm vụ
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

  // Lọc danh sách Nhiệm vụ theo Topic của Đợt
  const filteredTasksForAssignModal = useMemo(() => {
    if (!assignModalSelectedBatch) return allTasksOptions;
    return allTasksOptions.filter(t => t.topic === assignModalSelectedBatch.topic || !t.topic);
  }, [allTasksOptions, assignModalSelectedBatch]);

  const currentSelectedTask = useMemo(() => {
    return filteredTasksForAssignModal.find(t => t.uniqueKey === assignFormData.selectedUniqueKey) || filteredTasksForAssignModal[0];
  }, [assignFormData.selectedUniqueKey, filteredTasksForAssignModal]);

  const targetUserList = useMemo(() => {
    return currentSelectedTask?.role === "Speaker" ? activeSpeakers : activeReviewers;
  }, [currentSelectedTask, activeSpeakers, activeReviewers]);

  // Giới hạn số lượng tối đa
  const maxAllowedUsers = useMemo(() => {
    if (!assignModalSelectedBatch || !currentSelectedTask) return Infinity;
    return currentSelectedTask.role === "Speaker"
      ? Number(assignModalSelectedBatch.speakerCount || 0)
      : Number(assignModalSelectedBatch.reviewerCount || 0);
  }, [assignModalSelectedBatch, currentSelectedTask]);

  // Mở Modal PHÂN CÔNG NHIỆM VỤ MỚI
  const handleOpenAssignModal = () => {
    if (batches.length === 0) {
      showNotification("Vui lòng tạo đợt trước khi thực hiện phân công nhiệm vụ!");
      return;
    }

    setEditingAssignment(null);
    const initialBatch = batches.find(b => b.id === selectedBatchId) || batches[0] || null;
    const initialBatchTopic = initialBatch?.topic;
    const availableTasks = initialBatchTopic 
      ? allTasksOptions.filter(t => t.topic === initialBatchTopic || !t.topic) 
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
      ? allTasksOptions.filter(t => t.topic === nextBatch.topic || !t.topic)
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

  // Xác nhận lưu Modal Phân Công (ĐÃ CHỈNH CHO PHÉP PHÂN CÔNG KHÔNG CẦN CHỌN USER)
  const handleSubmitAssignForm = async (e) => {
    e.preventDefault();

    if (assignFormData.assignedUsers.length > maxAllowedUsers) {
      alert(`Số lượng người chọn (${assignFormData.assignedUsers.length}) lớn hơn số lượng đợt yêu cầu (${maxAllowedUsers})!`);
      return;
    }

    try {
      const taskObj = currentSelectedTask;
      const targetBatchId = assignFormData.selectedBatchId;

      if (!taskObj?.id) {
        showNotification("Vui lòng chọn nhiệm vụ hợp lệ!");
        return;
      }

      // 1. Cập nhật gán Task vào Đợt (campaignId)
      await taskService.updateTask(taskObj.id, {
        campaignId: Number(targetBatchId)
      });

      // 2. Nếu có chọn người dùng thì thực hiện gán user
      if (assignFormData.assignedUsers.length > 0) {
        const selectedUserObj = targetUserList.find(u => assignFormData.assignedUsers.includes(u.name));
        if (selectedUserObj) {
          await taskService.assignTask(taskObj.id, selectedUserObj.id).catch(err => {
            console.warn("Lỗi ngầm assignTask:", err);
          });
        }
      }

      showNotification(`Đã phân công nhiệm vụ "${taskObj.title}" vào đợt thành công!`);
      setIsAssignModalOpen(false);
      loadDataFromApi();
    } catch (err) {
      console.error("Lỗi khi phân công nhiệm vụ vào đợt:", err);
      showNotification("Phân công thất bại!");
    }
  };

  // Mở Modal TẠO ĐỢT MỚI
  const handleOpenCreateModal = () => {
    setModalMode("CREATE");
    setModalTargetBatchId(null);
    setBatchFormData({
      name: `Đợt thu thập demo ${Math.floor(Math.random() * 1000)}`,
      target: 2000,
      speakerCount: activeSpeakers.length || 5,
      reviewerCount: activeReviewers.length || 2,
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
    setModalMode("UPDATE");
    setModalTargetBatchId(batch.id);
    setSelectedBatchId(batch.id);
    setBatchFormData({
      name: batch.campaignName || batch.name || "",
      target: batch.target || 2000,
      speakerCount: batch.speakerCount ?? activeSpeakers.length ?? 5,
      reviewerCount: batch.reviewerCount ?? activeReviewers.length ?? 2,
      topic: batch.topic || "Công nghệ thông tin",
      startDate: batch.startDate ? formatDateToISO(batch.startDate) : todayIso,
      endDate: batch.endDate ? formatDateToISO(batch.endDate) : todayIso,
      assignedSpeakers: batch.assignedSpeakers || [],
      assignedReviewers: batch.assignedReviewers || []
    });
    setIsModalOpen(true);
  };

  // Xác nhận lưu Đợt qua API Backend (ĐÃ FIX SỬA BỎ CÁC TRƯỜNG THIẾU BỊ TỪ CHỐI)
  const handleConfirmSaveModal = async (e) => {
    if (e) e.preventDefault();

    if (!batchFormData.name || !batchFormData.name.trim()) {
      alert("Vui lòng nhập tên đợt!");
      return;
    }

    try {
      let rawStart = batchFormData.startDate || todayIso;
      let rawEnd = batchFormData.endDate || todayIso;

      if (rawStart.includes("/")) rawStart = formatDateToISO(rawStart);
      if (rawEnd.includes("/")) rawEnd = formatDateToISO(rawEnd);

      const startIso = rawStart.split("T")[0];
      const endIso = rawEnd.split("T")[0];

      if (endIso < startIso) {
        alert("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu!");
        return;
      }

      const domainMap = {
        "Công nghệ thông tin": "ItTechnology",
        "Giáo dục": "Education",
        "Hội thoại hàng ngày": "DailyLife"
      };

      if (modalMode === "CREATE") {
        await taskService.createCampaign({
        name: String(batchFormData.name), // Thêm trường name chuẩn
        campaignName: String(batchFormData.name),
        title: String(batchFormData.name),
        domain: domainMap[batchFormData.topic] || "ItTechnology",
        targetQty: Number(batchFormData.target),
        targetCount: Number(batchFormData.target),
        speakerCount: Number(batchFormData.speakerCount || 1),
        reviewerCount: Number(batchFormData.reviewerCount || 1),
        startDate: `${startIso}T00:00:00.000Z`,
        endDate: `${endIso}T00:00:00.000Z`
      });
        showNotification(`Đã tạo thành công ${batchFormData.name}!`);
      } else {
        await taskService.updateCampaign(modalTargetBatchId, {
          title: String(batchFormData.name),
          campaignName: String(batchFormData.name),
          domain: domainMap[batchFormData.topic] || "ItTechnology",
          targetQty: Number(batchFormData.target),
          targetCount: Number(batchFormData.target),
          speakerCount: Number(batchFormData.speakerCount || 1),
          reviewerCount: Number(batchFormData.reviewerCount || 1),
          startDate: `${startIso}T00:00:00.000Z`,
          endDate: `${endIso}T00:00:00.000Z`
        });
        showNotification(`Đã cập nhật ${batchFormData.name}!`);
      }

      setIsModalOpen(false);
      loadDataFromApi();
    } catch (err) {
      console.error("Lỗi API tạo/sửa đợt:", err?.response?.data || err);
      const apiErrorMsg = err?.response?.data?.message || err?.response?.data?.title || "Lưu đợt thất bại!";
      showNotification(apiErrorMsg);
    }
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

    if (selectedBatchId === deletingBatch.id) {
      setSelectedBatchId(remainingBatches.length > 0 ? remainingBatches[0].id : null);
    }

    showNotification(`Đã xóa thành công ${deletingBatch.name}!`);
    setDeletingBatch(null);
  };

  // SỬA & XÓA NHIỆM VỤ TRONG BẢNG
  const handleOpenEditTaskModal = (task) => {
    setEditingTask(task);
    setEditTaskFormData({
      title: task.title || "",
      role: task.role || "Speaker",
      assignedUsers: task.assignedUsers || []
    });
    setIsEditTaskModalOpen(true);
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

  const handleSaveEditTask = async (e) => {
    e.preventDefault();

    if (!editTaskFormData.title.trim()) {
      alert("Tên nhiệm vụ không được để trống!");
      return;
    }

    try {
      await taskService.updateTask(editingTask.id, {
        description: editTaskFormData.title
      });

      showNotification(`Đã cập nhật nhiệm vụ "${editTaskFormData.title}"!`);
      setIsEditTaskModalOpen(false);
      loadDataFromApi();
    } catch (err) {
      showNotification("Cập nhật nhiệm vụ thất bại!");
    }
  };

  const handleRequestDeleteTask = (task) => {
    setDeletingTask(task);
  };

  const handleConfirmDeleteTask = async () => {
    if (!deletingTask) return;
    const targetId = deletingTask.id || deletingTask.taskId;

    try {
      await taskService.updateTask(targetId, {
        campaignId: null
      });

      setAssignTasks((prev) =>
        prev.map((t) => (t.id === targetId ? { ...t, campaignId: null } : t))
      );

      showNotification(`Đã gỡ nhiệm vụ "${deletingTask.title}" khỏi đợt thành công!`);
      setDeletingTask(null);
    } catch (err) {
      console.warn("Lỗi API updateTask gỡ đợt, tiến hành gỡ khỏi UI đợt:", err?.response?.data || err);
      setAssignTasks((prev) =>
        prev.map((t) => (t.id === targetId ? { ...t, campaignId: null } : t))
      );
      showNotification(`Đã gỡ nhiệm vụ "${deletingTask.title}" khỏi đợt!`);
      setDeletingTask(null);
    }
  };

  // Lấy danh sách nhiệm vụ thuộc đợt đang chọn
  const currentBatchTaskIds = useMemo(() => {
    if (!selectedBatchId) return [];
    return batchMapping[selectedBatchId] || [];
  }, [batchMapping, selectedBatchId]);

  const currentTasks = useMemo(() => {
    if (!selectedBatchId) return assignTasks;
    return assignTasks.filter((t) => t.campaignId === selectedBatchId || currentBatchTaskIds.includes(t.id));
  }, [assignTasks, selectedBatchId, currentBatchTaskIds]);

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
                  {modalMode === "CREATE" ? "Tạo đợt nhiệm vụ mới" : `Chỉnh sửa ${targetModalBatchObj?.campaignName || targetModalBatchObj?.name || "đợt"}`}
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="p-1 hover:bg-gray-100 dark:hover:bg-[#25272E] rounded-lg transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-gray-500 dark:text-gray-400" />
                </button>
              </div>

              {/* Tên Đợt (campaignName) */}
              <div>
                <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase mb-1">
                  Tên đợt (Campaign Name)
                </label>
                <input
                  type="text"
                  required
                  value={batchFormData.name}
                  onChange={(e) => setBatchFormData({ ...batchFormData, name: e.target.value })}
                  placeholder="Nhập tên đợt..."
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

              {/* SỐ LƯỢNG SPEAKERS VÀ REVIEWERS */}
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
                  onClick={handleConfirmSaveModal}
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
                        {b.campaignName || b.name} - Số lượng(tối đa): {b.speakerCount} speakers, {b.reviewerCount} reviewers
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="w-3.5 h-3.5 absolute right-2.5 pointer-events-none text-gray-400 dark:text-gray-400" />
                </div>
              </div>
              
              {/* Select Chọn Nhiệm Vụ */}
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

              {/* DANH SÁCH SPEAKER / REVIEWER CHỌN (TÙY CHỌN, KHÔNG BẮT BUỘC) */}
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="block text-[10px] font-bold uppercase text-gray-500 dark:text-gray-400">
                    {currentSelectedTask?.role} đang hoạt động ({assignFormData.assignedUsers.length}/{maxAllowedUsers} tối đa - Không bắt buộc)
                  </label>
                  <span 
                    style={{ backgroundColor: currentSelectedTask?.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                    className="text-[9px] text-white px-1.5 py-0.2 rounded font-bold"
                  >
                    {currentSelectedTask?.role}
                  </span>
                </div>

                <div className="max-h-52 overflow-y-auto border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] rounded-lg p-2 space-y-1.5">
                  {targetUserList.length > 0 ? (
                    targetUserList.map((u) => {
                      const isChecked = assignFormData.assignedUsers.includes(u.name);
                      const isDisableUnchecked = !isChecked && assignFormData.assignedUsers.length >= maxAllowedUsers;
                      const hasPreferenceReason = currentSelectedTask?.role === "Speaker" && 
                                                  u.performanceReason && 
                                                  (u.performanceTag === "GOOD" || u.performanceTag === "BAD");

                      return (
                        <label 
                          key={u.id} 
                          className={`flex items-start justify-between p-2 rounded-lg text-xs transition-all border ${
                            isDisableUnchecked 
                              ? "opacity-50 cursor-not-allowed text-gray-400 dark:text-gray-500 border-transparent" 
                              : "cursor-pointer"
                          } ${
                            isChecked 
                              ? "bg-white dark:bg-[#1C1D22] border-gray-400 dark:border-gray-500 shadow-xs font-bold text-gray-900 dark:text-white" 
                              : "bg-white/50 dark:bg-[#1C1D22]/50 border-gray-200 dark:border-gray-700 hover:bg-black/5 dark:hover:bg-white/5 text-gray-600 dark:text-gray-300"
                          }`}
                        >
                          <div className="flex items-start gap-2 min-w-0 flex-1">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              disabled={isDisableUnchecked}
                              onChange={() => handleUserCheckboxToggle(u.name)}
                              style={{ accentColor: TASK_MANAGER_ACCENT }}
                              className="rounded focus:ring-0 cursor-pointer disabled:cursor-not-allowed shrink-0 mt-0.5"
                            />
                            <div className="flex flex-col min-w-0 pr-1">
                              <span className="truncate leading-tight">{u.name}</span>
                              <span className="text-[10px] font-normal text-gray-400 dark:text-gray-400 truncate">
                                {u.email}
                              </span>
                              
                              {hasPreferenceReason && (
                                <span className="text-[9.5px] font-semibold mt-0.5  text-gray-500 dark:text-gray-400 line-clamp-1">
                                  Lý do: {u.performanceReason}
                                </span>
                              )}
                            </div>
                          </div>

                          {currentSelectedTask?.role === "Speaker" && (
                            <div className="flex items-center gap-1 shrink-0 ml-1 mt-0.5">
                              {u.performanceTag === "GOOD" && (
                                <span 
                                  style={{
                                    backgroundColor: CHIP_SUCCESS_BG,
                                    borderColor: CHIP_SUCCESS_BORDER,
                                    color: CHIP_SUCCESS_TEXT
                                  }}
                                  className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[9px] font-bold border"
                                >
                                  <Award className="w-2.5 h-2.5 shrink-0" />
                                  <span>Ưu tiên</span>
                                </span>
                              )}

                              {u.performanceTag === "BAD" && (
                                <span 
                                  style={{
                                    backgroundColor: "rgba(243, 114, 127, 0.15)",
                                    borderColor: "rgba(243, 114, 127, 0.4)",
                                    color: DANGER
                                  }}
                                  className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[9px] font-bold border"
                                >
                                  <AlertCircle className="w-2.5 h-2.5 shrink-0" />
                                  <span>Hạn chế</span>
                                </span>
                              )}
                            </div>
                          )}
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
                  Bạn có chắc muốn xóa <span className="font-bold text-gray-900 dark:text-gray-200">"{deletingBatch.campaignName || deletingBatch.name}"</span>? 
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
                  Chỉnh sửa phân công nhiệm vụ
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
                  value={editTaskFormData.title}
                  onChange={(e) => setEditTaskFormData({ ...editTaskFormData, title: e.target.value })}
                  className="w-full px-3 py-1.5 text-xs border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] text-gray-900 dark:text-white rounded-lg outline-none font-medium transition-all font-sans focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500"
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

                  const userList = editTaskFormData.role === "Speaker" ? activeSpeakers : activeReviewers;

                  return (
                    <>
                      <div className="flex items-center justify-between mb-1">
                        <label className="block text-[10px] font-bold text-gray-500 dark:text-gray-400 uppercase">
                          {editTaskFormData.role} đang hoạt động ({editTaskFormData.assignedUsers.length}/{maxUsers} tối đa)
                        </label>
                        <span 
                          style={{ backgroundColor: editTaskFormData.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                          className="text-[9px] text-white px-1.5 py-0.2 rounded font-bold"
                        >
                          {editTaskFormData.role}
                        </span>
                      </div>

                      <div className="max-h-52 overflow-y-auto border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-[#25272E] rounded-lg p-2 space-y-1.5">
                        {userList.map((u) => {
                          const isChecked = editTaskFormData.assignedUsers.includes(u.name);
                          const isDisableUnchecked = !isChecked && editTaskFormData.assignedUsers.length >= maxUsers;
                          const hasPreferenceReason = editTaskFormData.role === "Speaker" && 
                                                      u.performanceReason && 
                                                      (u.performanceTag === "GOOD" || u.performanceTag === "BAD");

                          return (
                            <label 
                              key={u.id}
                              className={`flex items-start justify-between p-2 rounded-lg text-xs transition-all border ${
                                isDisableUnchecked 
                                  ? "opacity-50 cursor-not-allowed text-gray-400 dark:text-gray-500 border-transparent" 
                                  : "cursor-pointer"
                              } ${
                                isChecked 
                                  ? "bg-white dark:bg-[#1C1D22] border-gray-400 dark:border-gray-500 shadow-xs font-bold text-gray-900 dark:text-white" 
                                  : "bg-white/50 dark:bg-[#1C1D22]/50 border-gray-200 dark:border-gray-700 hover:bg-black/5 dark:hover:bg-white/5 text-gray-600 dark:text-gray-300"
                              }`}
                            >
                              <div className="flex items-start gap-2 min-w-0 flex-1">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  disabled={isDisableUnchecked}
                                  onChange={() => handleToggleUserInEditTask(u.name)}
                                  style={{ accentColor: TASK_MANAGER_ACCENT }}
                                  className="rounded focus:ring-0 cursor-pointer disabled:cursor-not-allowed shrink-0 mt-0.5"
                                />
                                <div className="flex flex-col min-w-0 pr-1">
                                  <span className="truncate leading-tight">{u.name}</span>
                                  <span className="text-[10px] font-normal text-gray-400 dark:text-gray-400 truncate">
                                    {u.email}
                                  </span>

                                  {hasPreferenceReason && (
                                    <span className="text-[9.5px] font-semibold mt-0.5  text-gray-500 dark:text-gray-400 line-clamp-1">
                                      Lý do: {u.performanceReason}
                                    </span>
                                  )}
                                </div>
                              </div>

                              {editTaskFormData.role === "Speaker" && (
                                <div className="flex items-center gap-1 shrink-0 ml-1 mt-0.5">
                                  {u.performanceTag === "GOOD" && (
                                    <span 
                                      style={{
                                        backgroundColor: CHIP_SUCCESS_BG,
                                        borderColor: CHIP_SUCCESS_BORDER,
                                        color: CHIP_SUCCESS_TEXT
                                      }}
                                      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[9px] font-bold border"
                                    >
                                      <Award className="w-2.5 h-2.5 shrink-0" />
                                      <span>Ưu tiên</span>
                                    </span>
                                  )}

                                  {u.performanceTag === "BAD" && (
                                    <span 
                                      style={{
                                        backgroundColor: "rgba(243, 114, 127, 0.15)",
                                        borderColor: "rgba(243, 114, 127, 0.4)",
                                        color: DANGER
                                      }}
                                      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[9px] font-bold border"
                                    >
                                      <AlertCircle className="w-2.5 h-2.5 shrink-0" />
                                      <span>Hạn chế</span>
                                    </span>
                                  )}
                                </div>
                              )}
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
                const batchTasks = assignTasks.filter((t) => t.campaignId === batch.id || mappedIds.includes(t.id));

                const isBatchCompleted = batchTasks.length > 0 && batchTasks.every((t) => t.status === "Hoàn thành");
                // Đợt chưa có nhiệm vụ nào HOẶC status là "Open" thì là "Đang mở"
                const isOpenStatus = batchTasks.length === 0 || batch.status === "Open";

                // Màu sắc động cho Badge của Đợt
                const batchBadgeBg = isBatchCompleted 
                  ? CHIP_SUCCESS_BG 
                  : isOpenStatus 
                    ? `${TASK_MANAGER_ACCENT}1A` 
                    : CHIP_WARNING_BG;

                const batchBadgeBorder = isBatchCompleted 
                  ? CHIP_SUCCESS_BORDER 
                  : isOpenStatus 
                    ? `${TASK_MANAGER_ACCENT}50` 
                    : CHIP_WARNING_BORDER;

                const batchBadgeText = isBatchCompleted 
                  ? CHIP_SUCCESS_TEXT 
                  : isOpenStatus 
                    ? TASK_MANAGER_ACCENT 
                    : CHIP_WARNING_TEXT;

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
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0 flex-1">
                        <span className="text-xs font-bold text-gray-900 dark:text-white truncate">
                          Đợt {batch.id}
                        </span>
                        
                        <span 
                          style={{
                            backgroundColor: batchBadgeBg,
                            borderColor: batchBadgeBorder,
                            color: batchBadgeText
                          }}
                          className="inline-flex items-center gap-1 pl-1.5 pr-2 py-0.5 rounded-full text-[9px] font-bold border shrink-0 leading-tight"
                        >
                          {isBatchCompleted ? (
                            <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: batchBadgeText }} />
                          ) : (
                            <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: batchBadgeText }} />
                          )}
                          <span>{isBatchCompleted ? "Hoàn thành" : isOpenStatus ? "Đang mở" : "Đang thực hiện"}</span>
                        </span>
                      </div>

                      <div className="flex items-center gap-0.5 shrink-0">
                        <button
                          type="button"
                          onClick={(e) => handleOpenUpdateModal(e, batch)}
                          title={`Sửa ${batch.campaignName || batch.name}`}
                          className="p-1 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md hover:text-gray-900 dark:hover:text-gray-100 transition-colors cursor-pointer dark:text-gray-400"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>

                        <button
                          type="button"
                          onClick={(e) => handleRequestDeleteBatch(e, batch)}
                          title={`Xóa ${batch.campaignName || batch.name}`}
                          className="p-1 text-gray-500 hover:bg-red-50 dark:hover:bg-red-900/30 rounded-md hover:text-red-600 dark:hover:text-[#E55353] transition-colors cursor-pointer dark:text-gray-400"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="text-[10px] space-y-1 text-gray-500 dark:text-gray-400 border-t border-gray-100 dark:border-gray-800/60 pt-1.5">
                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <FolderKanban className="w-3 h-3 text-indigo-500 shrink-0" />
                        <span className="truncate">Tên đợt: <strong className="text-gray-900 dark:text-white font-bold">{batch.campaignName || batch.name}</strong></span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Tag className="w-3 h-3 text-emerald-500 shrink-0" />
                        <span className="truncate">Chủ đề: <strong className="text-gray-900 dark:text-white font-bold">{batch.topic || "Công nghệ thông tin"}</strong></span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-gray-700 dark:text-gray-300">
                        <Target className="w-3 h-3 text-blue-500 shrink-0" />
                        <span>Mục tiêu: <strong className="text-gray-900 dark:text-white font-bold">{batch.target || 2000}</strong> câu</span>
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
                <option value="Đã hủy" className="bg-white dark:bg-[#25272E]">Đã hủy</option>
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
                          : `Không tìm thấy nhiệm vụ nào trong ${selectedBatchObj.campaignName || selectedBatchObj.name}.`}
                      </td>
                    </tr>
                  ) : (
                    filteredTasks.map((task, index) => {
                      const isCompleted = task.status === "Hoàn thành";
                      const isCancelled = task.status === "Đã hủy";
                      const countUsers = task.assignedUsers ? task.assignedUsers.length : 0;

                      // Style động cho dropdown trạng thái
                      const statusBg = isCompleted 
                        ? CHIP_SUCCESS_BG 
                        : isCancelled 
                          ? "rgba(243, 114, 127, 0.15)" 
                          : CHIP_WARNING_BG;

                      const statusBorder = isCompleted 
                        ? CHIP_SUCCESS_BORDER 
                        : isCancelled 
                          ? "rgba(243, 114, 127, 0.4)" 
                          : CHIP_WARNING_BORDER;

                      const statusText = isCompleted 
                        ? CHIP_SUCCESS_TEXT 
                        : isCancelled 
                          ? DANGER 
                          : CHIP_WARNING_TEXT;

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
                                <CheckCircle2 className="w-2.5 h-2.5 shrink-0 absolute left-2 pointer-events-none z-10" style={{ color: statusText }} />
                              ) : isCancelled ? (
                                <Ban className="w-2.5 h-2.5 shrink-0 absolute left-2 pointer-events-none z-10" style={{ color: statusText }} />
                              ) : (
                                <Clock className="w-2.5 h-2.5 shrink-0 absolute left-2 pointer-events-none z-10" style={{ color: statusText }} />
                              )}

                              <select
                                value={task.status || "Đang thực hiện"}
                                onChange={(e) => handleUpdateTaskStatus(task.id, e.target.value)}
                                style={{
                                  backgroundColor: statusBg,
                                  borderColor: statusBorder,
                                  color: statusText
                                }}
                                className="appearance-none inline-flex items-center pl-5 pr-5 py-0.5 rounded-full text-[9.5px] font-bold border cursor-pointer focus:outline-none transition-all leading-tight"
                              >
                                <option value="Đang thực hiện" className="bg-white dark:bg-[#1C1D22] text-amber-600 font-bold">
                                  Đang thực hiện
                                </option>
                                <option value="Hoàn thành" className="bg-white dark:bg-[#1C1D22] text-emerald-600 font-bold">
                                  Hoàn thành
                                </option>
                                <option value="Đã hủy" className="bg-white dark:bg-[#1C1D22] text-rose-600 font-bold">
                                  Đã hủy
                                </option>
                              </select>

                              <ChevronDown 
                                style={{ color: statusText }}
                                className="w-2.5 h-2.5 absolute right-1.5 pointer-events-none z-10" 
                              />
                            </div>
                          </td>

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