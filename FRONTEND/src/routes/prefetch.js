import { queryClient } from '../lib/queryClient';
import {
  getSpeakerProgressApi,
  getAllSpeakerRecordingHistoryApi,
  getSpeakerStatsApi,
  getAllSpeakerContributionHistoryApi,
} from '../services/speakerApi';
import { getTopicsApi } from '../services/configApi';
import {
  getReviewerProgressApi,
  getReviewerTotalsApi,
  getReviewerRejectReasonsApi,
  getTopRejectedSentencesApi,
  getTopRejectedSpeakersApi,
  getReviewerRecordingHistoryApi,
  getReviewerContributionHistoryApi,
} from '../services/reviewerApi';
import {
  GET_SPEAKER_PROGRESS_API,
  GET_SPEAKER_RECORDING_HISTORY_API,
  GET_SPEAKER_STATS_API,
  GET_SPEAKER_CONTRIBUTION_HISTORY_API,
  GET_TOPICS_API,
  GET_REVIEWER_PROGRESS_API,
  GET_REVIEWER_TOTALS_API,
  GET_REVIEWER_REJECT_REASONS_API,
  GET_TOP_REJECTED_SENTENCES_API,
  GET_TOP_REJECTED_SPEAKERS_API,
  GET_REVIEWER_RECORDING_HISTORY_API,
  GET_REVIEWER_CONTRIBUTION_HISTORY_API,
} from '../utils/queryKey';

/**
 * Tải trước khi người dùng di chuột / focus vào mục menu (sidebar Speaker, Reviewer) - bấm vào là trang hiện gần như ngay:
 * - code của trang: gọi lại đúng import() như trong routes/index.jsx, trình duyệt tải sẵn file đó;
 * - dữ liệu đầu tiên trang cần: queryClient.query() cùng queryKey + queryFn với trang, nên trang lấy luôn từ cache.
 * query() tự bỏ qua nếu dữ liệu trong cache còn mới, import() chỉ tải file 1 lần -> rê chuột nhiều lần không gọi thừa.
 * (TanStack Query 5.10x đánh dấu prefetchQuery / fetchQuery là lỗi thời, thay bằng query().)
 */
// Tải trước chỉ là "làm sẵn" - lỗi thì bỏ qua, trang sẽ tự gọi lại và tự hiện lỗi khi người dùng bấm vào
const prefetchData = (options) => queryClient.query(options).catch(() => {});

// Nhiệm vụ duyệt đang giao - dùng chung ở nhiều trang Reviewer
const prefetchReviewerProgress = () =>
  prefetchData({ queryKey: [GET_REVIEWER_PROGRESS_API], queryFn: getReviewerProgressApi });

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
    prefetchData({ queryKey: [GET_SPEAKER_STATS_API], queryFn: getSpeakerStatsApi });
  },
};

// Reviewer - queryKey phải trùng đúng với trang (kể cả bộ lọc mặc định "" và trang 1) thì trang mới lấy được từ cache
Object.assign(ROUTE_PREFETCH, {
  '/reviewer': () => {
    import('../pages/ReviewerTemplate/ReviewDashboard/index');
    prefetchReviewerProgress();
    prefetchData({ queryKey: [GET_REVIEWER_TOTALS_API], queryFn: getReviewerTotalsApi });
    prefetchData({ queryKey: [GET_REVIEWER_REJECT_REASONS_API], queryFn: getReviewerRejectReasonsApi });
    prefetchData({ queryKey: [GET_TOP_REJECTED_SENTENCES_API], queryFn: () => getTopRejectedSentencesApi({ limit: 20 }) });
    prefetchData({ queryKey: [GET_TOP_REJECTED_SPEAKERS_API], queryFn: () => getTopRejectedSpeakersApi({ limit: 20 }) });
  },
  '/reviewer/task': () => {
    import('../pages/ReviewerTemplate/ReviewTask/index');
    prefetchReviewerProgress();
  },
  '/reviewer/recording': () => {
    import('../pages/ReviewerTemplate/Review/ReviewRecording/index');
    // Danh sách bản ghi phụ thuộc nhiệm vụ đang chọn -> chỉ tải sẵn danh sách nhiệm vụ
    prefetchReviewerProgress();
  },
  '/reviewer/contribution': () => import('../pages/ReviewerTemplate/Review/ReviewContribution/index'),
  '/reviewer/history-recording': () => {
    import('../pages/ReviewerTemplate/ReviewHistory/ReviewHistoryRecording/index');
    prefetchReviewerProgress();
    prefetchData({
      queryKey: [GET_REVIEWER_RECORDING_HISTORY_API, '', '', '', 1],
      queryFn: () => getReviewerRecordingHistoryApi({ page: 1, pageSize: 10 }),
    });
    ['', 'Approved', 'Rejected', 'PendingReview'].forEach((status) =>
      prefetchData({
        queryKey: [GET_REVIEWER_RECORDING_HISTORY_API, 'count', status],
        queryFn: () => getReviewerRecordingHistoryApi({ status, page: 1, pageSize: 1 }),
      }),
    );
  },
  '/reviewer/history-contribution': () => {
    import('../pages/ReviewerTemplate/ReviewHistory/ReviewHistoryContribution/index');
    prefetchData({
      queryKey: [GET_REVIEWER_CONTRIBUTION_HISTORY_API, '', '', '', 1],
      queryFn: () => getReviewerContributionHistoryApi({ page: 1, pageSize: 10 }),
    });
    ['', 'contribution', 'edit', 'report'].forEach((kind) =>
      prefetchData({
        queryKey: [GET_REVIEWER_CONTRIBUTION_HISTORY_API, 'count', kind],
        queryFn: () => getReviewerContributionHistoryApi({ kind, page: 1, pageSize: 1 }),
      }),
    );
    prefetchData({ queryKey: [GET_TOPICS_API], queryFn: getTopicsApi, staleTime: Infinity });
  },
});

/** Gọi khi rê chuột / focus vào link. Đường dẫn chưa khai báo ở trên thì bỏ qua. */
export function prefetchRoute(path) {
  ROUTE_PREFETCH[path]?.();
}
