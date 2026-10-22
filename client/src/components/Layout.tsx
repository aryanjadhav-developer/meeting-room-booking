import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  AppBar,
  Avatar,
  Box,
  Button,
  Chip,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  Menu,
  MenuItem,
  Stack,
  TextField,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";
import MeetingRoomIcon from "@mui/icons-material/MeetingRoom";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { useNotify } from "./Notifier";
import { useHubStatus, type HubStatus } from "../realtime/BookingHubProvider";
import { PreviewEmailsDialog } from "./PreviewEmailsDialog";

const LINKS = [
  { to: "/", label: "Live board" },
  { to: "/timeline", label: "Timeline" },
  { to: "/mine", label: "My bookings" },
  { to: "/recurring", label: "Recurring" },
];

const STATUS: Record<HubStatus, { label: string; color: "success" | "warning" | "default" }> = {
  live: { label: "Live", color: "success" },
  connecting: { label: "Connecting", color: "warning" },
  reconnecting: { label: "Reconnecting", color: "warning" },
  offline: { label: "Offline", color: "default" },
};

function ProfileDialog({ open, onClose }: { open: boolean; onClose(): void }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const notify = useNotify();
  const [name, setName] = useState(user?.name ?? "");
  const [currentPw, setCurrentPw] = useState("");
  const [newPw, setNewPw] = useState("");
  const [error, setError] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () => api.updateProfile({
      name: name.trim() !== user?.name ? name.trim() : undefined,
      currentPassword: currentPw || undefined,
      newPassword: newPw || undefined,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["me"] });
      notify("Profile updated");
      setCurrentPw(""); setNewPw(""); setError(null);
      onClose();
    },
    onError: (e) => setError((e as Error).message),
  });

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Edit profile</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 100 } }} />
          <TextField label="E-mail" value={user?.email ?? ""} disabled />
          <Divider><Typography variant="caption" color="text.secondary">Change password (optional)</Typography></Divider>
          <TextField label="Current password" type="password" value={currentPw}
            onChange={(e) => setCurrentPw(e.target.value)} autoComplete="current-password" />
          <TextField label="New password" type="password" value={newPw}
            onChange={(e) => setNewPw(e.target.value)} autoComplete="new-password"
            helperText="At least 8 characters" />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={() => save.mutate()} disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const hub = useHubStatus();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [profileOpen, setProfileOpen] = useState(false);
  const links = isAdmin ? [...LINKS, { to: "/admin", label: "Admin" }] : LINKS;

  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "background.default" }}>
      <AppBar
        position="sticky"
        color="inherit"
        elevation={0}
        sx={{ borderBottom: 1, borderColor: "divider" }}
      >
        <Toolbar sx={{ gap: 1 }}>
          <MeetingRoomIcon color="primary" />
          <Typography variant="h6" sx={{ mr: 2, display: { xs: "none", sm: "block" } }}>
            Room Booking
          </Typography>

          <Box
            sx={{
              display: "flex",
              gap: 0.5,
              flex: 1,
              overflowX: "auto",
              scrollbarWidth: "none",
              "&::-webkit-scrollbar": { display: "none" },
            }}
          >
            {links.map((l) => (
              <Button
                key={l.to}
                component={NavLink}
                to={l.to}
                end={l.to === "/"}
                color="inherit"
                size="small"
                sx={{
                  whiteSpace: "nowrap",
                  "&.active": { color: "primary.main", bgcolor: "action.selected" },
                }}
              >
                {l.label}
              </Button>
            ))}
          </Box>

          <Tooltip title="Real-time connection: bookings by others appear instantly">
            <Chip
              size="small"
              variant="outlined"
              color={STATUS[hub].color}
              label={
                <Box component="span" sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                  <Box
                    component="span"
                    sx={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      bgcolor: `${STATUS[hub].color === "default" ? "grey" : STATUS[hub].color}.main`,
                    }}
                  />
                  {STATUS[hub].label}
                </Box>
              }
            />
          </Tooltip>

          {isAdmin && <PreviewEmailsDialog />}

          <IconButton onClick={(e) => setAnchor(e.currentTarget)} size="small" aria-label="Account">
            <Avatar sx={{ width: 32, height: 32, bgcolor: "primary.main", fontSize: 14 }}>
              {user?.name
                .split(" ")
                .map((p) => p[0])
                .slice(0, 2)
                .join("")
                .toUpperCase()}
            </Avatar>
          </IconButton>
          <Menu anchorEl={anchor} open={!!anchor} onClose={() => setAnchor(null)}>
            <MenuItem disabled>
              <Box>
                <Typography variant="body2">{user?.name}</Typography>
                <Typography variant="caption">
                  {user?.email} · {user?.role}
                </Typography>
              </Box>
            </MenuItem>
            <MenuItem onClick={() => { setAnchor(null); setProfileOpen(true); }}>
              Edit profile
            </MenuItem>
            <MenuItem
              onClick={() => {
                setAnchor(null);
                logout();
              }}
            >
              Sign out
            </MenuItem>
          </Menu>
          <ProfileDialog open={profileOpen} onClose={() => setProfileOpen(false)} />
        </Toolbar>
      </AppBar>

      <Container maxWidth="xl" sx={{ py: 3, px: { xs: 2, sm: 3 } }}>
        <Outlet />
      </Container>
    </Box>
  );
}
