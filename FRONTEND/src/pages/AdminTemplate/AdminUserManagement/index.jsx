import { useState, useMemo, useEffect } from "react";
import { Users, UserPlus, Search, Filter, Ban, CheckCircle2, Trash2, AlertTriangle, Lock, Unlock, X } from "lucide-react";
import { toast } from "sonner";
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
import { adminService } from "../../../services/adminService";

export default function AdminUserManagement() {
  const [users, setUsers] = useState([]);
  const [searchInput, setSearchInput] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [roleFilter, setRoleFilter] = useState("all");
  const [currentPage, setCurrentPage] = useState(1);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  
  const [userToDelete, setUserToDelete] = useState(null);
  const [userToToggleStatus, setUserToToggleStatus] = useState(null);
  const [newUser, setNewUser] = useState({ name: "", email: "", role: "Speaker" });
  
  const pageSize = 10;

  const fetchUsers = async () => {
    try {
      const res = await adminService.getUsers({
        page: currentPage,
        pageSize: 100
      });
      const items = res.items || res || [];
      setUsers(
        items.map((u) => {
          // Kiểm tra trạng thái vô hiệu hóa từ nhiều kiểu định dạng BE khác nhau
          const rawStatus = String(u.status || "").toLowerCase();
          const isDisabled = 
            u.isLocked === true || 
            u.isActive === false || 
            u.isLock === true || 
            rawStatus === "locked" || 
            rawStatus === "disabled" || 
            rawStatus === "inactive" || 
            rawStatus === "0";

          return {
            id: u.userId ? `USR-${String(u.userId).padStart(3, '0')}` : u.id,
            rawId: u.userId || u.id,
            name: u.fullName || u.name || "",
            email: u.email || "",
            role: u.role || "Speaker",
            status: isDisabled ? "Disabled" : "Active",
            createdAt: u.createdAt ? new Date(u.createdAt).toLocaleDateString("vi-VN") : ""
          };
        })
      );
    } catch (err) {
      console.error("Lỗi lấy danh sách người dùng:", err);
    }
  };

  useEffect(() => {
    fetchUsers();
  }, [currentPage]);

  const handleSearch = () => {
    setSearchTerm(searchInput);
    setCurrentPage(1);
  };

  const handleKeyDown = (e) => {
    if (e.key === "Enter") {
      handleSearch();
    }
  };

  const ConfirmToggleStatusUser = async () => {
    if (!userToToggleStatus) return;
    const targetUser = userToToggleStatus;

    try {
      if (targetUser.status === "Active") {
        await adminService.lockUser(targetUser.rawId);
        toast.success(`Đã vô hiệu hóa tài khoản "${targetUser.name}"!`);
      } else {
        await adminService.unlockUser(targetUser.rawId);
        toast.success(`Đã kích hoạt tài khoản "${targetUser.name}" thành công!`);
      }
      fetchUsers();
    } catch (err) {
      const errorMsg = err.response?.data?.title || "Thao tác khoá/mở khoá tài khoản thất bại!";
      toast.error(errorMsg);
    }

    setUserToToggleStatus(null);
  };

  const handleAddUser = async (e) => {
    e.preventDefault();
    if (!newUser.name || !newUser.email) return;

    try {
      await adminService.createUser({
        fullName: newUser.name,
        email: newUser.email,
        role: newUser.role
      });
      setIsAddModalOpen(false);
      toast.success(`Đã tạo thành công người dùng "${newUser.name}"!`);
      setNewUser({ name: "", email: "", role: "Speaker" });
      fetchUsers();
    } catch (err) {
      const errorMsg = err.response?.data?.title || "Tạo người dùng mới thất bại!";
      toast.error(errorMsg);
    }
  };

  const ConfirmDeleteUser = async () => {
    if (!userToDelete) return;
    try {
      await adminService.deleteUser(userToDelete.rawId);
      toast.success(`Đã xóa thành công người dùng "${userToDelete.name}"!`);
      fetchUsers();
    } catch (err) {
      const errorMsg = err.response?.data?.title || "Xóa người dùng thất bại!";
      toast.error(errorMsg);
    }
    setUserToDelete(null);
  };

  const filteredUsers = useMemo(() => {
    return users.filter((u) => {
      // Loại bỏ tài khoản Admin/Administrator khỏi danh sách
      const isNotAdmin = u.role !== "Admin" && u.role !== "Administrator";
      const matchSearch = u.name.toLowerCase().includes(searchTerm.toLowerCase()) || u.email.toLowerCase().includes(searchTerm.toLowerCase());
      const matchRole = roleFilter === "all" || u.role === roleFilter;
      return isNotAdmin && matchSearch && matchRole;
    });
  }, [users, searchTerm, roleFilter]);

  const totalPages = Math.ceil(filteredUsers.length / pageSize) || 1;

  const paginatedUsers = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredUsers.slice(start, start + pageSize);
  }, [filteredUsers, currentPage, pageSize]);

  const roleBadgeStyle = (role) => {
    switch (role) {
      case "TaskManager":
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
      
      {/* Filter Bar + Nút Thêm Người Dùng */}
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
                placeholder="Tìm kiếm người dùng..."
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
                value={roleFilter}
                onChange={(e) => {
                  setRoleFilter(e.target.value);
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
                <option value="all" className="bg-white dark:bg-[#25272E]">Tất cả vai trò</option>
                <option value="TaskManager" className="bg-white dark:bg-[#25272E]">Task Manager</option>
                <option value="Reviewer" className="bg-white dark:bg-[#25272E]">Reviewer</option>
                <option value="Speaker" className="bg-white dark:bg-[#25272E]">Speaker</option>
              </select>
            </div>

            <button
              onClick={() => setIsAddModalOpen(true)}
              style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
              className="px-3.5 py-2 text-white rounded-xl type-button flex items-center justify-center gap-1.5 hover:opacity-90 active:scale-[0.99] transition-all cursor-pointer whitespace-nowrap"
            >
              <UserPlus className="w-4 h-4" />
              <span>Thêm người dùng mới</span>
            </button>
          </div>
        </div>
      </div>

      {/* Khung chứa bảng */}
      <div className="bg-white dark:bg-[#1C1D22] rounded-2xl border border-[#E5E2D8] dark:border-gray-800 shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden transition-colors flex-1 flex flex-col justify-between my-1.5 min-h-0">
        <div className="flex-1 flex flex-col min-h-0 overflow-auto">
          <div className="min-w-[750px] h-full flex flex-col">
            {/* Table Header */}
            <div className="bg-[#F7F5EF] dark:bg-[#25272E] type-label text-[#9A9CA3] dark:text-gray-400 border-b border-[#E5E2D8] dark:border-gray-800 flex items-center shrink-0 h-8">
              <div className="px-3 text-center whitespace-nowrap w-[65px]">STT</div>
              <div className="px-4 text-left flex-1">Họ và tên</div>
              <div className="px-3 text-left flex-1 whitespace-nowrap">Email</div>
              <div className="px-3 text-left w-[140px] whitespace-nowrap">Vai trò</div>
              <div className="px-3 text-left w-[130px] whitespace-nowrap">Trạng thái</div>
              <div className="px-4 text-center w-[100px] whitespace-nowrap">Thao tác</div>
            </div>

            {/* Table Body */}
            <div className="divide-y divide-[#F0EEE6] dark:divide-gray-800 type-ui flex-1 grid grid-rows-10">
              {paginatedUsers.length > 0 ? (
                paginatedUsers.map((u, idx) => {
                  const badgeStyle = roleBadgeStyle(u.role);
                  return (
                    <div key={u.id} className="hover:bg-[#F7F5EF]/70 dark:hover:bg-[#25272E]/50 transition-colors flex items-center h-full">
                      <div className="px-3 text-center text-[#9A9CA3] dark:text-gray-500 font-sans whitespace-nowrap w-[65px]">
                        {(currentPage - 1) * pageSize + idx + 1}
                      </div>
                      <div className="px-4 font-bold text-[#16171C] dark:text-gray-100 flex-1 truncate">{u.name}</div>
                      <div className="px-3 text-[#6E7078] dark:text-gray-400 font-sans flex-1 truncate">{u.email}</div>
                      <div className="px-3 w-[140px] whitespace-nowrap">
                        <span 
                          className="inline-block px-2.5 py-0.5 rounded-full type-caption font-bold shadow-xs"
                          style={badgeStyle}
                        >
                          {u.role}
                        </span>
                      </div>
                      <div className="px-3 w-[130px] whitespace-nowrap">
                        {u.status === "Active" ? (
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border"
                            style={{ 
                              backgroundColor: CHIP_SUCCESS_BG, 
                              borderColor: CHIP_SUCCESS_BORDER, 
                              color: CHIP_SUCCESS_TEXT 
                            }}
                          >
                            <CheckCircle2 className="w-3 h-3 shrink-0" style={{ color: CHIP_SUCCESS_TEXT }} /> Hoạt động
                          </span>
                        ) : (
                          <span 
                            className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full type-caption font-label border"
                            style={{ 
                              backgroundColor: CHIP_DANGER_BG, 
                              borderColor: CHIP_DANGER_BORDER, 
                              color: CHIP_DANGER_TEXT 
                            }}
                          >
                            <Ban className="w-3 h-3 shrink-0" style={{ color: CHIP_DANGER_TEXT }} /> Vô hiệu hóa
                          </span>
                        )}
                      </div>
                      <div className="px-4 text-center w-[100px] whitespace-nowrap">
                        <div className="flex items-center justify-center gap-1">
                          <button
                            onClick={() => setUserToToggleStatus(u)}
                            title={u.status === "Active" ? "Vô hiệu hóa tài khoản" : "Kích hoạt tài khoản"}
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#16171C] dark:hover:text-white hover:bg-[#F7F5EF] dark:hover:bg-gray-700 transition-colors cursor-pointer"
                          >
                            {u.status === "Active" ? (
                              <Unlock className="w-4 h-4" />
                            ) : (
                              <Lock className="w-4 h-4" />
                            )}
                          </button>

                          <button
                            onClick={() => setUserToDelete(u)}
                            title="Xóa người dùng"
                            className="w-7 h-7 rounded-lg flex items-center justify-center text-[#6E7078] dark:text-gray-400 hover:text-[#C63B3B] dark:hover:text-[#E55353] hover:bg-[#FDEAEA] dark:hover:bg-[#C63B3B]/20 transition-colors cursor-pointer"
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
                  Không tìm thấy người dùng nào phù hợp.
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Pagination */}
        <div className="px-4 py-1 border-t border-[#E5E2D8] dark:border-gray-800 shrink-0 bg-white dark:bg-[#1C1D22]">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(p) => setCurrentPage(p)} accent={ADMIN_ACCENT} variant="soft" />
        </div>
      </div>

      {/* Modal Thêm Người Dùng */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setIsAddModalOpen(false)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-md shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: ADMIN_ACCENT }} />
            <form onSubmit={handleAddUser} className="p-6 space-y-4">
              <div className="flex items-center justify-between border-b border-[#E5E2D8] dark:border-gray-800 pb-3">
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Tạo người dùng mới</h3>
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="w-8 h-8 rounded-full flex items-center justify-center hover:bg-[#F0EEE6] dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4 text-[#6E7078]" />
                </button>
              </div>

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Họ và tên</label>
                <input
                  type="text"
                  required
                  value={newUser.name}
                  onChange={(e) => setNewUser({ ...newUser, name: e.target.value })}
                  placeholder="Nhập họ tên đầy đủ"
                  className="w-full h-11 px-3.5 py-2.5 bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 text-[#16171C] dark:text-white rounded-xl type-body outline-none transition-all placeholder:text-[#9A9CA3]"
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

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Địa chỉ Email</label>
                <input
                  type="email"
                  required
                  value={newUser.email}
                  onChange={(e) => setNewUser({ ...newUser, email: e.target.value })}
                  placeholder="example@fpt.edu.vn"
                  className="w-full h-11 px-3.5 py-2.5 bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 text-[#16171C] dark:text-white rounded-xl type-body outline-none transition-all placeholder:text-[#9A9CA3]"
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

              <div>
                <label className="block type-label text-[#2B2C31] dark:text-gray-200 mb-1.5">Phân quyền (Role)</label>
                <select
                  value={newUser.role}
                  onChange={(e) => setNewUser({ ...newUser, role: e.target.value })}
                  className="w-full h-11 px-3.5 py-2.5 bg-white dark:bg-[#25272E] border border-[#E5E2D8] dark:border-gray-700 rounded-xl type-body text-[#16171C] dark:text-white outline-none transition-all cursor-pointer font-sans"
                  onFocus={(e) => {
                    e.target.style.borderColor = ADMIN_ACCENT;
                    e.target.style.boxShadow = `0 0 0 4px ${ADMIN_ACCENT}1A`;
                  }}
                  onBlur={(e) => {
                    e.target.style.borderColor = '';
                    e.target.style.boxShadow = 'none';
                  }}
                >
                  <option value="Speaker" className="bg-white dark:bg-[#25272E]">Speaker</option>
                  <option value="Reviewer" className="bg-white dark:bg-[#25272E]">Reviewer</option>
                  <option value="TaskManager" className="bg-white dark:bg-[#25272E]">Task Manager</option>
                </select>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  style={{ backgroundColor: ADMIN_ACCENT, boxShadow: `0 10px 24px ${ADMIN_ACCENT}40` }}
                  className="flex-1 py-2.5 text-white rounded-xl type-button hover:opacity-90 active:scale-[0.99] cursor-pointer transition-all"
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setUserToToggleStatus(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: userToToggleStatus.status === "Active" ? DANGER : SUCCESS }} />
            <div className="p-6 text-center space-y-3.5">
              <div 
                className="w-11 h-11 rounded-full flex items-center justify-center mx-auto" 
                style={{ 
                  backgroundColor: `${userToToggleStatus.status === "Active" ? DANGER : SUCCESS}1A`, 
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
                <h3 className="type-section text-[#16171C] dark:text-gray-100">
                  {userToToggleStatus.status === "Active" ? "Xác nhận vô hiệu hóa tài khoản" : "Xác nhận kích hoạt tài khoản"}
                </h3>
                <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-1">
                  Bạn có chắc chắn muốn {userToToggleStatus.status === "Active" ? "vô hiệu hóa" : "kích hoạt"} tài khoản <span className="font-bold text-[#16171C] dark:text-gray-200">"{userToToggleStatus.name}"</span>?
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  onClick={() => setUserToToggleStatus(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={ConfirmToggleStatusUser}
                  className="flex-1 py-2.5 text-white rounded-xl type-button transition-colors cursor-pointer hover:opacity-90 active:scale-[0.99]"
                  style={{ 
                    backgroundColor: userToToggleStatus.status === "Active" ? DANGER : SUCCESS,
                    boxShadow: `0 10px 24px ${userToToggleStatus.status === "Active" ? DANGER : SUCCESS}40`
                  }}
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-[#16171C]/55 backdrop-blur-xs" onClick={() => setUserToDelete(null)}>
          <div className="bg-white dark:bg-[#1C1D22] rounded-[24px] w-full max-w-sm shadow-[0_20px_50px_rgba(16,17,20,0.25)] overflow-hidden border border-[#E5E2D8] dark:border-gray-800 animate-in fade-in zoom-in-95 duration-200" onClick={(e) => e.stopPropagation()}>
            <div className="h-1.5 w-full shrink-0" style={{ backgroundColor: DANGER }} />
            <div className="p-6 text-center space-y-3.5">
              <div className="w-11 h-11 rounded-full flex items-center justify-center mx-auto" style={{ backgroundColor: `${DANGER}1A`, color: DANGER }}>
                <AlertTriangle className="w-5 h-5" />
              </div>
              <div>
                <h3 className="type-section text-[#16171C] dark:text-gray-100">Xác nhận xóa người dùng</h3>
                <p className="type-ui text-[#6E7078] dark:text-gray-400 mt-1">
                  Bạn có chắc chắn muốn xóa tài khoản <span className="font-bold text-[#16171C] dark:text-gray-200">"{userToDelete.name}"</span>? Hành động này không thể hoàn tác.
                </p>
              </div>
              <div className="flex gap-2.5 pt-2">
                <button
                  onClick={() => setUserToDelete(null)}
                  className="flex-1 py-2.5 rounded-xl border border-[#E5E2D8] dark:border-gray-700 type-button text-[#6E7078] dark:text-gray-300 hover:bg-[#F7F5EF] dark:hover:bg-[#25272E] cursor-pointer transition-colors"
                >
                  Hủy
                </button>
                <button
                  onClick={ConfirmDeleteUser}
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