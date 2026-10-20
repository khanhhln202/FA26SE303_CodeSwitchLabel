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

// Bộ màu chủ đề
const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
  'ItTechnology': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Education': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
  'DailyLife': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' }
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
      // Gọi API lấy toàn bộ danh sách Task & Campaigns với pageSize lớn để tránh bị mất dữ liệu phân trang
      const [allTasksRes, campaignsRes] = await Promise.all([
        taskService.getTasks({ pageSize: 1000 }).catch(() => ({ items: [] })),
        taskService.getCampaigns({ pageSize: 1000 }).catch(() => ({ items: [] }))
      ]);

      const rawTasks = allTasksRes?.items || allTasksRes?.data || (Array.isArray(allTasksRes) ? allTasksRes : []);
      const campaigns = campaignsRes?.items || campaignsRes?.data || (Array.isArray(campaignsRes) ? campaignsRes : []);

      // Map danh sách topic/domain từ Campaign làm fallback cho Task
      const campaignTopicMap = {};
      campaigns.forEach(c => {
        const cId = c.campaignId || c.id;
        if (cId) {
          campaignTopicMap[cId] = c.domain || c.topic || c.campaignName;
        }
      });

      // Kiểm tra xem task đã được phân công VÀ đã được gán vào đợt (ở trang TaskManagerManagement) hay chưa
      const isTaskAssignedInCampaign = (t) => {
        const hasCampaign = Boolean(t.campaignId);
        if (!hasCampaign) return false;

        if (t.assignedToUserId || t.assigneeName || t.assigneeId) return true;
        if (Array.isArray(t.assignedUsers) && t.assignedUsers.length > 0) return true;
        if (Array.isArray(t.assignees) && t.assignees.length > 0) return true;
        if (Array.isArray(t.assignments) && t.assignments.length > 0) return true;
        return false;
      };

      // Phân loại task thành Speaker (Recording) và Reviewer (Review/Editing)
      const speakerItems = rawTasks.filter(t => {
        const rawType = String(t.taskType || t.type || t.role || "").toLowerCase();
        return rawType === "recording" || rawType === "speaker" || rawType === "0" || rawType.includes("record");
      });

      const reviewerItems = rawTasks.filter(t => {
        const rawType = String(t.taskType || t.type || t.role || "").toLowerCase();
        return rawType === "review" || rawType === "reviewer" || rawType === "editing" || rawType === "1" || rawType.includes("review");
      });

      // Đếm số lượng nhiệm vụ đã phân công (Chỉ đếm các nhiệm vụ thuộc Campaign/Đợt VÀ có người thực hiện)
      const assignedSpeakers = speakerItems.filter(isTaskAssignedInCampaign).length;
      const assignedReviewers = reviewerItems.filter(isTaskAssignedInCampaign).length;

      // Đếm số lượng nhiệm vụ theo Chủ đề chính xác
      let tech = 0;
      let edu = 0;
      let life = 0;

      rawTasks.forEach(t => {
        const campaignTopic = campaignTopicMap[t.campaignId] || "";
        const domainStr = String(t.domain || t.topic || t.categoryName || campaignTopic || "").toLowerCase();

        if (
          domainStr.includes("ittechnology") || 
          domainStr.includes("công nghệ") || 
          domainStr.includes("thông tin") || 
          domainStr.includes("tech") ||
          domainStr.includes("it")
        ) {
          tech++;
        } else if (
          domainStr.includes("education") || 
          domainStr.includes("giáo dục") || 
          domainStr.includes("edu")
        ) {
          edu++;
        } else if (
          domainStr.includes("dailylife") || 
          domainStr.includes("hội thoại") || 
          domainStr.includes("hàng ngày") || 
          domainStr.includes("life")
        ) {
          life++;
        } else {
          // Mặc định tính vào Công nghệ thông tin nếu không xác định được
          tech++;
        }
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