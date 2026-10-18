// ===== Auth =====
// Đăng nhập (POST /api/auth/login)
export const LOGIN_API = 'LOGIN_API'
// Tự đăng ký làm Speaker (POST /api/auth/register-speaker)
export const REGISTER_SPEAKER_API = 'REGISTER_SPEAKER_API'
// Tài khoản đang đăng nhập (GET /api/auth/me)
export const GET_ME_API = 'GET_ME_API'
// Sửa họ tên, số điện thoại của chính mình (PATCH /api/me)
export const UPDATE_ME_API = 'UPDATE_ME_API'
// Đổi mật khẩu của chính mình (PUT /api/me/password)
export const CHANGE_PASSWORD_API = 'CHANGE_PASSWORD_API'

// ===== Speaker =====
// Hồ sơ người đọc (GET /api/me/speaker-profile)
export const GET_SPEAKER_PROFILE_API = 'GET_SPEAKER_PROFILE_API'
// Lưu hồ sơ người đọc (PUT /api/me/speaker-profile)
export const UPDATE_SPEAKER_PROFILE_API = 'UPDATE_SPEAKER_PROFILE_API'
// Tiến độ + nhiệm vụ ghi âm đang giao (GET /api/speaker/progress)
export const GET_SPEAKER_PROGRESS_API = 'GET_SPEAKER_PROGRESS_API'
// Cặp câu tiếp theo để thu âm (GET /api/speaker/scripts/next)
export const GET_NEXT_SCRIPT_API = 'GET_NEXT_SCRIPT_API'
// Đóng góp một cặp câu mới (POST /api/speaker/scripts/contribute)
export const CONTRIBUTE_SCRIPT_API = 'CONTRIBUTE_SCRIPT_API'
// Lịch sử đóng góp câu của Speaker (GET /api/speaker/contributions/history)
export const GET_SPEAKER_CONTRIBUTION_HISTORY_API = 'GET_SPEAKER_CONTRIBUTION_HISTORY_API'

// ===== Script (cặp câu) =====
// Chi tiết một cặp câu (GET /api/scripts/{id})
export const GET_SCRIPT_DETAIL_API = 'GET_SCRIPT_DETAIL_API'
// Lý do báo lỗi nội dung câu (GET /api/script-error-reasons)
export const GET_SCRIPT_ERROR_REASONS_API = 'GET_SCRIPT_ERROR_REASONS_API'
// Sửa / báo lỗi một cặp câu (POST /api/scripts/{id}/review)
export const REVIEW_SCRIPT_API = 'REVIEW_SCRIPT_API'
// Speaker gửi đề xuất báo lỗi / sửa câu (POST /api/speaker/scripts/{scriptId}/proposals)
export const CREATE_SCRIPT_PROPOSAL_API = 'CREATE_SCRIPT_PROPOSAL_API'

// ===== Recording (bản ghi âm) =====
// Nộp bản ghi âm (POST /api/recordings)
export const UPLOAD_RECORDING_API = 'UPLOAD_RECORDING_API'
// Link nghe tạm của một bản ghi (GET /api/recordings/{id}/audio-url)
export const GET_RECORDING_AUDIO_URL_API = 'GET_RECORDING_AUDIO_URL_API'
// Lịch sử ghi âm của Speaker (GET /api/speaker/recordings/history)
export const GET_SPEAKER_RECORDING_HISTORY_API = 'GET_SPEAKER_RECORDING_HISTORY_API'
// Thống kê bản ghi của Speaker (GET /api/speaker/stats)
export const GET_SPEAKER_STATS_API = 'GET_SPEAKER_STATS_API'
// Đợt đang tham gia của Speaker (GET /api/speaker/rounds/current)
export const GET_SPEAKER_CURRENT_ROUND_API = 'GET_SPEAKER_CURRENT_ROUND_API'
// Bảng xếp hạng của một đợt (GET /api/speaker/rounds/{campaignId}/leaderboard)
export const GET_ROUND_LEADERBOARD_API = 'GET_ROUND_LEADERBOARD_API'
// Bản ghi bị từ chối cần ghi lại (GET /api/speaker/recordings/rejected)
export const GET_SPEAKER_REJECTED_RECORDINGS_API = 'GET_SPEAKER_REJECTED_RECORDINGS_API'

// ===== Config (tham số hệ thống) =====
// Danh sách chủ đề cố định (GET /api/config/topics)
export const GET_TOPICS_API = 'GET_TOPICS_API'

// ===== Reviewer (duyệt bản ghi) =====
// Tiến độ + nhiệm vụ duyệt đang giao (GET /api/reviewer/progress)
export const GET_REVIEWER_PROGRESS_API = 'GET_REVIEWER_PROGRESS_API'
// Danh sách bản ghi của một task duyệt (GET /api/reviewer/tasks/{taskId}/recordings)
export const GET_REVIEW_TASK_RECORDINGS_API = 'GET_REVIEW_TASK_RECORDINGS_API'
// Mở một bản ghi để duyệt (GET /api/reviewer/recordings/{id})
export const GET_REVIEW_RECORDING_API = 'GET_REVIEW_RECORDING_API'
// Lý do từ chối bản ghi âm (GET /api/rejection-reasons)
export const GET_REJECTION_REASONS_API = 'GET_REJECTION_REASONS_API'
// Nộp kết quả duyệt (POST /api/recordings/{id}/reviews)
export const SUBMIT_REVIEW_API = 'SUBMIT_REVIEW_API'

// ===== Reviewer: thống kê trang Tổng quan =====
// Tổng số bản đã duyệt đạt / từ chối (GET /api/reviewer/stats/totals)
export const GET_REVIEWER_TOTALS_API = 'GET_REVIEWER_TOTALS_API'
// Lý do từ chối dùng nhiều nhất (GET /api/reviewer/stats/reject-reasons)
export const GET_REVIEWER_REJECT_REASONS_API = 'GET_REVIEWER_REJECT_REASONS_API'
// Câu bị từ chối nhiều nhất (GET /api/reviewer/stats/top-rejected-sentences)
export const GET_TOP_REJECTED_SENTENCES_API = 'GET_TOP_REJECTED_SENTENCES_API'
// Người bị từ chối nhiều nhất (GET /api/reviewer/stats/top-rejected-speakers)
export const GET_TOP_REJECTED_SPEAKERS_API = 'GET_TOP_REJECTED_SPEAKERS_API'
// Lịch sử duyệt đề xuất câu của Reviewer (GET /api/reviewer/history/contributions)
export const GET_REVIEWER_CONTRIBUTION_HISTORY_API = 'GET_REVIEWER_CONTRIBUTION_HISTORY_API'
// Lịch sử duyệt bản ghi của Reviewer (GET /api/reviewer/history/recordings)
export const GET_REVIEWER_RECORDING_HISTORY_API = 'GET_REVIEWER_RECORDING_HISTORY_API'
// Hàng chờ đề xuất báo lỗi / sửa câu (GET /api/reviewer/proposals)
export const GET_REVIEWER_PROPOSALS_API = 'GET_REVIEWER_PROPOSALS_API'
// Chốt một đề xuất (POST /api/reviewer/proposals/{id}/resolve)
export const RESOLVE_PROPOSAL_API = 'RESOLVE_PROPOSAL_API'
// Câu đóng góp chờ duyệt của Reviewer (GET /api/reviewer/contributions)
export const GET_REVIEWER_CONTRIBUTIONS_API = 'GET_REVIEWER_CONTRIBUTIONS_API'
