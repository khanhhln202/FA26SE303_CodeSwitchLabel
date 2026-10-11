import { queryClient } from '../lib/queryClient';
import {
  getSpeakerProgressApi,
  getAllSpeakerRecordingHistoryApi,
  getSpeakerStatsApi,
  getAllSpeakerContributionHistoryApi,
} from '../services/speakerApi';
import { getTopicsApi } from '../services/configApi';
import {
  GET_SPEAKER_PROGRESS_API,
  GET_SPEAKER_RECORDING_HISTORY_API,
  GET_SPEAKER_STATS_API,
  GET_SPEAKER_CONTRIBUTION_HISTORY_API,
  GET_TOPICS_API,
} from '../utils/queryKey';

/**
 * Tải trước khi người dùng di chuột / focus vào mục menu (sidebar Speaker) - bấm vào là trang hiện gần như ngay:
 * - code của trang: gọi lại đúng import() như trong routes/index.jsx, trình duyệt tải sẵn file đó;
 * - dữ liệu đầu tiên trang cần: queryClient.query() cùng queryKey + queryFn với trang, nên trang lấy luôn từ cache.
 * query() tự bỏ qua nếu dữ liệu trong cache còn mới, import() chỉ tải file 1 lần -> rê chuột nhiều lần không gọi thừa.
 * (TanStack Query 5.10x đánh dấu prefetchQuery / fetchQuery là lỗi thời, thay bằng query().)
 */
// Tải trước chỉ là "làm sẵn" - lỗi thì bỏ qua, trang sẽ tự gọi lại và tự hiện lỗi khi người dùng bấm vào
const prefetchData = (options) => queryClient.query(options).catch(() => {});

const ROUTE_PREFETCH = {
  '/speaker': () => import('../pages/HomeTemplate/Home/index'),
  '/speaker/review-text': () => {
    import('../pages/HomeTemplate/ReviewText/index');
    prefetchData({ queryKey: [GET_SPEAKER_PROGRESS_API], queryFn: getSpeakerProgressApi });
  },
  '/speaker/contribute': () => {
    import('../pages/HomeTemplate/ContributeText/index');
    prefetchData({ queryKey: [GET_TOPICS_API], queryFn: getTopicsApi, staleTime: Infinity });
  },
  '/speaker/recording-history': () => {
    import('../pages/HomeTemplate/History/RecordingHistory/index');
    prefetchData({ queryKey: [GET_SPEAKER_RECORDING_HISTORY_API], queryFn: getAllSpeakerRecordingHistoryApi });
    prefetchData({ queryKey: [GET_SPEAKER_STATS_API], queryFn: getSpeakerStatsApi });
  },
  '/speaker/contribution-history': () => {
    import('../pages/HomeTemplate/History/ContributionHistory/index');
    prefetchData({ queryKey: [GET_SPEAKER_CONTRIBUTION_HISTORY_API], queryFn: getAllSpeakerContributionHistoryApi });
  },
};

/** Gọi khi rê chuột / focus vào link. Đường dẫn chưa khai báo ở trên thì bỏ qua. */
export function prefetchRoute(path) {
  ROUTE_PREFETCH[path]?.();
}
