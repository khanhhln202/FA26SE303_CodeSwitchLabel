import React, { useState, useEffect } from "react";
import { 
  ListTodo, 
  Layers, 
  Clock, 
  CheckCircle2, 
  FolderKanban,
  ArrowRight,
  Loader2
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
import { taskService } from "../../../services/taskService";

export default function TaskManagerHome({ onNavigateToManagement, navigate }) {
  const [isHovered, setIsHovered] = useState(false);
  const [loading, setLoading] = useState(true);
  const [campaigns, setCampaigns] = useState([]);
  const [assignTasks, setAssignTasks] = useState([]);
  const [batchMapping, setBatchMapping] = useState({});
  const [adminTargets, setAdminTargets] = useState({
    speaker: { target: 0, completed: 0 },
    reviewer: { target: 0, completed: 0 }
  });

  const loadData = async () => {
    try {
      setLoading(true);
      const [overviewData, campaignData, taskData] = await Promise.all([
        taskService.getOverview().catch(() => null),
        taskService.getCampaigns().catch(() => ({ items: [] })),
        taskService.getTasks().catch(() => ({ items: [] }))
      ]);

      if (overviewData) {
        setAdminTargets({
          speaker: {
            target: overviewData.speakerTarget || overviewData.totalSpeakerTarget || 0,
            completed: overviewData.speakerCompleted || overviewData.totalSpeakerCompleted || 0
          },
          reviewer: {
            target: overviewData.reviewerTarget || overviewData.totalReviewerTarget || 0,
            completed: overviewData.reviewerCompleted || overviewData.totalReviewerCompleted || 0
          }
        });
      }

      const campaignList = campaignData?.items || campaignData?.data || [];
      const rawTasks = taskData?.items || taskData?.data || [];

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
          campaignId: t.campaignId,
          status: calculatedStatus
        };
      });

      const mapping = {};
      mappedTasks.forEach((t) => {
        if (t.campaignId) {
          if (!mapping[t.campaignId]) mapping[t.campaignId] = [];
          mapping[t.campaignId].push(t.id);
        }
      });

      setCampaigns(campaignList);
      setAssignTasks(mappedTasks);
      setBatchMapping(mapping);
    } catch (error) {
      console.error("Lỗi tải trang Overview/Home:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleNavigate = (e) => {
    if (e) e.preventDefault();
    if (typeof onNavigateToManagement === "function") {
      onNavigateToManagement("management");
    }
    if (typeof navigate === "function") {
      navigate("/task-manager/management");
      return;
    }
    window.dispatchEvent(new CustomEvent("navigate_task_tab", { detail: "management" }));
    window.dispatchEvent(new CustomEvent("change_tab", { detail: "management" }));
    if (window.location.pathname !== "/task-manager/management") {
      window.history.pushState({}, "", "/task-manager/management");
      window.dispatchEvent(new PopStateEvent("popstate"));
    }
  };

  const speakerColor = SPEAKER_ACCENT || "#FF4B2E";
  const reviewerColor = REVIEWER_ACCENT || "#0052CC";

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-emerald-500" />
      </div>
    );
  }

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
                <th className="py-2.5 px-3 w-[120px]">Đợt nhiệm vụ</th>
                <th className="py-2.5 px-3 min-w-[200px]">Tên đợt</th>
                <th className="py-2.5 px-3 text-center w-[160px]">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
              {campaigns.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-8 text-center text-gray-400 font-medium">
                    Chưa có đợt nhiệm vụ nào được khởi tạo.
                  </td>
                </tr>
              ) : (
                campaigns.map((item, index) => {
                  const campaignId = item.campaignId || item.id;
                  const mappedIds = batchMapping[campaignId] || [];
                  const batchTasks = assignTasks.filter((t) => t.campaignId === campaignId || mappedIds.includes(t.id));

                  const isCompleted = batchTasks.length > 0 && batchTasks.every((t) => t.status === "Hoàn thành");
                  const isOpenStatus = batchTasks.length === 0 || item.status === "Open";

                  const badgeBg = isCompleted 
                    ? CHIP_SUCCESS_BG 
                    : isOpenStatus 
                      ? `${TASK_MANAGER_ACCENT}1A` 
                      : CHIP_WARNING_BG;

                  const badgeBorder = isCompleted 
                    ? CHIP_SUCCESS_BORDER 
                    : isOpenStatus 
                      ? `${TASK_MANAGER_ACCENT}50` 
                      : CHIP_WARNING_BORDER;

                  const badgeText = isCompleted 
                    ? CHIP_SUCCESS_TEXT 
                    : isOpenStatus 
                      ? TASK_MANAGER_ACCENT 
                      : CHIP_WARNING_TEXT;

                  return (
                    <tr key={campaignId} className="hover:bg-gray-50/80 dark:hover:bg-[#25272E]/50 transition-colors">
                      <td className="py-3 px-3 font-bold text-gray-900 dark:text-white">
                        Đợt {campaignId || index + 1}
                      </td>
                      <td className="py-3 px-3 font-semibold text-gray-800 dark:text-gray-200">
                        {item.campaignName || item.title || item.name}
                      </td>
                      <td className="py-3 px-3 text-center">
                        <span 
                          style={{
                            backgroundColor: badgeBg,
                            borderColor: badgeBorder,
                            color: badgeText
                          }}
                          className="inline-flex items-center gap-1 pl-2 pr-2.5 py-0.5 rounded-full text-[9.5px] font-bold border shrink-0 leading-tight"
                        >
                          {isCompleted ? (
                            <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: badgeText }} />
                          ) : (
                            <Clock className="w-2.5 h-2.5 shrink-0" style={{ color: badgeText }} />
                          )}
                          <span>{isCompleted ? "Hoàn thành" : isOpenStatus ? "Đang mở" : "Đang thực hiện"}</span>
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