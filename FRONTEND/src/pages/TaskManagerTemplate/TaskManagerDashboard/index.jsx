import { useState, useEffect } from "react";
import { 
  BarChart3, 
  ListTodo,
  UserCheck,
  Tag
} from "lucide-react";
import { SPEAKER_ACCENT, REVIEWER_ACCENT } from "../../../constants/theme";

// Bộ màu chủ đề
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

const getCatStyle = (cat) => CATEGORY_COLORS[cat] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };

// Các khóa lưu trữ chuẩn khớp với các màn hình
const SPEAKER_STORAGE_KEY = "task_manager_dataset_v3";
const REVIEWER_STORAGE_KEY = "task_manager_custom_dataset_v1";
const ASSIGNMENT_STORAGE_KEY = "task_manager_assign_v8";

export default function TaskManagerDashboard() {
  const [speakerTasks, setSpeakerTasks] = useState([]);
  const [reviewerTasks, setReviewerTasks] = useState([]);
  const [assignments, setAssignments] = useState([]);

  // Hàm đọc dữ liệu từ localStorage chuẩn hóa
  const parseSavedTasks = (key) => {
    const rawData = localStorage.getItem(key);
    if (!rawData) return [];

    try {
      const parsed = JSON.parse(rawData);
      if (!Array.isArray(parsed)) return [];

      const uniqueMap = new Map();
      parsed.forEach((item) => {
        if (item && item.id) {
          uniqueMap.set(item.id, item);
        }
      });
      return Array.from(uniqueMap.values());
    } catch (e) {
      console.error(`Lỗi đọc dữ liệu từ key [${key}]:`, e);
      return [];
    }
  };

  const loadAllData = () => {
    const speakers = parseSavedTasks(SPEAKER_STORAGE_KEY);
    setSpeakerTasks(speakers);

    const reviewers = parseSavedTasks(REVIEWER_STORAGE_KEY);
    setReviewerTasks(reviewers);

    const savedAssign = localStorage.getItem(ASSIGNMENT_STORAGE_KEY);
    if (savedAssign) {
      try {
        const parsedAssign = JSON.parse(savedAssign);
        setAssignments(Array.isArray(parsedAssign) ? parsedAssign : []);
      } catch (e) {
        setAssignments([]);
      }
    } else {
      setAssignments([]);
    }
  };

  useEffect(() => {
    loadAllData();

    const handleDataChange = () => loadAllData();

    window.addEventListener("storage", handleDataChange);
    window.addEventListener("speaker_tasks_updated", handleDataChange);
    window.addEventListener("reviewer_tasks_updated", handleDataChange);
    window.addEventListener("task_manager_assign_updated", handleDataChange);
    window.addEventListener("assign_tasks_updated", handleDataChange);

    return () => {
      window.removeEventListener("storage", handleDataChange);
      window.removeEventListener("speaker_tasks_updated", handleDataChange);
      window.removeEventListener("reviewer_tasks_updated", handleDataChange);
      window.removeEventListener("task_manager_assign_updated", handleDataChange);
      window.removeEventListener("assign_tasks_updated", handleDataChange);
    };
  }, []);

  const speakerColor = SPEAKER_ACCENT || "#FF4B2E";
  const reviewerColor = REVIEWER_ACCENT || "#0052CC";

  // Lấy danh sách các ID nhiệm vụ Speaker hợp lệ (tập hợp không lặp)
  const assignedSpeakerTaskIds = new Set(
    assignments
      .filter(
        (a) =>
          a.role === "Speaker" &&
          Array.isArray(a.assignedUsers) &&
          a.assignedUsers.length > 0 &&
          a.taskId
      )
      .map((a) => a.taskId)
  );
  const assignedSpeakerCount = assignedSpeakerTaskIds.size;

  // Lấy danh sách các ID nhiệm vụ Reviewer hợp lệ (tập hợp không lặp)
  const assignedReviewerTaskIds = new Set(
    assignments
      .filter(
        (a) =>
          a.role === "Reviewer" &&
          Array.isArray(a.assignedUsers) &&
          a.assignedUsers.length > 0 &&
          a.taskId
      )
      .map((a) => a.taskId)
  );
  const assignedReviewerCount = assignedReviewerTaskIds.size;

  const countTasksByTopic = (topicName, altName) => {
    const speakerMatch = speakerTasks.filter((t) => t.topic === topicName || t.topic === altName).length;
    const reviewerMatch = reviewerTasks.filter((t) => t.topic === topicName || t.topic === altName).length;
    return speakerMatch + reviewerMatch;
  };

  const techTasks = countTasksByTopic("Công nghệ thông tin", "IT/Technology");
  const eduTasks = countTasksByTopic("Giáo dục", "Education");
  const lifeTasks = countTasksByTopic("Hội thoại hàng ngày", "Daily Life");

  return (
    <div className="max-w-6xl mx-auto space-y-6 text-left font-sans transition-colors">
      
      {/* HEADER TỔNG */}
      <div className="space-y-1">
        <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
          <BarChart3 className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
          <span>Bảng điều khiển</span>
        </h2>
        <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
          Thống kê chính xác số lượng và tình trạng các nhiệm vụ
        </p>
      </div>

      {/* 1. HÀNG THỐNG KÊ 1: TỔNG SỐ NHIỆM VỤ CỦA SPEAKER & REVIEWER */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Ô SPEAKER TASKS */}
        <div className="bg-white dark:bg-[#1C1D22] p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-4 transition-colors">
          <div 
            className="p-3 rounded-full shrink-0"
            style={{ 
              backgroundColor: `${speakerColor}1F`, 
              color: speakerColor 
            }}
          >
            <ListTodo className="size-6" />
          </div>
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400 font-medium mb-1">Nhiệm vụ Speaker</p>
            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 font-sans">
              {speakerTasks.length} <span className="text-xs font-normal text-gray-500 dark:text-gray-400">nhiệm vụ</span>
            </h3>
          </div>
        </div>

        {/* Ô REVIEWER TASKS */}
        <div className="bg-white dark:bg-[#1C1D22] p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-4 transition-colors">
          <div 
            className="p-3 rounded-full shrink-0"
            style={{ 
              backgroundColor: `${reviewerColor}1F`, 
              color: reviewerColor 
            }}
          >
            <ListTodo className="size-6" />
          </div>
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400 font-medium mb-1">Nhiệm vụ Reviewer</p>
            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 font-sans">
              {reviewerTasks.length} <span className="text-xs font-normal text-gray-500 dark:text-gray-400">nhiệm vụ</span>
            </h3>
          </div>
        </div>
      </div>

      {/* 2. HÀNG THỐNG KÊ 2: NHIỆM VỤ ĐÃ PHÂN CÔNG CHO SPEAKER & REVIEWER */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* ĐÃ PHÂN CÔNG SPEAKER */}
        <div className="bg-white dark:bg-[#1C1D22] p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-4 transition-colors">
          <div 
            className="p-3 rounded-full shrink-0"
            style={{ 
              backgroundColor: `${speakerColor}1F`, 
              color: speakerColor 
            }}
          >
            <UserCheck className="size-6" />
          </div>
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400 font-medium mb-1">Đã phân công Speaker</p>
            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 font-sans">
              {assignedSpeakerCount} <span className="text-xs font-normal text-gray-500 dark:text-gray-400">nhiệm vụ</span>
            </h3>
          </div>
        </div>

        {/* ĐÃ PHÂN CÔNG REVIEWER */}
        <div className="bg-white dark:bg-[#1C1D22] p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs flex items-center gap-4 transition-colors">
          <div 
            className="p-3 rounded-full shrink-0"
            style={{ 
              backgroundColor: `${reviewerColor}1F`, 
              color: reviewerColor 
            }}
          >
            <UserCheck className="size-6" />
          </div>
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400 font-medium mb-1">Đã phân công Reviewer</p>
            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 font-sans">
              {assignedReviewerCount} <span className="text-xs font-normal text-gray-500 dark:text-gray-400">nhiệm vụ</span>
            </h3>
          </div>
        </div>
      </div>

      {/* 3. KHU VỰC THỐNG KÊ THEO CHỦ ĐỀ */}
      <div className="bg-white dark:bg-[#1C1D22] p-6 rounded-xl border border-gray-200 dark:border-gray-800 shadow-xs space-y-4 transition-colors">
        <h3 className="text-sm font-bold text-gray-900 dark:text-gray-100 flex items-center gap-2">
          <Tag className="w-4 h-4 text-gray-500 dark:text-gray-400" />
          Phân bố nhiệm vụ theo chủ đề
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
          {/* CÔNG NGHỆ THÔNG TIN */}
          <div className="flex justify-between items-center p-3 rounded-lg bg-gray-50 dark:bg-[#25272E] border border-gray-200 dark:border-gray-700/60 transition-colors">
            <span
              className="px-2.5 py-1 rounded-md text-[11px] font-bold border inline-block whitespace-nowrap"
              style={{
                backgroundColor: getCatStyle('Công nghệ thông tin').bg,
                color: getCatStyle('Công nghệ thông tin').text,
                borderColor: getCatStyle('Công nghệ thông tin').border
              }}
            >
              Công nghệ thông tin
            </span>
            <span className="font-bold text-gray-900 dark:text-white font-sans text-xs">{techTasks} nhiệm vụ</span>
          </div>

          {/* GIÁO DỤC */}
          <div className="flex justify-between items-center p-3 rounded-lg bg-gray-50 dark:bg-[#25272E] border border-gray-200 dark:border-gray-700/60 transition-colors">
            <span
              className="px-2.5 py-1 rounded-md text-[11px] font-bold border inline-block whitespace-nowrap"
              style={{
                backgroundColor: getCatStyle('Giáo dục').bg,
                color: getCatStyle('Giáo dục').text,
                borderColor: getCatStyle('Giáo dục').border
              }}
            >
              Giáo dục
            </span>
            <span className="font-bold text-gray-900 dark:text-white font-sans text-xs">{eduTasks} nhiệm vụ</span>
          </div>

          {/* HỘI THOẠI HÀNG NGÀY */}
          <div className="flex justify-between items-center p-3 rounded-lg bg-gray-50 dark:bg-[#25272E] border border-gray-200 dark:border-gray-700/60 transition-colors">
            <span
              className="px-2.5 py-1 rounded-md text-[11px] font-bold border inline-block whitespace-nowrap"
              style={{
                backgroundColor: getCatStyle('Hội thoại hàng ngày').bg,
                color: getCatStyle('Hội thoại hàng ngày').text,
                borderColor: getCatStyle('Hội thoại hàng ngày').border
              }}
            >
              Hội thoại hàng ngày
            </span>
            <span className="font-bold text-gray-900 dark:text-white font-sans text-xs">{lifeTasks} nhiệm vụ</span>
          </div>
        </div>
      </div>

    </div>
  );
}