import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Paper,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import AddIcon from "@mui/icons-material/Add";
import type { Booking } from "../api/types";
import { BookingDetailsDialog } from "../components/BookingDetailsDialog";
import { BookingDialog, type BookingDraft } from "../components/BookingDialog";
import { DayTimeline } from "../components/DayTimeline";
import { PhaseChip } from "../components/PhaseChip";
import { useBookingsForDay, useCabins, useNow, useRules } from "../hooks/queries";
import { fmtRange, officeAt, phaseOf, todayStr } from "../utils/time";

export function TimelinePage() {
  const { data: rules } = useRules();
  const { data: cabins = [] } = useCabins();
  const [date, setDate] = useState(todayStr());
  const { data: bookings = [], isLoading } = useBookingsForDay(date);
  const now = useNow(30_000);

  const [draft, setDraft] = useState<BookingDraft | null>(null);
  const [editing, setEditing] = useState<Booking | null>(null);
  const [selected, setSelected] = useState<Booking | null>(null);

  const day = officeAt(date, "12:00");
  const shift = (days: number) => setDate(day.add(days, "day").format("YYYY-MM-DD"));
  const isWorkingDay = rules?.workingDays.includes(day.format("dddd")) ?? true;
  const active = bookings
    .filter((b) => b.status === "Confirmed")
    .sort((a, b) => a.startTime.localeCompare(b.startTime));

  return (
    <Stack spacing={2}>
      <Stack
        direction={{ xs: "column", md: "row" }}
        spacing={2}
        sx={{ justifyContent: "space-between", alignItems: { md: "center" } }}
      >
        <Box>
          <Typography variant="h5">Timeline</Typography>
          <Typography color="text.secondary">
            Click an empty slot to book it. Changes by others appear instantly.
          </Typography>
        </Box>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: "center", flexWrap: "wrap" }}
          useFlexGap
        >
          <IconButton onClick={() => shift(-1)} aria-label="Previous day">
            <ChevronLeftIcon />
          </IconButton>
          <TextField
            type="date"
            size="small"
            value={date}
            onChange={(e) => e.target.value && setDate(e.target.value)}
          />
          <IconButton onClick={() => shift(1)} aria-label="Next day">
            <ChevronRightIcon />
          </IconButton>
          <Button onClick={() => setDate(todayStr())} disabled={date === todayStr()}>
            Today
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDraft({ date })}>
            Book
          </Button>
        </Stack>
      </Stack>

      <Typography variant="h6">{day.format("dddd, D MMMM YYYY")}</Typography>

      {!isWorkingDay && (
        <Alert severity="info">The office is closed on {day.format("dddd")}s.</Alert>
      )}

      {!rules || isLoading ? (
        <Skeleton variant="rounded" height={260} />
      ) : (
        <DayTimeline
          date={date}
          rules={rules}
          cabins={cabins}
          bookings={bookings}
          now={now}
          onSlotClick={(cabinId, start) => isWorkingDay && setDraft({ cabinId, date, start })}
          onBookingClick={setSelected}
        />
      )}

      <Paper variant="outlined">
        <Typography variant="subtitle1" sx={{ px: 2, pt: 2 }}>
          Bookings ({active.length})
        </Typography>
        {active.length === 0 ? (
          <Typography color="text.secondary" sx={{ p: 2 }}>
            No bookings on this day yet.
          </Typography>
        ) : (
          <List dense>
            {active.map((b) => (
              <ListItemButton key={b.id} onClick={() => setSelected(b)}>
                <Box
                  sx={{
                    width: 10,
                    height: 10,
                    borderRadius: "50%",
                    mr: 2,
                    flexShrink: 0,
                    bgcolor: cabins.find((c) => c.id === b.cabinId)?.color,
                  }}
                />
                <ListItemText
                  primary={`${fmtRange(b)} · ${b.title}`}
                  secondary={`${b.cabinName} · ${b.bookedByName}`}
                />
                <PhaseChip phase={phaseOf(b, now)} />
              </ListItemButton>
            ))}
          </List>
        )}
      </Paper>

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
