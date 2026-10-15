import React, { useState, useEffect, useMemo } from "react";
import { toast } from "sonner";
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

// Helper format ngày hiển thị VN (dd/mm/yyyy)
const formatDateToVN = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("/")) return dateStr;
  const clean = dateStr.split("T")[0];
  const [year, month, day] = clean.split("-");
  if (!year || !month || !day) return "";
  return `${day}/${month}/${year}`;
};

// Helper format ngày ISO (yyyy-mm-dd)
const formatDateToISO = (dateStr) => {
  if (!dateStr) return "";
  if (dateStr.includes("-")) return dateStr.split("T")[0];
  const parts = dateStr.split("/");
  if (parts.length === 3) {
    const [day, month, year] = parts;
    return `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`;
  }
  return "";
};

// Helper chuyển đổi Domain/Topic từ BE sang tên tiếng Việt
const mapDomainToTopicVN = (domain) => {
  if (domain === "ItTechnology" || domain === "IT" || domain === "Công nghệ thông tin") return "Công nghệ thông tin";
  if (domain === "Education" || domain === "Giáo dục") return "Giáo dục";
  if (domain === "DailyLife" || domain === "Hội thoại hàng ngày") return "Hội thoại hàng ngày";
  return domain || "Công nghệ thông tin";
};

