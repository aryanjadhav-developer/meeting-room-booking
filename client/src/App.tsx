import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { Alert, Box, CircularProgress } from "@mui/material";
import { useAuth } from "./auth/AuthContext";
import { Layout } from "./components/Layout";
import { useRules } from "./hooks/queries";
import { BookingHubProvider } from "./realtime/BookingHubProvider";
import { AdminPage } from "./pages/AdminPage";
import { DashboardPage } from "./pages/DashboardPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { LoginPage } from "./pages/LoginPage";
import { MyBookingsPage } from "./pages/MyBookingsPage";
import { RecurringPage } from "./pages/RecurringPage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { TimelinePage } from "./pages/TimelinePage";

const Centered = ({ children }: { children: ReactNode }) => (
  <Box sx={{ minHeight: "100vh", display: "grid", placeItems: "center", p: 2 }}>{children}</Box>
);

export function App() {
  const { user, loading } = useAuth();
  if (loading)
    return (
      <Centered>
        <CircularProgress />
      </Centered>
    );
  if (!user) {
    return (
      <Routes>
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="*" element={<LoginPage />} />
      </Routes>
    );
  }
  return <SignedInApp />;
}

function SignedInApp() {
  const { isAdmin } = useAuth();
  // Rules carry the office time zone, which every time display depends on, so load them first.
  const rules = useRules();
  if (rules.isLoading)
    return (
      <Centered>
        <CircularProgress />
      </Centered>
    );
  if (rules.error)
    return (
      <Centered>
        <Alert severity="error">Could not reach the server: {(rules.error as Error).message}</Alert>
      </Centered>
    );

  return (
    <BookingHubProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="timeline" element={<TimelinePage />} />
          <Route path="mine" element={<MyBookingsPage />} />
          <Route path="recurring" element={<RecurringPage />} />
          {isAdmin && <Route path="admin" element={<AdminPage />} />}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BookingHubProvider>
  );
}
