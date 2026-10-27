import { useState, useMemo, useEffect } from "react";
import { 
  Plus, 
  Search, 
  Filter, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  Clock, 
  X,
  AlertTriangle,
  Upload,
  FileJson,
  Loader2,
  ChevronDown
} from "lucide-react";
import { toast } from "sonner";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  DANGER,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_WARNING_BG,
  CHIP_WARNING_BORDER,
  CHIP_WARNING_TEXT
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";

const CATEGORY_COLORS = {
  'Hội thoại hàng ngày': { bg: '#E6F0FE', text: '#1E40AF', border: '#C9DEFB' },
  'Công nghệ thông tin': { bg: '#FBF0DA', text: '#92600A', border: '#F3E0B5' },
  'Giáo dục': { bg: '#FCE7F0', text: '#9D2662', border: '#F8CFE0' },
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

// Hàm xóa thẻ [vi] và [en] chỉ dùng khi cần kiểm tra trùng lặp Client
const cleanLanguageTags = (str) => {
  if (!str) return "";
  return str
    .replace(/\[vi\]/gi, " ")
    .replace(/\[en\]/gi, " ")
    .replace(/\[\/vi\]/gi, " ")
    .replace(/\[\/en\]/gi, " ")
    .replace(/\s+/g, " ") // Chuẩn hóa khoảng trắng thừa
    .trim();
};

// Helper tự động gắn nhãn [vi]...[/vi] và [en]...[/en] cho từng từ
const addTagsToCsContent = (str) => {
  if (!str) return "";
  if (str.includes("[vi]") || str.includes("[en]")) return str;

  const words = str.trim().split(/\s+/);
  return words
    .map((word) => {
      const cleanWord = word.replace(/^[.,!?;:]+|[.,!?;:]+$/g, "").toLowerCase();
      const isEnglishWord =
        /^[a-z]+$/i.test(cleanWord) &&
        !/^(la|cho|co|hay|ho|ma|voi|nay|ngay|hai|tu|khac|loi|truy|goc|gop|truoc|sau|duong|dan|hop|giao|dien|dung|dang|phien|ban|chua|cai|dat|phan|mem|chia|se|man|hinh|nen)$/i.test(
          cleanWord
        ) &&
        cleanWord.length > 1;

      if (isEnglishWord) {
        return `[en]${word}[/en]`;
      }
      return `[vi]${word}[/vi]`;
    })
    .join(" ");
};

// Hiển thị đầy đủ cả thẻ [vi], [/vi], [en], [/en] thô gốc và in đậm từ tiếng Anh
function HighlightCsText({ rawText }) {
  if (!rawText) return null;

  if (rawText.includes("[en]") || rawText.includes("[vi]")) {
    const parts = rawText.split(/(\[en\].*?\[\/en\]|\[vi\].*?\[\/vi\])/gi);
    
    return (
      <>
        {parts.map((part, index) => {
          if (!part) return null;
          const lowerPart = part.toLowerCase();

          if (lowerPart.startsWith("[en]")) {
            return (
              <span key={index} className="font-semibold">
                {part}
              </span>
            );
          }
          return part;
        })}
      </>
    );
  }

  return <>{rawText}</>;
}

export default function AdminTextData() {
  const [textList, setTextList] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("all");
  
  const [currentPage, setCurrentPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const pageSize = 10;
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  
  const [selectedJsonFile, setSelectedJsonFile] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [fileLoading, setFileLoading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isDragging, setIsDragging] = useState(false);

  const fetchTexts = async () => {
    try {
      const params = {
        page: currentPage,
        pageSize: pageSize
      };
      
      if (activeSearchTerm) {
        params.search = activeSearchTerm;
      }
      if (selectedStatus !== "all") {
        params.status = selectedStatus === "active" ? "Validated" : "Pending";
      }

      const res = await adminService.getTexts(params);
      const items = res.items || res.data || (Array.isArray(res) ? res : []);
      const total = res.totalCount || res.totalItems || res.total || (Array.isArray(res) ? res.length : items.length);

      setTotalItems(total);
      setTextList(
        items.map((item) => ({
          id: item.scriptId || item.id,
          // Giữ NGUYÊN 100% chuỗi gốc chứa [vi], [/vi], [en], [/en] cho cả 2 trường
          csContent: item.csContent || item.csPlain || item.content || "",
          viContent: item.viContent || item.viEquivalent || item.csContent || item.content || "",
          content: item.csContent || item.csPlain || item.content || "",
          rawCsContent: item.csContent || item.content || "",
          topic: normalizeTopicName(item.domain || item.topic),
          status: (item.status === "Validated" || item.status === "active") ? "active" : "pending"
        }))
      );
    } catch (err) {
      console.error("Lỗi lấy danh sách câu văn bản:", err);
    }
  };

  useEffect(() => {
    fetchTexts();
  }, [currentPage, activeSearchTerm, selectedStatus]);

  // Hàm cập nhật trạng thái từ Chờ duyệt sang Sẵn sàng
  const handleScriptStatusChange = async (item, newStatus) => {
    if (newStatus === "active" && item.status !== "active") {
      setTextList((prev) =>
        prev.map((t) => (t.id === item.id ? { ...t, status: "active" } : t))
      );

      try {
        await adminService.reviewScript(item.id, {
          action: "Accepted"
        });
        toast.success("Đã duyệt văn bản sang trạng thái Sẵn sàng!");
        fetchTexts();
      } catch (err) {
        console.error("Lỗi duyệt văn bản:", err);
        const serverMsg = err?.response?.data?.title || err?.response?.data?.message || "Không thể thay đổi trạng thái văn bản!";
        toast.error(`Lỗi: ${serverMsg}`);
        fetchTexts();
      }
    }
  };

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
    if (!file.name.endsWith(".json")) {
      toast.error("Vui lòng chọn đúng file định dạng .json!");
      return;
    }

    setFileLoading(true);
    setUploadProgress(0);
    setSelectedJsonFile(null);

    let current = 0;
    const interval = setInterval(() => {
      current += 25;
      setUploadProgress(current);
      if (current >= 100) {
        clearInterval(interval);
        setTimeout(() => {
          setSelectedJsonFile(file);
          setFileLoading(false);
        }, 150);
      }
    }, 60);
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setSelectedJsonFile(null);
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setSelectedJsonFile(null);
    setFileLoading(false);
    setUploadProgress(0);
    setShowModal(true);
  };

  const handleSubmitForm = async (e) => {
    e.preventDefault();

    try {
      if (!selectedJsonFile) {
        toast.error("Vui lòng chọn hoặc kéo thả file JSON!");
        return;
      }

      setIsSubmitting(true);

      const fileContent = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = (event) => resolve(event.target.result);
        reader.onerror = (error) => reject(error);
        reader.readAsText(selectedJsonFile, "UTF-8");
      });

      let parsedData;
      try {
        parsedData = JSON.parse(fileContent);
      } catch (jsonErr) {
        toast.error("File không đúng chuẩn JSON! Vui lòng kiểm tra lại cú pháp.");
        setIsSubmitting(false);
        return;
      }

      const itemsToUpload = Array.isArray(parsedData) ? parsedData : [parsedData];
      if (itemsToUpload.length === 0) {
        toast.error("File JSON rỗng!");
        setIsSubmitting(false);
        return;
      }

      const item = itemsToUpload[0];
      const rawCs = item.cs_transcript || item.csContent || item.content || "";
      const rawVi = item.vi_equivalent || item.viContent || rawCs;

      const taggedCsContent = addTagsToCsContent(rawCs);
      const cleanCsForCheck = cleanLanguageTags(rawCs).toLowerCase();

      const isDuplicateOnClient = textList.some((existingItem) => {
        if (editingItem && existingItem.id === editingItem.id) return false;
        const existingClean = cleanLanguageTags(existingItem.csContent).toLowerCase();
        return existingClean === cleanCsForCheck;
      });

      if (isDuplicateOnClient) {
        toast.error("Câu văn bản này đã tồn tại trên hệ thống! Vui lòng không thêm trùng.");
        setIsSubmitting(false);
        return;
      }

      if (editingItem) {
        await adminService.updatePendingScript(editingItem.id, {
          csContent: taggedCsContent,
          viContent: rawVi
        });
        toast.success("Đã cập nhật câu văn bản! Trạng thái vẫn giữ nguyên Chờ duyệt.");
      } else {
        await adminService.importScripts(selectedJsonFile);
        toast.success("Đã thêm thành công câu văn bản ở trạng thái Chờ duyệt!");
      }

      setShowModal(false);
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xử lý form:", err);
      const responseData = err?.response?.data;
      let errorMsg = responseData?.title || responseData?.message || err?.message || "Thao tác xử lý file thất bại!";

      if (
        errorMsg.toLowerCase().includes("trùng") ||
        errorMsg.toLowerCase().includes("exist") ||
        errorMsg.toLowerCase().includes("already") ||
        err?.response?.status === 409 ||
        err?.response?.status === 422
      ) {
        errorMsg = "Câu văn bản bị trùng lặp! Backend từ chối lưu câu đã tồn tại.";
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
                value={selectedStatus}
                onChange={(e) => {
                  setSelectedStatus(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-[160px] px-3 py-2 bg-[#F7F5EF] dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-ui font-label text-[#16171C] dark:text-white outline-none cursor-pointer transition-all"
                onFocus={(e) => {
                  e.target.style.borderColor = ADMIN_ACCENT;
                  e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                }}
                onBlur={(e) => {
                  e.target.style.borderColor = '';
                  e.target.style.boxShadow = 'none';
                }}
              >
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả trạng thái</option>
                <option value="active" className="bg-white dark:bg-[#25272E]">Sẵn sàng</option>
                <option value="pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
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
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0 overflow-auto">
          <div className="min-w-[650px] h-full flex flex-col">
            {/* Header Table */}
            <div className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center shrink-0 h-8">
              <div className="px-3 text-center whitespace-nowrap w-[65px]">STT</div>
              <div className="px-4 text-left flex-1">Nội dung</div>
              <div className="px-3 text-left w-[170px] whitespace-nowrap">Chủ đề</div>
              <div className="px-3 text-left w-[135px] whitespace-nowrap">Trạng thái</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui flex-1 grid grid-rows-10">
              {textList.length > 0 ? (
                textList.map((item, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  const style = catStyle(item.topic);
                  return (
                    <div key={item.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full py-1">
                      <div className="px-3 text-center font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap w-[65px]">
                        {stt}
                      </div>
                      
                      {/* Hiển thị 2 dòng dạng badge VI-EN và VI */}
                      <div className="px-4 flex-1 space-y-0.5 my-0.5 pr-6 min-w-0">
                        {/* Dòng 1: Trộn ngôn ngữ (VI-EN) - HIỂN THỊ ĐẦY ĐỦ THẺ [vi], [en] */}
                        <div className="flex items-center gap-2">
                          <span 
                            style={{ background: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                            className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label"
                          >
                            VI-EN
                          </span>
                          <p className="truncate type-ui text-[#16171C] dark:text-gray-100">
                            <HighlightCsText rawText={item.rawCsContent || item.csContent} />
                          </p>
                        </div>

                        {/* Dòng 2: Tiếng Việt thuần (VI) - GIỮ NGUYÊN MÃ THÔ NẾU CÓ */}
                        <div className="flex items-center gap-2">
                          <span 
                            style={{ background: '#F0EEE6', color: '#6E7078' }}
                            className="w-11 shrink-0 rounded text-center text-caption leading-4 font-label dark:bg-[#25272E] dark:text-gray-400"
                          >
                            VI
                          </span>
                          <p className="truncate type-ui text-[#16171C] dark:text-gray-300">
                            <HighlightCsText rawText={item.viContent} />
                          </p>
                        </div>
                      </div>
                      
                      <div className="px-3 w-[170px] whitespace-nowrap">
                        <span 
                          className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap" 
                          style={{ background: style.bg, color: style.text, borderColor: style.border }}
                        >
                          {normalizeTopicName(item.topic)}
                        </span>
                      </div>
                      
                      {/* CỘT TRẠNG THÁI */}
                      <div className="px-3 w-[135px] whitespace-nowrap">
                        {item.status === "active" ? (
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
                        ) : (
                          <div className="relative inline-block group">
                            <span 
                              className="inline-flex items-center justify-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border cursor-pointer select-none"
                              style={{
                                backgroundColor: CHIP_WARNING_BG,
                                borderColor: CHIP_WARNING_BORDER,
                                color: CHIP_WARNING_TEXT
                              }}
                            >
                              <Clock className="w-3 h-3 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Chờ duyệt
                              <ChevronDown className="w-3 h-3 ml-0.5 opacity-60" />
                            </span>

                            <select
                              value={item.status}
                              onChange={(e) => handleScriptStatusChange(item, e.target.value)}
                              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
                            >
                              <option value="pending">Chờ duyệt</option>
                              <option value="active">Sẵn sàng</option>
                            </select>
                          </div>
                        )}
                      </div>

                      <div className="px-4 text-center w-[100px] whitespace-nowrap">
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
                      </div>
                    </div>
                  );
                })
              ) : (
                <div className="row-span-10 flex items-center justify-center text-[#9A9CA3] dark:text-gray-500 type-ui">
                  Không tìm thấy dữ liệu văn bản phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Pagination */}
        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0 bg-[#F7F5EF]/50 dark:bg-[#1C1D22]">
          <Pagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={(p) => setCurrentPage(p)}
            accent={ADMIN_ACCENT}
            variant="soft"
          />
        </div>
      </div>

      {/* Modal Thêm & Chỉnh Sửa Dùng Chung Kéo Thả File JSON */}
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

              <div className="space-y-3">
                <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                  Kéo thả hoặc chọn file dữ liệu (.json)
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
                    accept=".json"
                    id="json-file-input"
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
                        Đang đọc dữ liệu JSON ({uploadProgress}%)...
                      </span>
                    </div>
                  ) : selectedJsonFile ? (
                    <label htmlFor="json-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2 py-1">
                      <div 
                        className="w-12 h-12 rounded-2xl flex items-center justify-center shadow-xs"
                        style={{ backgroundColor: `${ADMIN_ACCENT}1A`, color: ADMIN_ACCENT }}
                      >
                        <FileJson className="w-7 h-7" style={{ color: ADMIN_ACCENT }} />
                      </div>
                      <span className="type-body font-bold text-[#16171C] dark:text-gray-100 max-w-[260px] truncate">
                        {selectedJsonFile.name}
                      </span>
                      <span className="type-caption font-medium hover:underline" style={{ color: ADMIN_ACCENT }}>
                        Bấm để chọn file .json khác
                      </span>
                    </label>
                  ) : (
                    <label htmlFor="json-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2">
                      <div className="w-12 h-12 rounded-full bg-white dark:bg-[#1C1D22] flex items-center justify-center shadow-xs">
                        <Upload className="w-6 h-6 text-[#6E7078] dark:text-gray-300" />
                      </div>
                      <span className="type-body font-medium text-[#16171C] dark:text-gray-200">
                        Kéo thả file vào đây hoặc nhấp để tải lên
                      </span>
                    </label>
                  )}
                </div>
              </div>

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
                  disabled={isSubmitting || fileLoading || !selectedJsonFile}
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