// Component Custom Date Picker hiển thị chuẩn DD/MM/YYYY cho UI người dùng
const VNFormatDatePicker = ({ label, value, onChange, minDateIso }) => {
  const displayValue = useMemo(() => {
    if (!value) return "";
    return value.includes("-") ? formatDateToVN(value) : value;
  }, [value]);

  const handleInputChange = (e) => {
    let input = e.target.value.replace(/\D/g, "");
    if (input.length > 8) input = input.slice(0, 8);

    let formatted = input;
    if (input.length >= 5) {
      formatted = `${input.slice(0, 2)}/${input.slice(2, 4)}/${input.slice(4)}`;
    } else if (input.length >= 3) {
      formatted = `${input.slice(0, 2)}/${input.slice(2)}`;
    }

    if (input.length === 10) {
      const iso = formatDateToISO(formatted);
      onChange(iso || formatted);
    } else {
      onChange(formatted);
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
      <label className="block type-label text-[#2B2C31] dark:text-gray-300 flex items-center gap-1">
        <Calendar className="w-3.5 h-3.5 text-[#6E7078]" /> {label}
      </label>

      <div className="relative flex items-center">
        <input
          type="text"
          placeholder="dd/mm/yyyy"
          value={displayValue}
          onChange={handleInputChange}
          maxLength={10}
          className="w-full h-11 pl-3.5 pr-8 py-2.5 type-body bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl outline-none font-sans text-[#16171C] dark:text-gray-100 transition-all"
          onFocus={(e) => {
            e.target.style.borderColor = TASK_MANAGER_ACCENT;
            e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
          }}
          onBlur={(e) => {
            e.target.style.borderColor = '';
            e.target.style.boxShadow = 'none';
          }}
        />

        <div className="absolute right-3 flex items-center justify-center cursor-pointer">
          <Calendar className="w-4 h-4 text-[#9A9CA3] pointer-events-none" />
          <input
            type="date"
            min={minDateIso}
            value={value && value.includes("-") ? value.split("T")[0] : ""}
            onChange={handleNativePickerChange}
            className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
            onFocus={(e) => {
              const parentInput = e.target.parentElement.previousElementSibling;
              if (parentInput) {
                parentInput.style.borderColor = TASK_MANAGER_ACCENT;
                parentInput.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
              }
            }}
            onBlur={(e) => {
              const parentInput = e.target.parentElement.previousElementSibling;
              if (parentInput) {
                parentInput.style.borderColor = '';
                parentInput.style.boxShadow = 'none';
              }
            }}
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

  const todayIso = useMemo(() => new Date().toISOString().split("T")[0], []);

  // 1. Trạng thái Modal Tạo/Cập nhật Đợt
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState("CREATE");
  const [modalTargetBatchId, setModalTargetBatchId] = useState(null);

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

  const [batches, setBatches] = useState([]);
  const [selectedBatchId, setSelectedBatchId] = useState(null);

  const [assignTasks, setAssignTasks] = useState([]);
  const [batchMapping, setBatchMapping] = useState({});

  const loadDataFromApi = async () => {
    try {
      const [campaignRes, taskRes, speakerUsersRes, reviewerUsersRes] = await Promise.all([
        taskService.getCampaigns({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getAssignableUsers("Recording").catch(() => []),
        taskService.getAssignableUsers("Review").catch(() => [])
      ]);

      const rawCampaigns = campaignRes?.items || campaignRes?.data || (Array.isArray(campaignRes) ? campaignRes : []);

      // SẮP XẾP TỪ RẢI RÁC SANG THỨ TỰ SỐ NHỎ ĐẾN LỚN
      const campaignList = rawCampaigns
        .map((c) => {
          const rawId = c.campaignId || c.id;
          return {
            id: Number(rawId),
            name: c.campaignName || c.title || c.name || `Đợt ${rawId}`,
            campaignName: c.campaignName || c.title || c.name || `Đợt ${rawId}`,
            target: c.targetQty || c.targetCount || c.target || 2000,
            speakerCount: c.speakerCount || 5,
            reviewerCount: c.reviewerCount || 2,
            topic: mapDomainToTopicVN(c.domain || c.topic),
            startDate: formatDateToVN(c.startDate || todayIso),
            endDate: formatDateToVN(c.endDate || todayIso),
            status: c.status || "Draft",
            assignedSpeakers: [],
            assignedReviewers: []
          };
        })
        .sort((a, b) => a.id - b.id);

      const rawTasks = taskRes?.items || taskRes?.data || (Array.isArray(taskRes) ? taskRes : []);

      const mappedTasks = rawTasks.map((t) => {
        const rawStatus = String(t.status || t.taskStatus || t.state || "").toLowerCase();
        
        let calculatedStatus = "Đang thực hiện";
        if (rawStatus === "completed" || rawStatus === "hoàn thành" || rawStatus === "done" || rawStatus === "1") {
          calculatedStatus = "Hoàn thành";
        } else if (rawStatus === "cancelled" || rawStatus === "canceled" || rawStatus === "đã hủy" || rawStatus === "3") {
          calculatedStatus = "Đã hủy";
        }

        const rawType = String(t.taskType || t.type || t.role || "").toLowerCase();
        const isSpeaker = rawType === "recording" || rawType === "speaker" || rawType === "0";

        return {
          id: t.taskId || t.id,
          taskId: t.taskId || t.id,
          title: t.title || t.description || "Nhiệm vụ",
          role: isSpeaker ? "Speaker" : "Reviewer",
          campaignId: Number(t.campaignId),
          topic: mapDomainToTopicVN(t.domain || t.topic || t.categoryName),
          assignedUsers: t.assigneeName ? [t.assigneeName] : [],
          assignedToUserId: t.assignedToUserId || t.assigneeId,
          status: calculatedStatus
        };
      });

      const mappedSpeakers = (speakerUsersRes || []).map((u) => ({
        id: u.userId || u.id,
        name: u.fullName || u.userName || u.name,
        email: u.email,
        role: "Speaker",
        status: "Active",
        performanceTag: u.performanceTag || "GOOD"
      }));

      const mappedReviewers = (reviewerUsersRes || []).map((u) => ({
        id: u.userId || u.id,
        name: u.fullName || u.userName || u.name,
        email: u.email,
        role: "Reviewer",
        status: "Active",
        performanceTag: u.performanceTag || "GOOD"
      }));

      setBatches(campaignList);
      if (campaignList.length > 0 && !selectedBatchId) {
        setSelectedBatchId(campaignList[0].id);
      }

      setAssignTasks(mappedTasks);
      setAllAdminUsers([...mappedSpeakers, ...mappedReviewers]);

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

  const assignModalSelectedBatch = useMemo(() => {
    return batches.find(b => b.id === assignFormData.selectedBatchId) || batches[0] || null;
  }, [batches, assignFormData.selectedBatchId]);

  const filteredTasksForAssignModal = useMemo(() => {
    if (!assignModalSelectedBatch) return allTasksOptions;
    return allTasksOptions.filter(t => !t.topic || t.topic === assignModalSelectedBatch.topic);
  }, [allTasksOptions, assignModalSelectedBatch]);

  const currentSelectedTask = useMemo(() => {
    return filteredTasksForAssignModal.find(t => t.uniqueKey === assignFormData.selectedUniqueKey) || filteredTasksForAssignModal[0];
  }, [assignFormData.selectedUniqueKey, filteredTasksForAssignModal]);

  const targetUserList = useMemo(() => {
    return currentSelectedTask?.role === "Speaker" ? activeSpeakers : activeReviewers;
  }, [currentSelectedTask, activeSpeakers, activeReviewers]);

  const maxAllowedUsers = useMemo(() => {
    if (!assignModalSelectedBatch || !currentSelectedTask) return Infinity;
    return currentSelectedTask.role === "Speaker"
      ? Number(assignModalSelectedBatch.speakerCount || 0)
      : Number(assignModalSelectedBatch.reviewerCount || 0);
  }, [assignModalSelectedBatch, currentSelectedTask]);

  const handleOpenAssignModal = () => {
    if (batches.length === 0) {
      toast.error("Vui lòng tạo đợt trước khi thực hiện phân công nhiệm vụ!");
      return;
    }

    setEditingAssignment(null);
    const initialBatch = batches.find(b => b.id === selectedBatchId) || batches[0] || null;
    const initialBatchTopic = initialBatch?.topic;
    const availableTasks = initialBatchTopic 
      ? allTasksOptions.filter(t => !t.topic || t.topic === initialBatchTopic) 
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
      ? allTasksOptions.filter(t => !t.topic || t.topic === nextBatch.topic)
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
          toast.error(`Số lượng ${currentSelectedTask?.role} chọn không được vượt quá số lượng đợt yêu cầu (${maxAllowedUsers} người)!`);
          return prev;
        }
        return { ...prev, assignedUsers: [...prev.assignedUsers, userName] };
      }
    });
  };

  const handleSubmitAssignForm = async (e) => {
    e.preventDefault();

    try {
      const taskObj = currentSelectedTask;
      const targetBatchId = Number(assignFormData.selectedBatchId);
      const taskId = Number(taskObj?.id || taskObj?.taskId);

      if (!taskId || isNaN(targetBatchId)) {
        toast.error("Vui lòng chọn đợt và nhiệm vụ hợp lệ!");
        return;
      }

      await taskService.attachTaskToCampaign(taskId, targetBatchId);

      if (assignFormData.assignedUsers.length > 0) {
        const selectedUserObj = targetUserList.find(u => assignFormData.assignedUsers.includes(u.name));
        if (selectedUserObj?.id) {
          await taskService.assignTask(taskId, selectedUserObj.id).catch(err => {
            console.warn("Lỗi ngầm assignTask:", err);
          });
        }
      }

      toast.success(`Đã phân công nhiệm vụ "${taskObj.title}" vào đợt thành công!`);
      setIsAssignModalOpen(false);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi khi phân công nhiệm vụ vào đợt:", err?.response?.data || err);
      const msg = err?.response?.data?.title || err?.response?.data?.message || "Phân công thất bại!";
      toast.error(msg);
    }
  };

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

  const handleConfirmSaveModal = async (e) => {
    if (e) e.preventDefault();

    if (!batchFormData.name || !batchFormData.name.trim()) {
      toast.error("Vui lòng nhập tên đợt!");
      return;
    }

    try {
      const parseToCleanIso = (str) => {
        if (!str) return todayIso;
        if (str.includes("-")) return str.split("T")[0];
        if (str.includes("/")) {
          const parts = str.split("/");
          if (parts.length === 3) {
            return `${parts[2]}-${parts[1].padStart(2, "0")}-${parts[0].padStart(2, "0")}`;
          }
        }
        return todayIso;
      };

      const cleanStart = parseToCleanIso(batchFormData.startDate);
      const cleanEnd = parseToCleanIso(batchFormData.endDate);

      if (cleanEnd < cleanStart) {
        toast.error("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu!");
        return;
      }

      const domainMap = {
        "Công nghệ thông tin": "ItTechnology",
        "Giáo dục": "Education",
        "Hội thoại hàng ngày": "DailyLife"
      };

      const startIso = `${cleanStart}T00:00:00Z`;
      const endIso = `${cleanEnd}T23:59:59Z`;

      const campaignPayload = {
        title: String(batchFormData.name).trim(),
        campaignName: String(batchFormData.name).trim(),
        domain: domainMap[batchFormData.topic] || "ItTechnology",
        targetQty: Number(batchFormData.target),
        startDate: cleanStart,
        endDate: cleanEnd,
        StartDate: cleanStart,
        EndDate: cleanEnd,
        startDateIso: startIso,
        endDateIso: endIso
      };

      if (modalMode === "CREATE") {
        await taskService.createCampaign(campaignPayload);
        toast.success(`Đã tạo thành công ${batchFormData.name}!`);
      } else {
        await taskService.updateCampaign(modalTargetBatchId, campaignPayload);
        toast.success(`Đã cập nhật ${batchFormData.name}!`);
      }

      setIsModalOpen(false);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi API tạo/sửa đợt:", err?.response?.data || err);
      
      const responseData = err?.response?.data;
      let apiErrorMsg = responseData?.title || responseData?.message || "Lưu đợt thất bại!";
      
      if (responseData?.errors) {
        const errorKeys = Object.keys(responseData.errors);
        if (errorKeys.length > 0) {
          const firstErr = responseData.errors[errorKeys[0]];
          apiErrorMsg = Array.isArray(firstErr) ? firstErr[0] : firstErr;
        }
      }
      
      toast.error(apiErrorMsg);
    }
  };

  const handleRequestDeleteBatch = (e, batchToDelete) => {
    e.stopPropagation();
    setDeletingBatch(batchToDelete);
  };

  const handleConfirmDeleteBatch = async () => {
    if (!deletingBatch) return;
    const targetCampaignId = Number(deletingBatch.id);

    try {
      if (typeof taskService.deleteCampaign === "function") {
        await taskService.deleteCampaign(targetCampaignId);
      } else {
        throw new Error("Không tìm thấy hàm deleteCampaign trong taskService");
      }

      toast.success(`Đã xóa đợt ${deletingBatch.campaignName || deletingBatch.name}!`);
      setDeletingBatch(null);
      await loadDataFromApi();
    } catch (err) {
      console.error("Chi tiết lỗi xóa đợt từ Server:", err?.response || err);
      const errorMsg = err?.response?.data?.message || err?.response?.data?.title || "Server không cho phép xóa đợt này!";
      toast.error(`Xóa thất bại: ${errorMsg}`);
      setDeletingBatch(null);
    }
  };

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
          toast.error(`Số lượng không được vượt quá số lượng đợt yêu cầu (${maxUsers} người)!`);
          return prev;
        }
        return { ...prev, assignedUsers: [...prev.assignedUsers, userName] };
      }
    });
  };

  const handleSaveEditTask = async (e) => {
    e.preventDefault();

    if (!editTaskFormData.title.trim()) {
      toast.error("Tên nhiệm vụ không được để trống!");
      return;
    }

    try {
      await taskService.updateTask(editingTask.id, {
        description: editTaskFormData.title
      });

      if (editTaskFormData.assignedUsers.length > 0) {
        const userObj = activeSpeakers.concat(activeReviewers).find(u => editTaskFormData.assignedUsers.includes(u.name));
        if (userObj?.id) {
          await taskService.assignTask(editingTask.id, userObj.id);
        }
      }

      toast.success(`Đã cập nhật nhiệm vụ "${editTaskFormData.title}" thành công!`);
      setIsEditTaskModalOpen(false);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi cập nhật nhiệm vụ:", err?.response?.data || err);
      toast.error("Cập nhật nhiệm vụ thất bại!");
    }
  };

  const handleRequestDeleteTask = (task) => {
    setDeletingTask(task);
  };

  const handleConfirmDeleteTask = async () => {
    if (!deletingTask || !selectedBatchId) return;
    const targetTaskId = Number(deletingTask.id || deletingTask.taskId);

    try {
      await taskService.detachTaskFromCampaign(Number(selectedBatchId), targetTaskId);
      toast.success(`Đã gỡ nhiệm vụ "${deletingTask.title}" khỏi đợt thành công!`);
    } catch (err) {
      console.warn("Lỗi gỡ task khỏi đợt:", err?.response?.data || err);
      toast.success(`Đã gỡ nhiệm vụ "${deletingTask.title}" khỏi đợt!`);
    } finally {
      setAssignTasks((prev) =>
        prev.map((t) => (t.id === targetTaskId ? { ...t, campaignId: null } : t))
      );
      setDeletingTask(null);
      await loadDataFromApi();
    }
  };

  const currentTasks = useMemo(() => {
    if (!selectedBatchId) return assignTasks;
    return assignTasks.filter((t) => Number(t.campaignId) === Number(selectedBatchId));
  }, [assignTasks, selectedBatchId]);

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
    <div className="w-full h-full flex flex-col gap-3.5 text-left font-sans p-1 overflow-y-auto relative transition-colors">

      {/* MODAL TẠO / CẬP NHẬT ĐỢT */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setIsModalOpen(false)}>
          <div 
            className="rounded-[24px] w-full max-w-lg shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <form onSubmit={handleConfirmSaveModal} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  {modalMode === "CREATE" ? "Tạo đợt nhiệm vụ mới" : `Chỉnh sửa ${targetModalBatchObj?.campaignName || targetModalBatchObj?.name || "đợt"}`}
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
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Tên đợt (Campaign Name)
                </label>
                <input
                  type="text"
                  required
                  value={batchFormData.name}
                  onChange={(e) => setBatchFormData({ ...batchFormData, name: e.target.value })}
                  placeholder="Nhập tên đợt..."
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

              <div className="grid grid-cols-2 gap-3.5">
                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Chủ đề (Topic)
                  </label>
                  <select
                    disabled={modalMode === "UPDATE"}
                    value={batchFormData.topic}
                    onChange={(e) => setBatchFormData({ ...batchFormData, topic: e.target.value })}
                    className={`w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans ${
                      modalMode === "UPDATE"
                        ? "bg-[#F7F5EF] dark:bg-[#18191D] cursor-not-allowed opacity-80 appearance-none"
                        : "bg-white dark:bg-[#25272E] cursor-pointer"
                    }`}
                    onFocus={(e) => {
                      if (modalMode !== "UPDATE") {
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

                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Mục tiêu (Số câu)
                  </label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={batchFormData.target}
                    onChange={(e) => setBatchFormData({ ...batchFormData, target: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans"
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
              </div>

              <div className="grid grid-cols-2 gap-3.5">
                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Số lượng Speakers
                  </label>
                  <input
                    type="number"
                    min="0"
                    required
                    value={batchFormData.speakerCount}
                    onChange={(e) => setBatchFormData({ ...batchFormData, speakerCount: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans"
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

                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Số lượng Reviewers
                  </label>
                  <input
                    type="number"
                    min="0"
                    required
                    value={batchFormData.reviewerCount}
                    onChange={(e) => setBatchFormData({ ...batchFormData, reviewerCount: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans"
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
              </div>

              <div className="grid grid-cols-2 gap-3.5">
                <VNFormatDatePicker
                  label="Ngày bắt đầu"
                  value={batchFormData.startDate}
                  minDateIso={todayIso}
                  onChange={(newVal) => setBatchFormData({ ...batchFormData, startDate: newVal })}
                />

                <VNFormatDatePicker
                  label="Ngày kết thúc"
                  value={batchFormData.endDate}
                  minDateIso={batchFormData.startDate && batchFormData.startDate.includes("-") ? batchFormData.startDate : todayIso}
                  onChange={(newVal) => setBatchFormData({ ...batchFormData, endDate: newVal })}
                />
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
                  style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all"
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setIsAssignModalOpen(false)}>
          <div 
            className="rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            <form onSubmit={handleSubmitAssignForm} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section truncate text-[#16171C] dark:text-gray-100">
                  Phân công nhiệm vụ mới
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsAssignModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Chọn đợt
                </label>
                <div className="relative flex items-center">
                  <select
                    value={assignFormData.selectedBatchId}
                    onChange={(e) => handleBatchChangeInAssignModal(e.target.value)}
                    className="w-full h-11 appearance-none border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl type-body outline-none font-bold transition-all font-sans truncate pl-3.5 pr-8 py-2.5 cursor-pointer"
                    onFocus={(e) => {
                      e.target.style.borderColor = TASK_MANAGER_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                    }}
                    onBlur={(e) => {
                      e.target.style.borderColor = '';
                      e.target.style.boxShadow = 'none';
                    }}
                  >
                    {batches.map((b, idx) => (
                      <option key={b.id} value={b.id} className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">
                        Đợt {idx + 1} - {b.campaignName || b.name} - Số lượng(tối đa): {b.speakerCount} speakers, {b.reviewerCount} reviewers
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-3 pointer-events-none text-[#9A9CA3]" />
                </div>
              </div>
              
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                    Chọn nhiệm vụ (Thuộc chủ đề: {assignModalSelectedBatch?.topic || "Tất cả"})
                  </label>
                </div>
                <div className="relative flex items-center">
                  <select
                    value={assignFormData.selectedUniqueKey}
                    onChange={(e) => handleTaskChangeInAssignModal(e.target.value)}
                    className="w-full h-11 appearance-none border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl type-body outline-none font-bold transition-all font-sans truncate pl-3.5 pr-8 py-2.5 cursor-pointer"
                    onFocus={(e) => {
                      e.target.style.borderColor = TASK_MANAGER_ACCENT;
                      e.target.style.boxShadow = `0 0 0 4px ${TASK_MANAGER_ACCENT}1A`;
                    }}
                    onBlur={(e) => {
                      e.target.style.borderColor = '';
                      e.target.style.boxShadow = 'none';
                    }}
                  >
                    {filteredTasksForAssignModal.length > 0 ? (
                      filteredTasksForAssignModal.map((t) => (
                        <option key={t.uniqueKey} value={t.uniqueKey} className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">
                          {t.label}
                        </option>
                      ))
                    ) : (
                      <option value="" disabled className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">
                        Không có nhiệm vụ nào thuộc chủ đề {assignModalSelectedBatch?.topic}
                      </option>
                    )}
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-3 pointer-events-none text-[#9A9CA3]" />
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                    {currentSelectedTask?.role} đang hoạt động ({assignFormData.assignedUsers.length}/{maxAllowedUsers} tối đa)
                  </label>
                  <span 
                    style={{ backgroundColor: currentSelectedTask?.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                    className="type-caption text-white px-2 py-0.5 rounded-md font-bold"
                  >
                    {currentSelectedTask?.role}
                  </span>
                </div>

                <div className="max-h-52 overflow-y-auto border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl p-2.5 space-y-2">
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
                          style={isChecked ? { borderColor: TASK_MANAGER_ACCENT } : {}}
                          className={`flex items-start justify-between p-2.5 rounded-xl type-ui transition-all border ${
                            isDisableUnchecked 
                              ? "opacity-50 cursor-not-allowed text-[#9A9CA3] border-transparent" 
                              : "cursor-pointer"
                          } ${
                            isChecked 
                              ? "bg-white dark:bg-[#1C1D22] shadow-xs font-bold text-[#16171C] dark:text-white" 
                              : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#6E7078] dark:text-gray-300"
                          }`}
                        >
                          <div className="flex items-start gap-2.5 min-w-0 flex-1">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              disabled={isDisableUnchecked}
                              onChange={() => handleUserCheckboxToggle(u.name)}
                              style={{ accentColor: TASK_MANAGER_ACCENT }}
                              className="rounded focus:ring-0 cursor-pointer disabled:cursor-not-allowed shrink-0 mt-0.5"
                            />
                            <div className="flex flex-col min-w-0 pr-1">
                              <span className="truncate leading-tight font-bold">{u.name}</span>
                              <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate">
                                {u.email}
                              </span>
                              
                              {hasPreferenceReason && (
                                <span className="type-meta font-medium mt-0.5 text-[#6E7078] dark:text-gray-400 line-clamp-1">
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
                                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md type-caption font-bold border"
                                >
                                  <Award className="w-3 h-3 shrink-0" />
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
                                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md type-caption font-bold border"
                                >
                                  <AlertCircle className="w-3 h-3 shrink-0" />
                                  <span>Hạn chế</span>
                                </span>
                              )}
                            </div>
                          )}
                        </label>
                      );
                    })
                  ) : (
                    <div className="p-3 text-center type-ui text-[#6E7078] dark:text-gray-400">
                      Không có {currentSelectedTask?.role} nào đang hoạt động (Active).
                    </div>
                  )}
                </div>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsAssignModalOpen(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={filteredTasksForAssignModal.length === 0}
                  style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setDeletingBatch(null)}>
          <div 
            className="border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: DANGER }} className="h-1.5 w-full shrink-0" />
            <div className="p-6 text-center space-y-3.5">
              <div 
                style={{ backgroundColor: "rgba(243, 114, 127, 0.15)", color: DANGER }}
                className="w-11 h-11 rounded-full flex items-center justify-center mx-auto"
              >
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa đợt</h3>
                <p className="type-ui mt-1 break-words text-[#6E7078] dark:text-gray-400">
                  Bạn có chắc muốn xóa <span className="font-bold text-[#16171C] dark:text-gray-200">"{deletingBatch.campaignName || deletingBatch.name}"</span>? 
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setDeletingBatch(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDeleteBatch}
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setIsEditTaskModalOpen(false)}>
          <div 
            className="rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            
            <form onSubmit={handleSaveEditTask} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  Chỉnh sửa phân công nhiệm vụ
                </h3>
                <button 
                  type="button"
                  onClick={() => setIsEditTaskModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Tên nhiệm vụ
                </label>
                <input
                  type="text"
                  value={editTaskFormData.title}
                  onChange={(e) => setEditTaskFormData({ ...editTaskFormData, title: e.target.value })}
                  className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-medium transition-all font-sans"
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

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Vai trò thực hiện
                </label>
                <select
                  disabled
                  value={editTaskFormData.role}
                  className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#18191D] text-[#6E7078] dark:text-gray-400 rounded-xl outline-none font-bold transition-all cursor-not-allowed opacity-80 appearance-none"
                >
                  <option value="Speaker" className="dark:bg-[#1C1D22]">Speaker</option>
                  <option value="Reviewer" className="dark:bg-[#1C1D22]">Reviewer</option>
                </select>
              </div>

              <div>
                {(() => {
                  const editBatch = batches.find(b => b.id === selectedBatchId);
                  const maxUsers = editTaskFormData.role === "Speaker" 
                    ? Number(editBatch?.speakerCount || 999) 
                    : Number(editBatch?.reviewerCount || 999);

                  const userList = editTaskFormData.role === "Speaker" ? activeSpeakers : activeReviewers;

                  return (
                    <>
                      <div className="flex items-center justify-between mb-1.5">
                        <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                          {editTaskFormData.role} đang hoạt động ({editTaskFormData.assignedUsers.length}/{maxUsers} tối đa)
                        </label>
                        <span 
                          style={{ backgroundColor: editTaskFormData.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                          className="type-caption text-white px-2 py-0.5 rounded-md font-bold"
                        >
                          {editTaskFormData.role}
                        </span>
                      </div>

                      <div className="max-h-52 overflow-y-auto border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl p-2.5 space-y-2">
                        {userList.map((u) => {
                          const isChecked = editTaskFormData.assignedUsers.includes(u.name);
                          const isDisableUnchecked = !isChecked && editTaskFormData.assignedUsers.length >= maxUsers;
                          const hasPreferenceReason = editTaskFormData.role === "Speaker" && 
                                                      u.performanceReason && 
                                                      (u.performanceTag === "GOOD" || u.performanceTag === "BAD");

                          return (
                            <label 
                              key={u.id}
                              style={isChecked ? { borderColor: TASK_MANAGER_ACCENT } : {}}
                              className={`flex items-start justify-between p-2.5 rounded-xl type-ui transition-all border ${
                                isDisableUnchecked 
                                  ? "opacity-50 cursor-not-allowed text-[#9A9CA3] border-transparent" 
                                  : "cursor-pointer"
                              } ${
                                isChecked 
                                  ? "bg-white dark:bg-[#1C1D22] shadow-xs font-bold text-[#16171C] dark:text-white" 
                                  : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#6E7078] dark:text-gray-300"
                              }`}
                            >
                              <div className="flex items-start gap-2.5 min-w-0 flex-1">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  disabled={isDisableUnchecked}
                                  onChange={() => handleToggleUserInEditTask(u.name)}
                                  style={{ accentColor: TASK_MANAGER_ACCENT }}
                                  className="rounded focus:ring-0 cursor-pointer disabled:cursor-not-allowed shrink-0 mt-0.5"
                                />
                                <div className="flex flex-col min-w-0 pr-1">
                                  <span className="truncate leading-tight font-bold">{u.name}</span>
                                  <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate">
                                    {u.email}
                                  </span>

                                  {hasPreferenceReason && (
                                    <span className="type-meta font-medium mt-0.5 text-[#6E7078] dark:text-gray-400 line-clamp-1">
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
                                      className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md type-caption font-bold border"
                                    >
                                      <Award className="w-3 h-3 shrink-0" />
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
                                      className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md type-caption font-bold border"
                                    >
                                      <AlertCircle className="w-3 h-3 shrink-0" />
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

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsEditTaskModalOpen(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all"
                >
                  Lưu thay đổi
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL XÓA NHIỆM VỤ CỦA ĐỢT */}
      {deletingTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setDeletingTask(null)}>
          <div 
            className="border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden transition-colors animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: DANGER }} className="h-1.5 w-full shrink-0" />
            <div className="p-6 text-center space-y-3.5">
              <div 
                style={{ backgroundColor: "rgba(243, 114, 127, 0.15)", color: DANGER }}
                className="w-11 h-11 rounded-full flex items-center justify-center mx-auto"
              >
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận gỡ nhiệm vụ</h3>
                <p className="type-ui mt-1 break-words text-[#6E7078] dark:text-gray-400">
                  Bạn có chắc muốn gỡ nhiệm vụ <span className="font-bold text-[#16171C] dark:text-gray-200">"{deletingTask.title}"</span> khỏi đợt này?
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setDeletingTask(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  onClick={handleConfirmDeleteTask}
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
                >
                  Gỡ ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL XEM DANH SÁCH NGƯỜI ĐƯỢC PHÂN CÔNG */}
      {viewingAssignedTask && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setViewingAssignedTask(null)}>
          <div 
            className="rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ backgroundColor: TASK_MANAGER_ACCENT }} className="h-1.5 w-full shrink-0" />
            <div className="p-5 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Users style={{ color: TASK_MANAGER_ACCENT }} className="w-4 h-4" />
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  Danh sách phân công ({viewingAssignedTask.role || "Người thực hiện"})
                </h3>
              </div>
              <button 
                onClick={() => setViewingAssignedTask(null)}
                className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
              >
                <X className="w-4 h-4 text-[#6E7078]" />
              </button>
            </div>

            <div className="p-5 space-y-3.5">
              <p className="type-body font-bold break-words text-[#16171C] dark:text-gray-200">
                "{viewingAssignedTask.title}"
              </p>

              <div className="max-h-60 overflow-y-auto space-y-2 pr-1">
                {viewingAssignedTask.assignedUsers && viewingAssignedTask.assignedUsers.length > 0 ? (
                  viewingAssignedTask.assignedUsers.map((userName, idx) => (
                    <div 
                      key={idx} 
                      className="flex items-center justify-between p-3 rounded-xl type-ui font-label bg-[#F7F5EF] dark:bg-[#25272E] text-[#16171C] dark:text-gray-200 border border-[#E5E2D8]/80 dark:border-gray-700/60"
                    >
                      <div className="flex items-center gap-2.5">
                        <div 
                          style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                          className="w-7 h-7 rounded-full text-white flex items-center justify-center type-caption font-bold"
                        >
                          {userName.charAt(0)}
                        </div>
                        <span className="font-bold">{userName}</span>
                      </div>
                      <span 
                        style={{ backgroundColor: viewingAssignedTask.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                        className="type-caption text-white px-2.5 py-0.5 rounded-md font-bold"
                      >
                        {viewingAssignedTask.role || "Thực hiện"}
                      </span>
                    </div>
                  ))
                ) : (
                  <p className="type-ui text-center py-4 text-[#6E7078] dark:text-gray-400">Chưa phân công người nào</p>
                )}
              </div>
            </div>

            <div className="p-4 border-t border-[#E5E2D8] dark:border-gray-800 text-right bg-[#F7F5EF]/50 dark:bg-[#25272E]/50">
              <button
                type="button"
                onClick={() => setViewingAssignedTask(null)}
                style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                className="w-full py-2.5 text-white rounded-xl type-button hover:opacity-90 transition-opacity cursor-pointer shadow-sm text-center"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Grid Layout chính */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-3.5 flex-1 min-h-0">
        
        {/* Cột trái: Danh sách các Đợt */}
        <div className="lg:col-span-1 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-3.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col gap-2.5 min-h-0">
          
          <div className="flex items-center justify-between px-1 pb-1.5 border-b border-[#E5E2D8] dark:border-gray-800">
            <h3 className="type-card-title text-[#16171C] dark:text-white flex items-center gap-1.5 uppercase">
              <Layers style={{ color: TASK_MANAGER_ACCENT }} className="w-4 h-4" />
              DANH SÁCH ĐỢT
            </h3>
          </div>

          <div className="flex flex-col gap-2.5 overflow-y-auto max-h-[580px] pr-1">
            {batches.length === 0 ? (
              <div className="p-4 text-center type-ui text-[#9A9CA3] dark:text-gray-500">
                Chưa có đợt nào. Bấm nút tạo đợt ở trên để tạo.
              </div>
            ) : (
              batches.map((batch, index) => {
                const active = batch.id === selectedBatchId;
                
                const mappedIds = batchMapping[batch.id] || [];
                const batchTasks = assignTasks.filter((t) => t.campaignId === batch.id || mappedIds.includes(t.id));

                const isBatchCompleted = batchTasks.length > 0 && batchTasks.every((t) => t.status === "Hoàn thành");
                const isOpenStatus = batchTasks.length === 0 || batch.status === "Open";

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
                    className={`relative w-full text-left p-3.5 rounded-xl transition-all border cursor-pointer flex flex-col gap-2 ${
                      active 
                        ? "shadow-xs" 
                        : "bg-[#F7F5EF]/60 dark:bg-[#25272E]/50 border-[#E5E2D8] dark:border-gray-800 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E]"
                    }`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0 flex-1">
                        <span className="type-ui font-bold text-[#16171C] dark:text-white truncate">
                          Đợt {index + 1}
                        </span>
                        
                        <span 
                          style={{
                            backgroundColor: batchBadgeBg,
                            borderColor: batchBadgeBorder,
                            color: batchBadgeText
                          }}
                          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full type-caption font-label border shrink-0 leading-tight whitespace-nowrap"
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
                          className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] hover:bg-[#E5E2D8] dark:hover:bg-gray-700 hover:text-[#16171C] dark:hover:text-gray-100 transition-colors cursor-pointer"
                        >
                          <Edit3 className="w-3.5 h-3.5" />
                        </button>

                        <button
                          type="button"
                          onClick={(e) => handleRequestDeleteBatch(e, batch)}
                          title={`Xóa ${batch.campaignName || batch.name}`}
                          className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] hover:bg-[#FDEAEA] dark:hover:bg-red-900/30 hover:text-[#C63B3B] dark:hover:text-[#E55353] transition-colors cursor-pointer"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    <div className="type-caption space-y-1 text-[#6E7078] dark:text-gray-400 border-t border-[#E5E2D8] dark:border-gray-800/60 pt-2">
                      <div className="flex items-center gap-1 font-medium text-[#16171C] dark:text-gray-300">
                        <FolderKanban className="w-3 h-3 text-indigo-500 shrink-0" />
                        <span className="truncate">Tên đợt: <strong className="text-[#16171C] dark:text-white font-bold">{batch.campaignName || batch.name}</strong></span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-[#16171C] dark:text-gray-300">
                        <Tag className="w-3 h-3 text-emerald-500 shrink-0" />
                        <span className="truncate">Chủ đề: <strong className="text-[#16171C] dark:text-white font-bold">{batch.topic || "Công nghệ thông tin"}</strong></span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-[#16171C] dark:text-gray-300">
                        <Target className="w-3 h-3 text-blue-500 shrink-0" />
                        <span>Mục tiêu: <strong className="text-[#16171C] dark:text-white font-bold">{batch.target || 2000}</strong> câu</span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-[#16171C] dark:text-gray-300">
                        <Users className="w-3 h-3 text-amber-500 shrink-0" />
                        <span>Số lượng(tối đa): <strong className="text-[#16171C] dark:text-white font-bold">{batch.speakerCount || 0}</strong> Speakers, <strong className="text-[#16171C] dark:text-gray-200">{batch.reviewerCount || 0}</strong> Reviewers</span>
                      </div>

                      <div className="flex items-center gap-1 font-medium text-[#16171C] dark:text-gray-300">
                        <Calendar className="w-3 h-3 text-purple-500 shrink-0" />
                        <span>Thời gian: <strong className="text-[#16171C] dark:text-white font-bold">{batch.startDate || "N/A"} - {batch.endDate || "N/A"}</strong></span>
                      </div>
                    </div>

                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Cột phải: Bảng danh sách nhiệm vụ */}
        <div className="lg:col-span-3 flex flex-col gap-3.5 min-h-0">
          
          {/* Filter Bar + Cụm Nút Thao Tác */}
          <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-2.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-2.5 justify-between items-center transition-colors">
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
                  placeholder="Tìm theo tên nhiệm vụ..."
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
                />
                <button
                  type="button"
                  onClick={handleSearchSubmit}
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
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
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
                  <option value="ALL" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
                  <option value="Đang thực hiện" className="bg-white dark:bg-[#25272E]">Đang thực hiện</option>
                  <option value="Hoàn thành" className="bg-white dark:bg-[#25272E]">Hoàn thành</option>
                  <option value="Đã hủy" className="bg-white dark:bg-[#25272E]">Đã hủy</option>
                </select>
              </div>

              <button
                type="button"
                onClick={handleOpenCreateModal}
                style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                className="inline-flex items-center gap-1.5 py-2 px-3.5 type-button text-white rounded-xl hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
              >
                <Plus className="w-4 h-4" />
                <span>Tạo đợt</span>
              </button>

              <button
                type="button"
                onClick={handleOpenAssignModal}
                style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                className="inline-flex items-center gap-1.5 py-2 px-3.5 type-button text-white rounded-xl hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
              >
                <UserCheck className="w-4 h-4" />
                <span>Phân công</span>
              </button>
            </div>
          </div>

          {/* BẢNG CHUẨN */}
          <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden flex-1 flex flex-col">
            <div className="overflow-x-auto flex-1">
              <table className="w-full text-left type-ui min-w-[700px]">
                <thead>
                  <tr className="border-b border-[#E5E2D8] dark:border-gray-800 text-[#9A9CA3] dark:text-gray-400 type-label h-8 bg-[#F7F5EF] dark:bg-[#25272E]">
                    <th className="py-2 px-3 text-center w-[50px]">STT</th>
                    <th className="py-2 px-3 min-w-[200px]">Nhiệm vụ</th>
                    <th className="py-2 px-3 text-center w-[100px]">Vai trò</th>
                    <th className="py-2 px-3 text-center w-[200px]">Người được phân công</th>
                    <th className="py-2 px-3 text-center w-[150px]">Trạng thái</th>
                    <th className="py-2 px-3 text-center w-[100px]">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
                  {!selectedBatchObj || filteredTasks.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-12 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
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
                        <tr key={task.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors h-11">
                          <td className="py-2 px-3 text-center text-[#9A9CA3] font-medium type-caption">
                            {index + 1}
                          </td>
                          <td className="py-2 px-3">
                            <p className="font-bold text-[#16171C] dark:text-white type-ui leading-snug">{task.title}</p>
                          </td>
                          <td className="py-2 px-3 text-center">
                            <span 
                              style={{ backgroundColor: task.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                              className="type-caption font-bold text-white px-2.5 py-0.5 rounded-md"
                            >
                              {task.role}
                            </span>
                          </td>

                          <td className="py-2 px-3 text-center whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() => setViewingAssignedTask(task)}
                              className="inline-flex items-center justify-center gap-1.5 px-3 py-1 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] hover:bg-[#E5E2D8] dark:hover:bg-gray-700 text-[#16171C] dark:text-gray-200 type-meta font-label border border-[#E5E2D8] dark:border-gray-700 transition-colors cursor-pointer"
                            >
                              <Eye className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
                              <span>Xem danh sách ({countUsers})</span>
                            </button>
                          </td>
                          
                          <td className="py-2 px-3 text-center whitespace-nowrap">
                            <span
                              style={{
                                backgroundColor: statusBg,
                                borderColor: statusBorder,
                                color: statusText
                              }}
                              className="inline-flex items-center justify-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border leading-tight select-none whitespace-nowrap"
                            >
                              {isCompleted ? (
                                <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                              ) : isCancelled ? (
                                <Ban className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                              ) : (
                                <Clock className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                              )}
                              <span>{task.status || "Đang thực hiện"}</span>
                            </span>
                          </td>

                          <td className="py-2 px-3 text-center">
                            <div className="flex items-center justify-center gap-1">
                              <button
                                type="button"
                                onClick={() => handleOpenEditTaskModal(task)}
                                title="Sửa nhiệm vụ"
                                className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                              >
                                <Edit3 className="w-3.5 h-3.5" />
                              </button>
                              <button
                                type="button"
                                onClick={() => handleRequestDeleteTask(task)}
                                title="Gỡ nhiệm vụ khỏi đợt"
                                className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
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