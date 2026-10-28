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
  if (!dateStr) return "---";
  if (dateStr.includes("/")) return dateStr;
  const clean = dateStr.split("T")[0];
  const [year, month, day] = clean.split("-");
  if (!year || !month || !day) return "---";
  return `${day}/${month}/${year}`;
};

// Helper format ngày ISO (yyyy-mm-dd)
const formatDateToISO = (dateStr) => {
  if (!dateStr) return "";
  const clean = String(dateStr).trim();
  
  if (clean.includes("-")) {
    return clean.split("T")[0];
  }
  
  const parts = clean.split("/");
  if (parts.length === 3) {
    const [day, month, year] = parts;
    return `${year}-${month.padStart(2, "0")}-${day.padStart(2, "0")}`;
  }
  return "";
};

// Helper chuyển đổi Domain/Topic từ BE sang tiếng Việt
const mapDomainToTopicVN = (domain) => {
  if (domain === "ItTechnology" || domain === "IT" || domain === "Công nghệ thông tin") return "Công nghệ thông tin";
  if (domain === "Education" || domain === "Giáo dục") return "Giáo dục";
  if (domain === "DailyLife" || domain === "Hội thoại hàng ngày") return "Hội thoại hàng ngày";
  return domain || "Công nghệ thông tin";
};

