import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  ButtonGroup,
  Card,
  CardActions,
  CardContent,
  Chip,
  Divider,
  LinearProgress,
  Skeleton,
  Stack,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import GroupsIcon from "@mui/icons-material/Groups";
import { api } from "../api/client";
import type { Booking, CabinStatus } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { BookingDetailsDialog } from "../components/BookingDetailsDialog";
import { BookingDialog, type BookingDraft } from "../components/BookingDialog";
import { useNotify } from "../components/Notifier";
import { useBookingMutation, useCabinStatus, useNow } from "../hooks/queries";
import { capacityText } from "../utils/format";
import { fmtRange, fmtTime, minutesUntil } from "../utils/time";

export function DashboardPage() {
  const { user } = useAuth();
  const { data: statuses, isLoading, error } = useCabinStatus();
  const now = useNow(15_000);
  const [draft, setDraft] = useState<BookingDraft | null>(null);
  const [editing, setEditing] = useState<Booking | null>(null);
  const [selected, setSelected] = useState<Booking | null>(null);

  const freeCount = statuses?.filter((s) => !isBusy(s)).length ?? 0;

  function isBusy(s: CabinStatus) {
    return !!s.current && now.isBefore(s.current.endTime);
  }

  return (
    <Stack spacing={3}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}
      >
        <Box>
          <Typography variant="h5">
            Good {now.hour() < 12 ? "morning" : now.hour() < 17 ? "afternoon" : "evening"},{" "}
            {user?.name.split(" ")[0]}
          </Typography>
          <Typography color="text.secondary">
            {now.format("dddd, D MMMM · HH:mm")} —{" "}
            {statuses ? `${freeCount} of ${statuses.length} cabins free right now` : "loading…"}
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDraft({})}>
          New booking
        </Button>
      </Stack>

      {error && <Alert severity="error">{(error as Error).message}</Alert>}

      <Box
        sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", md: "repeat(3, 1fr)" } }}
      >
        {isLoading && [0, 1, 2].map((i) => <Skeleton key={i} variant="rounded" height={260} />)}
        {statuses?.map((s) => (
          <CabinCard
            key={s.cabin.id}
            status={s}
            busy={isBusy(s)}
            now={now}
            onBook={() => setDraft({ cabinId: s.cabin.id })}
            onOpen={setSelected}
          />
        ))}
      </Box>

      <BookingDialog
        open={!!draft || !!editing}
        draft={draft ?? undefined}
        editing={editing}
        onClose={() => {
          setDraft(null);
          setEditing(null);
        }}
      />
      <BookingDetailsDialog
        booking={selected}
        onClose={() => setSelected(null)}
        onEdit={(b) => {
          setSelected(null);
          setEditing(b);
        }}
      />
    </Stack>
  );
}

interface CardProps {
  status: CabinStatus;
  busy: boolean;
  now: ReturnType<typeof useNow>;
  onBook(): void;
  onOpen(b: Booking): void;
}

function CabinCard({ status, busy, now, onBook, onOpen }: CardProps) {
  const { cabin, current, next } = status;
  const notify = useNotify();
  const quick = useBookingMutation((minutes: number) => api.quickBook(cabin.id, minutes));

  const freeFor = next ? minutesUntil(next.startTime, now) : null;
  const progress = current
    ? Math.min(100, (now.diff(current.startTime, "minute", true) / current.durationMinutes) * 100)
    : 0;

  const quickBook = async (minutes: number) => {
    try {
      const b = await quick.mutateAsync(minutes);
      notify(`${cabin.name} is yours until ${fmtTime(b.endTime)}`);
    } catch (e) {
      notify((e as Error).message, "error");
    }
  };

  return (
    <Card sx={{ borderTop: `4px solid ${cabin.color}`, display: "flex", flexDirection: "column" }}>
      <CardContent sx={{ flex: 1 }}>
        <Stack
          direction="row"
          sx={{ justifyContent: "space-between", alignItems: "flex-start", mb: 1 }}
        >
          <Box>
            <Typography variant="h6">{cabin.name}</Typography>
            <Stack
              direction="row"
              spacing={0.5}
              sx={{ alignItems: "center", color: "text.secondary" }}
            >
              <GroupsIcon fontSize="small" />
              <Typography variant="body2">{capacityText(cabin)}</Typography>
            </Stack>
          </Box>
          <Chip
            label={busy ? "Occupied" : "Free"}
            color={busy ? "error" : "success"}
            sx={{ fontWeight: 700 }}
          />
        </Stack>

        {cabin.amenities.length > 0 && (
          <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: "wrap", mb: 2 }}>
            {cabin.amenities.map((a) => (
              <Chip key={a} label={a} size="small" variant="outlined" />
            ))}
          </Stack>
        )}

        {busy && current ? (
          <Box
            onClick={() => onOpen(current)}
            sx={{ cursor: "pointer", p: 1.5, borderRadius: 2, bgcolor: "action.hover" }}
          >
            <Typography variant="subtitle2" noWrap>
              {current.title}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {current.bookedByName} · {fmtRange(current)}
            </Typography>
            <LinearProgress
              variant="determinate"
              value={progress}
              sx={{ mt: 1, borderRadius: 1 }}
            />
            <Typography variant="caption" color="text.secondary">
              Ends in {minutesUntil(current.endTime, now)} min
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" color="text.secondary">
            {freeFor === null ? "Free for the rest of the day" : `Free for the next ${freeFor} min`}
          </Typography>
        )}

        <Divider sx={{ my: 2 }} />
        <Typography variant="overline" color="text.secondary">
          Next
        </Typography>
        {next ? (
          <Box onClick={() => onOpen(next)} sx={{ cursor: "pointer" }}>
            <Typography variant="body2" noWrap>
              <b>{fmtRange(next)}</b> · {next.title}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {next.bookedByName}
            </Typography>
          </Box>
        ) : (
          <Typography variant="body2" color="text.secondary">
            Nothing else booked today
          </Typography>
        )}
      </CardContent>

      <CardActions sx={{ px: 2, pb: 2, gap: 1, flexWrap: "wrap" }}>
        {!busy && (freeFor === null || freeFor >= 15) && (
          <ButtonGroup size="small" variant="contained" disabled={quick.isPending}>
            {[15, 30, 60]
              .filter((m) => freeFor === null || m <= freeFor)
              .map((m) => (
                <Button key={m} onClick={() => quickBook(m)}>
                  Now · {m}m
                </Button>
              ))}
          </ButtonGroup>
        )}
        <Button size="small" onClick={onBook}>
          Book later
        </Button>
      </CardActions>
    </Card>
  );
}
