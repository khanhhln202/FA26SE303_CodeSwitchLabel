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
  Upload
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

// Map chuẩn domain gửi về API Backend
const mapDomainToApi = (rawTopic) => {
  if (!rawTopic) return 'ItTechnology';
  const topicLower = String(rawTopic).trim().toLowerCase();

  if (topicLower.includes('it') || topicLower.includes('tech') || topicLower.includes('công nghệ')) {
    return 'ItTechnology';
  }
  if (topicLower.includes('daily') || topicLower.includes('life') || topicLower.includes('hội thoại')) {
    return 'DailyLife';
  }
  if (topicLower.includes('edu') || topicLower.includes('giáo dục')) {
    return 'Education';
  }
  return 'ItTechnology';
};

const catStyle = (cat) => {
  const normalized = normalizeTopicName(cat);
  return CATEGORY_COLORS[normalized] || { bg: '#F7F5EF', text: '#6E7078', border: '#E5E2D8' };
};

const countWords = (text) => {
  if (!text || !text.trim()) return 0;
  const words = text.trim().match(/\S+/g);
  return words ? words.length : 0;
};

export default function AdminTextData() {
  const [textList, setTextList] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("all");
  
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  const [formData, setFormData] = useState({ content: "", topic: "Công nghệ thông tin", status: "active" });
  
  const [selectedJsonFile, setSelectedJsonFile] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchTexts = async () => {
    try {
      const res = await adminService.getTexts({
        page: currentPage,
        pageSize: 100
      });
      const items = res.items || res || [];
      setTextList(
        items.map((item) => ({
          id: item.scriptId || item.id,
          content: item.csContent || item.csPlain || item.content || "",
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
  }, [currentPage]);

  const handleSearch = () => {
    setActiveSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setFormData({ content: "", topic: "Công nghệ thông tin", status: "active" });
    setSelectedJsonFile(null);
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setFormData({ content: item.content, topic: item.topic, status: item.status });
    setSelectedJsonFile(null);
    setShowModal(true);
  };

  const handleSubmitForm = async (e) => {
    e.preventDefault();

    try {
      if (editingItem) {
        if (!formData.content.trim()) return;
        await adminService.reviewScript(editingItem.id, {
          action: "Edited",
          editedCsContent: formData.content,
          editedViContent: formData.content
        });
        toast.success(`Đã cập nhật thành công câu văn bản!`);
        setShowModal(false);
        fetchTexts();
      } else {
        if (!selectedJsonFile) {
          toast.error("Vui lòng chọn file JSON để thêm mới!");
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
          console.error("Lỗi cú pháp file JSON:", jsonErr);
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

        let successCount = 0;
        let lastErrorMsg = "";

        for (const item of itemsToUpload) {
          const csContent = item.cs_transcript || item.csContent || item.content || "";
          const viContent = item.vi_equivalent || item.viContent || csContent;
          const domain = mapDomainToApi(item.domain || item.topic);
          const alignments = item.alignment || item.alignments || [];

          if (csContent) {
            try {
              await adminService.createText({
                csContent,
                viContent,
                domain,
                alignment: alignments
              });
              successCount++;
            } catch (apiErr) {
              console.error("Lỗi gửi dữ liệu câu văn bản:", csContent, apiErr);
              lastErrorMsg = apiErr.response?.data?.title || apiErr.response?.data?.message || apiErr.message;
            }
          }
        }

        if (successCount > 0) {
          toast.success(`Đã thêm thành công ${successCount}/${itemsToUpload.length} văn bản!`);
          setShowModal(false);
          fetchTexts();
        } else {
          toast.error(`Không thể lưu dữ liệu: ${lastErrorMsg || "Lỗi API Server!"}`);
        }
      }
    } catch (err) {
      console.error("Lỗi xử lý form:", err);
      const errorMsg = err.response?.data?.title || "Thao tác cập nhật văn bản thất bại!";
      toast.error(errorMsg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (!itemToDelete) return;
    try {
      await adminService.deleteText(itemToDelete.id);
      toast.success(`Đã loại bỏ thành công câu văn bản!`);
      fetchTexts();
    } catch (err) {
      console.error("Lỗi xóa văn bản:", err.response?.data);
      const errorData = err.response?.data;
      let errorMsg = errorData?.title || errorData?.message || "Loại bỏ văn bản thất bại!";
      
      if (errorData?.errors) {
        const firstKey = Object.keys(errorData.errors)[0];
        errorMsg = errorData.errors[firstKey][0] || errorMsg;
      }
      
      toast.error(errorMsg);
    }
    setItemToDelete(null);
  };

  const filteredTexts = useMemo(() => {
    return textList.filter((item) => {
      const matchesSearch = 
        item.content.toLowerCase().includes(activeSearchTerm.toLowerCase()) || 
        String(item.id).toLowerCase().includes(activeSearchTerm.toLowerCase());
      const matchesStatus = selectedStatus === "all" || item.status === selectedStatus;
      
      return matchesSearch && matchesStatus;
    });
  }, [textList, activeSearchTerm, selectedStatus]);

  const totalPages = Math.ceil(filteredTexts.length / pageSize) || 1;
  const paginatedTexts = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredTexts.slice(start, start + pageSize);
  }, [filteredTexts, currentPage, pageSize]);

  return (
    <div className="max-w-6xl mx-auto h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
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
          <div className="min-w-[750px] h-full flex flex-col">
            {/* Header Table */}
            <div className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center shrink-0 h-8">
              <div className="px-3 text-center whitespace-nowrap w-[65px]">STT</div>
              <div className="px-4 text-left flex-1">Nội dung</div>
              <div className="px-3 text-left w-[170px] whitespace-nowrap">Chủ đề</div>
              <div className="px-3 text-left w-[95px] whitespace-nowrap">Số từ</div>
              <div className="px-3 text-left w-[125px] whitespace-nowrap">Trạng thái</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui flex-1 grid grid-rows-10">
              {paginatedTexts.length > 0 ? (
                paginatedTexts.map((item, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  const style = catStyle(item.topic);
                  return (
                    <div key={item.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full">
                      <div className="px-3 text-center font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap w-[65px]">
                        {stt}
                      </div>
                      
                      <div className="px-4 flex-1 truncate pr-6 text-[#16171C] dark:text-gray-200 font-medium">
                        {item.content}
                      </div>
                      
                      <div className="px-3 w-[170px] whitespace-nowrap">
                        <span 
                          className="px-2.5 py-0.5 rounded-md type-caption font-label border inline-block whitespace-nowrap" 
                          style={{ background: style.bg, color: style.text, borderColor: style.border }}
                        >
                          {normalizeTopicName(item.topic)}
                        </span>
                      </div>
                      
                      <div className="px-3 w-[95px] font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap">
                        {countWords(item.content)} từ
                      </div>
                      
                      <div className="px-3 w-[125px] whitespace-nowrap">
                        {item.status === "active" ? (
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border"
                            style={{
                              backgroundColor: CHIP_SUCCESS_BG,
                              borderColor: CHIP_SUCCESS_BORDER,
                              color: CHIP_SUCCESS_TEXT
                            }}
                          >
                            <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Sẵn sàng
                          </span>
                        ) : (
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border"
                            style={{
                              backgroundColor: CHIP_WARNING_BG,
                              borderColor: CHIP_WARNING_BORDER,
                              color: CHIP_WARNING_TEXT
                            }}
                          >
                            <Clock className="w-3 h-3 shrink-0" style={{ color: CHIP_WARNING_TEXT }} /> Chờ duyệt
                          </span>
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

      {/* Modal Thêm & Chỉnh Sửa */}
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

              {editingItem ? (
                <>
                  <div>
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                      Chủ đề
                    </label>
                    <select
                      value={formData.topic}
                      onChange={(e) => setFormData({ ...formData, topic: e.target.value })}
                      className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-body text-[#16171C] dark:text-white outline-none cursor-pointer transition-all font-sans"
                      onFocus={(e) => {
                        e.target.style.borderColor = ADMIN_ACCENT;
                        e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                      }}
                      onBlur={(e) => {
                        e.target.style.borderColor = '';
                        e.target.style.boxShadow = 'none';
                      }}
                    >
                      <option value="Công nghệ thông tin" className="bg-white dark:bg-[#25272E]">Công nghệ thông tin</option>
                      <option value="Giáo dục" className="bg-white dark:bg-[#25272E]">Giáo dục</option>
                      <option value="Hội thoại hàng ngày" className="bg-white dark:bg-[#25272E]">Hội thoại hàng ngày</option>
                    </select>
                  </div>

                  <div>
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                      Nội dung câu mẫu
                    </label>
                    <textarea
                      rows="4"
                      required
                      placeholder="Nhập câu mẫu..."
                      value={formData.content}
                      onChange={(e) => setFormData({ ...formData, content: e.target.value })}
                      className="w-full p-3.5 bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-body text-[#16171C] dark:text-white outline-none transition-all resize-none"
                      onFocus={(e) => {
                        e.target.style.borderColor = ADMIN_ACCENT;
                        e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                      }}
                      onBlur={(e) => {
                        e.target.style.borderColor = '';
                        e.target.style.boxShadow = 'none';
                      }}
                    ></textarea>
                    <div className="flex justify-between items-center mt-1 type-meta text-[#6E7078] dark:text-gray-400">
                      <span>Không giới hạn độ dài câu</span>
                      <span className="font-bold text-[#16171C] dark:text-gray-200">
                        Số từ: {countWords(formData.content)} từ
                      </span>
                    </div>
                  </div>

                  <div>
                    <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                      Trạng thái
                    </label>
                    <select
                      value={formData.status}
                      onChange={(e) => setFormData({ ...formData, status: e.target.value })}
                      className="w-full h-11 px-3.5 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 bg-white dark:bg-[#25272E] type-body text-[#16171C] dark:text-white outline-none cursor-pointer transition-all font-sans"
                      onFocus={(e) => {
                        e.target.style.borderColor = ADMIN_ACCENT;
                        e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                      }}
                      onBlur={(e) => {
                        e.target.style.borderColor = '';
                        e.target.style.boxShadow = 'none';
                      }}
                    >
                      <option value="active" className="bg-white dark:bg-[#25272E]">Sẵn sàng</option>
                      <option value="pending" className="bg-white dark:bg-[#25272E]">Chờ duyệt</option>
                    </select>
                  </div>
                </>
              ) : (
                <div className="space-y-3">
                  <label className="block type-label text-[#2B2C31] dark:text-gray-200">
                    Tải lên file dữ liệu (.json)
                  </label>
                  <div className="border-2 border-dashed border-[#E5E2D8] dark:border-gray-700 rounded-2xl p-6 text-center hover:border-gray-400 dark:hover:border-gray-500 transition-colors bg-[#F7F5EF]/50 dark:bg-[#25272E]/50">
                    <input
                      type="file"
                      accept=".json"
                      id="json-file-input"
                      className="hidden"
                      onChange={(e) => {
                        if (e.target.files && e.target.files[0]) {
                          setSelectedJsonFile(e.target.files[0]);
                        }
                      }}
                    />
                    <label htmlFor="json-file-input" className="cursor-pointer flex flex-col items-center justify-center space-y-2">
                      <div className="w-12 h-12 rounded-full bg-white dark:bg-[#1C1D22] flex items-center justify-center shadow-xs">
                        <Upload className="w-6 h-6 text-[#6E7078] dark:text-gray-300" />
                      </div>
                      <span className="type-body font-medium text-[#16171C] dark:text-gray-200">
                        {selectedJsonFile ? selectedJsonFile.name : "Nhấp để chọn file JSON"}
                      </span>
                      <span className="type-meta text-[#6E7078] dark:text-gray-400">
                        Chấp nhận file JSON dạng 1 đối tượng hoặc mảng các câu văn bản
                      </span>
                    </label>
                  </div>
                </div>
              )}

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  disabled={isSubmitting}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all disabled:opacity-50"
                >
                  {isSubmitting ? "Đang xử lý..." : editingItem ? "Lưu thay đổi" : "Lưu Văn Bản"}
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