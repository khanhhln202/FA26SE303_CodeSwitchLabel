import React, { useState, useEffect, useMemo } from "react";
import { 
  Target, 
  ListTodo, 
  Layers, 
  Clock, 
  CheckCircle2, 
  FolderKanban,
  ArrowRight
} from "lucide-react";
import { 
  TASK_MANAGER_ACCENT, 
  SPEAKER_ACCENT, 
  REVIEWER_ACCENT, 
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT
} from "../../../constants/theme";

// Khóa Storage đồng bộ trực tiếp với TaskManagerManagement
const ASSIGN_TASKS_STORAGE_KEY = "task_manager_assign_v8";
const BATCH_LIST_STORAGE_KEY = "task_manager_management_batches_v4";
const BATCH_ASSIGNMENT_STORAGE_KEY = "task_manager_management_batch_assignments_v4";

const FULL_DATASETS = [
  { id: "TSK-001", title: "Nhiệm vụ ghi âm thuật ngữ công nghệ", topic: "IT/Technology", target: 100, reviewed: 65, status: "Đang thực hiện" },
  { id: "TSK-002", title: "Nhiệm vụ ghi âm hội thoại giáo dục phổ thông", topic: "Education", target: 80, reviewed: 80, status: "Hoàn thành" },
  { id: "TSK-003", title: "Thu âm giao tiếp đời sống hàng ngày", topic: "Daily Life", target: 150, reviewed: 135, status: "Đang thực hiện" },
  { id: "TSK-004", title: "Đọc ngữ liệu lệnh thoại nhà thông minh", topic: "IT/Technology", target: 120, reviewed: 40, status: "Đang thực hiện" },
  { id: "TSK-005", title: "Thu âm kịch bản hỏi đáp y tế cơ bản", topic: "Daily Life", target: 90, reviewed: 0, status: "Chưa bắt đầu" },
  { id: "TSK-006", title: "Ghi âm bài giảng toán học trực tuyến", topic: "Education", target: 110, reviewed: 110, status: "Hoàn thành" },
  { id: "TSK-007", title: "Đọc tin tức kinh tế và thị trường tài chính", topic: "Daily Life", target: 70, reviewed: 20, status: "Đang thực hiện" },
  { id: "TSK-008", title: "Thu âm dữ liệu hội thoại bán hàng tự động", topic: "IT/Technology", target: 130, reviewed: 130, status: "Hoàn thành" }
];

