import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
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
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { PURPOSES, type Booking, type BookingPurpose } from "../api/types";
import { useBookingMutation, useBookingsForDay, useCabins, useRules } from "../hooks/queries";
import { capacityText } from "../utils/format";
import {
  fmtDuration,
  fmtRange,
  office,
  officeAt,
  officeWindow,
  overlaps,
  todayStr,
} from "../utils/time";
import { useNotify } from "./Notifier";

/** Select value for the "Other" project; the typed name is used instead. */
const OTHER_PROJECT = "__other__";

export interface BookingDraft {
  cabinId?: number;
  date?: string; // YYYY-MM-DD (office date)
  start?: string; // HH:mm
  duration?: number; // minutes
}

interface Props {
  open: boolean;
  onClose(): void;
  draft?: BookingDraft;
  /** When set, the dialog edits this booking instead of creating one. */
  editing?: Booking | null;
}

const QUICK_DURATIONS = [15, 30, 45, 60, 90, 120];

export function BookingDialog({ open, onClose, draft, editing }: Props) {
  const { data: rules } = useRules();
  const { data: cabins = [] } = useCabins();
  const notify = useNotify();

  const [cabinId, setCabinId] = useState<number | "">("");
  const [date, setDate] = useState(todayStr());
  const [start, setStart] = useState("");
  const [duration, setDuration] = useState(30);
  const [project, setProject] = useState(""); // a configured project or OTHER_PROJECT
  const [customProject, setCustomProject] = useState(""); // typed name when "Other"
  const [purpose, setPurpose] = useState<BookingPurpose | "">("");
  const [purposeDetail, setPurposeDetail] = useState("");
  const [notes, setNotes] = useState("");
  const [attendeeEmails, setAttendeeEmails] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showErrors, setShowErrors] = useState(false);
  const [lockedStart, setLockedStart] = useState(false); // true when opened via quick-book

  const { user: me } = useAuth();
  const { data: projectList = [] } = useQuery({ queryKey: ["projects"], queryFn: api.projects });
  const { data: directory = [] } = useQuery({ queryKey: ["directory"], queryFn: api.directory });
  const projects = projectList.length ? projectList.map((p) => p.name) : (rules?.projects ?? []);

  const pickProject = (value: string) => {
    setProject(value);
    const p = projectList.find((x) => x.name === value);
    if (!p) return;
    const emails = directory
      .filter((u) => p.memberUserIds.includes(u.id) && u.email.toLowerCase() !== me?.email.toLowerCase())
      .map((u) => u.email);
    setAttendeeEmails(emails);
  };

  // Reset the form each time the dialog opens.
  useEffect(() => {
    if (!open) return;
    setError(null);
    setShowErrors(false);
    if (editing) {
      setCabinId(editing.cabinId);
      setDate(office(editing.startTime).format("YYYY-MM-DD"));
      setStart(office(editing.startTime).format("HH:mm"));
      setDuration(editing.durationMinutes);
      const known = projects.includes(editing.title);
      setProject(known ? editing.title : OTHER_PROJECT);
      setCustomProject(known ? "" : editing.title);
      setPurpose(editing.purpose);
      setPurposeDetail(editing.purposeDetail ?? "");
      setNotes(editing.notes ?? "");
      setAttendeeEmails(editing.attendeeEmails ?? []);
    } else {
      setCabinId(draft?.cabinId ?? cabins[0]?.id ?? "");
      setDate(draft?.date ?? todayStr());
      setStart(draft?.start ?? "");
      setDuration(draft?.duration ?? 30);
      setLockedStart(!!draft?.start);
      setProject("");
      setCustomProject("");
      setPurpose("");
      setPurposeDetail("");
      setNotes("");
      setAttendeeEmails([]);
    }
  }, [open, editing, draft, cabins, rules]);

  const title = project === OTHER_PROJECT ? customProject.trim() : project;
  const missing = {
    project: !project,
    customProject: project === OTHER_PROJECT && !customProject.trim(),
    purpose: !purpose,
    purposeDetail: purpose === "Other" && !purposeDetail.trim(),
  };
  const detailsValid = !Object.values(missing).some(Boolean);

  const { data: dayBookings = [] } = useBookingsForDay(date);

  const startOptions = useMemo(() => {
    if (!rules) return [];
    const [open, close] = officeWindow(date, rules);
    const now = office();
    const opts: string[] = [];
    for (
      let t = open;
      t.add(rules.minDurationMinutes, "minute").isBefore(close.add(1, "second"));
      t = t.add(rules.slotMinutes, "minute")
    ) {
      if (t.isBefore(now.subtract(1, "minute"))) continue;
      opts.push(t.format("HH:mm"));
    }
    return opts;
  }, [rules, date]);

  // Derived rather than synced via effects: if the chosen start/duration doesn't fit the selected
  // day (past time, too close to closing), fall back to the nearest valid option.
  const effStart = lockedStart ? start : (startOptions.includes(start) ? start : (startOptions[0] ?? ""));

  const durationOptions = useMemo(() => {
    if (!rules || !effStart) return [];
    const [, close] = officeWindow(date, rules);
    const s = officeAt(date, effStart);
    const opts: number[] = [];
    for (let m = rules.minDurationMinutes; m <= rules.maxDurationMinutes; m += rules.slotMinutes) {
      if (s.add(m, "minute").isAfter(close)) break;
      opts.push(m);
    }
    return opts;
  }, [rules, date, effStart]);

  const effDuration = durationOptions.includes(duration)
    ? duration
    : (durationOptions.filter((d) => d <= duration).at(-1) ?? durationOptions[0] ?? 0);

  const startAt = effStart && effDuration ? officeAt(date, effStart) : null;
  const endAt = startAt?.add(effDuration, "minute") ?? null;

  const clash = useMemo(() => {
    if (!startAt || !endAt || !cabinId) return null;
    return (
      dayBookings.find(
        (b) =>
          b.cabinId === cabinId &&
          b.status === "Confirmed" &&
          b.id !== editing?.id &&
          overlaps(startAt, endAt, b)
      ) ?? null
    );
  }, [dayBookings, cabinId, startAt, endAt, editing]);

  const save = useBookingMutation(async () => {
    const input = {
      cabinId: Number(cabinId),
      title,
      purpose: purpose as BookingPurpose,
      purposeDetail: purpose === "Other" ? purposeDetail.trim() : null,
      notes: notes.trim() || null,
      startTime: startAt!.toISOString(),
      endTime: endAt!.toISOString(),
      attendeeEmails: attendeeEmails.length > 0 ? attendeeEmails : undefined,
    };
    return editing ? api.updateBooking(editing.id, input) : api.createBooking(input);
  });

  const submit = async () => {
    setError(null);
    if (!detailsValid) {
      setShowErrors(true);
      return;
    }
    try {
      const b = await save.mutateAsync(undefined);
      notify(`${b.cabinName} ${editing ? "updated" : "booked"} · ${fmtRange(b)}`);
      onClose();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const maxDate = rules ? office().add(rules.maxDaysAhead, "day").format("YYYY-MM-DD") : undefined;
  // Project/purpose problems are reported on click (field highlights), so the button stays clickable for them.
  const canSubmit = !!cabinId && !!startAt && !clash && !save.isPending && effDuration > 0;
  const err = (key: keyof typeof missing) => showErrors && missing[key];

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{editing ? "Edit booking" : "Book a room"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Cabin"
            value={cabinId}
            onChange={(e) => setCabinId(Number(e.target.value))}
          >
            {cabins.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                <Box
                  sx={{
                    width: 10,
                    height: 10,
                    borderRadius: "50%",
                    bgcolor: c.color,
                    mr: 1.5,
                    display: "inline-block",
                  }}
                />
                {c.name} · {capacityText(c)}
                {c.amenities.length ? ` · ${c.amenities.join(", ")}` : ""}
              </MenuItem>
            ))}
          </TextField>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              type="date"
              label="Date"
              value={date}
              fullWidth
              onChange={(e) => setDate(e.target.value)}
              slotProps={{
                inputLabel: { shrink: true },
                htmlInput: { min: todayStr(), max: maxDate },
              }}
            />
            {lockedStart ? (
              <TextField
                label="Start"
                value={effStart}
                fullWidth
                disabled
                helperText="Set to now"
              />
            ) : (
              <TextField
                select
                label="Start"
                value={effStart}
                fullWidth
                onChange={(e) => setStart(e.target.value)}
                disabled={!startOptions.length}
                helperText={!startOptions.length ? "No times left on this day" : " "}
              >
                {startOptions.map((s) => (
                  <MenuItem key={s} value={s}>
                    {s}
                  </MenuItem>
                ))}
              </TextField>
            )}
          </Stack>

          <Box>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
              Duration
            </Typography>
            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
              {QUICK_DURATIONS.filter((d) => durationOptions.includes(d)).map((d) => (
                <Chip
                  key={d}
                  label={fmtDuration(d)}
                  color={effDuration === d ? "primary" : "default"}
                  variant={effDuration === d ? "filled" : "outlined"}
                  onClick={() => setDuration(d)}
                />
              ))}
              <TextField
                select
                size="small"
                value={effDuration || ""}
                sx={{ minWidth: 120 }}
                onChange={(e) => setDuration(Number(e.target.value))}
              >
                {durationOptions.map((d) => (
                  <MenuItem key={d} value={d}>
                    {fmtDuration(d)}
                  </MenuItem>
                ))}
              </TextField>
            </Stack>
            {startAt && endAt && (
              <Typography variant="body2" sx={{ mt: 1 }}>
                {startAt.format("ddd, D MMM")} ·{" "}
                <b>
                  {startAt.format("HH:mm")} – {endAt.format("HH:mm")}
                </b>
              </Typography>
            )}
          </Box>

          {clash && (
            <Alert severity="warning">
              Already booked by <b>{clash.bookedByName}</b> {fmtRange(clash)} ({clash.title}). Pick
              another time or room.
            </Alert>
          )}

          <TextField
            select
            label="Project"
            value={project}
            required
            onChange={(e) => pickProject(e.target.value)}
            error={err("project")}
            helperText={err("project") ? "Please choose a project" : undefined}
          >
            {projects.map((p) => (
              <MenuItem key={p} value={p}>
                {p}
              </MenuItem>
            ))}
            <MenuItem value={OTHER_PROJECT}>Other</MenuItem>
          </TextField>
          {project === OTHER_PROJECT && (
            <TextField
              label="Project name"
              value={customProject}
              required
              autoFocus
              onChange={(e) => setCustomProject(e.target.value)}
              error={err("customProject")}
              helperText={err("customProject") ? "Please enter the project name" : undefined}
              slotProps={{ htmlInput: { maxLength: 120 } }}
            />
          )}

          <TextField
            select
            label="Purpose"
            value={purpose}
            required
            onChange={(e) => setPurpose(e.target.value as BookingPurpose)}
            error={err("purpose")}
            helperText={err("purpose") ? "Please choose a purpose" : undefined}
          >
            {PURPOSES.map((p) => (
              <MenuItem key={p} value={p}>
                {p}
              </MenuItem>
            ))}
          </TextField>
          {purpose === "Other" && (
            <TextField
              label="Describe the purpose"
              value={purposeDetail}
              required
              autoFocus
              placeholder="e.g. Vendor demo"
              onChange={(e) => setPurposeDetail(e.target.value)}
              error={err("purposeDetail")}
              helperText={err("purposeDetail") ? "Please describe the purpose" : undefined}
              slotProps={{ htmlInput: { maxLength: 100 } }}
            />
          )}
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
              const valid = v.filter((e) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e));
              setAttendeeEmails([...new Set(valid)]);
            }}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Attendees (optional)"
                helperText="Project members are added automatically. Pick anyone from the employee list or type an e-mail. Each gets a confirmation with a calendar invite."
              />
            )}
          />

          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={!canSubmit}>
          {save.isPending ? "Saving…" : editing ? "Save changes" : "Book"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
