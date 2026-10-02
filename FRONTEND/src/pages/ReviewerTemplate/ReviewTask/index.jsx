import { useState, useMemo } from 'react';
import { Search, Filter, ArrowRight, UserCheck, Headphones, FileText } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import Pagination from '../../../components/Pagination/Pagination';
import { REVIEWER_ACCENT as ACCENT } from '../../../constants/theme';
import { ASSIGNED_TASKS } from '../../../mocks/reviewer/tasks';

// Thứ tự ưu tiên hiển thị theo nhóm trạng thái
const STATUS_ORDER = { 'in-progress': 0, 'pending': 1, 'completed': 2 };

// Loại task quyết định trang kiểm duyệt được mở và đơn vị tiến độ
const TASK_TYPES = {
  recording: { label: 'Duyệt ghi âm', icon: Headphones, unit: 'bản', path: (task) => `/reviewer/recording?task=${encodeURIComponent(task.title)}` },
  script: { label: 'Duyệt câu', icon: FileText, unit: 'câu', path: () => '/reviewer/contribution' },
};

export default function ReviewTasks() {
  const navigate = useNavigate();

  const [currentPage, setCurrentPage] = useState(1);
  const [searchTerm, setSearchTerm] = useState('');
  const [filterAssignee, setFilterAssignee] = useState('all');
  const [filterStatus, setFilterStatus] = useState('all');

  const assignedTasks = ASSIGNED_TASKS;

  const filteredTasks = useMemo(() => {
    const list = assignedTasks.filter((task) => {
      const matchSearch = task.title.toLowerCase().includes(searchTerm.toLowerCase());
      const matchAssignee = filterAssignee === 'all' || task.assignedBy.includes(filterAssignee);
      const matchStatus = filterStatus === 'all' || task.statusType === filterStatus;
      return matchSearch && matchAssignee && matchStatus;
    });
    // Sắp xếp: Đang thực hiện -> Chưa bắt đầu -> Hoàn thành (giữ thứ tự gốc trong từng nhóm)
    return [...list].sort((a, b) => STATUS_ORDER[a.statusType] - STATUS_ORDER[b.statusType]);
  }, [assignedTasks, searchTerm, filterAssignee, filterStatus]);

  // Cố định 10 nhiệm vụ mỗi trang; trang đầy thì các dòng giãn đều hết khung (màn thấp thì bảng cuộn bên trong)
  const pageSize = 10;
  const totalPages = Math.ceil(filteredTasks.length / pageSize) || 1;
  const paginatedTasks = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredTasks.slice(start, start + pageSize);
  }, [filteredTasks, currentPage, pageSize]);

  // Style trạng thái theo hệ màu app
  const statusStyle = (type) => {
    if (type === 'completed') return { chip: 'bg-[#3FA66B]/10 text-[#1F5C3F]', dot: '#3FA66B' };
    if (type === 'in-progress') return { chip: 'text-[#4C4D9E]', dot: ACCENT, chipBg: 'rgba(129,140,248,0.14)' };
    return { chip: 'bg-[#F0EEE6] text-[#6E7078]', dot: '#B7B4A9' };
  };

  return (
    <div className="h-full min-h-0 flex flex-col gap-4 text-left font-sans">

      {/* Thanh lọc & tìm kiếm */}
      <div className="shrink-0 bg-white p-3 rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] flex flex-col md:flex-row gap-3 justify-between items-center">
        <div className="relative w-full md:w-80">
          <Search className="w-4 h-4 text-[#9A9CA3] absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => { setSearchTerm(e.target.value); setCurrentPage(1); }}
            placeholder="Tìm theo tên nhiệm vụ..."
            className="w-full pl-9 pr-4 py-2 text-meta border border-[#E5E2D8] rounded-xl outline-none focus:border-[#818CF8] focus:ring-4 focus:ring-[#818CF8]/10 transition-all"
          />
        </div>
        <div className="flex flex-wrap items-center gap-2.5 w-full md:w-auto justify-end">
          <div className="flex items-center gap-2">
            <Filter className="w-3.5 h-3.5 text-[#9A9CA3]" />
            <select
              value={filterAssignee}
              onChange={(e) => { setFilterAssignee(e.target.value); setCurrentPage(1); }}
              className="text-meta border border-[#E5E2D8] rounded-xl px-3 py-2 bg-white text-[#16171C] font-label outline-none focus:border-[#818CF8]"
            >
              <option value="all">Tất cả người giao</option>
              <option value="Nguyễn Hoàng">Task Manager - Nguyễn Hoàng</option>
              <option value="Trần Anh">Task Manager - Trần Anh</option>
            </select>
          </div>
          <select
            value={filterStatus}
            onChange={(e) => { setFilterStatus(e.target.value); setCurrentPage(1); }}
            className="text-meta border border-[#E5E2D8] rounded-xl px-3 py-2 bg-white text-[#16171C] font-label outline-none focus:border-[#818CF8]"
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="in-progress">Đang thực hiện</option>
            <option value="pending">Chưa bắt đầu</option>
            <option value="completed">Hoàn thành</option>
          </select>
        </div>
      </div>

      {/* Bảng */}
      <div className="flex-1 min-h-0 flex flex-col bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className={`w-full border-collapse ${paginatedTasks.length === pageSize ? 'h-full' : ''}`}>
            <thead>
              <tr className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8] whitespace-nowrap">
                <th className="py-2.5 px-3 text-center w-12">STT</th>
                <th className="py-2.5 px-3 text-left w-[26%]">Nhiệm vụ</th>
                <th className="py-2.5 px-3 text-left">Người giao</th>
                <th className="py-2.5 px-3 text-left min-w-[140px]">Mục tiêu (Target)</th>
                <th className="py-2.5 px-3 text-left">Hạn chót</th>
                <th className="py-2.5 px-3 text-left">Trạng thái</th>
                <th className="py-2.5 px-3 text-left">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] type-ui">
              {paginatedTasks.length > 0 ? paginatedTasks.map((task, idx) => {
                const percent = Math.round((task.reviewed / task.target) * 100);
                const st = statusStyle(task.statusType);
                const type = TASK_TYPES[task.type];
                const TypeIcon = type.icon;
                return (
                  <tr key={task.id} className="hover:bg-[#F7F5EF]/70 transition-colors">
                    <td className="py-2 px-3 text-center whitespace-nowrap">
                      <span className="text-[#9A9CA3] type-meta">{(currentPage - 1) * pageSize + idx + 1}</span>
                    </td>
                    <td className="py-2 px-3 text-left">
                      <p className="text-ui font-emphasis text-[#16171C] break-words">{task.title}</p>
                      <span className="mt-0.5 flex items-center gap-1 type-meta text-[#6E7078]">
                        <TypeIcon className="w-3 h-3" /> {type.label}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-left whitespace-nowrap">
                      <span className="flex items-center gap-1.5 min-w-0 text-[#6E7078]">
                        <UserCheck className="w-3.5 h-3.5 text-[#9A9CA3] shrink-0" /> <span className="truncate" title={task.assignedBy}>{task.assignedBy.replace(/^Task Manager - /, '')}</span>
                      </span>
                    </td>
                    <td className="py-2 px-3 text-left">
                      <div className="flex flex-col gap-1 max-w-[180px]">
                        <div className="flex justify-between gap-2 whitespace-nowrap type-meta text-[#6E7078]">
                          <span>{task.reviewed}/{task.target} {type.unit}</span>
                          <span className="font-emphasis text-[#16171C] tabular-nums">{percent}%</span>
                        </div>
                        <div className="w-full bg-[#F0EEE6] h-1.5 rounded-full overflow-hidden">
                          <div style={{ width: `${percent}%`, background: task.statusType === 'completed' ? '#3FA66B' : ACCENT }} className="h-full rounded-full transition-all duration-300" />
                        </div>
                      </div>
                    </td>
                    <td className="py-2 px-3 text-left whitespace-nowrap">
                      <span className="type-meta text-[#6E7078]">{task.deadline}</span>
                    </td>
                    <td className="py-2 px-3 text-left whitespace-nowrap">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 h-6 rounded-full type-caption ${st.chip}`} style={st.chipBg ? { background: st.chipBg } : {}}>
                        <span className="w-1.5 h-1.5 rounded-full" style={{ background: st.dot }} />
                        {task.status}
                      </span>
                    </td>
                    <td className="py-2 px-3 text-left whitespace-nowrap">
                      <button
                        onClick={() => navigate(type.path(task))}
                        className="h-7 px-3 rounded-lg border bg-white type-label flex items-center gap-1 text-(--accent) border-(--accent) hover:bg-(--accent) hover:text-white transition-colors cursor-pointer"
                        style={{ '--accent': ACCENT }}
                      >
                        Vào kiểm duyệt <ArrowRight className="w-3.5 h-3.5" />
                      </button>
                    </td>
                  </tr>
                );
              }) : (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-[#9A9CA3] text-meta">Không tìm thấy nhiệm vụ nào phù hợp với bộ lọc.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="shrink-0 px-3 border-t border-[#E5E2D8]">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(page) => setCurrentPage(page)} accent={ACCENT} />
        </div>
      </div>
    </div>
  );
}