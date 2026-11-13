import { useState, useMemo, useEffect } from "react";
import { 
  Plus, 
  Search, 
  Filter, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  X,
  AlertTriangle,
  Upload,
  FileText,
  Loader2
} from "lucide-react";
import { toast } from "sonner";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  DANGER,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  AUDIO_PRIMARY
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";
import { parseCodeSwitch } from "../../../utils/codeSwitch";

const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
};

// Danh sách các giá trị domain có thể được lưu trong DB để backend lọc trực tiếp
const TOPIC_DOMAIN_CANDIDATES = {
  "IT/Technology": [
    "IT/Technology",
    "Information Technology",
    "Công nghệ thông tin",
    "IT",
    "Technology",
    "IT / Technology",
    "IT-Technology",
    "Tech"
  ],
  "Education": ["Education", "Giáo dục"],
  "Daily Life": ["Daily Life", "Hội thoại hàng ngày", "DailyLife"]
};

// Chuẩn hóa tên chủ đề từ tiếng Anh/mã code sang tiếng Việt để hiển thị giao diện
const normalizeTopicName = (rawTopic) => {
  if (!rawTopic) return 'Công nghệ thông tin';
  const topicLower = String(rawTopic).trim().toLowerCase();

  if (topicLower.includes('it') || topicLower.includes('tech') || topicLower.includes('công nghệ')) {
    return 'Công nghệ thông tin';
  }
  if (topicLower.includes('daily') || topicLower.includes('life') || topicLower.includes('hội thoại')) {
    return 'Hội thoại hàng ngày';
  }
  if (topicLower.includes('edu') || topicLower.includes('giáo dục')) {
    return 'Giáo dục';
  }
  return rawTopic;
};

const catStyle = (cat) => {
  const normalized = normalizeTopicName(cat);
  return CATEGORY_COLORS[normalized] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };
};

// Loại bỏ sạch sẽ các thẻ [vi], [/vi], [en], [/en] và tô màu chữ tiếng Anh
function HighlightCsText({ rawText }) {
  if (!rawText) return null;

  // Sử dụng helper parseCodeSwitch chuẩn để bóc tách nhãn và làm sạch chuỗi
  const segments = parseCodeSwitch(rawText);

  return (
    <>
      {segments.map((segment, i) => (
        <span 
          key={i} 
          style={segment.lang === "en" ? { color: AUDIO_PRIMARY, fontWeight: 600 } : undefined}
        >
          {segment.text}
        </span>
      ))}
    </>
  );
}

