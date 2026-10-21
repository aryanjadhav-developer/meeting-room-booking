import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  Switch,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import RepeatIcon from "@mui/icons-material/Repeat";
import { api } from "../api/client";
import { DAY_LABELS, PURPOSES, type BookingPurpose, type RecurringBooking } from "../api/types";
import { useNotify } from "../components/Notifier";
import { useCabins, useRules } from "../hooks/queries";
import { office } from "../utils/time";

const DAY_NAMES = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"] as const;

function minutesToHHMM(minutes: number): string {
  const h = Math.floor(minutes / 60).toString().padStart(2, "0");
  const m = (minutes % 60).toString().padStart(2, "0");
  return `${h}:${m}`;
}

function hhmmToMinutes(hhmm: string): number {
  const [h, m] = hhmm.split(":").map(Number);
  return (h ?? 0) * 60 + (m ?? 0);
}

function formatDays(days: number[]): string {
  if (days.length === 7) return "Every day";
  if (JSON.stringify([...days].sort()) === JSON.stringify([1, 2, 3, 4, 5])) return "Weekdays";
  return days.map((d) => DAY_LABELS[d]).join(", ");
}

function formatTime(minutes: number): string {
  return minutesToHHMM(minutes);
}

// ------------------------------------------------------------------ dialog

interface DialogProps {
  open: boolean;
  onClose(): void;
  editing?: RecurringBooking | null;
}