export default function TaskManagerHome({ onNavigateToManagement, navigate }) {
  const [isHovered, setIsHovered] = useState(false);
  const [datasets] = useState(() => {
    const saved = localStorage.getItem("speaker_tasks_v1") || localStorage.getItem("task_manager_dataset_v3");
    if (saved) {
      try { return JSON.parse(saved); } catch (e) { console.error(e); }
    }
    return FULL_DATASETS;
  });

  // State đồng bộ từ TaskManagerManagement
  const [batches, setBatches] = useState([]);
  const [batchMapping, setBatchMapping] = useState({});
  const [assignTasks, setAssignTasks] = useState([]);

  // Tải toàn bộ dữ liệu quản lý đợt
  const loadBatchManagementData = () => {
    const savedBatches = localStorage.getItem(BATCH_LIST_STORAGE_KEY);
    if (savedBatches) {
      try { setBatches(JSON.parse(savedBatches)); } catch (e) { console.error(e); }
    } else {
      setBatches([
        { id: "BATCH-01", name: "Đợt 1" },
        { id: "BATCH-02", name: "Đợt 2" },
        { id: "BATCH-03", name: "Đợt 3" }
      ]);
    }

    const savedMapping = localStorage.getItem(BATCH_ASSIGNMENT_STORAGE_KEY);
    if (savedMapping) {
      try { setBatchMapping(JSON.parse(savedMapping)); } catch (e) { console.error(e); }
    }

    const savedTasks = localStorage.getItem(ASSIGN_TASKS_STORAGE_KEY);
    if (savedTasks) {
      try {
        const parsed = JSON.parse(savedTasks);
        if (Array.isArray(parsed)) {
          setAssignTasks(parsed);
        }
      } catch (e) { console.error(e); }
    }
  };

  useEffect(() => {
    loadBatchManagementData();

    const handleStorageChange = () => {
      loadBatchManagementData();
    };

    window.addEventListener("storage", handleStorageChange);
    window.addEventListener("assign_tasks_updated", handleStorageChange);
    return () => {
      window.removeEventListener("storage", handleStorageChange);
      window.removeEventListener("assign_tasks_updated", handleStorageChange);
    };
  }, []);

  // Điều hướng chính xác đến trang task-manager/management
  const handleNavigate = (e) => {
    if (e) e.preventDefault();

    // 1. Nếu có hàm callback từ props
    if (typeof onNavigateToManagement === "function") {
      onNavigateToManagement("management");
    }
    
    // 2. Nếu có prop navigate của React Router
    if (typeof navigate === "function") {
      navigate("/task-manager/management");
      return;
    }

    // 3. Phát Custom Event cho các Router/State Management ở Parent
    window.dispatchEvent(new CustomEvent("navigate_task_tab", { detail: "management" }));
    window.dispatchEvent(new CustomEvent("change_tab", { detail: "management" }));

    // 4. Đẩy đường dẫn URL mới và kích hoạt sự kiện Router
    if (window.location.pathname !== "/task-manager/management") {
      window.history.pushState({}, "", "/task-manager/management");
      window.dispatchEvent(new PopStateEvent("popstate"));
    }
  };

  // Tính toán trạng thái chính xác cho từng đợt nhiệm vụ
  const batchDisplayList = useMemo(() => {
    if (!batches || batches.length === 0) return [];

    return batches.map((batch, index) => {
      const mappedTaskIds = batchMapping[batch.id] || [];
      const batchTasks = assignTasks.filter((t) => mappedTaskIds.includes(t.id || t.taskId));

      const isBatchCompleted = batchTasks.length > 0 && batchTasks.every((t) => t.status === "Hoàn thành");

      return {
        batchId: batch.id,
        name: batch.name || `Đợt ${index + 1}`,
        status: isBatchCompleted ? "Hoàn thành" : "Đang thực hiện",
        isCompleted: isBatchCompleted
      };
    });
  }, [batches, batchMapping, assignTasks]);

  // Chỉ tiêu do Admin giao cho Task Manager
  const adminTargets = useMemo(() => {
    const totalSpeakerTarget = datasets.reduce((sum, d) => sum + (d.target || 0), 0);
    const totalSpeakerDone = datasets.reduce((sum, d) => sum + (d.reviewed || 0), 0);
    
    const totalReviewerTarget = Math.round(totalSpeakerTarget * 0.9);
    const totalReviewerDone = Math.round(totalSpeakerDone * 0.85);

    return {
      speaker: { target: totalSpeakerTarget, completed: totalSpeakerDone },
      reviewer: { target: totalReviewerTarget, completed: totalReviewerDone }
    };
  }, [datasets]);

  const speakerColor = SPEAKER_ACCENT || "#FF4B2E";
  const reviewerColor = REVIEWER_ACCENT || "#0052CC";

  return (
    <div className="w-full h-full flex flex-col gap-4 text-left font-sans p-1 overflow-y-auto">
      {/* Header */}
      <div>
        <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
          <FolderKanban className="w-6 h-6 text-slate-900 dark:text-white shrink-0" />
          Tổng quan chỉ tiêu & đợt nhiệm vụ
        </h2>
        <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
          Theo dõi chỉ tiêu từ Admin và danh sách các đợt nhiệm vụ đang triển khai
        </p>
      </div>

      {/* Cards Chỉ tiêu từ Admin */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Chỉ tiêu Speaker */}
        <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-5 shadow-xs relative overflow-hidden">
          <div className="flex items-center gap-2.5 mb-3">
            <div 
              className="p-3 rounded-full shrink-0"
              style={{ 
                backgroundColor: `${speakerColor}1F`, 
                color: speakerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-gray-900 dark:text-white">Chỉ tiêu Speaker</h3>
              <p className="text-[11px] text-gray-500 dark:text-gray-400">Admin giao cho Task Manager</p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 mt-3 bg-gray-50 dark:bg-[#25272E] p-3 rounded-xl border border-gray-100 dark:border-gray-800">
            <div>
              <span className="text-[10px] uppercase font-bold text-gray-400">Chỉ tiêu giao</span>
              <p className="text-lg font-extrabold text-gray-900 dark:text-white">{adminTargets.speaker.target.toLocaleString()} <span className="text-xs font-normal text-gray-500">câu</span></p>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-gray-400">Đã thu âm</span>
              <p className="text-lg font-extrabold" style={{ color: speakerColor }}>
                {adminTargets.speaker.completed.toLocaleString()} <span className="text-xs font-normal text-gray-500">câu</span>
              </p>
            </div>
          </div>
        </div>

        {/* Chỉ tiêu Reviewer */}
        <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-5 shadow-xs relative overflow-hidden">
          <div className="flex items-center gap-2.5 mb-3">
            <div 
              className="p-3 rounded-full shrink-0"
              style={{ 
                backgroundColor: `${reviewerColor}1F`, 
                color: reviewerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-gray-900 dark:text-white">Chỉ tiêu Reviewer</h3>
              <p className="text-[11px] text-gray-500 dark:text-gray-400">Admin giao cho Task Manager</p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 mt-3 bg-gray-50 dark:bg-[#25272E] p-3 rounded-xl border border-gray-100 dark:border-gray-800">
            <div>
              <span className="text-[10px] uppercase font-bold text-gray-400">Chỉ tiêu duyệt</span>
              <p className="text-lg font-extrabold text-gray-900 dark:text-white">{adminTargets.reviewer.target.toLocaleString()} <span className="text-xs font-normal text-gray-500">câu</span></p>
            </div>
            <div>
              <span className="text-[10px] uppercase font-bold text-gray-400">Đã kiểm duyệt</span>
              <p className="text-lg font-extrabold" style={{ color: reviewerColor }}>
                {adminTargets.reviewer.completed.toLocaleString()} <span className="text-xs font-normal text-gray-500">câu</span>
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Danh sách các Đợt nhiệm vụ */}
      <div className="rounded-2xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-[#1C1D22] shadow-xs p-4 flex-1 flex flex-col min-h-0">
        
        {/* Header danh sách đợt + Link màu xám hiện đại (Màu xám Slate chuẩn UI) */}
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-bold text-gray-900 dark:text-white flex items-center gap-2">
            <Layers style={{ color: TASK_MANAGER_ACCENT }} className="w-4 h-4" />
            Danh sách các đợt nhiệm vụ
          </h3>

          <a
            href="/task-manager/management"
            onClick={handleNavigate}
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}
            className="inline-flex items-center gap-1.5 text-xs font-semibold transition-colors cursor-pointer shrink-0"
          >
            <span 
              className={isHovered ? "" : "text-slate-500 dark:text-gray-300"}
              style={{ color: isHovered ? TASK_MANAGER_ACCENT : undefined }}
            >
              Đi đến trang quản lý nhiệm vụ
            </span>
            <ArrowRight 
              className={`w-3.5 h-3.5 ${isHovered ? "" : "text-slate-500 dark:text-gray-300"}`}
              style={{ color: isHovered ? TASK_MANAGER_ACCENT : undefined }}
            />
          </a>
        </div>

        <div className="overflow-x-auto flex-1">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-gray-200 dark:border-gray-800 text-gray-400 text-[10px] uppercase font-bold bg-gray-50 dark:bg-[#25272E]">
                <th className="py-2.5 px-3 min-w-[200px]">Đợt nhiệm vụ</th>
                <th className="py-2.5 px-3 text-center w-[160px]">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
              {batchDisplayList.length === 0 ? (
                <tr>
                  <td colSpan={2} className="py-8 text-center text-gray-400 font-medium">
                    Chưa có đợt nhiệm vụ nào được khởi tạo.
                  </td>
                </tr>
              ) : (
                batchDisplayList.map((item) => {
                  return (
                    <tr key={item.batchId} className="hover:bg-gray-50/80 dark:hover:bg-[#25272E]/50 transition-colors">
                      <td className="py-3 px-3 font-bold text-gray-900 dark:text-white">{item.name}</td>
                      
                      {/* Trạng thái chuẩn kích thước nhỏ 100% giống TaskManagerManagement */}
                      <td className="py-3 px-3 text-center">
                        <span 
                          style={{
                            backgroundColor: item.isCompleted ? CHIP_SUCCESS_BG : CHIP_WARNING_BG,
                            borderColor: item.isCompleted ? CHIP_SUCCESS_BORDER : CHIP_WARNING_BORDER,
                            color: item.isCompleted ? CHIP_SUCCESS_TEXT : CHIP_WARNING_TEXT
                          }}
                          className="inline-flex items-center gap-1 pl-2 pr-2.5 py-0.5 rounded-full text-[9.5px] font-bold border shrink-0 leading-tight"
                        >
                          {item.isCompleted ? (
                            <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} />
                          ) : (
                            <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_WARNING_TEXT }} />
                          )}
                          <span>{item.isCompleted ? "Hoàn thành" : "Đang thực hiện"}</span>
                        </span>
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
  );
}