export default function AdminTextData() {
  const [textList, setTextList] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  const [selectedTopic, setSelectedTopic] = useState("all");
  
  const [currentPage, setCurrentPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const pageSize = 10;
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  
  // State phục vụ modal thêm mới file .txt & chọn domain
  const [selectedTxtFile, setSelectedTxtFile] = useState(null);
  const [domainSelect, setDomainSelect] = useState("IT/Technology");
  
  // State phục vụ modal chỉnh sửa 1 cặp câu
  const [editCsText, setEditCsText] = useState("");
  const [editViText, setEditViText] = useState("");

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [fileLoading, setFileLoading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isDragging, setIsDragging] = useState(false);

  const mapScriptItem = (item) => ({
    id: item.scriptId || item.id,
    csContent: item.csContent || item.csPlain || item.content || "",
    viContent: item.viContent || item.viEquivalent || item.csContent || item.content || "",
    content: item.csContent || item.csPlain || item.content || "",
    rawCsContent: item.csContent || item.content || "",
    topic: normalizeTopicName(item.domain || item.topic),
    status: "active"
  });

  const fetchTexts = async () => {
    try {
      const baseParams = {
        page: currentPage,
        pageSize: pageSize,
        sort: "createdAt",
        dir: "desc"
      };
      
      if (activeSearchTerm) {
        baseParams.search = activeSearchTerm;
        baseParams.q = activeSearchTerm;
      }

      if (selectedTopic === "all") {
        const response = await adminService.getTexts(baseParams);
        const resData = response?.data || response;
        const items = Array.isArray(resData) ? resData : (resData?.items || []);
        const total = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? (Array.isArray(resData) ? resData.length : items.length);

        setTotalItems(total);
        setTextList(items.map(mapScriptItem));
        return;
      }

      // Thử các biến thể tên domain lưu trong DB để backend phân trang đầy đủ trên toàn bộ dataset
      const candidates = [...(TOPIC_DOMAIN_CANDIDATES[selectedTopic] || [selectedTopic])];
      let resolvedItems = [];
      let resolvedTotal = 0;
      let foundMatch = false;

      for (const candidateDomain of candidates) {
        const response = await adminService.getTexts({
          ...baseParams,
          domain: candidateDomain
        });
        const resData = response?.data || response;
        const items = Array.isArray(resData) ? resData : (resData?.items || []);
        const total = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? (Array.isArray(resData) ? resData.length : items.length);

        if (total > 0 || items.length > 0) {
          resolvedItems = items;
          resolvedTotal = total;
          foundMatch = true;
          break;
        }
      }

      // Nếu chưa khớp biến thể nào, dò chuỗi domain thô thực tế từ DB rồi gọi lại API với đúng chuỗi đó
      if (!foundMatch) {
        const targetLabel = normalizeTopicName(selectedTopic);
        const probeRes = await adminService.getTexts({
          page: 1,
          pageSize: 100,
          sort: "createdAt",
          dir: "desc"
        });
        const probeData = probeRes?.data || probeRes;
        const probeItems = Array.isArray(probeData) ? probeData : (probeData?.items || []);
        const matchedSample = probeItems.find(
          (item) => normalizeTopicName(item.domain || item.topic) === targetLabel
        );
        const actualRawDomain = matchedSample?.domain || matchedSample?.topic;

        if (actualRawDomain && !candidates.includes(actualRawDomain)) {
          const response = await adminService.getTexts({
            ...baseParams,
            domain: actualRawDomain
          });
          const resData = response?.data || response;
          resolvedItems = Array.isArray(resData) ? resData : (resData?.items || []);
          resolvedTotal = resData?.totalCount ?? resData?.totalItems ?? resData?.total ?? resolvedItems.length;
        }
      }

      setTotalItems(resolvedTotal);
      setTextList(resolvedItems.map(mapScriptItem));
    } catch (err) {
      console.error("Lỗi lấy danh sách câu văn bản:", err);
    }
  };

  useEffect(() => {
    fetchTexts();
  }, [currentPage, activeSearchTerm, selectedTopic]);

  const handleSearch = () => {
    setActiveSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const handleFileSelected = (file) => {
    if (!file) return;
    if (!file.name.endsWith(".txt")) {
      toast.error("Vui lòng chọn đúng file định dạng .txt!");
      return;
    }

    setFileLoading(true);
    setUploadProgress(0);
    setSelectedTxtFile(null);

    let current = 0;
    const interval = setInterval(() => {
      current += 25;
      setUploadProgress(current);
      if (current >= 100) {
        clearInterval(interval);
        setTimeout(() => {
          setSelectedTxtFile(file);
          setFileLoading(false);
        }, 150);
      }
    }, 60);
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setSelectedTxtFile(null);
    setDomainSelect("IT/Technology");
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setEditCsText(item.csContent || item.rawCsContent || "");
    setEditViText(item.viContent || "");
    setSelectedTxtFile(null);
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleSubmitForm = async (e) => {
    e.preventDefault();

    try {
      setIsSubmitting(true);

      if (editingItem) {
        // LUỒNG EDIT: Chỉnh sửa 1 cặp câu text trực tiếp
        if (!editCsText.trim() || !editViText.trim()) {
          toast.error("Vui lòng nhập đầy đủ cả câu trộn VI-EN và câu thuần VI!");
          setIsSubmitting(false);
          return;
        }

        await adminService.updatePendingScript(editingItem.id, {
          csContent: editCsText.trim(),
          viContent: editViText.trim()
        });
        toast.success("Đã cập nhật câu văn bản thành công!");
      } else {
        // LUỒNG ADD NEW: Import file .txt kèm Domain
        if (!selectedTxtFile) {
          toast.error("Vui lòng chọn hoặc kéo thả file .txt!");
          setIsSubmitting(false);
          return;
        }

        const res = await adminService.importScripts(selectedTxtFile, domainSelect);
        
        // KIỂM TRA PHẢN HỒI SKIPPED TỪ BACKEND ĐỂ BÁO TOAST LỖI
        const skippedList = res?.skipped || res?.data?.skipped || [];
        const importedCount = res?.imported ?? res?.data?.imported ?? 0;

        if (skippedList.length > 0) {
          const dupReason = skippedList.find(
            (s) => s.code?.toLowerCase().includes("duplicate") || s.reason?.toLowerCase().includes("trùng")
          );
          
          if (dupReason) {
            toast.error(`Import bị từ chối: Phát hiện ${skippedList.length} câu văn bản bị trùng lặp trong hệ thống!`);
          } else {
            const firstReason = skippedList[0]?.reason || "Câu văn bản bị trùng lặp hoặc không đúng định dạng!";
            toast.error(`Lỗi import (${skippedList.length} câu bị bỏ qua): ${firstReason}`);
          }
        } else if (importedCount > 0) {
          toast.success(`Đã tải lên thành công ${importedCount} câu văn bản!`);
        } else {
          toast.success("Đã tải lên file .txt thành công!");
        }
      }

      setShowModal(false);
      setCurrentPage(1);
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xử lý form:", err);
      const responseData = err?.response?.data;
      let errorMsg = responseData?.title || responseData?.message || err?.message || "Thao tác xử lý thất bại!";

      if (
        errorMsg.toLowerCase().includes("trùng") ||
        errorMsg.toLowerCase().includes("exist") ||
        errorMsg.toLowerCase().includes("already") ||
        err?.response?.status === 409 ||
        err?.response?.status === 422
      ) {
        errorMsg = "Câu văn bản bị trùng lặp hoặc không đúng định dạng! Backend từ chối lưu.";
      }

      toast.error(errorMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (!itemToDelete) return;
    try {
      await adminService.deleteText(itemToDelete.id);
      toast.success("Đã loại bỏ thành công câu văn bản khỏi hệ thống!");
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xóa văn bản:", err?.response?.data || err);
      const errorData = err?.response?.data;
      
      let errorMsg = errorData?.title || errorData?.message || "Loại bỏ văn bản thất bại!";
      if (errorData?.errors) {
        const firstKey = Object.keys(errorData.errors)[0];
        errorMsg = errorData.errors[firstKey][0] || errorMsg;
      }
      
      toast.error(`Xóa thất bại: ${errorMsg}`);
    } finally {
      setItemToDelete(null);
    }
  };

  const totalPages = Math.ceil(totalItems / pageSize) || 1;
  const emptyRowsCount = useMemo(() => {
    return Math.max(0, pageSize - textList.length);
  }, [textList.length, pageSize]);

  return (
    <div className="w-full h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden transition-colors relative">
      
      {/* Toolbar & Filter Bar + Nút Thêm Văn Bản */}
      <div className="shrink-0 space-y-2">
        <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 p-2.5 shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-2.5 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div 
              className="relative w-full flex items-center bg-[#F7F5EF] dark:bg-[#25272E] rounded-xl border border-[#E5E2D8] dark:border-gray-700 transition-all p-1"
              onFocus={(e) => {
                e.currentTarget.style.borderColor = ADMIN_ACCENT;
                e.currentTarget.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = '';
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA3] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm văn bản..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none type-ui text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
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
                value={selectedTopic}
                onChange={(e) => {
                  setSelectedTopic(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[185px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả chủ đề</option>
                <option value="IT/Technology" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                <option value="Education" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                <option value="Daily Life" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
              </select>
            </div>

            <button
              onClick={handleOpenAddModal}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <Plus className="w-4 h-4" />
              <span>Thêm văn bản mới</span>
            </button>
          </div>
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1 min-h-0">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className="w-full h-full text-left border-collapse table-fixed min-w-[1000px]">
            <thead>
              <tr className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 h-8 sticky top-0 z-10">
                <th className="w-[52px] px-2 text-center font-label">STT</th>
                <th className="w-[32%] px-3 text-left font-label">Nội dung</th>
                <th className="px-3 text-left font-label">Nội dung gắn nhãn</th>
                <th className="w-[155px] px-2 text-left font-label whitespace-nowrap">Chủ đề</th>
                <th className="w-[115px] px-2 text-left font-label whitespace-nowrap">Trạng thái</th>
                <th className="w-[80px] px-2 text-center font-label whitespace-nowrap">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui">
              {textList.length > 0 ? (
                <>
                  {textList.map((item, index) => {
                    const stt = (currentPage - 1) * pageSize + index + 1;
                    const style = catStyle(item.topic);
                    return (
                      <tr key={item.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors duration-150 h-14">
                        <td className="px-2 text-center font-bold text-[#16171C] dark:text-gray-100 align-middle whitespace-nowrap">
                          {stt}
                        </td>
                        
                        {/* Cột 1: Hiển thị 2 dòng dạng badge VI-EN và VI (In đậm tiếng Anh màu xanh) */}
                        <td className="px-3 align-middle min-w-0">
                          <div className="min-w-0 flex-1 space-y-0.5 my-0.5">
                            {/* Dòng 1: Trộn ngôn ngữ (VI-EN) */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label mt-0.5"
                              >
                                VI-EN
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-100 leading-snug">
                                <HighlightCsText rawText={item.rawCsContent || item.csContent} />
                              </p>
                            </div>

                            {/* Dòng 2: Tiếng Việt thuần (VI) */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: '#F0EEE6', color: '#6E7078' }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400 mt-0.5"
                              >
                                VI
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-300 leading-snug">
                                <HighlightCsText rawText={item.viContent} />
                              </p>
                            </div>
                          </div>
                        </td>

                        {/* Cột 2: Hiển thị đầy đủ cặp câu gắn nhãn [vi] [en] */}
                        <td className="px-3 align-middle min-w-0">
                          <div className="min-w-0 flex-1 space-y-0.5 my-0.5">
                            {/* Dòng 1: Trộn ngôn ngữ (VI-EN) có thẻ nhãn */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label mt-0.5"
                              >
                                VI-EN
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-100 leading-snug">
                                {item.rawCsContent || item.csContent}
                              </p>
                            </div>

                            {/* Dòng 2: Tiếng Việt thuần (VI) có thẻ nhãn */}
                            <div className="flex items-start gap-2">
                              <span 
                                style={{ background: '#F0EEE6', color: '#6E7078' }}
                                className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400 mt-0.5"
                              >
                                VI
                              </span>
                              <p className="break-words whitespace-normal type-ui text-[#16171C] dark:text-gray-300 leading-snug">
                                {item.viContent}
                              </p>
                            </div>
                          </div>
                        </td>
                        
                        <td className="px-2 align-middle whitespace-nowrap">
                          <span 
                            className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap" 
                            style={{ background: style.bg, color: style.text, borderColor: style.border }}
                          >
                            {normalizeTopicName(item.topic)}
                          </span>
                        </td>

                        {/* CỘT TRẠNG THÁI: Chỉ giữ Sẵn sàng */}
                        <td className="px-2 align-middle whitespace-nowrap">
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border select-none"
                            style={{
                              backgroundColor: CHIP_SUCCESS_BG,
                              borderColor: CHIP_SUCCESS_BORDER,
                              color: CHIP_SUCCESS_TEXT
                            }}
                          >
                            <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Sẵn sàng
                          </span>
                        </td>

                        <td className="px-2 text-center align-middle whitespace-nowrap">
                          <div className="flex items-center justify-center gap-1">
                            <button 
                              onClick={() => handleOpenEditModal(item)}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                              title="Chỉnh sửa văn bản"
                            >
                              <Edit3 className="w-4 h-4" />
                            </button>
                            <button 
                              onClick={() => setItemToDelete({ ...item, stt })}
                              className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                              title="Xóa văn bản"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}

                  {Array.from({ length: emptyRowsCount }).map((_, idx) => (
                    <tr key={`empty-${idx}`} className="border-b border-transparent">
                      <td colSpan={6} className="py-0.5"> </td>
                    </tr>
                  ))}
                </>
              ) : (
                <tr>
                  <td colSpan={6} className="py-16 text-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                    Không tìm thấy dữ liệu văn bản phù hợp.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer Pagination */}
        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0 bg-white dark:bg-[#1C1D22]">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
            variant="soft"
          />
        </div>
      </div>

      {/* MODAL THÊM & CHỈNH SỬA VĂN BẢN */}
      {showModal && (
        <div className="fixed inset-0 bg-[#16171C]/55 backdrop-blur-xs flex items-center justify-center p-4 z-50" onClick={() => setShowModal(false)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: ADMIN_ACCENT }} />
            <form onSubmit={handleSubmitForm} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  {editingItem ? "Chỉnh sửa văn bản" : "Thêm văn bản mới"}
                </h3>
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              {!editingItem ? (
                /* GIAO DIỆN ADD NEW: UPLOAD FILE .TXT + SELECT DOMAIN */
                <div className="space-y-4">
                  <div className="space-y-2">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                      Kéo thả hoặc chọn file dữ liệu (.txt)
                    </label>
                    
                    <div 
                      onDragOver={(e) => {
                        e.preventDefault();
                        setIsDragging(true);
                      }}
                      onDragLeave={(e) => {
                        e.preventDefault();
                        setIsDragging(false);
                      }}
                      onDrop={(e) => {
                        e.preventDefault();
                        setIsDragging(false);
                        if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                          handleFileSelected(e.dataTransfer.files[0]);
                        }
                      }}
                      style={{
                        borderColor: isDragging ? ADMIN_ACCENT : undefined,
                        backgroundColor: isDragging ? `${ADMIN_ACCENT}0D` : undefined
                      }}
                      className="border-2 border-dashed rounded-2xl p-6 text-center transition-all bg-[#F7F5EF]/50 dark:bg-[#25272E]/50 border-[#E5E2D8] dark:border-gray-700 hover:border-gray-400 dark:hover:border-gray-500"
                    >
                      <input
                        type="file"
                        accept=".txt"
                        id="txt-file-input"
                        className="hidden"
                        onChange={(e) => {
                          if (e.target.files && e.target.files[0]) {
                            handleFileSelected(e.target.files[0]);
                          }
                        }}
                      />

                      {fileLoading ? (
                        <div className="flex flex-col items-center justify-center space-y-3 py-2">
                          <Loader2 className="w-8 h-8 animate-spin" style={{ color: ADMIN_ACCENT }} />
                          <div className="w-full bg-[#E5E2D8] dark:bg-gray-700 h-2 rounded-full overflow-hidden max-w-[200px]">
                            <div 
                              className="h-full transition-all duration-100 ease-out"
                              style={{ width: `${uploadProgress}%`, backgroundColor: ADMIN_ACCENT }}
                            />
                          </div>
                          <span className="type-meta font-medium text-[#6E7078] dark:text-gray-300">
                            Đang đọc file text ({uploadProgress}%)...
                          </span>
                        </div>
                      ) : selectedTxtFile ? (
                        <label htmlFor="txt-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2 py-1">
                          <div 
                            className="w-12 h-12 rounded-2xl flex items-center justify-center shadow-xs"
                            style={{ backgroundColor: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                          >
                            <FileText className="w-7 h-7" style={{ color: ADMIN_ACCENT }} />
                          </div>
                          <span className="type-body font-bold text-[#16171C] dark:text-gray-100 max-w-[260px] truncate">
                            {selectedTxtFile.name}
                          </span>
                          <span className="type-caption font-medium hover:underline" style={{ color: ADMIN_ACCENT }}>
                            Bấm để chọn file .txt khác
                          </span>
                        </label>
                      ) : (
                        <label htmlFor="txt-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2">
                          <div className="w-12 h-12 rounded-full bg-white dark:bg-[#1C1D22] flex items-center justify-center shadow-xs">
                            <Upload className="w-6 h-6 text-[#6E7078] dark:text-gray-300" />
                          </div>
                          <span className="type-body font-medium text-[#16171C] dark:text-gray-200">
                            Kéo thả file .txt vào đây hoặc nhấp để tải lên
                          </span>
                        </label>
                      )}
                    </div>
                  </div>

                  {/* Thanh Select Chọn Chủ đề Bắt buộc cho file .txt */}
                  <div className="space-y-1.5 text-left">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Chủ đề văn bản <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={domainSelect}
                      onChange={(e) => setDomainSelect(e.target.value)}
                      className="w-full px-3.5 py-2.5 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                      onFocus={(e) => {
                        e.target.style.borderColor = ADMIN_ACCENT;
                        e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                      }}
                      onBlur={(e) => {
                        e.target.style.borderColor = '';
                        e.target.style.boxShadow = 'none';
                      }}
                    >
                      <option value="IT/Technology" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                      <option value="Education" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                      <option value="Daily Life" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
                    </select>
                  </div>
                </div>
              ) : (
                /* GIAO DIỆN EDIT: NHẬP 1 CẶP CÂU TEXT */
                <div className="space-y-3.5 text-left">
                  <div className="space-y-1.5">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Câu trộn Việt - Anh (Code-Switching)
                    </label>
                    <textarea
                      rows={3}
                      value={editCsText}
                      onChange={(e) => setEditCsText(e.target.value)}
                      placeholder="Nhập câu trộn ngôn ngữ..."
                      className="w-full px-3.5 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all resize-none"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 font-label">
                      Câu thuần Việt tương đương (Pure Vietnamese)
                    </label>
                    <textarea
                      rows={3}
                      value={editViText}
                      onChange={(e) => setEditViText(e.target.value)}
                      placeholder="Nhập câu thuần Việt..."
                      className="w-full px-3.5 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui text-[#16171C] dark:text-white outline-none transition-all resize-none"
                    />
                  </div>
                </div>
              )}

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  disabled={isSubmitting || fileLoading}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting || fileLoading || (!editingItem && !selectedTxtFile)}
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all disabled:opacity-50"
                >
                  {isSubmitting ? "Đang xử lý..." : editingItem ? "Lưu cập nhật" : "Lưu văn bản"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác nhận Xóa */}
      {itemToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setItemToDelete(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />
            <div className="p-6 text-center space-y-3.5">
              <div className="w-11 h-11 rounded-full flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}1A`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa văn bản</h3>
                <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-1 break-words">
                  Bạn có chắc chắn muốn xóa văn bản STT <span className="font-bold text-[#16171C] dark:text-gray-200">"{itemToDelete.stt}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  onClick={() => setItemToDelete(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={handleConfirmDelete}
                  className="flex-1 py-2.5 text-white rounded-xl type-button transition-colors cursor-pointer hover:opacity-90 active:scale-[0.99]"
                  style={{ backgroundColor: DANGER, boxShadow: `0 10px 24px ${DANGER}40` }}
                >
                  Xóa ngay
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}