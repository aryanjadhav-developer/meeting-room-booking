import { useEffect, useState, type ReactNode } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Stack,
  Typography,
} from "@mui/material";
import { api } from "../api/client";
import type { Booking } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { useBookingMutation, useNow, useRules } from "../hooks/queries";
import { fmtDate, fmtDuration, fmtRange, phaseOf } from "../utils/time";
import { useNotify } from "./Notifier";
import { PhaseChip } from "./PhaseChip";
import { purposeText } from "../utils/format";

interface Props {
  booking: Booking | null;
  onClose(): void;
  onEdit(b: Booking): void;
}

export function BookingDetailsDialog({ booking, onClose, onEdit }: Props) {
  const { user, isAdmin } = useAuth();
  const { data: rules } = useRules();
  const now = useNow(15_000);
  const notify = useNotify();
  const [error, setError] = useState<string | null>(null);
  // In-app confirmation: window.confirm() is silently blocked in some browsers / embedded views.
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  useEffect(() => {
    setError(null);
    setConfirmingCancel(false);
  }, [booking?.id]);

  const action = useBookingMutation(async (fn: () => Promise<unknown>) => fn());

  if (!booking) return null;

  const phase = phaseOf(booking, now);
  const canManage = booking.bookedByUserId === user?.id || isAdmin;
  const checkInOpens = rules
    ? now.add(rules.checkInOpensMinutesBefore, "minute").isAfter(booking.startTime)
    : false;
  const canCheckIn =
    canManage &&
    booking.status === "Confirmed" &&
    !booking.checkedInAt &&
    (phase === "InProgress" || (phase === "Upcoming" && checkInOpens));

  const run = async (fn: () => Promise<unknown>, success: string, close = false) => {
    setError(null);
    try {
      await action.mutateAsync(fn);
      notify(success);
      if (close) onClose();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle sx={{ pr: 2 }}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", justifyContent: "space-between" }}
        >
          <span>{booking.title}</span>
          <PhaseChip phase={phase} />
        </Stack>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={1.25}>
          <Row label="Project" value={booking.title} />
          <Row label="Cabin" value={booking.cabinName} />
          <Row label="When" value={`${fmtDate(booking.startTime)} · ${fmtRange(booking)}`} />
          <Row label="Duration" value={fmtDuration(booking.durationMinutes)} />
          <Row label="Booked by" value={`${booking.bookedByName} (${booking.bookedByEmail})`} />
          <Row label="Purpose" value={purposeText(booking)} />
          {booking.checkedInAt && (
            <Row label="Checked in" value={<Chip size="small" color="success" label="Yes" />} />
          )}
          {booking.notes && (
            <>
              <Divider />
              <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
                {booking.notes}
              </Typography>
            </>
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>

      {confirmingCancel && phase === "Upcoming" ? (
        <Box sx={{ px: 3, pb: 2 }}>
          <Alert severity="warning" sx={{ mb: 1.5 }}>
            Cancel this booking? The slot will be freed for everyone.
          </Alert>
          <Stack direction="row" spacing={1} sx={{ justifyContent: "flex-end" }}>
            <Button onClick={() => setConfirmingCancel(false)} disabled={action.isPending}>
              Keep booking
            </Button>
            <Button
              color="error"
              variant="contained"
              disabled={action.isPending}
              onClick={() => run(() => api.cancelBooking(booking.id), "Booking cancelled", true)}
            >
              {action.isPending ? "Cancelling…" : "Yes, cancel it"}
            </Button>
          </Stack>
        </Box>
      ) : (
        canManage &&
        (phase === "Upcoming" || phase === "InProgress") && (
          <DialogActions sx={{ flexWrap: "wrap", gap: 1, px: 3, pb: 2 }}>
            {canCheckIn && (
              <Button
                variant="contained"
                color="success"
                disabled={action.isPending}
                onClick={() => run(() => api.checkIn(booking.id), "Checked in", true)}
              >
                Check in
              </Button>
            )}
            <Button
              variant="outlined"
              disabled={action.isPending}
              onClick={() =>
                run(() => api.extendBooking(booking.id, 15), "Extended by 15 min", true)
              }
            >
              +15 min
            </Button>
            <Button
              variant="outlined"
              disabled={action.isPending}
              onClick={() =>
                run(() => api.extendBooking(booking.id, 30), "Extended by 30 min", true)
              }
            >
              +30 min
            </Button>
            {phase === "InProgress" && (
              <Button
                color="warning"
                variant="contained"
                disabled={action.isPending}
                onClick={() =>
                  run(() => api.endBooking(booking.id), "Meeting ended, cabin is free", true)
                }
              >
                End now
              </Button>
            )}
            {phase === "Upcoming" && (
              <>
                <Button onClick={() => onEdit(booking)} disabled={action.isPending}>
                  Edit
                </Button>
                <Button
                  color="error"
                  disabled={action.isPending}
                  onClick={() => setConfirmingCancel(true)}
                >
                  Cancel booking
                </Button>
              </>
            )}
          </DialogActions>
        )
      )}
      {!(canManage && (phase === "Upcoming" || phase === "InProgress")) && (
        <DialogActions>
          <Button onClick={onClose}>Close</Button>
        </DialogActions>
      )}
    </Dialog>
  );
}

function Row({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Stack direction="row" spacing={2}>
      <Typography variant="body2" color="text.secondary" sx={{ width: 90, flexShrink: 0 }}>
        {label}
      </Typography>
      <Typography variant="body2" component="div">
        {value}
      </Typography>
    </Stack>
  );
}