function RecurringDialog({ open, onClose, editing }: DialogProps) {
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: cabins = [] } = useCabins();
  const { data: rules } = useRules();
  const { data: directory = [] } = useQuery({ queryKey: ["directory"], queryFn: api.directory });

  const today = office().format("YYYY-MM-DD");

  const [cabinId, setCabinId] = useState<number | "">(cabins[0]?.id ?? "");
  const [title, setTitle] = useState("");
  const [purpose, setPurpose] = useState<BookingPurpose | "">("");
  const [purposeDetail, setPurposeDetail] = useState("");
  const [notes, setNotes] = useState("");
  const [startDate, setStartDate] = useState(today);
  const [endDate, setEndDate] = useState(office().add(3, "month").format("YYYY-MM-DD"));
  const [startTime, setStartTime] = useState("09:00");
  const [duration, setDuration] = useState(30);
  const [days, setDays] = useState<number[]>([1, 2, 3, 4, 5]);
  const [attendeeEmails, setAttendeeEmails] = useState<string[]>([]);
  const [isActive, setIsActive] = useState(true);
  const [showErrors, setShowErrors] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset when dialog opens
  const reset = () => {
    setShowErrors(false);
    setError(null);
    if (editing) {
      setCabinId(editing.cabinId);
      setTitle(editing.title);
      setPurpose(editing.purpose);
      setPurposeDetail(editing.purposeDetail ?? "");
      setNotes(editing.notes ?? "");
      setStartDate(editing.startDate);
      setEndDate(editing.endDate);
      setStartTime(minutesToHHMM(editing.startMinuteOfDay));
      setDuration(editing.durationMinutes);
      setDays(editing.daysOfWeek);
      setAttendeeEmails(editing.attendeeEmails);
      setIsActive(editing.isActive);
    } else {
      setCabinId(cabins[0]?.id ?? "");
      setTitle("");
      setPurpose("");
      setPurposeDetail("");
      setNotes("");
      setStartDate(today);
      setEndDate(office().add(3, "month").format("YYYY-MM-DD"));
      setStartTime(rules?.officeStart?.slice(0, 5) ?? "09:00");
      setDuration(30);
      setDays([1, 2, 3, 4, 5]);
      setAttendeeEmails([]);
      setIsActive(true);
    }
  };

  // Reset when open changes
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { if (open) reset(); }, [open]);

  const missing = {
    cabin: !cabinId,
    title: !title.trim() || title.trim().length < 2,
    purpose: !purpose,
    purposeDetail: purpose === "Other" && !purposeDetail.trim(),
    days: days.length === 0,
    endDate: endDate < startDate,
  };
  const valid = !Object.values(missing).some(Boolean);

  const mut = useMutation({
    mutationFn: () => {
      const body = {
        cabinId: Number(cabinId),
        title: title.trim(),
        purpose: purpose as BookingPurpose,
        purposeDetail: purpose === "Other" ? purposeDetail.trim() : null,
        notes: notes.trim() || null,
        startDate,
        endDate,
        startMinuteOfDay: hhmmToMinutes(startTime),
        durationMinutes: duration,
        daysOfWeek: days,
        attendeeEmails,
        isActive,
      };
      return editing
        ? api.updateRecurringBooking(editing.id, body)
        : api.createRecurringBooking(body);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["recurring"] });
      notify(editing ? "Schedule updated" : "Recurring schedule created");
      onClose();
    },
    onError: (e) => setError((e as Error).message),
  });

  const submit = () => {
    setError(null);
    if (!valid) { setShowErrors(true); return; }
    mut.mutate();
  };

  const err = (k: keyof typeof missing) => showErrors && missing[k];

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{editing ? "Edit recurring schedule" : "New recurring schedule"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Cabin"
            value={cabinId}
            required
            onChange={(e) => setCabinId(Number(e.target.value))}
            error={err("cabin")}
          >
            {cabins.map((c) => (
              <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>
            ))}
          </TextField>

          <TextField
            label="Project / meeting name"
            value={title}
            required
            onChange={(e) => setTitle(e.target.value)}
            error={err("title")}
            helperText={err("title") ? "At least 2 characters" : undefined}
            slotProps={{ htmlInput: { maxLength: 120 } }}
          />

          <TextField
            select
            label="Purpose"
            value={purpose}
            required
            onChange={(e) => setPurpose(e.target.value as BookingPurpose)}
            error={err("purpose")}
          >
            {PURPOSES.map((p) => <MenuItem key={p} value={p}>{p}</MenuItem>)}
          </TextField>
          {purpose === "Other" && (
            <TextField
              label="Describe the purpose"
              value={purposeDetail}
              required
              onChange={(e) => setPurposeDetail(e.target.value)}
              error={err("purposeDetail")}
              slotProps={{ htmlInput: { maxLength: 100 } }}
            />
          )}

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              type="date"
              label="From date"
              value={startDate}
              fullWidth
              disabled={!!editing}
              onChange={(e) => setStartDate(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              type="date"
              label="Until date"
              value={endDate}
              fullWidth
              onChange={(e) => setEndDate(e.target.value)}
              error={err("endDate")}
              helperText={err("endDate") ? "Must be after start date" : undefined}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              type="time"
              label="Start time"
              value={startTime}
              fullWidth
              onChange={(e) => setStartTime(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              select
              label="Duration"
              value={duration}
              fullWidth
              onChange={(e) => setDuration(Number(e.target.value))}
            >
              {[15, 30, 45, 60, 90, 120].map((m) => (
                <MenuItem key={m} value={m}>{m < 60 ? `${m} min` : `${m / 60}h`}</MenuItem>
              ))}
            </TextField>
          </Stack>

          <Box>
            <Typography variant="body2" sx={{ mb: 1 }}>
              Repeat on <span style={{ color: err("days") ? "red" : undefined }}>*</span>
            </Typography>
            <ToggleButtonGroup
              value={days}
              onChange={(_, v: number[]) => setDays(v)}
              sx={{ flexWrap: "wrap", gap: 0.5 }}
            >
              {DAY_NAMES.map((label, i) => (
                <ToggleButton key={i} value={i} size="small" sx={{ minWidth: 48 }}>
                  {label}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
            {err("days") && (
              <Typography variant="caption" color="error">Select at least one day</Typography>
            )}
          </Box>

          <TextField
            label="Notes (optional)"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            multiline
            minRows={2}
            slotProps={{ htmlInput: { maxLength: 1000 } }}
          />

          <Autocomplete
            multiple
            freeSolo
            options={directory.map((u) => u.email)}
            value={attendeeEmails}
            getOptionLabel={(email) => {
              const u = directory.find((d) => d.email === email);
              return u ? `${u.name} (${email})` : email;
            }}
            onChange={(_, v) => {
              const valid = (v as string[]).filter((e) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e));
              setAttendeeEmails([...new Set(valid)]);
            }}
            renderInput={(params) => (
              <TextField {...params} label="Attendees (optional)"
                helperText="Each gets a confirmation + 15-min reminder before every meeting." />
            )}
          />

          {editing && (
            <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
              <Switch checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              <Typography variant="body2">{isActive ? "Active" : "Paused"}</Typography>
            </Stack>
          )}

          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={mut.isPending}>
          {mut.isPending ? "Saving…" : editing ? "Save changes" : "Create schedule"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ------------------------------------------------------------------ main page

export function RecurringPage() {
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: schedules = [], isLoading, error } = useQuery({
    queryKey: ["recurring"],
    queryFn: api.recurringBookings,
  });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RecurringBooking | null>(null);

  const remove = useMutation({
    mutationFn: (id: string) => api.deleteRecurringBooking(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["recurring"] }); notify("Schedule deleted"); },
    onError: (e) => notify((e as Error).message, "error"),
  });

  const openNew = () => { setEditing(null); setDialogOpen(true); };
  const openEdit = (s: RecurringBooking) => { setEditing(s); setDialogOpen(true); };

  return (
    <Stack spacing={3}>
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }} spacing={2}>
        <Box>
          <Typography variant="h5">Recurring Schedules</Typography>
          <Typography color="text.secondary" variant="body2">
            Meetings that repeat on a schedule — booked automatically each day.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openNew}>
          New schedule
        </Button>
      </Stack>

      {error && <Alert severity="error">{(error as Error).message}</Alert>}
      {isLoading && <Typography color="text.secondary">Loading…</Typography>}

      {!isLoading && schedules.length === 0 && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <RepeatIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
          <Typography color="text.secondary">No recurring schedules yet.</Typography>
          <Button sx={{ mt: 2 }} variant="outlined" onClick={openNew}>Create one</Button>
        </Paper>
      )}

      <Stack spacing={2}>
        {schedules.map((s) => (
          <Paper key={s.id} variant="outlined" sx={{ p: 2, opacity: s.isActive ? 1 : 0.6 }}>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "flex-start" }}>
              <Box sx={{ flex: 1 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
                  <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>{s.title}</Typography>
                  {!s.isActive && <Chip label="Paused" size="small" color="default" />}
                  <Chip label={s.cabinName} size="small" variant="outlined" />
                </Stack>
                <Typography variant="body2" color="text.secondary">
                  {formatDays(s.daysOfWeek)} · {formatTime(s.startMinuteOfDay)} ({s.durationMinutes} min)
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {s.startDate} → {s.endDate}
                </Typography>
                {s.attendeeEmails.length > 0 && (
                  <Typography variant="caption" color="text.secondary">
                    Attendees: {s.attendeeEmails.join(", ")}
                  </Typography>
                )}
              </Box>
              <Stack direction="row" spacing={0.5}>
                <Tooltip title="Edit">
                  <IconButton size="small" onClick={() => openEdit(s)}><EditIcon fontSize="small" /></IconButton>
                </Tooltip>
                <Tooltip title="Delete">
                  <IconButton
                    size="small"
                    color="error"
                    onClick={() => window.confirm(`Delete schedule "${s.title}"?`) && remove.mutate(s.id)}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
            </Stack>
            {s.notes && (
              <>
                <Divider sx={{ my: 1 }} />
                <Typography variant="caption" color="text.secondary">{s.notes}</Typography>
              </>
            )}
          </Paper>
        ))}
      </Stack>

      <RecurringDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        editing={editing}
      />
    </Stack>
  );
}
