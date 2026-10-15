import { useState } from 'react';
import { Search, ArrowRight, Headphones } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import Pagination from '../../../components/Pagination/Pagination';
import Loading from '../../../components/Loading/Loading';
import ErrorState from '../../../components/ErrorState/ErrorState';
import { SPEAKER_ACCENT_ACTIVE_ICON as ACCENT } from '../../../constants/theme'; // xanh lá thương hiệu
import { getReviewerProgressApi } from '../../../services/reviewerApi';
import { GET_REVIEWER_PROGRESS_API } from '../../../utils/queryKey';

// "2026-11-07T23:59:59+00:00" -> "07/11/2026" (giờ máy người dùng)
function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  const pad = (n) => String(n).padStart(2, '0');
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
}

// hoursRemaining của API -> "Còn 26 ngày" / "Còn 5 giờ"
function remainingText(hours) {
  if (hours === null || hours === undefined) return '';
  return hours >= 24 ? `Còn ${Math.floor(hours / 24)} ngày` : `Còn ${Math.max(0, Math.floor(hours))} giờ`;
}

export default function ReviewTasks() {
  const navigate = useNavigate();

  const [currentPage, setCurrentPage] = useState(1);
  const [searchTerm, setSearchTerm] = useState('');

  // Call API lấy nhiệm vụ duyệt đang giao cho mình (chỉ trả task còn đang chạy)
  const { data: progress, isLoading, isError, error, refetch } = useQuery({
    queryKey: [GET_REVIEWER_PROGRESS_API],
    queryFn: getReviewerProgressApi,
  });

  const query = searchTerm.trim().toLocaleLowerCase('vi');
  const filteredTasks = (progress?.activeTasks ?? []).filter((task) =>
    (task.description ?? '').toLocaleLowerCase('vi').includes(query),
  );

  // Cố định 10 nhiệm vụ mỗi trang; trang đầy thì các dòng giãn đều hết khung (màn thấp thì bảng cuộn bên trong)
  const pageSize = 10;
  const totalPages = Math.ceil(filteredTasks.length / pageSize) || 1;
  const paginatedTasks = filteredTasks.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  if (isLoading) return <Loading />;
  if (isError) return <ErrorState message={error.message} onRetry={refetch} />;

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
            className="w-full pl-9 pr-4 py-2 text-meta border border-[#E5E2D8] rounded-xl outline-none focus:border-[#16A34A] focus:ring-4 focus:ring-[#16A34A]/10 transition-all"
          />
        </div>
      </div>

      {/* Bảng */}
      <div className="flex-1 min-h-0 flex flex-col bg-white rounded-2xl border border-[#E5E2D8] shadow-[0_1px_3px_rgba(16,17,20,0.04)] overflow-hidden">
        <div className="flex-1 min-h-0 overflow-auto">
          <table className={`w-full border-collapse ${paginatedTasks.length === pageSize ? 'h-full' : ''}`}>
            <thead>
              <tr className="bg-[#F7F5EF] type-label text-[#9A9CA3] border-b border-[#E5E2D8] whitespace-nowrap">
                <th className="py-2 px-3 text-center w-12">STT</th>
                <th className="py-2 px-3 text-left w-[38%]">Nhiệm vụ</th>
                <th className="py-2 px-3 text-left min-w-[140px]">Mục tiêu (Target)</th>
                <th className="py-2 px-3 text-left">Hạn chót</th>
                <th className="py-2 px-3 text-left">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#F0EEE6] type-ui">
              {paginatedTasks.length > 0 ? paginatedTasks.map((task, idx) => {
                return (
                  <tr key={task.taskId} className="hover:bg-[#F7F5EF]/70 transition-colors">
                    <td className="py-1.5 px-3 text-center whitespace-nowrap">
                      <span className="text-[#9A9CA3] type-meta">{(currentPage - 1) * pageSize + idx + 1}</span>
                    </td>
                    <td className="py-1.5 px-3 text-left">
                      <p className="text-ui font-emphasis text-[#16171C] break-words">{task.description || `Nhiệm vụ #${task.taskId}`}</p>
                      {/* API này chỉ trả task duyệt ghi âm */}
                      <span className="mt-0.5 flex items-center gap-1 type-meta text-[#6E7078]">
                        <Headphones className="w-3 h-3" /> Duyệt ghi âm
                      </span>
                    </td>
                    <td className="py-1.5 px-3 text-left">
                      <div className="flex flex-col gap-1 max-w-[180px]">
                        <div className="flex justify-between gap-2 whitespace-nowrap type-meta text-[#6E7078]">
                          <span>{task.done}/{task.targetQty} bản</span>
                          <span className="font-emphasis text-[#16171C] tabular-nums">{task.percent}%</span>
                        </div>
                        <div className="w-full bg-[#F0EEE6] h-1.5 rounded-full overflow-hidden">
                          <div style={{ width: `${Math.min(100, task.percent)}%`, background: ACCENT }} className="h-full rounded-full transition-all duration-300" />
                        </div>
                      </div>
                    </td>
                    <td className="py-1.5 px-3 text-left whitespace-nowrap">
                      <p className="type-meta text-[#16171C]">{formatDate(task.deadline)}</p>
                      <p className={`type-caption ${task.isOverdue ? 'text-[#C63B3B]' : 'text-[#9A9CA3]'}`}>
                        {task.isOverdue ? 'Quá hạn' : remainingText(task.hoursRemaining)}
                      </p>
                    </td>
                    <td className="py-1.5 px-3 text-left whitespace-nowrap">
                      <button
                        onClick={() => navigate(`/reviewer/recording?taskId=${task.taskId}`)}
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
                  <td colSpan={5} className="py-8 text-center text-[#9A9CA3] text-meta">
                    {progress?.activeTasks?.length ? 'Không tìm thấy nhiệm vụ nào phù hợp với từ khoá.' : 'Bạn chưa được giao nhiệm vụ duyệt nào.'}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <div className="shrink-0 px-3 border-t border-[#E5E2D8]">
          <Pagination currentPage={currentPage} totalPages={totalPages} onPageChange={(page) => setCurrentPage(page)} accent={ACCENT} variant="soft" />
        </div>
      </div>
    </div>
  );
}