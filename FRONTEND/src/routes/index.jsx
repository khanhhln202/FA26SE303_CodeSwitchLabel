import { lazy, Suspense } from "react";
import { Route, Navigate } from "react-router-dom";
import HomeTemplate from "../pages/HomeTemplate/index";
import ReviewerTemplate from "../pages/ReviewerTemplate/index";
import Loading from "../components/Loading/Loading";
import TaskManagerTemplate from "../pages/TaskManagerTemplate/index";
import AdminTemplate from "../pages/AdminTemplate/index";

const HelpCenterLazy = lazy(() => import("../pages/HelpCenter/HelpCenter"));

const routes = [
  // LANDING PAGE & LOGIN ROUTE
  {
    path: "/",
    element: lazy(() => import("../pages/LandingPage")),
  },
  {
    path: "/login",
    element: lazy(() => import("../pages/Auth/Login/index")),
  },
  {
    path: "/register",
    element: lazy(() => import("../pages/Auth/Register/index")),
  },
  {
    path: "/onboarding",
    element: lazy(() => import("../pages/Auth/Onboarding/index")),
  },
  {
    path: "/speaker",
    element: HomeTemplate,
    nested: [
      {
        path: "",
        element: lazy(() => import("../pages/HomeTemplate/Home/index")),
      },
      {
        path: "review-text",
        element: lazy(() => import("../pages/HomeTemplate/ReviewText/index")),
      },
      {
        path: "record-speech",
        element: lazy(() => import("../pages/HomeTemplate/RecordSpeech/index")),
      },
      {
        path: "submit-task",
        element: lazy(() => import("../pages/HomeTemplate/SubmitTask/index")),
      },
      {
        path: "contribute",
        element: lazy(
          () => import("../pages/HomeTemplate/ContributeText/index"),
        ),
      },
      {
        path: "recording-history",
        element: lazy(
          () => import("../pages/HomeTemplate/History/RecordingHistory/index"),
        ),
      },
      {
        path: "contribution-history",
        element: lazy(
          () => import("../pages/HomeTemplate/History/ContributionHistory/index"),
        ),
      },
      {
        path: "profile",
        element: lazy(() => import("../pages/HomeTemplate/Profile/index")),
      },
      {
        path: "help-center",
        element: HelpCenterLazy,
      },
    ],
  },
  {
    path: "/reviewer",
    element: ReviewerTemplate,
    nested: [
      {
        path: "",
        element: lazy(
          () => import("../pages/ReviewerTemplate/ReviewDashboard/index"),
        ),
      },
      {
        path: "task",
        element: lazy(
          () => import("../pages/ReviewerTemplate/ReviewTask/index"),
        ),
      },
      // Kiểm duyệt
      {
        path: "recording",
        element: lazy(
          () => import("../pages/ReviewerTemplate/Review/ReviewRecording/index"),
        ),
      },
      {
        path: "contribution",
        element: lazy(
          () => import("../pages/ReviewerTemplate/Review/ReviewContribution/index"),
        ),
      },
      // Lịch sử kiểm duyệt
      {
        path: "history-recording",
        element: lazy(
          () => import("../pages/ReviewerTemplate/ReviewHistory/ReviewHistoryRecording/index"),
        ),
      },
      {
        path: "history-contribution",
        element: lazy(
          () => import("../pages/ReviewerTemplate/ReviewHistory/ReviewHistoryContribution/index"),
        ),
      },
      {
        path: "profile",
        element: lazy(() => import("../pages/ReviewerTemplate/Profile/index")),
      },
      {
        path: "help-center",
        element: HelpCenterLazy,
      },
    ],
  },
  // Bắt mọi URL không tồn tại - phải đặt cuối danh sách
  {
    path: "*",
    element: lazy(() => import("../pages/NotFound/index")),
  },
    // 3. TASK MANAGER ROLE
  {
    path: "/task-manager",
    element: TaskManagerTemplate,
    role: "Task Manager",
    nested: [
      {
        path: "profile",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerProfile/index")),
      },
      {
        path: "",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerHome/index")),
      },
      {
        path: "dashboard",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerDashboard/index")),
      },
      {
        path: "speaker-tasks",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerSpeakerTasks/index")),
      },
      {
        path: "reviewer-tasks",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerReviewerTasks/index")),
      },
      {
        path: "management",
        element: lazy(() => import("../pages/TaskManagerTemplate/TaskManagerManagement/index")),
      },
      {
        path: "help-center",
        element: HelpCenterLazy,
      },
    ],
  },

  // 4. ADMIN ROLE (Đã thêm 2 route con mới cho Thiết lập tiêu chuẩn kiểm duyệt)
  {
    path: "/admin",
    element: AdminTemplate,
    role: "Administrator",
    nested: [
      {
        path: "profile",
        element: lazy(() => import("../pages/AdminTemplate/AdminProfile/index")),
      },
      {
        path: "",
        element: lazy(() => import("../pages/AdminTemplate/AdminDashboard/index")),
      },
      {
        path: "dashboard",
        element: lazy(() => import("../pages/AdminTemplate/AdminDashboard/index")),
      },
      {
        path: "users",
        element: lazy(() => import("../pages/AdminTemplate/AdminUserManagement/index")),
      },
      {
        path: "text-data",
        element: lazy(() => import("../pages/AdminTemplate/AdminTextData/index")),
      },
      {
        path: "recordings",
        element: lazy(() => import("../pages/AdminTemplate/AdminRecordings")),
      },
      // ROUTE MỚI: Quản lý Chủ đề tiêu chuẩn
      {
        path: "configuration/topic",
        element: lazy(() => import("../pages/AdminTemplate/AdminTopicConfig/index")),
      },
      // ROUTE MỚI: Quản lý Câu đóng góp tiêu chuẩn
      {
        path: "configuration/text",
        element: lazy(() => import("../pages/AdminTemplate/AdminTextConfig/index")),
      },
      {
        path: "help-center",
        element: HelpCenterLazy,
      },
    ],
  },
];

export const renderRoutes = () => {
  return routes.map((route, idx) => {
    const Component = route.element;

    if (route.nested) {
      return (
        <Route key={idx} path={route.path} element={<Component />}>
          {route.nested.map((item) => {
            const NestedComponent = item.element;
            return (
              <Route
                key={item.path}
                path={item.path}
                element={
                  <Suspense fallback={<Loading />}>
                    <NestedComponent {...item.props} />
                  </Suspense>
                }
              />
            );
          })}
        </Route>
      );
    }

    return (
      <Route
        key={idx}
        path={route.path}
        element={
          <Suspense fallback={<Loading className="min-h-screen" />}>
            <Component />
          </Suspense>
        }
      />
    );
  });
};
