import { useState, useMemo, useEffect } from "react";
import { Users, UserPlus, Search, Filter, Ban, CheckCircle2, X, Trash2, AlertTriangle, Lock, Unlock } from "lucide-react";
import Pagination from "../../../components/Pagination/Pagination";

import { 
  ADMIN_ACCENT,
  SPEAKER_ACCENT, 
  REVIEWER_ACCENT, 
  TASK_MANAGER_ACCENT,
  SUCCESS,
  DANGER,
  CHIP_SUCCESS_BG,
  CHIP_SUCCESS_BORDER,
  CHIP_SUCCESS_TEXT,
  CHIP_DANGER_BG,
  CHIP_DANGER_BORDER,
  CHIP_DANGER_TEXT
} from "../../../constants/theme";

const DEFAULT_USERS = [
  { id: "USR-001", name: "Quản Lý", email: "manager@fpt.edu.vn", role: "Task Manager", status: "Active", createdAt: "15/01/2026" },
  { id: "USR-002", name: "Trần Minh Tâm", email: "tam.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "20/01/2026" },
  { id: "USR-005", name: "Nguyễn Văn Anh", email: "anh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "06/02/2026" },
  { id: "USR-006", name: "Trần Thị Bình", email: "binh.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "07/02/2026" },
  { id: "USR-007", name: "Lê Văn Cường", email: "cuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "08/02/2026" },
  { id: "USR-008", name: "Phạm Thị Dung", email: "dung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "09/02/2026" },
  { id: "USR-009", name: "Hoàng Văn Em", email: "em.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "10/02/2026" },
  { id: "USR-010", name: "Vũ Thị Phương", email: "phuong.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "11/02/2026" },
  { id: "USR-011", name: "Đặng Văn Giang", email: "giang.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "12/02/2026" },
  { id: "USR-012", name: "Bùi Thị Hải", email: "hai.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "13/02/2026" },
  { id: "USR-013", name: "Đinh Văn Hùng", email: "hung.reviewer@fpt.edu.vn", role: "Reviewer", status: "Active", createdAt: "14/02/2026" },
  { id: "USR-003", name: "Phạm Thu Thảo", email: "thao.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "01/02/2026" },
  { id: "USR-004", name: "Lê Hoàng Nam", email: "nam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "05/02/2026" },
  { id: "USR-014", name: "Đỗ Thị Khánh", email: "khanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "15/02/2026" },
  { id: "USR-015", name: "Hoàng Văn Lâm", email: "lam.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "16/02/2026" },
  { id: "USR-016", name: "Ngô Thị Minh", email: "minh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "17/02/2026" },
  { id: "USR-017", name: "Dương Văn Nghĩa", email: "nghia.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "18/02/2026" },
  { id: "USR-018", name: "Lý Thị Oanh", email: "oanh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "19/02/2026" },
  { id: "USR-019", name: "Võ Văn Phong", email: "phong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "20/02/2026" },
  { id: "USR-020", name: "Đoàn Thị Quỳnh", email: "quynh.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "21/02/2026" },
  { id: "USR-021", name: "Trịnh Văn Rồng", email: "rong.speaker@fpt.edu.vn", role: "Speaker", status: "Active", createdAt: "22/02/2026" },
];

const LOCAL_STORAGE_KEY = "admin_users_list_v2";

export default function AdminUserManagement() {
  const [users, setUsers] = useState(() => {
    const saved = localStorage.getItem(LOCAL_STORAGE_KEY);
    let list = DEFAULT_USERS;
    if (saved) {
      try { list = JSON.parse(saved); } catch (e) { console.error(e); }
    }

    const tmProfile = localStorage.getItem("task_manager_user_profile");
    if (tmProfile) {
      try {
        const parsed = JSON.parse(tmProfile);
        if (parsed.name) {
          list = list.map((u) => u.role === "Task Manager" ? { ...u, name: parsed.name } : u);
        }
      } catch (e) { console.error(e); }
    }
    return list;
  });

  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  
  const [userToDelete, setUserToDelete] = useState(null);
  const [userToToggleStatus, setUserToToggleStatus] = useState(null);
  const [toast, setToast] = useState({ show: false, message: "" });
  const [newUser, setNewUser] = useState({ name: "", email: "", role: "Speaker" });
  
  const pageSize = 10;

  useEffect(() => {
    const syncTaskManagerName = () => {
      const tmProfile = localStorage.getItem("task_manager_user_profile");
      if (tmProfile) {
        try {
          const parsed = JSON.parse(tmProfile);
          if (parsed.name) {
            setUsers((prev) =>
              prev.map((u) => (u.role === "Task Manager" ? { ...u, name: parsed.name } : u))
            );
          }
        } catch (e) {
          console.error(e);
        }
      }
    };

    window.addEventListener("userProfileUpdated", syncTaskManagerName);
    return () => window.removeEventListener("userProfileUpdated", syncTaskManagerName);
  }, []);

  useEffect(() => {
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(users));
  }, [users]);

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
    setSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const ConfirmToggleStatusUser = () => {
    if (!userToToggleStatus) return;
    const targetUser = userToToggleStatus;

    setUsers((prev) => {
      const updatedList = prev.map((u) => {
        if (u.id === targetUser.id) {
          const nextStatus = u.status === "Active" ? "Disabled" : "Active";
          if (nextStatus === "Active") {
            showNotification(`Đã kích hoạt tài khoản "${u.name}" thành công!`);
          } else {
            showNotification(`Đã vô hiệu hóa tài khoản "${u.name}"!`);
          }
          return { ...u, status: nextStatus };
        }
        return u;
      });

      localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(updatedList));
      return updatedList;
    });

    setUserToToggleStatus(null);
  };

  const handleAddUser = (e) => {
    e.preventDefault();
    if (!newUser.name || !newUser.email) return;

    const created = {
      id: `USR-00${users.length + 1}`,
      name: newUser.name,
      email: newUser.email,
      role: newUser.role,
      status: "Active",
      createdAt: new Date().toLocaleDateString("vi-VN"),
    };

    const updated = [...users, created];
    setUsers(updated);
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(updated));
    setIsAddModalOpen(false);
    showNotification(`Đã tạo thành công người dùng "${newUser.name}"!`);
    setNewUser({ name: "", email: "", role: "Speaker" });
  };

  const ConfirmDeleteUser = () => {
    if (!userToDelete) return;
    const updated = users.filter((u) => u.id !== userToDelete.id);
    setUsers(updated);
    localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify(updated));
    showNotification(`Đã xóa thành công người dùng "${userToDelete.name}"!`);
    setUserToDelete(null);
  };

  const filteredUsers = useMemo(() => {
    return users.filter((u) => {
      const matchSearch = u.name.toLowerCase().includes(searchTerm.toLowerCase()) || u.email.toLowerCase().includes(searchTerm.toLowerCase());
      const matchRole = roleFilter === "all" || u.role === roleFilter;
      return matchSearch && matchRole;
    });
  }, [users, searchTerm, roleFilter]);

  const totalPages = Math.ceil(filteredUsers.length / pageSize) || 1;

  const paginatedUsers = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredUsers.slice(start, start + pageSize);
  }, [filteredUsers, currentPage, pageSize]);

  const roleBadgeStyle = (role) => {
    switch (role) {
      case "Task Manager": 
        return { backgroundColor: TASK_MANAGER_ACCENT, color: "#FFFFFF" };
      case "Reviewer": 
        return { backgroundColor: REVIEWER_ACCENT, color: "#FFFFFF" };
      default:
        return { backgroundColor: SPEAKER_ACCENT, color: "#FFFFFF" };
    }
  };

  return (
    <div className="max-w-6xl mx-auto h-full flex flex-col justify-between text-left font-sans p-1 overflow-hidden relative transition-colors">
      
      {/* Toast Notification */}
      <div 
        className={`fixed top-4 right-4 z-[9999] flex items-center gap-2 bg-[#16171C] dark:bg-white text-white dark:text-[#16171C] px-3.5 py-2 rounded-xl shadow-xl border border-[#3FA66B]/40 transform transition-all duration-300 ease-out ${
          toast.show ? "translate-y-0 opacity-100 scale-100" : "-translate-y-4 opacity-0 scale-95 pointer-events-none"
        }`}
      >
        <CheckCircle2 className="w-4 h-4 shrink-0" style={{ color: SUCCESS }} />
        <span className="text-xs font-bold">{toast.message}</span>
        <button 
          onClick={() => setToast((prev) => ({ ...prev, show: false }))} 
          className="p-1 hover:bg-white/10 dark:hover:bg-black/10 rounded-lg transition-colors cursor-pointer ml-1"
        >
          <X className="w-3.5 h-3.5 text-[#9A9CA3] dark:text-[#6E7078]" />
        </button>
      </div>

      {/* Header & Filter */}
      <div className="shrink-0 space-y-2">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1.5">
          <div>
            <h2 className="text-[22px] font-bold text-slate-900 dark:text-white tracking-tight flex items-center gap-2">
              <Users className="w-5 h-5 text-slate-900 dark:text-white shrink-0" />
              Quản lý người dùng
            </h2>
            <p className="text-[13px] font-semibold bg-clip-text text-transparent bg-gradient-to-r from-[#15803D] via-emerald-600 to-teal-700 dark:from-[#1DB954] dark:via-emerald-400 dark:to-green-300 mt-1">
              Quản lý tài khoản, phân quyền hệ thống và trạng thái hoạt động
            </p>
          </div>

          <button
            onClick={() => setIsAddModalOpen(true)}
            style={{ backgroundColor: ADMIN_ACCENT }}
            className="px-3 py-1.5 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 hover:opacity-90 transition-all cursor-pointer shadow-xs shrink-0"
          >
            <UserPlus className="w-3.5 h-3.5" />
            <span>Thêm người dùng mới</span>
          </button>
        </div>

        {/* Filter Bar */}
        <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 p-2 shadow-xs flex flex-col md:flex-row gap-2 justify-between items-center transition-colors">
          <div className="w-full md:w-auto flex-1 max-w-xl">
            <div className="relative w-full flex items-center bg-[#F9FAFB] dark:bg-[#25272E] rounded-xl border border-[#E5E7EB] dark:border-gray-700 focus-within:border-gray-400 dark:focus-within:border-gray-500 focus-within:bg-white dark:focus-within:bg-[#1C1D22] transition-all p-1">
              <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-[#9A9CA3] pointer-events-none z-10" />
              <input
                type="text"
                placeholder="Tìm kiếm người dùng..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                className="w-full pl-10 pr-28 py-1.5 bg-transparent border-none text-xs font-medium text-[#16171C] dark:text-white outline-none transition-all placeholder:text-gray-400"
              />
              <button
                type="button"
                onClick={handleSearch}
                style={{ backgroundColor: ADMIN_ACCENT }}
                className="absolute right-1 top-1/2 -translate-y-1/2 px-4 py-1.5 text-white rounded-lg text-xs font-bold hover:opacity-90 transition-all cursor-pointer z-10 shadow-xs"
              >
                Tìm kiếm
              </button>
            </div>
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto shrink-0">
            <Filter className="w-3.5 h-3.5 text-[#6E7078] dark:text-gray-400 shrink-0" />
            <select
              value={roleFilter}
              onChange={(e) => {
                setRoleFilter(e.target.value);
                setCurrentPage(1);
              }}
              className="w-[150px] px-2.5 py-2 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-xl text-xs font-bold text-[#16171C] dark:text-white outline-none cursor-pointer transition-all font-sans"
            >
              <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả vai trò</option>
              <option value="Task Manager" className="bg-white dark:bg-[#25272E]">Task Manager</option>
              <option value="Reviewer" className="bg-white dark:bg-[#25272E]">Reviewer</option>
              <option value="Speaker" className="bg-white dark:bg-[#25272E]">Speaker</option>
            </select>
          </div>
        </div>
      </div>

      {/* Khung chứa bảng */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-xl border border-[#E5E7EB] dark:border-gray-800 shadow-xs overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0">
          <div className="min-w-[700px] h-full flex flex-col">
            {/* Table Header */}
            <div className="bg-[#F9FAFB] dark:bg-[#25272E] text-[10px] uppercase tracking-wider text-[#6E7078] dark:text-gray-400 border-b border-[#E5E7EB] dark:border-gray-800 font-bold flex items-center shrink-0 h-9">
              <div className="px-3 text-center whitespace-nowrap w-[60px]">STT</div>
              <div className="px-4 text-left flex-1">Họ và Tên</div>
              <div className="px-3 text-left flex-1 whitespace-nowrap">Email</div>
              <div className="px-3 text-left w-[140px] whitespace-nowrap">Vai trò</div>
              <div className="px-3 text-left w-[130px] whitespace-nowrap">Trạng thái</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#E5E7EB] dark:divide-gray-800 text-[11px] font-medium flex-1 grid grid-rows-10">
              {paginatedUsers.length > 0 ? (
                paginatedUsers.map((u, idx) => {
                  const badgeStyle = roleBadgeStyle(u.role);
                  return (
                    <div key={u.id} className="hover:bg-[#F3F4F6] dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full">
                      <div className="px-3 text-center text-[#9A9CA3] dark:text-gray-500 font-sans whitespace-nowrap w-[60px]">
                        {(currentPage - 1) * pageSize + idx + 1}
                      </div>
                      <div className="px-4 font-bold text-[#16171C] dark:text-gray-100 flex-1 truncate">{u.name}</div>
                      <div className="px-3 text-[#6E7078] dark:text-gray-400 font-sans flex-1 truncate">{u.email}</div>
                      <div className="px-3 w-[140px] whitespace-nowrap">
                        <span 
                          className="inline-block px-2 py-0.5 rounded-full text-[9px] font-bold shadow-xs"
                          style={badgeStyle}
                        >
                          {u.role}
                        </span>
                      </div>
                      <div className="px-3 w-[130px] whitespace-nowrap">
                        {u.status === "Active" ? (
                          <span 
                            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                            style={{ 
                              backgroundColor: CHIP_SUCCESS_BG, 
                              borderColor: CHIP_SUCCESS_BORDER, 
                              color: CHIP_SUCCESS_TEXT 
                            }}
                          >
                            <CheckCircle2 className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Hoạt động
                          </span>
                        ) : (
                          <span 
                            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[9.5px] font-bold border"
                            style={{ 
                              backgroundColor: CHIP_DANGER_BG, 
                              borderColor: CHIP_DANGER_BORDER, 
                              color: CHIP_DANGER_TEXT 
                            }}
                          >
                            <Ban className="w-2.5 h-2.5 shrink-0" style={{ color: CHIP_DANGER_TEXT }} /> Vô hiệu hóa
                          </span>
                        )}
                      </div>
                      <div className="px-4 text-center w-[100px] whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            onClick={() => setUserToToggleStatus(u)}
                            title={u.status === "Active" ? "Vô hiệu hóa tài khoản" : "Kích hoạt tài khoản"}
                            className="p-1 rounded-md text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F3F4F6] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                          >
                            {u.status === "Active" ? (
                              <Unlock className="w-3.5 h-3.5" />
                            ) : (
                              <Lock className="w-3.5 h-3.5" />
                            )}
                          </button>

                          <button
                            onClick={() => setUserToDelete(u)}
                            title="Xóa người dùng"
                            className="p-1 rounded-md text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div className="row-span-10 flex items-center justify-center text-[#9A9CA3] dark:text-gray-500 text-xs">
                  Không tìm thấy người dùng nào phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Pagination */}
        <div className="px-2 py-1.5 border-t border-[#E5E7EB] dark:border-gray-800 shrink-0">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(p) => setCurrentPage(p)} accent={ADMIN_ACCENT} />
        </div>
      </div>

      {/* Modal Thêm Người Dùng */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setIsAddModalOpen(false)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-md shadow-2xl overflow-hidden border border-transparent dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full" style={{ backgroundColor: ADMIN_ACCENT }} />
            <form onSubmit={handleAddUser} className="p-5 space-y-3">
              <h3 className="text-sm font-bold text-[#16171C] dark:text-gray-100">Tạo người dùng mới</h3>
              
              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">Họ và Tên</label>
                <input
                  type="text"
                  required
                  value={newUser.name}
                  onChange={(e) => setNewUser({ ...newUser, name: e.target.value })}
                  placeholder="Nhập họ tên đầy đủ"
                  className="w-full px-3 py-1.5 text-xs border border-[#E5E7EB] dark:border-gray-700 bg-[#F9FAFB] dark:bg-[#25272E] focus:bg-white dark:focus:bg-[#1C1D22] text-[#16171C] dark:text-white rounded-lg outline-none focus:border-gray-400 dark:focus:border-gray-500 transition-all font-medium"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">Địa chỉ Email</label>
                <input
                  type="email"
                  required
                  value={newUser.email}
                  onChange={(e) => setNewUser({ ...newUser, email: e.target.value })}
                  placeholder="example@fpt.edu.vn"
                  className="w-full px-3 py-1.5 text-xs border border-[#E5E7EB] dark:border-gray-700 bg-[#F9FAFB] dark:bg-[#25272E] focus:bg-white dark:focus:bg-[#1C1D22] text-[#16171C] dark:text-white rounded-lg outline-none focus:border-gray-400 dark:focus:border-gray-500 transition-all font-medium"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-[#6E7078] dark:text-gray-400 uppercase mb-1">Phân quyền (Role)</label>
                <select
                  value={newUser.role}
                  onChange={(e) => setNewUser({ ...newUser, role: e.target.value })}
                  className="w-full px-3 py-1.5 bg-[#F9FAFB] dark:bg-[#25272E] border border-[#E5E7EB] dark:border-gray-700 focus:bg-white dark:focus:bg-[#1C1D22] focus:border-gray-400 dark:focus:border-gray-500 rounded-lg text-xs font-bold text-[#16171C] dark:text-white outline-none transition-all cursor-pointer font-sans"
                >
                  <option value="Speaker" className="bg-white dark:bg-[#25272E]">Speaker</option>
                  <option value="Reviewer" className="bg-white dark:bg-[#25272E]">Reviewer</option>
                  <option value="Task Manager" className="bg-white dark:bg-[#25272E]">Task Manager</option>
                </select>
              </div>

              <div className="flex gap-2 pt-1">
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="flex-1 py-2 rounded-lg border border-[#E5E7EB] dark:border-gray-700 text-xs font-bold text-[#6E7078] dark:text-gray-300 hover:bg-[#F9FAFB] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: ADMIN_ACCENT }}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold hover:opacity-90 cursor-pointer transition-all"
                >
                  Tạo người dùng
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Xác Nhận Vô Hiệu Hóa / Kích Hoạt */}
      {userToToggleStatus && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setUserToToggleStatus(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden border border-transparent dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full" style={{ backgroundColor: userToToggleStatus.status === "Active" ? DANGER : SUCCESS }} />
            <div className="p-5 text-center space-y-3">
              <div 
                className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto" 
                style={{ 
                  backgroundColor: `${userToToggleStatus.status === "Active" ? DANGER : SUCCESS}20`, 
                  color: userToToggleStatus.status === "Active" ? DANGER : SUCCESS 
                }}
              >
                {userToToggleStatus.status === "Active" ? (
                  <Ban className="w-5 h-5" />
                ) : (
                  <CheckCircle2 className="w-5 h-5" />
                )}
              </div>
              <div>
                <h3 className="text-sm font-bold text-[#16171C] dark:text-gray-100">
                  {userToToggleStatus.status === "Active" ? "Xác nhận vô hiệu hóa tài khoản" : "Xác nhận kích hoạt tài khoản"}
                </h3>
                <p className="text-[11px] text-[#6E7078] dark:text-gray-400 mt-1">
                  Bạn có chắc chắn muốn {userToToggleStatus.status === "Active" ? "vô hiệu hóa" : "kích hoạt"} tài khoản <span className="font-bold text-[#16171C] dark:text-gray-200">"{userToToggleStatus.name}"</span>?
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  onClick={() => setUserToToggleStatus(null)}
                  className="flex-1 py-2 rounded-lg border border-[#E5E7EB] dark:border-gray-700 text-xs font-bold text-[#6E7078] dark:text-gray-300 hover:bg-[#F9FAFB] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={ConfirmToggleStatusUser}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold transition-colors cursor-pointer"
                  style={{ backgroundColor: userToToggleStatus.status === "Active" ? DANGER : SUCCESS }}
                >
                  {userToToggleStatus.status === "Active" ? "Vô hiệu hóa" : "Kích hoạt"}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal Xác Nhận Xóa */}
      {userToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/50 dark:bg-black/60 backdrop-blur-[2px]" onClick={() => setUserToDelete(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden border border-transparent dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full" style={{ backgroundColor: DANGER }} />
            <div className="p-5 text-center space-y-3">
              <div className="w-10 h-10 rounded-xl flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}20`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-[#16171C] dark:text-gray-100">Xác nhận xóa người dùng</h3>
                <p className="text-[11px] text-[#6E7078] dark:text-gray-400 mt-1">
                  Bạn có chắc chắn muốn xóa tài khoản <span className="font-bold text-[#16171C] dark:text-gray-200">"{userToDelete.name}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2 pt-1">
                <button
                  onClick={() => setUserToDelete(null)}
                  className="flex-1 py-2 rounded-lg border border-[#E5E7EB] dark:border-gray-700 text-xs font-bold text-[#6E7078] dark:text-gray-300 hover:bg-[#F9FAFB] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={ConfirmDeleteUser}
                  className="flex-1 py-2 text-white rounded-lg text-xs font-bold transition-colors cursor-pointer"
                  style={{ backgroundColor: DANGER }}
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