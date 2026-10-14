import { useState, useMemo, useEffect } from "react";
import { 
  FolderKanban, 
  Plus, 
  Search, 
  Trash2, 
  Edit3, 
  CheckCircle2, 
  X,
  AlertTriangle
} from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";
import {
  ADMIN_ACCENT,
  SUCCESS,
  DANGER
} from "../../../constants/theme";
import { adminService } from "../../../services/adminService";

export default function AdminTopicConfig() {
  const [topicList, setTopicList] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [activeSearchTerm, setActiveSearchTerm] = useState("");
  
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;
  
  const [showModal, setShowModal] = useState(false);
  const [editingItem, setEditingItem] = useState(null); 
  const [itemToDelete, setItemToDelete] = useState(null);
  const [toast, setToast] = useState({ show: false, message: "" });
  const [formData, setFormData] = useState({ name: "" });

  const fetchTopics = async () => {
    try {
      const data = await adminService.getTopics();
      if (Array.isArray(data)) {
        setTopicList(
          data.map((item, idx) => ({
            id: item.code || `TOPIC-${String(idx + 1).padStart(3, '0')}`,
            name: item.name || item.code
          }))
        );
      }
    } catch (err) {
      console.error("Lỗi lấy danh sách chủ đề:", err);
    }
  };

  useEffect(() => {
    fetchTopics();
  }, []);

  useEffect(() => {
    if (toast.show) {
      const timer = setTimeout(() => {
        setToast((prev) => ({ ...prev, show: false }));
      }, 3500);
      return () => clearTimeout(timer);
    }
  }, [toast.show]);

  const showNotification = (msg) => {
    setToast({ show: true, message: msg });
  };

  const handleSearch = () => {
    setActiveSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") handleSearch();
  };

  const handleOpenAddModal = () => {
    setEditingItem(null);
    setFormData({ name: "" });
    setShowModal(true);
  };

  const handleOpenEditModal = (item) => {
    setEditingItem(item);
    setFormData({ name: item.name });
    setShowModal(true);
  };

  const handleSubmitForm = (e) => {
    e.preventDefault();
    if (!formData.name.trim()) return;

    if (editingItem) {
      setTopicList((prev) =>
        prev.map((item) =>
          item.id === editingItem.id
            ? { ...item, name: formData.name }
            : item
        )
      );
      showNotification(`Đã cập nhật chủ đề "${formData.name}" thành công!`);
    } else {
      const newItem = {
        id: `TOPIC-${String(topicList.length + 1).padStart(3, '0')}`,
        name: formData.name
      };
      setTopicList((prev) => [...prev, newItem]);
      showNotification("Đã thêm chủ đề mới thành công!");
    }
    setShowModal(false);
  };

  const handleConfirmDelete = () => {
    if (!itemToDelete) return;
    setTopicList((prev) => prev.filter((t) => t.id !== itemToDelete.id));
    showNotification(`Đã xóa thành công chủ đề "${itemToDelete.name}"!`);
    setItemToDelete(null);
  };

  const filteredTopics = useMemo(() => {
    return topicList.filter((item) => {
      return (
        item.name.toLowerCase().includes(activeSearchTerm.toLowerCase()) || 
        item.id.toLowerCase().includes(activeSearchTerm.toLowerCase())
      );
    });
  }, [topicList, activeSearchTerm]);

  const totalPages = Math.ceil(filteredTopics.length / pageSize) || 1;
  const paginatedTopics = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredTopics.slice(start, start + pageSize);
  }, [filteredTopics, currentPage, pageSize]);

  return (
    <div className="max-w-6xl mx-auto h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
      {/* Toast Notification */}
      <div 
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2.5 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-4 py-3 rounded-2xl shadow-2xl border border-[#3FA66B]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-5 h-5 shrink-0" style={{ color: SUCCESS }} />
        <span className="type-ui font-label">{toast.message}</span>
        <button 
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-1"
        >
          <X className="w-4 h-4 text-[#9A9CA3] dark:text-[#6E7078]" />
        </button>
      </div>

      {/* Header & Toolbar */}
      <div className="shrink-0 space-y-2">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1.5">
          <div>
            <h2 className="text-[22px] font-bold text-[#16171C] dark:text-white tracking-tight flex items-center gap-2">
              <FolderKanban className="w-5 h-5 text-[#16171C] dark:text-white shrink-0" />
              Thiết lập danh mục chủ đề
            </h2>
            <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-0.5">
              Quản lý danh sách các chủ đề phân loại câu đóng góp trong hệ thống
            </p>
          </div>

          <button
            onClick={handleOpenAddModal}
            style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
            className="px-4 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer shrink-0"
          >
            <Plus className="w-4 h-4" />
            <span>Thêm chủ đề mới</span>
          </button>
        </div>

        {/* Toolbar & Search Bar */}
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
                placeholder="Tìm kiếm chủ đề..."
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
        </div>
      </div>

      {/* Main Table Container */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0 overflow-auto">
          <div className="min-w-[500px] h-full flex flex-col">
            {/* Header Table */}
            <div className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center shrink-0 h-8">
              <div className="px-3 text-center whitespace-nowrap w-[65px]">STT</div>
              <div className="px-4 text-left flex-1 whitespace-nowrap">Tên chủ đề</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui flex-1 grid grid-rows-10">
              {paginatedTopics.length > 0 ? (
                paginatedTopics.map((item, index) => {
                  const stt = (currentPage - 1) * pageSize + index + 1;
                  return (
                    <div key={item.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full">
                      <div className="px-3 text-center font-bold text-[#16171C] dark:text-gray-100 whitespace-nowrap w-[65px]">
                        {stt}
                      </div>
                      
                      <div className="px-4 flex-1 truncate font-bold text-[#16171C] dark:text-white">
                        {item.name}
                      </div>

                      <div className="px-4 text-center w-[100px] whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button 
                            onClick={() => handleOpenEditModal(item)}
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                            title="Chỉnh sửa chủ đề"
                          >
                            <Edit3 className="w-4 h-4" />
                          </button>
                          <button 
                            onClick={() => setItemToDelete(item)}
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                            title="Xóa chủ đề"
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
                  Không tìm thấy chủ đề phù hợp.
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
                  {editingItem ? `Chỉnh sửa chủ đề` : "Thêm chủ đề mới"}
                </h3>
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">
                  Tên chủ đề
                </label>
                <input
                  type="text"
                  required
                  placeholder="Nhập tên chủ đề..."
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="w-full h-11 px-3.5 py-2.5 bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-body text-[#16171C] dark:text-white outline-none transition-all placeholder:text-[#9A9CA3]"
                  onFocus={(e) => {
                    e.target.style.borderColor = ADMIN_ACCENT;
                    e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = '';
                    e.target.style.boxShadow = 'none';
                  }}
                />
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all"
                >
                  {editingItem ? "Lưu thay đổi" : "Lưu Chủ Đề"}
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
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa chủ đề</h3>
                <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-1 break-words">
                  Bạn có chắc chắn muốn xóa chủ đề <span className="font-bold text-[#16171C] dark:text-gray-200">"{itemToDelete.name}"</span>? Hành động này không thể hoàn tác.
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