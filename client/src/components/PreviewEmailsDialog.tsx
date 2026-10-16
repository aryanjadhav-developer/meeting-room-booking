import { useState } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import MarkEmailReadIcon from "@mui/icons-material/MarkEmailRead";
import RefreshIcon from "@mui/icons-material/Refresh";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";

export function PreviewEmailsDialog() {
  const [open, setOpen] = useState(false);
  const [selectedIdx, setSelectedIdx] = useState<number | null>(null);

  const { data: emails = [], refetch, isFetching } = useQuery({
    queryKey: ["previewEmails"],
    queryFn: api.previewEmails,
    enabled: open,
    refetchInterval: open ? 5000 : false,
  });

  const selected = selectedIdx !== null && emails[selectedIdx] ? emails[selectedIdx] : null;

  return (
    <>
      <Tooltip title="View Test Emails (Demo Outbox)">
        <IconButton color="inherit" size="small" onClick={() => setOpen(true)} aria-label="Demo Outbox">
          <MarkEmailReadIcon fontSize="small" />
        </IconButton>
      </Tooltip>

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <Box component="span" sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <MarkEmailReadIcon color="primary" />
            <span>Demo In-App Email Outbox</span>
          </Box>
          <IconButton size="small" onClick={() => refetch()} disabled={isFetching}>
            <RefreshIcon fontSize="small" />
          </IconButton>
        </DialogTitle>
        <DialogContent dividers sx={{ p: 0 }}>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "280px 1fr" }, minHeight: 350 }}>
            <Box sx={{ borderRight: 1, borderColor: "divider", overflowY: "auto", maxHeight: 450 }}>
              {emails.length === 0 ? (
                <Box sx={{ p: 3, textAlign: "center", color: "text.secondary" }}>
                  <Typography variant="body2">No emails sent yet.</Typography>
                  <Typography variant="caption" sx={{ display: "block", mt: 1 }}>
                    Book a room or request a password reset to see sent emails here.
                  </Typography>
                </Box>
              ) : (
                <List dense disablePadding>
                  {emails.map((m, idx) => (
                    <ListItem key={idx} disablePadding divider>
                      <ListItemButton
                        selected={selectedIdx === idx}
                        onClick={() => setSelectedIdx(idx)}
                      >
                        <ListItemText
                          primary={
                            <Typography variant="body2" noWrap sx={{ fontWeight: selectedIdx === idx ? 600 : 400 }}>
                              {m.subject}
                            </Typography>
                          }
                          secondary={`To: ${m.to} · ${new Date(m.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`}
                        />
                      </ListItemButton>
                    </ListItem>
                  ))}
                </List>
              )}
            </Box>

            <Box sx={{ p: 2, overflowY: "auto", maxHeight: 450 }}>
              {selected ? (
                <Stack spacing={1.5}>
                  <Box>
                    <Typography variant="subtitle2" color="primary">
                      {selected.subject}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      To: <strong>{selected.to}</strong> · {new Date(selected.sentAt).toLocaleString()}
                    </Typography>
                  </Box>
                  <Divider />
                  <Paper
                    variant="outlined"
                    sx={{ p: 2, bgcolor: "background.paper", "& a": { color: "primary.main" } }}
                    dangerouslySetInnerHTML={{ __html: selected.body }}
                  />
                </Stack>
              ) : (
                <Box sx={{ p: 4, textAlign: "center", color: "text.secondary" }}>
                  <Typography variant="body2">Select an email to view its rendered body</Typography>
                </Box>
              )}
            </Box>
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
