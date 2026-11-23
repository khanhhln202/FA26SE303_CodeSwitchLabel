import React, { useState, useEffect } from "react";
import { 
  ListTodo, 
  Layers, 
  Clock, 
  CheckCircle2, 
  ArrowRight,
  Loader2,
  UserCheck,
  Tag
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

// Helper chuyển đổi Domain/Topic từ BE sang tiếng Việt giống TaskManagerManagement
const mapDomainToTopicVN = (domain) => {
  if (domain === "ItTechnology" || domain === "IT" || domain === "Công nghệ thông tin") return "Công nghệ thông tin";
  if (domain === "Education" || domain === "Giáo dục") return "Giáo dục";
  if (domain === "DailyLife" || domain === "Hội thoại hàng ngày") return "Hội thoại hàng ngày";
  return domain || "Công nghệ thông tin";
};

// Bộ màu chủ đề
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' }
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };

export default function TaskManagerHome({ onNavigateToManagement, navigate }) {
  const [isHovered, setIsHovered] = useState(false);
  const [loading, setLoading] = useState(true);
  const [waves, setWaves] = useState([]);
  const [assignTasks, setAssignTasks] = useState([]);
  const [adminTargets, setAdminTargets] = useState({
    speaker: { target: 0, completed: 0 },
    reviewer: { target: 0, completed: 0 }
  });
  const [stats, setStats] = useState({
    speakerTasksCount: 0,
    reviewerTasksCount: 0,
    assignedSpeakerCount: 0,
    assignedReviewerCount: 0,
    techTasks: 0,
    eduTasks: 0,
    lifeTasks: 0
  });

  const loadData = async () => {
    try {
      setLoading(true);
      const [overviewData, waveData, taskData] = await Promise.all([
        taskService.getOverview().catch(() => null),
        taskService.getWaves({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] }))
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

      const rawWaves = waveData?.items || waveData?.data || (Array.isArray(waveData) ? waveData : []);
      
      // Bảng tra cứu thông tin Wave theo waveId (cho phần thống kê từ TaskManagerDashboard)
      const waveMap = {};
      const validWaveIds = new Set();

      rawWaves.forEach(w => {
        const wId = Number(w.waveId || w.id);
        if (wId) {
          waveMap[wId] = {
            ...w,
            topic: mapDomainToTopicVN(w.domain || w.topic)
          };
          validWaveIds.add(wId);
        }
      });

      // Sắp xếp các Đợt nhiệm vụ theo thứ tự ID số tăng dần (giống hệt TaskManagerManagement)
      const waveList = rawWaves
        .map((c) => ({
          ...c,
          id: Number(c.waveId || c.id),
          waveName: c.waveName || c.title || c.name || `Đợt ${c.waveId || c.id}`,
          target: Number(c.targetQty || c.targetCount || c.target || 2000),
          status: c.status || "Draft"
        }))
        .sort((a, b) => a.id - b.id);

      const rawTasks = taskData?.items || taskData?.data || (Array.isArray(taskData) ? taskData : []);

      // Lấy chi tiết từng Task giống hệt TaskManagerManagement để trạng thái và thống kê đạt độ chính xác 100%
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

          const rawStatus = String(currentTaskData.status || currentTaskData.summary?.status || "").toLowerCase();
          let calculatedStatus = "Đang thực hiện";
          if (rawStatus === "completed" || rawStatus === "hoàn thành" || rawStatus === "done") calculatedStatus = "Hoàn thành";
          else if (rawStatus === "cancelled" || rawStatus === "canceled" || rawStatus === "đã hủy") calculatedStatus = "Đã hủy";

          const waveId = Number(currentTaskData.waveId || currentTaskData.summary?.waveId);
          const parentWave = waveMap[waveId];

          const rawType = String(currentTaskData.taskType || currentTaskData.summary?.taskType || "").toLowerCase();
          const isSpeaker =
            rawType === "recording" ||
            rawType === "speaker" ||
            rawType === "0" ||
            rawType === "contribution" ||
            rawType === "2";

          let rawAssignees =
            currentTaskData.assignees ||
            currentTaskData.summary?.assignees ||
            currentTaskData.assignments ||
            currentTaskData.assignedUsers ||
            currentTaskData.assignedUserIds ||
            [];

          let assignedUserIds = [];
          if (Array.isArray(rawAssignees) && rawAssignees.length > 0) {
            rawAssignees.forEach((item) => {
              const uId = typeof item === "number" || typeof item === "string"
                ? Number(item)
                : Number(item.userId || item.id || item.user?.id || item.user?.userId);
              if (uId && !assignedUserIds.includes(uId)) {
                assignedUserIds.push(uId);
              }
            });
          }

          const hasAssignees = assignedUserIds.length > 0;

          // Ưu tiên lấy chủ đề chuẩn theo Đợt giống bên TaskManagerManagement
          const topicVN = parentWave?.topic || mapDomainToTopicVN(currentTaskData.domain || currentTaskData.summary?.domain);

          return {
            id: taskId,
            waveId: waveId,
            status: calculatedStatus,
            role: isSpeaker ? "Speaker" : "Reviewer",
            hasAssignees: hasAssignees,
            assignedUserIds: assignedUserIds,
            topic: topicVN
          };
        })
      );

      // CHỈ LỌC CÁC TASK CÓ WAVEID HỢP LỆ VÀ ĐANG THUỘC ĐỢT TRONG TASKMANAGERMANAGEMENT
      const validTasks = parsedTasks.filter(t => t.waveId && validWaveIds.has(t.waveId));

      // 1. Thống kê nhiệm vụ Speakers và Reviewers trong các Đợt
      const speakerItems = validTasks.filter(t => t.role === "Speaker");
      const reviewerItems = validTasks.filter(t => t.role === "Reviewer");

      // 2. Thống kê nhiệm vụ đã phân công (có người thực hiện)
      const assignedSpeakers = speakerItems.filter(t => t.hasAssignees).length;
      const assignedReviewers = reviewerItems.filter(t => t.hasAssignees).length;

      // 3. Thống kê phân bố theo chủ đề của các đợt
      let tech = 0;
      let edu = 0;
      let life = 0;

      validTasks.forEach(t => {
        if (t.topic === "Công nghệ thông tin") tech++;
        else if (t.topic === "Giáo dục") edu++;
        else if (t.topic === "Hội thoại hàng ngày") life++;
        else tech++;
      });

      setStats({
        speakerTasksCount: speakerItems.length,
        reviewerTasksCount: reviewerItems.length,
        assignedSpeakerCount: assignedSpeakers,
        assignedReviewerCount: assignedReviewers,
        techTasks: tech,
        eduTasks: edu,
        lifeTasks: life
      });

      setWaves(waveList);
      setAssignTasks(parsedTasks);
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
        <Loader2 className="w-8 h-8 animate-spin text-[#16A34A]" />
      </div>
    );
  }

  return (
    <div className="w-full h-full flex flex-col gap-4 text-left font-sans p-1 overflow-y-auto transition-colors">

      {/* Cards Chỉ tiêu từ Admin */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
        {/* Chỉ tiêu Speaker */}
        <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] relative overflow-hidden transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
          <div className="flex items-center gap-3 mb-3">
            <div 
              className="w-10 h-10 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${speakerColor}1F`, 
                color: speakerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <h3 className="type-card-title text-[#16171C] dark:text-white">Chỉ tiêu Speaker</h3>
              <p className="type-meta text-[#6E7078] dark:text-gray-400">Admin giao cho Task Manager</p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 mt-3 bg-[#F7F5EF] dark:bg-[#25272E] p-3.5 rounded-xl border border-[#E5E2D8]/80 dark:border-gray-800">
            <div>
              <span className="type-label text-[#9A9CA3] uppercase block">Chỉ tiêu giao</span>
              <p className="type-stat text-[#16171C] dark:text-white mt-0.5">{adminTargets.speaker.target.toLocaleString()} <span className="type-label font-normal text-[#6E7078]">câu</span></p>
            </div>
            <div>
              <span className="type-label text-[#9A9CA3] uppercase block">Đã thu âm</span>
              <p className="type-stat mt-0.5" style={{ color: speakerColor }}>
                {adminTargets.speaker.completed.toLocaleString()} <span className="type-label font-normal text-[#6E7078]">câu</span>
              </p>
            </div>
          </div>
        </div>

        {/* Chỉ tiêu Reviewer */}
        <div className="rounded-2xl border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] p-5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] relative overflow-hidden transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
          <div className="flex items-center gap-3 mb-3">
            <div 
              className="w-10 h-10 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${reviewerColor}1F`, 
                color: reviewerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <h3 className="type-card-title text-[#16171C] dark:text-white">Chỉ tiêu Reviewer</h3>
              <p className="type-meta text-[#6E7078] dark:text-gray-400">Admin giao cho Task Manager</p>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 mt-3 bg-[#F7F5EF] dark:bg-[#25272E] p-3.5 rounded-xl border border-[#E5E2D8]/80 dark:border-gray-800">
            <div>
              <span className="type-label text-[#9A9CA3] uppercase block">Chỉ tiêu duyệt</span>
              <p className="type-stat text-[#16171C] dark:text-white mt-0.5">{adminTargets.reviewer.target.toLocaleString()} <span className="type-label font-normal text-[#6E7078]">câu</span></p>
            </div>
            <div>
              <span className="type-label text-[#9A9CA3] uppercase block">Đã kiểm duyệt</span>
              <p className="type-stat mt-0.5" style={{ color: reviewerColor }}>
                {adminTargets.reviewer.completed.toLocaleString()} <span className="type-label font-normal text-[#6E7078]">câu</span>
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* TOÀN BỘ NỘI DUNG TỪ TASKMANAGERDASHBOARD */}
      <div className="w-full space-y-4 text-left font-sans transition-colors">

        {/* 1. HÀNG THỐNG KÊ 1: TỔNG SỐ NHIỆM VỤ CỦA SPEAKER & REVIEWER */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
          {/* Ô SPEAKER TASKS */}
          <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
            <div 
              className="w-11 h-11 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${speakerColor}1F`, 
                color: speakerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <p className="type-label text-[#6E7078] dark:text-gray-400">Nhiệm vụ Speaker</p>
              <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
                {stats.speakerTasksCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">nhiệm vụ</span>
              </h3>
            </div>
          </div>

          {/* Ô REVIEWER TASKS */}
          <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
            <div 
              className="w-11 h-11 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${reviewerColor}1F`, 
                color: reviewerColor 
              }}
            >
              <ListTodo className="w-5 h-5" />
            </div>
            <div>
              <p className="type-label text-[#6E7078] dark:text-gray-400">Nhiệm vụ Reviewer</p>
              <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
                {stats.reviewerTasksCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">nhiệm vụ</span>
              </h3>
            </div>
          </div>
        </div>

        {/* 2. HÀNG THỐNG KÊ 2: NHIỆM VỤ ĐÃ PHÂN CÔNG CHO SPEAKER & REVIEWER */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
          {/* ĐÃ PHÂN CÔNG SPEAKER */}
          <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
            <div 
              className="w-11 h-11 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${speakerColor}1F`, 
                color: speakerColor 
              }}
            >
              <UserCheck className="w-5 h-5" />
            </div>
            <div>
              <p className="type-label text-[#6E7078] dark:text-gray-400">Đã phân công Speaker</p>
              <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
                {stats.assignedSpeakerCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">nhiệm vụ</span>
              </h3>
            </div>
          </div>

          {/* ĐÃ PHÂN CÔNG REVIEWER */}
          <div className="bg-white dark:bg-[#1C1D22] p-4.5 rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex items-center gap-3.5 transition-all hover:shadow-[0_4px_12px_rgba(16,17,20,0.06)]">
            <div 
              className="w-11 h-11 rounded-2xl shrink-0 flex items-center justify-center"
              style={{ 
                backgroundColor: `${reviewerColor}1F`, 
                color: reviewerColor 
              }}
            >
              <UserCheck className="w-5 h-5" />
            </div>
            <div>
              <p className="type-label text-[#6E7078] dark:text-gray-400">Đã phân công Reviewer</p>
              <h3 className="type-stat text-[#16171C] dark:text-gray-100 font-sans mt-0.5">
                {stats.assignedReviewerCount} <span className="type-label font-normal text-[#6E7078] dark:text-gray-400">nhiệm vụ</span>
              </h3>
            </div>
          </div>
        </div>

        {/* 3. KHU VỰC THỐNG KÊ THEO CHỦ ĐỀ */}
        <div className="bg-white dark:bg-[#1C1D22] p-5 rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] space-y-3 transition-colors">
          <h3 className="type-card-title text-[#16171C] dark:text-gray-100 flex items-center gap-2">
            <Tag className="w-4 h-4 text-[#6E7078] dark:text-gray-400" />
            Phân bố nhiệm vụ theo chủ đề
          </h3>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 type-ui">
            {/* CÔNG NGHỆ THÔNG TIN */}
            <div className="flex justify-between items-center p-3 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8]/80 dark:border-gray-700/60 transition-colors">
              <span
                className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                style={{
                  backgroundColor: getCatStyle('Công nghệ thông tin').bg,
                  color: getCatStyle('Công nghệ thông tin').text,
                  borderColor: getCatStyle('Công nghệ thông tin').border
                }}
              >
                Công nghệ thông tin
              </span>
              <span className="type-label text-[#16171C] dark:text-white font-sans">{stats.techTasks} nhiệm vụ</span>
            </div>

            {/* GIÁO DỤC */}
            <div className="flex justify-between items-center p-3 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8]/80 dark:border-gray-700/60 transition-colors">
              <span
                className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                style={{
                  backgroundColor: getCatStyle('Giáo dục').bg,
                  color: getCatStyle('Giáo dục').text,
                  borderColor: getCatStyle('Giáo dục').border
                }}
              >
                Giáo dục
              </span>
              <span className="type-label text-[#16171C] dark:text-white font-sans">{stats.eduTasks} nhiệm vụ</span>
            </div>

            {/* HỘI THOẠI HÀNG NGÀY */}
            <div className="flex justify-between items-center p-3 rounded-xl bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8]/80 dark:border-gray-700/60 transition-colors">
              <span
                className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                style={{
                  backgroundColor: getCatStyle('Hội thoại hàng ngày').bg,
                  color: getCatStyle('Hội thoại hàng ngày').text,
                  borderColor: getCatStyle('Hội thoại hàng ngày').border
                }}
              >
                Hội thoại hàng ngày
              </span>
              <span className="type-label text-[#16171C] dark:text-white font-sans">{stats.lifeTasks} nhiệm vụ</span>
            </div>
          </div>
        </div>

      </div>

      {/* Danh sách các Đợt nhiệm vụ */}
      <div className="rounded-[24px] border border-[#E5E2D8] dark:border-gray-800 bg-white dark:bg-[#1C1D22] shadow-[0_1px_3px_rgba(16,17,20,0.04)] p-5 flex-1 flex flex-col min-h-0">
        <div className="flex items-center justify-between mb-3.5">
          <h3 className="type-card-title text-[#16171C] dark:text-white flex items-center gap-2">
            <Layers style={{ color: TASK_MANAGER_ACCENT }} className="w-4 h-4" />
            Danh sách các đợt nhiệm vụ
          </h3>

          <a
            href="/task-manager/management"
            onClick={handleNavigate}
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}
            className="inline-flex items-center gap-1.5 type-ui font-label transition-colors cursor-pointer shrink-0"
          >
            <span 
              className={isHovered ? "" : "text-[#6E7078] dark:text-gray-300"}
              style={{ color: isHovered ? TASK_MANAGER_ACCENT : undefined }}
            >
              Đi đến trang quản lý nhiệm vụ
            </span>
            <ArrowRight 
              className={`w-3.5 h-3.5 ${isHovered ? "" : "text-[#6E7078] dark:text-gray-300"}`}
              style={{ color: isHovered ? TASK_MANAGER_ACCENT : undefined }}
            />
          </a>
        </div>

        <div className="overflow-x-auto flex-1 rounded-2xl border border-[#E5E2D8] dark:border-gray-800">
          <table className="w-full table-fixed text-left type-ui min-w-[920px]">
            <thead>
              <tr className="border-b border-[#E5E2D8] dark:border-gray-800 text-[#9A9CA3] dark:text-gray-400 type-label h-8 bg-[#F7F5EF] dark:bg-[#25272E]">
                <th className="py-2 px-3.5 w-[11%]">Đợt nhiệm vụ</th>
                <th className="py-2 px-3.5 w-[19%]">Tên đợt</th>
                <th className="py-2 px-3.5 text-center w-[17%] whitespace-nowrap">Chủ đề</th>
                <th className="py-2 px-3.5 text-center w-[13%] whitespace-nowrap">Tổng số Speaker</th>
                <th className="py-2 px-3.5 text-center w-[13%] whitespace-nowrap">Tổng số Reviewer</th>
                <th className="py-2 px-3.5 text-center w-[14%] whitespace-nowrap">Tổng số câu và bản</th>
                <th className="py-2 px-3.5 text-center w-[13%]">Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800">
              {waves.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-[#9A9CA3] dark:text-gray-400 type-ui">
                    Chưa có đợt nhiệm vụ nào được khởi tạo.
                  </td>
                </tr>
              ) : (
                waves.map((item, index) => {
                  const waveId = Number(item.waveId || item.id);
                  const batchTasks = assignTasks.filter((t) => Number(t.waveId) === waveId);
                  const waveTopic = mapDomainToTopicVN(item.domain || item.topic);
                  const catStyle = getCatStyle(waveTopic);

                  // Thống kê tổng số Speaker và Reviewer duy nhất được phân công trong các nhiệm vụ của đợt
                  const uniqueSpeakerIds = new Set();
                  const uniqueReviewerIds = new Set();
                  batchTasks.forEach((t) => {
                    const userIds = Array.isArray(t.assignedUserIds) ? t.assignedUserIds : [];
                    if (t.role === "Speaker") {
                      userIds.forEach((id) => uniqueSpeakerIds.add(Number(id)));
                    } else if (t.role === "Reviewer") {
                      userIds.forEach((id) => uniqueReviewerIds.add(Number(id)));
                    }
                  });
                  const totalSpeakersCount = uniqueSpeakerIds.size;
                  const totalReviewersCount = uniqueReviewerIds.size;
                  const waveTargetCount = Number(item.targetQty ?? item.targetCount ?? item.target ?? 2000);

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
                    <tr key={waveId} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors h-11">
                      <td className="py-2 px-3.5 font-bold text-[#16171C] dark:text-white">
                        Đợt {index + 1}
                      </td>
                      <td className="py-2 px-3.5 font-medium text-[#16171C] dark:text-gray-200 truncate">
                        {item.waveName || item.title || item.name}
                      </td>
                      <td className="py-2 px-3.5 text-center">
                        <span
                          className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap"
                          style={{
                            backgroundColor: catStyle.bg,
                            color: catStyle.text,
                            borderColor: catStyle.border
                          }}
                        >
                          {waveTopic}
                        </span>
                      </td>
                      <td className="py-2 px-3.5 text-center font-bold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                        {totalSpeakersCount.toLocaleString()}
                      </td>
                      <td className="py-2 px-3.5 text-center font-bold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                        {totalReviewersCount.toLocaleString()}
                      </td>
                      <td className="py-2 px-3.5 text-center font-bold text-[#16171C] dark:text-white whitespace-nowrap font-sans">
                        {waveTargetCount}
                      </td>
                      <td className="py-2 px-3.5 text-center">
                        <span 
                          style={{
                            backgroundColor: badgeBg,
                            borderColor: badgeBorder,
                            color: badgeText
                          }}
                          className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border shrink-0 leading-tight"
                        >
                          {isCompleted ? (
                            <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: badgeText }} />
                          ) : (
                            <Clock className="w-3 h-3 shrink-0" style={{ color: badgeText }} />
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