import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  FormControlLabel,
  Skeleton,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DownloadIcon from "@mui/icons-material/Download";
import { api } from "../api/client";
import type { Booking } from "../api/types";
import { BookingDetailsDialog } from "../components/BookingDetailsDialog";
import { BookingDialog, type BookingDraft } from "../components/BookingDialog";
import { PhaseChip } from "../components/PhaseChip";
import { useMyBookings, useNow } from "../hooks/queries";
import { purposeText } from "../utils/format";
import { fmtDate, fmtDuration, fmtRange, phaseOf } from "../utils/time";

export function MyBookingsPage() {
  const [includePast, setIncludePast] = useState(false);
  const { data: bookings = [], isLoading, error } = useMyBookings(includePast);
  const now = useNow(30_000);

  const [draft, setDraft] = useState<BookingDraft | null>(null);
  const [editing, setEditing] = useState<Booking | null>(null);
  const [selected, setSelected] = useState<Booking | null>(null);

  const list = includePast ? [...bookings].reverse() : bookings;

  return (
    <Stack spacing={2}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={2}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" } }}
      >
        <Box>
          <Typography variant="h5">My bookings</Typography>
          <Typography color="text.secondary">
            Check in, extend, end early, edit or cancel your bookings.
          </Typography>
        </Box>
        <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
          <FormControlLabel
            control={
              <Switch checked={includePast} onChange={(e) => setIncludePast(e.target.checked)} />
            }
            label="Show history"
          />
          <Button
            variant="outlined"
            startIcon={<DownloadIcon />}
            href={api.downloadBookingsCsv()}
            download="bookings.csv"
          >
            Export CSV
          </Button>
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDraft({})}>
            New booking
          </Button>
        </Stack>
      </Stack>

      {error && <Alert severity="error">{(error as Error).message}</Alert>}
      {isLoading && <Skeleton variant="rounded" height={120} />}
      {!isLoading && list.length === 0 && (
        <Alert severity="info">You have no {includePast ? "" : "upcoming "}bookings.</Alert>
      )}

      <Box
        sx={{
          display: "grid",
          gap: 2,
          gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", lg: "repeat(3, 1fr)" },
        }}
      >
        {list.map((b) => (
          <Card key={b.id}>
            <CardActionArea onClick={() => setSelected(b)}>
              <CardContent>
                <Stack
                  direction="row"
                  sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}
                >
                  <Typography variant="overline" color="text.secondary">
                    {b.cabinName}
                  </Typography>
                  <PhaseChip phase={phaseOf(b, now)} />
                </Stack>
                <Typography variant="h6" noWrap>
                  {b.title}
                </Typography>
                <Typography variant="body2">
                  {fmtDate(b.startTime)} · <b>{fmtRange(b)}</b>
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {fmtDuration(b.durationMinutes)} · {purposeText(b)}
                </Typography>
              </CardContent>
            </CardActionArea>
          </Card>
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
