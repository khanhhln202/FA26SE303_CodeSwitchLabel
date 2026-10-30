import { useState, useEffect } from "react";
import { 
  BarChart3, 
  ListTodo,
  UserCheck,
  Tag,
  Loader2
} from "lucide-react";
import { SPEAKER_ACCENT, REVIEWER_ACCENT } from "../../../constants/theme";
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

export default function TaskManagerDashboard() {
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState({
    speakerTasksCount: 0,
    reviewerTasksCount: 0,
    assignedSpeakerCount: 0,
    assignedReviewerCount: 0,
    techTasks: 0,
    eduTasks: 0,
    lifeTasks: 0
  });

  const loadDataFromApi = async () => {
    try {
      setLoading(true);
      // Gọi API lấy danh sách Tasks và Waves tương tự TaskManagerManagement
      const [allTasksRes, wavesRes] = await Promise.all([
        taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getWaves({ pageSize: 1000 }).catch(() => ({ items: [] }))
      ]);

      const rawTasks = allTasksRes?.items || allTasksRes?.data || (Array.isArray(allTasksRes) ? allTasksRes : []);
      const rawWaves = wavesRes?.items || wavesRes?.data || (Array.isArray(wavesRes) ? wavesRes : []);

      // Bảng tra cứu thông tin Wave theo waveId
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

      // Lấy chi tiết từng Task
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

          const waveId = Number(currentTaskData.waveId || currentTaskData.summary?.waveId);
          const parentWave = waveMap[waveId];

          const rawType = String(currentTaskData.taskType || currentTaskData.summary?.taskType || "").toLowerCase();
          const isSpeaker = rawType === "recording" || rawType === "speaker" || rawType === "0";

          let rawAssignees =
            currentTaskData.assignees ||
            currentTaskData.summary?.assignees ||
            currentTaskData.assignments ||
            currentTaskData.assignedUsers ||
            currentTaskData.assignedUserIds ||
            [];

          const hasAssignees = Array.isArray(rawAssignees) && rawAssignees.length > 0;

          // Ưu tiên lấy chủ đề chuẩn theo Đợt giống bên TaskManagerManagement
          const topicVN = parentWave?.topic || mapDomainToTopicVN(currentTaskData.domain || currentTaskData.summary?.domain);

          return {
            id: taskId,
            waveId: waveId,
            role: isSpeaker ? "Speaker" : "Reviewer",
            hasAssignees: hasAssignees,
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
    } catch (err) {
      console.error("Lỗi khi tải dữ liệu Bảng điều khiển từ API:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDataFromApi();
  }, []);

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
    <div className="max-w-6xl mx-auto space-y-4 text-left font-sans transition-colors p-1">

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
  );
}