import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Stack,
  Switch,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { api } from "../api/client";
import type { Cabin, UpsertCabin, UserRole } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { useNotify } from "../components/Notifier";
import { useCabins } from "../hooks/queries";
import { capacityText } from "../utils/format";
import { office } from "../utils/time";

export function AdminPage() {
  const [tab, setTab] = useState<"reports" | "cabins" | "users">("reports");
  return (
    <Stack spacing={2}>
      <Typography variant="h5">Admin</Typography>
      <Tabs value={tab} onChange={(_, v) => setTab(v)}>
        <Tab value="reports" label="Utilisation" />
        <Tab value="cabins" label="Cabins" />
        <Tab value="users" label="Users" />
      </Tabs>
      {tab === "reports" && <ReportsTab />}
      {tab === "cabins" && <CabinsTab />}
      {tab === "users" && <UsersTab />}
    </Stack>
  );
}

// ------------------------------------------------------------------ reports

function ReportsTab() {
  const [from, setFrom] = useState(office().subtract(29, "day").format("YYYY-MM-DD"));
  const [to, setTo] = useState(office().format("YYYY-MM-DD"));
  const { data, error, isLoading } = useQuery({
    queryKey: ["report", from, to],
    queryFn: () => api.utilization(from, to),
  });
  const maxHourly = Math.max(1, ...(data?.hourlyLoad.map((h) => h.bookingCount) ?? [1]));

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap" }}>
        <TextField
          type="date"
          size="small"
          label="From"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <TextField
          type="date"
          size="small"
          label="To"
          value={to}
          onChange={(e) => setTo(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
      </Stack>
      {error && <Alert severity="error">{(error as Error).message}</Alert>}
      {isLoading && <LinearProgress />}

      {data && (
        <>
          <Box
            sx={{
              display: "grid",
              gap: 2,
              gridTemplateColumns: { xs: "1fr", md: "repeat(3, 1fr)" },
            }}
          >
            {data.cabins.map((c) => (
              <Card key={c.cabinId}>
                <CardContent>
                  <Typography variant="subtitle1">{c.cabinName}</Typography>
                  <Typography variant="h4" sx={{ fontWeight: 700, my: 1 }}>
                    {c.utilizationPercent}%
                  </Typography>
                  <LinearProgress
                    variant="determinate"
                    value={Math.min(100, c.utilizationPercent)}
                    sx={{ height: 8, borderRadius: 4, mb: 1.5 }}
                  />
                  <Typography variant="body2" color="text.secondary">
                    {c.bookedHours} h booked of {c.availableHours} h · {c.bookingCount} bookings
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    {c.cancelledCount} cancelled · {c.noShowCount} no-shows
                  </Typography>
                </CardContent>
              </Card>
            ))}
          </Box>

          <Box sx={{ display: "grid", gap: 2, gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" } }}>
            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle1" sx={{ mb: 2 }}>
                Busiest start times
              </Typography>
              <Stack spacing={0.75}>
                {data.hourlyLoad.map((h) => (
                  <Stack key={h.hour} direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
                    <Typography
                      variant="caption"
                      sx={{ width: 40, fontVariantNumeric: "tabular-nums" }}
                    >
                      {String(h.hour).padStart(2, "0")}:00
                    </Typography>
                    <Tooltip title={`${h.bookingCount} bookings`}>
                      <Box
                        sx={{
                          flex: 1,
                          height: 14,
                          bgcolor: "action.hover",
                          borderRadius: 1,
                          overflow: "hidden",
                        }}
                      >
                        <Box
                          sx={{
                            height: "100%",
                            width: `${(h.bookingCount / maxHourly) * 100}%`,
                            bgcolor: "primary.main",
                            borderRadius: 1,
                          }}
                        />
                      </Box>
                    </Tooltip>
                    <Typography variant="caption" sx={{ width: 24, textAlign: "right" }}>
                      {h.bookingCount}
                    </Typography>
                  </Stack>
                ))}
              </Stack>
            </Paper>

            <Paper variant="outlined" sx={{ p: 2 }}>
              <Typography variant="subtitle1" sx={{ mb: 1 }}>
                Top bookers
              </Typography>
              {data.topUsers.length === 0 ? (
                <Typography color="text.secondary">No bookings in this range.</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Name</TableCell>
                      <TableCell align="right">Bookings</TableCell>
                      <TableCell align="right">Hours</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.topUsers.map((u) => (
                      <TableRow key={u.userId}>
                        <TableCell>
                          {u.name}
                          <Typography
                            variant="caption"
                            color="text.secondary"
                            sx={{ display: "block" }}
                          >
                            {u.email}
                          </Typography>
                        </TableCell>
                        <TableCell align="right">{u.bookingCount}</TableCell>
                        <TableCell align="right">{u.bookedHours}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Paper>
          </Box>
        </>
      )}
    </Stack>
  );
}

// ------------------------------------------------------------------ cabins

const EMPTY_CABIN: UpsertCabin = {
  name: "",
  description: "",
  capacity: 4,
  capacityLabel: "",
  amenities: [],
  color: "#3949ab",
  isActive: true,
};

function CabinsTab() {
  const { data: cabins = [] } = useCabins(true);
  const [editing, setEditing] = useState<Cabin | "new" | null>(null);

  return (
    <Stack spacing={2}>
      <Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing("new")}>
          Add cabin
        </Button>
      </Box>
      <TableContainer component={Paper} variant="outlined">
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Capacity</TableCell>
              <TableCell>Amenities</TableCell>
              <TableCell>Status</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {cabins.map((c) => (
              <TableRow key={c.id}>
                <TableCell>
                  <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
                    <Box sx={{ width: 12, height: 12, borderRadius: "50%", bgcolor: c.color }} />
                    <Box>
                      {c.name}
                      <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ display: "block" }}
                      >
                        {c.description}
                      </Typography>
                    </Box>
                  </Stack>
                </TableCell>
                <TableCell>{capacityText(c)}</TableCell>
                <TableCell>{c.amenities.join(", ")}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={c.isActive ? "Active" : "Disabled"}
                    color={c.isActive ? "success" : "default"}
                  />
                </TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => setEditing(c)}>
                    Edit
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <CabinDialog cabin={editing} onClose={() => setEditing(null)} />
    </Stack>
  );
}

function CabinDialog({ cabin, onClose }: { cabin: Cabin | "new" | null; onClose(): void }) {
  const qc = useQueryClient();
  const notify = useNotify();
  const [form, setForm] = useState<UpsertCabin>(EMPTY_CABIN);
  const [amenities, setAmenities] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!cabin) return;
    const initial =
      cabin === "new"
        ? EMPTY_CABIN
        : {
            ...cabin,
            description: cabin.description ?? "",
            capacityLabel: cabin.capacityLabel ?? "",
          };
    setForm(initial);
    setAmenities(initial.amenities.join(", "));
    setError(null);
  }, [cabin]);

  const save = useMutation({
    mutationFn: () => {
      const body = {
        ...form,
        amenities: amenities
          .split(",")
          .map((a) => a.trim())
          .filter(Boolean),
      };
      return cabin === "new" ? api.createCabin(body) : api.updateCabin((cabin as Cabin).id, body);
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["cabins"] });
      qc.invalidateQueries({ queryKey: ["cabin-status"] });
      notify("Cabin saved");
      onClose();
    },
    onError: (e) => setError((e as Error).message),
  });

  return (
    <Dialog open={!!cabin} onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>{cabin === "new" ? "Add cabin" : "Edit cabin"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Name"
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            required
          />
          <TextField
            label="Description"
            value={form.description ?? ""}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
          <TextField
            label="Capacity"
            type="number"
            value={form.capacity}
            onChange={(e) => setForm({ ...form, capacity: Number(e.target.value) })}
            slotProps={{ htmlInput: { min: 1, max: 100 } }}
          />
          <TextField
            label="Capacity label (optional)"
            value={form.capacityLabel ?? ""}
            placeholder='e.g. "9+" or "3-4"'
            helperText="Shown instead of the number, e.g. 9+ people"
            onChange={(e) => setForm({ ...form, capacityLabel: e.target.value })}
            slotProps={{ htmlInput: { maxLength: 20 } }}
          />
          <TextField
            label="Amenities (comma separated)"
            value={amenities}
            onChange={(e) => setAmenities(e.target.value)}
            placeholder="TV, Whiteboard, VC"
          />
          <TextField
            label="Colour"
            type="color"
            value={form.color}
            onChange={(e) => setForm({ ...form, color: e.target.value })}
          />
          <FormControlLabel
            control={
              <Switch
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              />
            }
            label="Available for booking"
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={() => save.mutate()} disabled={save.isPending}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ------------------------------------------------------------------ users

function UsersTab() {
  const { user: me } = useAuth();
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: users = [] } = useQuery({ queryKey: ["users"], queryFn: api.users });
  const update = useMutation({
    mutationFn: (args: { id: string; role: UserRole; isActive: boolean }) =>
      api.updateUser(args.id, args.role, args.isActive),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
      notify("User updated");
    },
    onError: (e) => notify((e as Error).message, "error"),
  });

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table>
        <TableHead>
          <TableRow>
            <TableCell>Name</TableCell>
            <TableCell>E-mail</TableCell>
            <TableCell>Role</TableCell>
            <TableCell>Active</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {users.map((u) => (
            <TableRow key={u.id}>
              <TableCell>{u.name}</TableCell>
              <TableCell>{u.email}</TableCell>
              <TableCell>
                <TextField
                  select
                  size="small"
                  value={u.role}
                  disabled={u.id === me?.id}
                  onChange={(e) =>
                    update.mutate({
                      id: u.id,
                      role: e.target.value as UserRole,
                      isActive: u.isActive,
                    })
                  }
                >
                  <MenuItem value="Employee">Employee</MenuItem>
                  <MenuItem value="Admin">Admin</MenuItem>
                </TextField>
              </TableCell>
              <TableCell>
                <Switch
                  checked={u.isActive}
                  disabled={u.id === me?.id}
                  onChange={(e) =>
                    update.mutate({ id: u.id, role: u.role, isActive: e.target.checked })
                  }
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