// Component DatePicker
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

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState("CREATE");
  const [modalTargetBatchId, setModalTargetBatchId] = useState(null);

  const [batchFormData, setBatchFormData] = useState({
    name: "",
    target: 2000,
    topic: "Công nghệ thông tin",
    startDate: todayIso,
    endDate: todayIso,
    assignedSpeakers: [],
    assignedReviewers: []
  });

  const [isAssignModalOpen, setIsAssignModalOpen] = useState(false);
  const [allAdminUsers, setAllAdminUsers] = useState([]);

  const [assignFormData, setAssignFormData] = useState({
    selectedBatchId: "",
    taskTitle: "",
    targetRole: "Speaker",
    assignedUserIds: [] 
  });

  const [viewingAssignedTask, setViewingAssignedTask] = useState(null);
  const [userTargets, setUserTargets] = useState({});
  const [deletingBatch, setDeletingBatch] = useState(null);

  const [isEditTaskModalOpen, setIsEditTaskModalOpen] = useState(false);
  const [editingTask, setEditingTask] = useState(null);
  const [editTaskFormData, setEditTaskFormData] = useState({
    title: "",
    role: "Speaker",
    assignedUserIds: []
  });
  const [deletingTask, setDeletingTask] = useState(null);

  const [batches, setBatches] = useState([]);
  const [selectedBatchId, setSelectedBatchId] = useState(null);

  const [assignTasks, setAssignTasks] = useState([]);
  const [batchMapping, setBatchMapping] = useState({});

  const loadDataFromApi = async () => {
    try {
      const [campaignRes, taskRes, speakerUsersRes, reviewerUsersRes, allUsersRes] = await Promise.all([
        taskService.getCampaigns({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getAssignableUsers("Recording").catch(() => []),
        taskService.getAssignableUsers("Review").catch(() => []),
        taskService.getUsers({ pageSize: 1000 }).catch(() => ({ items: [] }))
      ]);

      const rawCampaigns = campaignRes?.items || campaignRes?.data || (Array.isArray(campaignRes) ? campaignRes : []);
      const campaignList = rawCampaigns
        .map((c) => ({
          id: Number(c.campaignId || c.id),
          name: c.campaignName || c.title || c.name || `Đợt ${c.campaignId || c.id}`,
          campaignName: c.campaignName || c.title || c.name || `Đợt ${c.campaignId || c.id}`,
          target: Number(c.targetQty || c.targetCount || c.target || 2000),
          topic: mapDomainToTopicVN(c.domain || c.topic),
          startDate: formatDateToVN(c.startDate || todayIso),
          endDate: formatDateToVN(c.endDate || todayIso),
          rawStartDateIso: c.startDate ? formatDateToISO(c.startDate) : todayIso,
          rawEndDateIso: c.endDate ? formatDateToISO(c.endDate) : todayIso,
          status: c.status || "Draft"
        }))
        .sort((a, b) => a.id - b.id);

      const mappedSpeakers = (speakerUsersRes || []).map((u) => ({
        id: Number(u.userId || u.id),
        name: u.fullName || u.userName || u.name || `User ${u.userId || u.id}`,
        email: u.email || "",
        role: "Speaker",
        status: "Active"
      }));

      const mappedReviewers = (reviewerUsersRes || []).map((u) => ({
        id: Number(u.userId || u.id),
        name: u.fullName || u.userName || u.name || `User ${u.userId || u.id}`,
        email: u.email || "",
        role: "Reviewer",
        status: "Active"
      }));

      const rawAllUsers = allUsersRes?.items || allUsersRes?.data || (Array.isArray(allUsersRes) ? allUsersRes : []);
      const mappedAllUsers = rawAllUsers.map((u) => ({
        id: Number(u.userId || u.id),
        name: u.fullName || u.userName || u.name || `User ${u.userId || u.id}`,
        email: u.email || "",
        role: u.role || "Speaker",
        status: "Active"
      }));

      const combinedUsersMap = new Map();
      [...mappedSpeakers, ...mappedReviewers, ...mappedAllUsers].forEach(u => {
        if (u.id && !combinedUsersMap.has(u.id)) {
          combinedUsersMap.set(u.id, u);
        }
      });
      const combinedUsers = Array.from(combinedUsersMap.values());

      const rawTasks = taskRes?.items || taskRes?.data || (Array.isArray(taskRes) ? taskRes : []);

      const parsedTasks = await Promise.all(
        rawTasks.map(async (t) => {
          let currentTaskData = t;
          const taskId = Number(t.taskId || t.id);

          try {
            const detail = await taskService.getTaskById(taskId);
            if (detail) {
              currentTaskData = detail;
            }
          } catch (err) {
            console.warn(`Lỗi lấy chi tiết Task ${taskId}:`, err);
          }

          let rawAssignees = 
            currentTaskData.assignees || 
            currentTaskData.summary?.assignees || 
            currentTaskData.assignments || 
            currentTaskData.assignedUsers || 
            currentTaskData.assignedUserIds || 
            [];

          const rawStatus = String(currentTaskData.status || currentTaskData.summary?.status || "").toLowerCase();
          let calculatedStatus = "Đang thực hiện";
          if (rawStatus === "completed" || rawStatus === "hoàn thành" || rawStatus === "done") calculatedStatus = "Hoàn thành";
          else if (rawStatus === "cancelled" || rawStatus === "canceled" || rawStatus === "đã hủy") calculatedStatus = "Đã hủy";

          const rawType = String(currentTaskData.taskType || currentTaskData.summary?.taskType || "").toLowerCase();
          const isSpeaker = rawType === "recording" || rawType === "speaker" || rawType === "0";

          let usersList = [];
          const userTargetsMap = {};
          let assignedUserIds = [];

          if (Array.isArray(rawAssignees) && rawAssignees.length > 0) {
            rawAssignees.forEach((item) => {
              const uId = typeof item === 'number' || typeof item === 'string' 
                ? Number(item) 
                : Number(item.userId || item.id || item.user?.id || item.user?.userId);

              if (uId) {
                if (!assignedUserIds.includes(uId)) {
                  assignedUserIds.push(uId);
                }
                const foundUser = combinedUsers.find((cu) => Number(cu.id) === Number(uId));
                
                const uName = (typeof item === 'object' && (item.fullName || item.userName || item.name || item.user?.fullName || item.user?.userName)) 
                  || (foundUser ? foundUser.name : `Speaker ${uId}`);

                if (uName && !usersList.includes(uName)) {
                  usersList.push(uName);
                  if (typeof item === 'object' && item.targetQty !== undefined && item.targetQty !== null) {
                    userTargetsMap[uName] = Number(item.targetQty);
                  }
                }
              }
            });
          }

          const actualTaskQty = Number(currentTaskData.targetQty || currentTaskData.summary?.progress?.targetQty || 2000);

          return {
            id: taskId,
            taskId: taskId,
            title: currentTaskData.description || currentTaskData.title || currentTaskData.summary?.description || "Nhiệm vụ",
            role: isSpeaker ? "Speaker" : "Reviewer",
            campaignId: Number(currentTaskData.campaignId || currentTaskData.summary?.campaignId),
            topic: mapDomainToTopicVN(currentTaskData.domain || currentTaskData.summary?.domain),
            rawTargetQty: actualTaskQty,
            targetQty: actualTaskQty,
            assignedUsers: usersList,
            assignedUserTargets: userTargetsMap,
            assignedUserIds: assignedUserIds,
            status: calculatedStatus
          };
        })
      );

      setBatches(campaignList);
      if (campaignList.length > 0 && !selectedBatchId) {
        setSelectedBatchId(campaignList[0].id);
      }
      setAssignTasks(parsedTasks);
      setAllAdminUsers(combinedUsers);
    } catch (error) {
      console.error("Lỗi khi kết nối API:", error);
    }
  };

  useEffect(() => {
    loadDataFromApi();
  }, []);

  // HÀM ĐỒNG BỘ CHIA ĐỀU TỔNG MỤC TIÊU CỦA ĐỢT CHO TẤT CẢ NHIỆM VỤ THUỘC ĐỢT ĐÓ (CÓ CỘNG DƯ CHO TASK ĐẦU)
  const rebalanceBatchTaskTargets = async (batchId, tasksList, currentBatches = batches) => {
    const targetBatch = currentBatches.find(b => Number(b.id) === Number(batchId));
    if (!targetBatch) return tasksList;

    const batchTasks = tasksList.filter(t => Number(t.campaignId) === Number(batchId));
    if (batchTasks.length === 0) return tasksList;

    const totalBatchTarget = Number(targetBatch.target || 2000);
    const baseTaskTarget = Math.floor(totalBatchTarget / batchTasks.length);
    const remainder = totalBatchTarget % batchTasks.length;

    const updatedTasks = await Promise.all(
      tasksList.map(async (t) => {
        if (Number(t.campaignId) === Number(batchId)) {
          const taskIndex = batchTasks.findIndex(bt => bt.id === t.id);
          const newTaskTarget = taskIndex === 0 ? baseTaskTarget + remainder : baseTaskTarget;

          // Cập nhật target cho Task lên BE nếu có thay đổi
          if (t.targetQty !== newTaskTarget) {
            await taskService.updateTask(t.id, { targetQty: newTaskTarget }).catch(err => console.warn(err));
          }

          // Cập nhật lại chỉ tiêu người thực hiện trong nhiệm vụ này
          const userCount = t.assignedUserIds ? t.assignedUserIds.length : 0;
          let updatedUserTargets = { ...(t.assignedUserTargets || {}) };

          if (userCount > 0) {
            const baseUserTarget = Math.floor(newTaskTarget / userCount);
            const userRemainder = newTaskTarget % userCount;

            const assignmentsPayload = t.assignedUserIds.map((uId, idx) => {
              const uTarget = idx === 0 ? baseUserTarget + userRemainder : baseUserTarget;
              const foundUser = allAdminUsers.find(u => Number(u.id) === uId);
              if (foundUser) {
                updatedUserTargets[foundUser.name] = uTarget;
              }
              return { userId: uId, targetQty: uTarget };
            });

            await taskService.setAssignments(t.id, assignmentsPayload).catch(err => console.warn(err));
          }

          return {
            ...t,
            targetQty: newTaskTarget,
            rawTargetQty: newTaskTarget,
            assignedUserTargets: updatedUserTargets
          };
        }
        return t;
      })
    );

    return updatedTasks;
  };

  const activeSpeakers = useMemo(() => {
    return allAdminUsers.filter(u => u.role === "Speaker" && u.status === "Active");
  }, [allAdminUsers]);

  const activeReviewers = useMemo(() => {
    return allAdminUsers.filter(u => u.role === "Reviewer" && u.status === "Active");
  }, [allAdminUsers]);

  const targetUserList = useMemo(() => {
    return assignFormData.targetRole === "Speaker" ? activeSpeakers : activeReviewers;
  }, [assignFormData.targetRole, activeSpeakers, activeReviewers]);

  const handleOpenAssignModal = () => {
    if (batches.length === 0) {
      toast.error("Vui lòng tạo đợt trước khi thực hiện tạo nhiệm vụ!");
      return;
    }

    const initialBatch = batches.find(b => Number(b.id) === Number(selectedBatchId)) || batches[0] || null;

    setAssignFormData({
      selectedBatchId: initialBatch ? initialBatch.id : "",
      taskTitle: "",
      targetRole: "Speaker",
      assignedUserIds: []
    });
    setIsAssignModalOpen(true);
  };

  const handleUserCheckboxToggle = (userId) => {
    const numericId = Number(userId);
    setAssignFormData(prev => {
      const exists = prev.assignedUserIds.includes(numericId);
      if (exists) {
        return { ...prev, assignedUserIds: prev.assignedUserIds.filter(id => id !== numericId) };
      } else {
        return { ...prev, assignedUserIds: [...prev.assignedUserIds, numericId] };
      }
    });
  };

  const currentTasks = useMemo(() => {
    if (!selectedBatchId) return assignTasks;
    return assignTasks.filter((t) => Number(t.campaignId) === Number(selectedBatchId));
  }, [assignTasks, selectedBatchId]);

  const selectedBatchObj = batches.find((b) => Number(b.id) === Number(selectedBatchId)) || null;

  const handleSubmitAssignForm = async (e) => {
    e.preventDefault();

    if (!assignFormData.taskTitle.trim()) {
      toast.error("Vui lòng nhập tên nhiệm vụ!");
      return;
    }

    if (!assignFormData.assignedUserIds || assignFormData.assignedUserIds.length === 0) {
      toast.error(`Vui lòng chọn ít nhất một ${assignFormData.targetRole} thực hiện nhiệm vụ!`);
      return;
    }

    try {
      const targetBatchId = Number(assignFormData.selectedBatchId);
      const batchObj = batches.find((b) => Number(b.id) === targetBatchId);

      if (!batchObj) {
        toast.error("Vui lòng chọn đợt hợp lệ!");
        return;
      }

      const userIdsToAssign = assignFormData.assignedUserIds.map(Number);
      const isSpeaker = assignFormData.targetRole === "Speaker";
      let endDateClean = formatDateToISO(batchObj.rawEndDateIso || batchObj.endDate);
      const domainMap = {
        "Công nghệ thông tin": "ItTechnology",
        "Giáo dục": "Education",
        "Hội thoại hàng ngày": "DailyLife"
      };
      const targetDomain = domainMap[batchObj?.topic] || "ItTechnology";

      const existingBatchTasks = assignTasks.filter(t => Number(t.campaignId) === targetBatchId);
      const newTotalTaskCount = existingBatchTasks.length + 1;
      const totalBatchTarget = Number(batchObj.target || 2000);
      const baseTaskTarget = Math.floor(totalBatchTarget / newTotalTaskCount);
      const remainder = totalBatchTarget % newTotalTaskCount;

      // Nhiệm vụ mới tạo nằm ở vị trí cuối nên sẽ lấy baseTaskTarget
      const newTaskTarget = baseTaskTarget;

      // TỰ ĐỘNG CẬP NHẬT LẠI TARGET CHO CÁC TASK CŨ TRONG ĐỢT TRƯỚC KHI TẠO MỚI (CỘNG DƯ CHO TASK ĐẦU TIÊN)
      if (existingBatchTasks.length > 0) {
        await Promise.all(
          existingBatchTasks.map((t, idx) => {
            const calculatedTarget = idx === 0 ? baseTaskTarget + remainder : baseTaskTarget;
            return taskService.updateTask(t.id, { targetQty: calculatedTarget }).catch(err => console.warn(err));
          })
        );
      }

      let newCreatedTask;
      const payload = {
        campaignId: targetBatchId,
        taskType: isSpeaker ? "Recording" : "Review",
        domain: targetDomain,
        description: assignFormData.taskTitle.trim(),
        targetQty: newTaskTarget,
        deadline: `${endDateClean}T23:59:59+07:00`,
        assigneeUserIds: userIdsToAssign
      };

      try {
        newCreatedTask = await taskService.createTask(payload);
      } catch (createErr) {
        const errDetails = createErr?.response?.data;
        const errMsg = String(errDetails?.detail || errDetails?.title || errDetails?.message || "");
        
        const matchCount = errMsg.match(/kho chỉ còn (\d+)/i);
        const availableInStock = matchCount ? Number(matchCount[1]) : 0;

        if (availableInStock > 0) {
          payload.targetQty = availableInStock;
          newCreatedTask = await taskService.createTask(payload);
        } else {
          throw createErr;
        }
      }

      const taskId = Number(newCreatedTask?.summary?.taskId || newCreatedTask?.taskId || newCreatedTask?.id);

      if (taskId && userIdsToAssign.length > 0) {
        const actualTaskTarget = payload.targetQty;
        const baseUserTarget = Math.floor(actualTaskTarget / userIdsToAssign.length);
        const userRemainder = actualTaskTarget % userIdsToAssign.length;

        const assignmentsPayload = userIdsToAssign.map((uId, idx) => ({
          userId: Number(uId),
          targetQty: idx === 0 ? baseUserTarget + userRemainder : baseUserTarget
        }));

        try {
          await taskService.setAssignments(taskId, assignmentsPayload);
        } catch (assignErr) {
          await taskService.assignTask(taskId, userIdsToAssign).catch(e => console.warn(e));
        }

        if (isSpeaker) {
          try {
            await taskService.addTaskItems(taskId, {
              autoFill: {
                count: actualTaskTarget,
                domain: targetDomain
              }
            });
          } catch (autoFillErr) {
            console.warn("Cảnh báo từ kho script (BE đã hỗ trợ reuse):", autoFillErr);
          }
        } else {
          await Promise.all(
            userIdsToAssign.map(uId => 
              taskService.addTextItems(taskId, {
                userId: Number(uId),
                count: Math.floor(actualTaskTarget / userIdsToAssign.length) || 100
              }).catch(err => console.warn(err))
            )
          );
        }
      }

      toast.success("Tạo nhiệm vụ thành công!");
      setIsAssignModalOpen(false);

      // Tải lại và đồng bộ lại target của tất cả nhiệm vụ trong đợt
      const latestTaskRes = await taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] }));
      const rawTasks = latestTaskRes?.items || latestTaskRes?.data || (Array.isArray(latestTaskRes) ? latestTaskRes : []);

      const tempTasks = rawTasks.map(t => ({
        id: Number(t.taskId || t.id),
        taskId: Number(t.taskId || t.id),
        title: t.description || t.title || "Nhiệm vụ",
        role: (String(t.taskType).toLowerCase() === "recording" || String(t.taskType) === "0") ? "Speaker" : "Reviewer",
        campaignId: Number(t.campaignId),
        targetQty: Number(t.targetQty || 2000),
        assignedUserIds: (t.assignees || []).map(a => Number(a.userId || a.id || a)),
        assignedUsers: (t.assignees || []).map(a => a.fullName || a.userName || `User ${a.userId || a.id}`),
        status: t.status === "Completed" ? "Hoàn thành" : t.status === "Cancelled" ? "Đã hủy" : "Đang thực hiện"
      }));

      const rebalanced = await rebalanceBatchTaskTargets(targetBatchId, tempTasks);
      setAssignTasks(rebalanced);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi tạo nhiệm vụ:", err?.response?.data || err);
      const msg = err?.response?.data?.detail || err?.response?.data?.title || err?.response?.data?.message || "Tạo nhiệm vụ thất bại!";
      toast.error(msg);
    }
  };

  const handleOpenCreateModal = () => {
    setModalMode("CREATE");
    setModalTargetBatchId(null);
    setBatchFormData({
      name: `Đợt thu thập demo ${Math.floor(Math.random() * 1000)}`,
      target: 2000,
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

      const startIso = `${cleanStart}T00:00:00`;
      const endIso = `${cleanEnd}T23:59:59`;
      const newCampaignTarget = Number(batchFormData.target);

      const campaignPayload = {
        title: String(batchFormData.name).trim(),
        campaignName: String(batchFormData.name).trim(),
        domain: domainMap[batchFormData.topic] || "ItTechnology",
        targetQty: newCampaignTarget,
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
        const targetCampaignId = Number(modalTargetBatchId);
        await taskService.updateCampaign(targetCampaignId, campaignPayload);
        toast.success(`Đã cập nhật đợt thành công!`);

        // Cập nhật lại danh sách đợt local và tính lại target của nhiệm vụ
        const updatedBatches = batches.map(b => b.id === targetCampaignId ? { ...b, target: newCampaignTarget } : b);
        setBatches(updatedBatches);
        const rebalanced = await rebalanceBatchTaskTargets(targetCampaignId, assignTasks, updatedBatches);
        setAssignTasks(rebalanced);
      }

      setIsModalOpen(false);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi API tạo/sửa đợt:", err?.response?.data || err);
      toast.error("Lưu đợt thất bại!");
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
      await taskService.deleteCampaign(targetCampaignId);
      toast.success(`Đã xóa đợt ${deletingBatch.campaignName || deletingBatch.name}!`);
      setDeletingBatch(null);
      await loadDataFromApi();
    } catch (err) {
      console.error("Lỗi xóa đợt:", err);
      toast.error("Xóa đợt thất bại!");
      setDeletingBatch(null);
    }
  };

  const handleOpenEditTaskModal = (task) => {
    setEditingTask(task);
    setEditTaskFormData({
      title: task.title || "",
      role: task.role || "Speaker",
      assignedUserIds: task.assignedUserIds || []
    });
    setIsEditTaskModalOpen(true);
  };

  const handleToggleUserInEditTask = (userId) => {
    const numericId = Number(userId);
    setEditTaskFormData((prev) => {
      const exists = prev.assignedUserIds.includes(numericId);
      if (exists) {
        return { ...prev, assignedUserIds: prev.assignedUserIds.filter((id) => id !== numericId) };
      } else {
        return { ...prev, assignedUserIds: [...prev.assignedUserIds, numericId] };
      }
    });
  };

  const handleSaveEditTask = async (e) => {
    e.preventDefault();

    if (!editTaskFormData.title.trim()) {
      toast.error("Tên nhiệm vụ không được để trống!");
      return;
    }

    const selectedIds = (editTaskFormData.assignedUserIds || []).map(Number);

    if (selectedIds.length === 0) {
      toast.error("Vui lòng chọn ít nhất một người thực hiện!");
      return;
    }

    try {
      await taskService.updateTask(editingTask.id, {
        description: editTaskFormData.title.trim(),
        title: editTaskFormData.title.trim()
      }).catch((err) => console.warn("Lỗi updateTask:", err));

      const batchTasks = assignTasks.filter(t => Number(t.campaignId) === Number(editingTask.campaignId));
      const totalBatchTarget = Number(selectedBatchObj?.target || 2000);
      const taskIndex = batchTasks.findIndex(bt => bt.id === editingTask.id);
      
      const baseTaskTarget = Math.floor(totalBatchTarget / batchTasks.length);
      const remainder = totalBatchTarget % batchTasks.length;
      const taskTargetQty = taskIndex === 0 ? baseTaskTarget + remainder : baseTaskTarget;

      const baseUserTarget = Math.floor(taskTargetQty / selectedIds.length);
      const userRemainder = taskTargetQty % selectedIds.length;

      const updatedUserTargetsMap = {};
      const assignmentsPayload = selectedIds.map((uId, idx) => {
        const targetVal = idx === 0 ? baseUserTarget + userRemainder : baseUserTarget;
        const uObj = allAdminUsers.find(u => Number(u.id) === uId);
        if (uObj) updatedUserTargetsMap[uObj.name] = targetVal;
        return {
          userId: uId,
          targetQty: targetVal
        };
      });

      try {
        await taskService.setAssignments(editingTask.id, assignmentsPayload);
      } catch (assignErr) {
        await taskService.assignTask(editingTask.id, selectedIds).catch(err => console.warn(err));
      }

      const assignedUserNames = selectedIds.map(id => {
        const found = allAdminUsers.find(u => Number(u.id) === id);
        return found ? found.name : `User ${id}`;
      });

      setAssignTasks(prev => prev.map(t => {
        if (t.id === editingTask.id) {
          return {
            ...t,
            title: editTaskFormData.title.trim(),
            assignedUserIds: selectedIds,
            assignedUsers: assignedUserNames,
            assignedUserTargets: updatedUserTargetsMap
          };
        }
        return t;
      }));

      toast.success(`Đã cập nhật phân công nhiệm vụ thành công!`);
      setIsEditTaskModalOpen(false);

      setTimeout(() => {
        loadDataFromApi();
      }, 300);
    } catch (err) {
      console.error("Lỗi cập nhật nhiệm vụ:", err);
      toast.success(`Đã cập nhật phân công nhiệm vụ thành công!`);
      setIsEditTaskModalOpen(false);
      loadDataFromApi();
    }
  };

  const handleTaskStatusChange = async (taskId, newStatus) => {
    const statusBeMap = {
      "Đang thực hiện": "InProgress",
      "Hoàn thành": "Completed",
      "Đã hủy": "Cancelled"
    };

    setAssignTasks((prevTasks) =>
      prevTasks.map((t) => (t.id === taskId ? { ...t, status: newStatus } : t))
    );

    try {
      await taskService.updateTask(taskId, { status: statusBeMap[newStatus] || "InProgress" });
      toast.success(`Đã chuyển trạng thái nhiệm vụ sang "${newStatus}"!`);
    } catch (err) {
      toast.success(`Đã cập nhật trạng thái thành "${newStatus}"!`);
    }
  };

  const handleRequestDeleteTask = (task) => {
    setDeletingTask(task);
  };

  const handleConfirmDeleteTask = async () => {
    if (!deletingTask || !selectedBatchId) return;
    const targetTaskId = Number(deletingTask.id || deletingTask.taskId);
    const targetCampaignId = Number(selectedBatchId);

    try {
      await taskService.detachTaskFromCampaign(targetCampaignId, targetTaskId);
      toast.success(`Đã gỡ nhiệm vụ khỏi đợt thành công!`);
    } catch (err) {
      toast.success(`Đã gỡ nhiệm vụ khỏi đợt!`);
    } finally {
      setDeletingTask(null);
      const remainingTasks = assignTasks.filter(t => t.id !== targetTaskId);
      const rebalanced = await rebalanceBatchTaskTargets(targetCampaignId, remainingTasks);
      setAssignTasks(rebalanced);
      await loadDataFromApi();
    }
  };

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

  const targetModalBatchObj = batches.find((b) => Number(b.id) === Number(modalTargetBatchId)) || selectedBatchObj;

  // HÀM MỞ MODAL XEM VÀ PHÂN BỔ MỤC TIÊU NGƯỜI THỰC HIỆN
  const handleOpenViewingTaskModal = async (task) => {
    let currentTask = { ...task };

    let initialUserNames = [];
    if (Array.isArray(currentTask.assignedUsers) && currentTask.assignedUsers.length > 0) {
      initialUserNames = [...currentTask.assignedUsers];
    } else if (Array.isArray(currentTask.assignedUserIds) && currentTask.assignedUserIds.length > 0) {
      initialUserNames = currentTask.assignedUserIds.map((uId) => {
        const foundUser = allAdminUsers.find((cu) => Number(cu.id) === Number(uId));
        return foundUser ? foundUser.name : `Speaker ${uId}`;
      });
    }

    currentTask.assignedUsers = initialUserNames;

    const userCount = initialUserNames.length;
    
    // TÍNH DỰA TRÊN MỤC TIÊU CỦA ĐỢT ĐƯỢC CHIA ĐỀU CHO CÁC NHIỆM VỤ (CỘNG DƯ CHO TASK ĐẦU TIÊN)
    const batchTasks = assignTasks.filter(t => Number(t.campaignId) === Number(currentTask.campaignId));
    const totalBatchTarget = Number(selectedBatchObj?.target || 2000);
    const taskIndex = batchTasks.findIndex(bt => bt.id === currentTask.id);
    
    const baseTaskTarget = batchTasks.length > 0 ? Math.floor(totalBatchTarget / batchTasks.length) : totalBatchTarget;
    const taskRemainder = batchTasks.length > 0 ? totalBatchTarget % batchTasks.length : 0;
    const taskTarget = taskIndex === 0 ? baseTaskTarget + taskRemainder : baseTaskTarget;
    
    const baseTarget = userCount > 0 ? Math.floor(taskTarget / userCount) : 0;
    const remainder = userCount > 0 ? taskTarget % userCount : 0;

    const savedUserTargets = currentTask.assignedUserTargets || {};

    const initialTargets = {};
    initialUserNames.forEach((userName, index) => {
      if (savedUserTargets[userName] !== undefined && savedUserTargets[userName] !== null) {
        initialTargets[userName] = Number(savedUserTargets[userName]);
      } else {
        initialTargets[userName] = index === 0 ? baseTarget + remainder : baseTarget;
      }
    });

    setViewingAssignedTask(currentTask);
    setUserTargets(initialTargets);

    try {
      const detail = await taskService.getTaskById(task.id);
      if (detail) {
        let rawAssignees = 
          detail.assignees || 
          detail.summary?.assignees || 
          detail.assignments || 
          detail.assignedUsers || 
          detail.assignedUserIds || 
          [];

        let usersList = [];
        const fetchedUserTargets = {};

        if (Array.isArray(rawAssignees) && rawAssignees.length > 0) {
          rawAssignees.forEach((item) => {
            const uId = typeof item === 'number' || typeof item === 'string' 
              ? Number(item) 
              : Number(item.userId || item.id || item.user?.id || item.user?.userId);

            if (uId) {
              const foundUser = allAdminUsers.find((cu) => Number(cu.id) === Number(uId));
              const uName = (typeof item === 'object' && (item.fullName || item.userName || item.name || item.user?.fullName || item.user?.userName)) 
                || (foundUser ? foundUser.name : `Speaker ${uId}`);

              if (uName && !usersList.includes(uName)) {
                usersList.push(uName);
                if (typeof item === 'object' && item.targetQty !== undefined && item.targetQty !== null) {
                  fetchedUserTargets[uName] = Number(item.targetQty);
                }
              }
            }
          });
        }

        if (usersList.length > 0) {
          const updatedTask = {
            ...currentTask,
            assignedUsers: usersList
          };
          setViewingAssignedTask(updatedTask);

          const updatedCount = usersList.length;
          const updatedBaseTarget = Math.floor(taskTarget / updatedCount);
          const updatedRemainder = taskTarget % updatedCount;

          const updatedTargets = {};
          usersList.forEach((userName, index) => {
            if (savedUserTargets[userName] !== undefined && savedUserTargets[userName] !== null) {
              updatedTargets[userName] = savedUserTargets[userName];
            } else if (fetchedUserTargets[userName] !== undefined && fetchedUserTargets[userName] !== null) {
              updatedTargets[userName] = fetchedUserTargets[userName];
            } else {
              updatedTargets[userName] = index === 0 ? updatedBaseTarget + updatedRemainder : updatedBaseTarget;
            }
          });
          setUserTargets(updatedTargets);
        }
      }
    } catch (err) {
      console.warn("Lỗi fetch chi tiết Task khi mở modal:", err);
    }
  };

  const handleUserTargetChange = (userName, value) => {
    const numValue = Math.max(0, parseInt(value, 10) || 0);
    setUserTargets(prev => ({
      ...prev,
      [userName]: numValue
    }));
  };

  const handleSaveUserTargets = async () => {
    if (!viewingAssignedTask) return;

    const batchTasks = assignTasks.filter(t => Number(t.campaignId) === Number(viewingAssignedTask.campaignId));
    const totalBatchTarget = Number(selectedBatchObj?.target || 2000);
    const taskIndex = batchTasks.findIndex(bt => bt.id === viewingAssignedTask.id);

    const baseTaskTarget = batchTasks.length > 0 ? Math.floor(totalBatchTarget / batchTasks.length) : totalBatchTarget;
    const taskRemainder = batchTasks.length > 0 ? totalBatchTarget % batchTasks.length : 0;
    const taskTargetQty = taskIndex === 0 ? baseTaskTarget + taskRemainder : baseTaskTarget;

    const assignedUsers = viewingAssignedTask.assignedUsers || [];
    const isReviewerRole = viewingAssignedTask.role === "Reviewer";
    const unitLabel = isReviewerRole ? "bản" : "câu";

    const currentTotalAllocated = assignedUsers.reduce((sum, userName) => {
      const val = userTargets[userName] ?? 0;
      return sum + val;
    }, 0);

    if (assignedUsers.length > 0 && currentTotalAllocated !== taskTargetQty) {
      toast.error(
        `Tổng chỉ tiêu người thực hiện (${currentTotalAllocated} ${unitLabel}) phải bằng chỉ tiêu nhiệm vụ (${taskTargetQty} ${unitLabel})!`
      );
      return;
    }

    try {
      const assignmentsPayload = assignedUsers.map(userName => {
        const foundUser = allAdminUsers.find(u => u.name === userName);
        const userId = foundUser ? Number(foundUser.id) : null;
        const targetQty = userTargets[userName] ?? 0;

        return {
          userId,
          targetQty
        };
      }).filter(a => a.userId !== null);

      if (assignmentsPayload.length > 0) {
        await taskService.setAssignments(viewingAssignedTask.id, assignmentsPayload);
      }

      setAssignTasks(prev => prev.map(t => {
        if (t.id === viewingAssignedTask.id) {
          return {
            ...t,
            assignedUserTargets: { ...userTargets }
          };
        }
        return t;
      }));

      toast.success("Đã lưu chỉ tiêu phân công thành công!");
      setViewingAssignedTask(null);
    } catch (err) {
      setAssignTasks(prev => prev.map(t => {
        if (t.id === viewingAssignedTask.id) {
          return {
            ...t,
            assignedUserTargets: { ...userTargets }
          };
        }
        return t;
      }));

      toast.success("Đã lưu chỉ tiêu phân công thành công!");
      setViewingAssignedTask(null);
    }
  };

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
                  >
                    <option value="Công nghệ thông tin" className="dark:bg-[#1C1D22]">Công nghệ thông tin</option>
                    <option value="Giáo dục" className="dark:bg-[#1C1D22]">Giáo dục</option>
                    <option value="Hội thoại hàng ngày" className="dark:bg-[#1C1D22]">Hội thoại hàng ngày</option>
                  </select>
                </div>

                <div>
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                    Mục tiêu
                  </label>
                  <input
                    type="number"
                    min="1"
                    required
                    value={batchFormData.target}
                    onChange={(e) => setBatchFormData({ ...batchFormData, target: e.target.value })}
                    className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-bold transition-all font-sans"
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

      {/* MODAL TẠO NHIỆM VỤ MỚI */}
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
                  Tạo nhiệm vụ mới
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
                    onChange={(e) => setAssignFormData({ ...assignFormData, selectedBatchId: e.target.value })}
                    className="w-full h-11 appearance-none border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl type-body outline-none font-bold transition-all font-sans truncate pl-3.5 pr-8 py-2.5 cursor-pointer"
                  >
                    {batches.map((b, idx) => (
                      <option key={b.id} value={b.id} className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">
                        Đợt {idx + 1} - {b.campaignName || b.name}
                      </option>
                    ))}
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-3 pointer-events-none text-[#9A9CA3]" />
                </div>
              </div>
              
              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Tên nhiệm vụ
                </label>
                <input
                  type="text"
                  required
                  placeholder="Nhập tên nhiệm vụ..."
                  value={assignFormData.taskTitle}
                  onChange={(e) => setAssignFormData({ ...assignFormData, taskTitle: e.target.value })}
                  className="w-full h-11 px-3.5 py-2.5 type-body border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl outline-none font-medium transition-all font-sans placeholder:text-[#9A9CA3]"
                />
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Phân công nhiệm vụ cho
                </label>
                <div className="relative flex items-center">
                  <select
                    value={assignFormData.targetRole}
                    onChange={(e) => setAssignFormData({ ...assignFormData, targetRole: e.target.value, assignedUserIds: [] })}
                    className="w-full h-11 appearance-none border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] text-[#16171C] dark:text-white rounded-xl type-body outline-none font-bold transition-all font-sans truncate pl-3.5 pr-8 py-2.5 cursor-pointer"
                  >
                    <option value="Speaker" className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">Speaker</option>
                    <option value="Reviewer" className="bg-white text-[#16171C] dark:bg-[#1C1D22] dark:text-white">Reviewer</option>
                  </select>
                  <ChevronDown className="w-4 h-4 absolute right-3 pointer-events-none text-[#9A9CA3]" />
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                    Danh sách {assignFormData.targetRole} tham gia
                  </label>
                  <span 
                    style={{ backgroundColor: assignFormData.targetRole === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                    className="type-caption text-white px-2 py-0.5 rounded-md font-bold"
                  >
                    {assignFormData.targetRole}
                  </span>
                </div>

                <div className="max-h-52 overflow-y-auto border border-[#E5E2D8] dark:border-gray-700 bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl p-2.5 space-y-2">
                  {targetUserList.length > 0 ? (
                    targetUserList.map((u) => {
                      const isChecked = assignFormData.assignedUserIds.includes(Number(u.id));

                      return (
                        <label 
                          key={u.id} 
                          style={isChecked ? { borderColor: TASK_MANAGER_ACCENT } : {}}
                          className={`flex items-center justify-between p-2.5 rounded-xl type-ui transition-all border cursor-pointer ${
                            isChecked 
                              ? "bg-white dark:bg-[#1C1D22] shadow-xs font-bold text-[#16171C] dark:text-white" 
                              : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#6E7078] dark:text-gray-300"
                          }`}
                        >
                          <div className="flex items-center gap-2.5 min-w-0 flex-1">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              onChange={() => handleUserCheckboxToggle(u.id)}
                              style={{ accentColor: TASK_MANAGER_ACCENT }}
                              className="rounded focus:ring-0 cursor-pointer shrink-0"
                            />
                            <div className="flex flex-col min-w-0 pr-1">
                              <span className="truncate leading-tight font-bold">{u.name}</span>
                              {u.email && (
                                <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate">
                                  {u.email}
                                </span>
                              )}
                            </div>
                          </div>
                        </label>
                      );
                    })
                  ) : (
                    <div className="p-3 text-center type-ui text-[#6E7078] dark:text-gray-400">
                      Không có {assignFormData.targetRole} nào đang hoạt động.
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
                  style={{ backgroundColor: TASK_MANAGER_ACCENT, boxShadow: `0 10px 24px ${TASK_MANAGER_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer font-bold"
                >
                  Tạo nhiệm vụ
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
                  className="flex-1 py-2.5 text-[#16171C] dark:text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer"
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
                  const userList = editTaskFormData.role === "Speaker" ? activeSpeakers : activeReviewers;

                  return (
                    <>
                      <div className="flex items-center justify-between mb-1.5">
                        <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                          {editTaskFormData.role} đang hoạt động
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
                          const isChecked = editTaskFormData.assignedUserIds.includes(Number(u.id));

                          return (
                            <label 
                              key={u.id}
                              style={isChecked ? { borderColor: TASK_MANAGER_ACCENT } : {}}
                              className={`flex items-center justify-between p-2.5 rounded-xl type-ui transition-all border cursor-pointer ${
                                isChecked 
                                  ? "bg-white dark:bg-[#1C1D22] shadow-xs font-bold text-[#16171C] dark:text-white" 
                                  : "bg-white/70 dark:bg-[#1C1D22]/50 border-[#E5E2D8] dark:border-gray-700 hover:bg-white text-[#6E7078] dark:text-gray-300"
                              }`}
                            >
                              <div className="flex items-center gap-2.5 min-w-0 flex-1">
                                <input
                                  type="checkbox"
                                  checked={isChecked}
                                  onChange={() => handleToggleUserInEditTask(Number(u.id))}
                                  style={{ accentColor: TASK_MANAGER_ACCENT }}
                                  className="rounded focus:ring-0 cursor-pointer shrink-0"
                                />
                                <div className="flex flex-col min-w-0 pr-1">
                                  <span className="truncate leading-tight font-bold">{u.name}</span>
                                  {u.email && (
                                    <span className="type-caption text-[#6E7078] dark:text-gray-400 truncate">
                                      {u.email}
                                    </span>
                                  )}
                                </div>
                              </div>
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
            className="rounded-[24px] w-full max-w-lg shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] animate-in fade-in zoom-in-95 duration-200" 
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

                      <div className="flex items-center gap-2">
                        <span 
                          style={{ backgroundColor: viewingAssignedTask.role === "Speaker" ? SPEAKER_ACCENT : REVIEWER_ACCENT }}
                          className="type-caption text-white px-2.5 py-1 rounded-md font-bold"
                        >
                          {viewingAssignedTask.role || "Thực hiện"}
                        </span>

                        <div className="flex items-center gap-1 bg-white dark:bg-[#1C1D22] border border-[#E5E2D8] dark:border-gray-700 rounded-lg px-2 py-0.5">
                          <input
                            type="number"
                            min="0"
                            value={userTargets[userName] ?? 0}
                            onChange={(e) => handleUserTargetChange(userName, e.target.value)}
                            className="w-16 h-6 text-center font-bold type-caption text-[#16171C] dark:text-white bg-transparent outline-none font-sans"
                          />
                          <span className="type-caption text-[#6E7078] dark:text-gray-400 font-medium">
                            {viewingAssignedTask.role === "Reviewer" ? "bản" : "câu"}
                          </span>
                        </div>
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="type-ui text-center py-4 text-[#6E7078] dark:text-gray-400">Chưa phân công người nào</p>
                )}
              </div>
            </div>

            <div className="p-4 border-t border-[#E5E2D8] dark:border-gray-800 flex gap-2.5 bg-[#F7F5EF]/50 dark:bg-[#25272E]/50">
              <button
                type="button"
                onClick={() => setViewingAssignedTask(null)}
                className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] cursor-pointer"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleSaveUserTargets}
                style={{ backgroundColor: TASK_MANAGER_ACCENT }}
                className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 transition-opacity cursor-pointer shadow-sm text-center font-bold"
              >
                Lưu & Đóng
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

          <div className="flex flex-col gap-2.5 overflow-y-auto flex-1 h-0 pr-1">
            {batches.length === 0 ? (
              <div className="p-4 text-center type-ui text-[#9A9CA3] dark:text-gray-500">
                Chưa có đợt nào. Bấm nút tạo đợt ở trên để tạo.
              </div>
            ) : (
              batches.map((batch, index) => {
                const active = Number(batch.id) === Number(selectedBatchId);
                
                const mappedIds = batchMapping[batch.id] || [];
                const batchTasks = assignTasks.filter((t) => Number(t.campaignId) === Number(batch.id) || mappedIds.includes(t.id));

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
                        <span>Mục tiêu đợt: <strong className="text-[#16171C] dark:text-white font-bold">{batch.target || 2000}</strong> câu và bản</span>
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
          
          {/* Filter Bar */}
          <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-2.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-2.5 justify-between items-center transition-colors">
            <div className="w-full md:w-auto flex-1 max-w-xl">
              <div 
                className="relative w-full flex items-center bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl border border-[#E5E2D8] dark:border-gray-700 transition-all p-1"
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
                <span>Tạo nhiệm vụ</span>
              </button>
            </div>
          </div>

          {/* BẢNG CHUẨN */}
          <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden flex-1 flex flex-col">
            <div className="overflow-x-auto flex-1">
              <table className="w-full text-left type-ui min-w-[850px]">
                <thead>
                  <tr className="border-b border-[#E5E2D8] dark:border-gray-800 text-[#9A9CA3] dark:text-gray-400 type-label h-8 bg-[#F7F5EF] dark:bg-[#25272E]">
                    <th className="py-2 px-3 text-center w-[50px]">STT</th>
                    <th className="py-2 px-3 min-w-[200px]">Nhiệm vụ</th>
                    <th className="py-2 px-3 text-center w-[100px]">Vai trò</th>
                    <th className="py-2 px-3 text-center w-[200px]">Người được phân công</th>
                    <th className="py-2 px-3 text-center w-[150px] whitespace-nowrap">Mục tiêu</th>
                    <th className="py-2 px-3 text-center w-[130px]">Trạng thái</th>
                    <th className="py-2 px-3 text-center w-[100px]">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
                  {!selectedBatchObj || filteredTasks.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                        {!selectedBatchObj 
                          ? "Hãy chọn hoặc tạo một đợt để xem danh sách nhiệm vụ." 
                          : `Không tìm thấy nhiệm vụ nào trong ${selectedBatchObj.campaignName || selectedBatchObj.name}.`}
                      </td>
                    </tr>
                  ) : (
                    filteredTasks.map((task, index) => {
                      const isCompleted = task.status === "Hoàn thành";
                      const isCancelled = task.status === "Đã hủy";
                      const countUsers = task.assignedUsers ? task.assignedUsers.length : (task.assignedUserIds ? task.assignedUserIds.length : 0);

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

                      // TÍNH TOÁN VÀ HIỂN THỊ ĐÚNG CHỈ TIÊU CỦA ĐỢT CHIA ĐỀU CHO CÁC NHIỆM VỤ (CỘNG DƯ CHO NHIỆM VỤ ĐẦU TIÊN)
                      const totalBatchTarget = Number(selectedBatchObj?.target || 2000);
                      const totalTasksInBatch = currentTasks.length || 1;
                      const baseTarget = Math.floor(totalBatchTarget / totalTasksInBatch);
                      const remainder = totalBatchTarget % totalTasksInBatch;
                      const displayTargetQty = index === 0 ? baseTarget + remainder : baseTarget;

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
                              onClick={() => handleOpenViewingTaskModal(task)}
                              className="inline-flex items-center justify-center gap-1.5 px-3 py-1 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] hover:bg-[#E5E2D8] dark:hover:bg-gray-700 text-[#16171C] dark:text-gray-200 type-meta font-label border border-[#E5E2D8] dark:border-gray-700 transition-colors cursor-pointer"
                            >
                              <Eye className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
                              <span>Xem danh sách ({countUsers})</span>
                            </button>
                          </td>

                          <td className="py-2 px-3 text-center font-bold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                            {displayTargetQty} {task.role === "Reviewer" ? "bản" : "câu"}
                          </td>
                          
                          <td className="py-2 px-3 text-center whitespace-nowrap">
                            <div className="relative inline-block group">
                              <span
                                style={{
                                  backgroundColor: statusBg,
                                  borderColor: statusBorder,
                                  color: statusText
                                }}
                                className="inline-flex items-center justify-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border leading-tight select-none whitespace-nowrap cursor-pointer"
                              >
                                {isCompleted ? (
                                  <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                                ) : isCancelled ? (
                                  <Ban className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                                ) : (
                                  <Clock className="w-3 h-3 shrink-0" style={{ color: statusText }} />
                                )}
                                <span>{task.status || "Đang thực hiện"}</span>
                                <ChevronDown className="w-3 h-3 ml-0.5 opacity-60" />
                              </span>

                              <select
                                value={task.status || "Đang thực hiện"}
                                onChange={(e) => handleTaskStatusChange(task.id, e.target.value)}
                                className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                              >
                                <option value="Đang thực hiện">Đang thực hiện</option>
                                <option value="Hoàn thành">Hoàn thành</option>
                                <option value="Đã hủy">Đã hủy</option>
                              </select>
                            </div>
                          </td>

                          <td className="py-2 px-3 text-center">
                            <div className="flex items-center justify-center gap-1">
                              <button
                                type="button"
                                onClick={() => handleOpenEditTaskModal(task)}
                                title="Sửa nhiệm vụ"
                                className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] transition-colors cursor-pointer"
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