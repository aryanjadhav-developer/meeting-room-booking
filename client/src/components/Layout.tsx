import { useState } from "react";
import { NavLink, Outlet } from "react-router-dom";
import {
  AppBar,
  Avatar,
  Box,
  Button,
  Chip,
  Container,
  IconButton,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from "@mui/material";
import MeetingRoomIcon from "@mui/icons-material/MeetingRoom";
import { useAuth } from "../auth/AuthContext";
import { useHubStatus, type HubStatus } from "../realtime/BookingHubProvider";
import { PreviewEmailsDialog } from "./PreviewEmailsDialog";

const LINKS = [
  { to: "/", label: "Live board" },
  { to: "/timeline", label: "Timeline" },
  { to: "/mine", label: "My bookings" },
];

const STATUS: Record<HubStatus, { label: string; color: "success" | "warning" | "default" }> = {
  live: { label: "Live", color: "success" },
  connecting: { label: "Connecting", color: "warning" },
  reconnecting: { label: "Reconnecting", color: "warning" },
  offline: { label: "Offline", color: "default" },
};

export function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const hub = useHubStatus();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
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
            Cabin Booking
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

          <PreviewEmailsDialog />

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
            <MenuItem
              onClick={() => {
                setAnchor(null);
                logout();
              }}
            >
              Sign out
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Container maxWidth="xl" sx={{ py: 3, px: { xs: 2, sm: 3 } }}>
        <Outlet />
      </Container>
    </Box>
  );
}
