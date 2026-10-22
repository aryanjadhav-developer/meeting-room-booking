import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Alert,
  Autocomplete,
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
  IconButton,
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
  ToggleButton,
  ToggleButtonGroup,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import DownloadIcon from "@mui/icons-material/Download";
import EditIcon from "@mui/icons-material/Edit";
import SaveIcon from "@mui/icons-material/Save";
import { api, tokenStore } from "../api/client";
import type { Cabin, Project, UpsertCabin, User, UserRole, UpdateSettingsRequest } from "../api/types";
import { useAuth } from "../auth/AuthContext";
import { useNotify } from "../components/Notifier";
import { useCabins } from "../hooks/queries";
import { capacityText } from "../utils/format";
import { office } from "../utils/time";

export function AdminPage() {
  const [tab, setTab] = useState<"reports" | "cabins" | "users" | "projects" | "audit" | "settings">("reports");
  return (
    <Stack spacing={2}>
      <Typography variant="h5">Admin</Typography>
      <Tabs value={tab} onChange={(_, v) => setTab(v)}>
        <Tab value="reports" label="Utilisation" />
        <Tab value="cabins" label="Cabins" />
        <Tab value="users" label="Users" />
        <Tab value="projects" label="Projects" />
        <Tab value="audit" label="Audit Log" />
        <Tab value="settings" label="Settings" />
      </Tabs>
      {tab === "reports" && <ReportsTab />}
      {tab === "cabins" && <CabinsTab />}
      {tab === "users" && <UsersTab />}
      {tab === "projects" && <ProjectsTab />}
      {tab === "audit" && <AuditTab />}
      {tab === "settings" && <SettingsTab />}
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
      <Stack direction="row" spacing={2} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
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
        <Button
          variant="outlined"
          startIcon={<DownloadIcon />}
          onClick={async () => {
            const url = api.downloadUtilizationCsv(from, to);
            const token = tokenStore.get();
            const res = await fetch(url, { headers: token ? { Authorization: `Bearer ${token}` } : {} });
            if (!res.ok) return;
            const blob = await res.blob();
            const a = document.createElement("a");
            a.href = URL.createObjectURL(blob);
            a.download = `utilization_${from}_${to}.csv`;
            a.click();
            URL.revokeObjectURL(a.href);
          }}
        >
          Export CSV
        </Button>
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
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: cabins = [] } = useCabins(true);
  const [editing, setEditing] = useState<Cabin | "new" | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<Cabin | null>(null);

  const remove = useMutation({
    mutationFn: (id: number) => api.deleteCabin(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["cabins"] });
      notify("Room deleted");
      setConfirmDelete(null);
    },
    onError: (e) => { notify((e as Error).message, "error"); setConfirmDelete(null); },
  });

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
              <TableCell align="right">Actions</TableCell>
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
                  <Tooltip title="Edit">
                    <IconButton size="small" onClick={() => setEditing(c)}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <IconButton size="small" color="error" onClick={() => setConfirmDelete(c)}><DeleteIcon fontSize="small" /></IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <CabinDialog cabin={editing} onClose={() => setEditing(null)} />
      <Dialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Delete room?</DialogTitle>
        <DialogContent>
          <Typography>
            Delete <strong>{confirmDelete?.name}</strong>? This cannot be undone.
            Rooms with existing bookings cannot be deleted — disable them instead.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmDelete(null)}>Cancel</Button>
          <Button color="error" variant="contained" disabled={remove.isPending}
            onClick={() => confirmDelete && remove.mutate(confirmDelete.id)}>
            {remove.isPending ? "Deleting…" : "Delete"}
          </Button>
        </DialogActions>
      </Dialog>
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
  const [userSearch, setUserSearch] = useState("");
  const { data: users = [] } = useQuery({ queryKey: ["users"], queryFn: api.users });
  const filteredUsers = userSearch.trim()
    ? users.filter(
        (u) =>
          u.name.toLowerCase().includes(userSearch.toLowerCase()) ||
          u.email.toLowerCase().includes(userSearch.toLowerCase())
      )
    : users;

  const update = useMutation({
    mutationFn: (args: { id: string; role: UserRole; isActive: boolean }) =>
      api.updateUser(args.id, args.role, args.isActive),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["users"] }); notify("User updated"); },
    onError: (e) => notify((e as Error).message, "error"),
  });

  // --- add ---
  const [adding, setAdding] = useState(false);
  const [form, setForm] = useState({ name: "", email: "", password: "", role: "Employee" as UserRole });
  const create = useMutation({
    mutationFn: () => api.createUser(form),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
      qc.invalidateQueries({ queryKey: ["directory"] });
      notify("Employee added");
      setAdding(false);
      setForm({ name: "", email: "", password: "", role: "Employee" });
    },
    onError: (e) => notify((e as Error).message, "error"),
  });

  // --- edit ---
  const [editUser, setEditUser] = useState<User | null>(null);
  const [editForm, setEditForm] = useState({ name: "", role: "Employee" as UserRole, isActive: true });
  const openEdit = (u: User) => { setEditUser(u); setEditForm({ name: u.name, role: u.role, isActive: u.isActive }); };
  const saveEdit = useMutation({
    mutationFn: () => {
      if (!editUser) throw new Error("No user");
      return api.updateUser(editUser.id, editForm.role, editForm.isActive);
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["users"] }); notify("User updated"); setEditUser(null); },
    onError: (e) => notify((e as Error).message, "error"),
  });

  // --- delete ---
  const [confirmDelete, setConfirmDelete] = useState<User | null>(null);
  const remove = useMutation({
    mutationFn: (id: string) => api.deleteUser(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["users"] });
      qc.invalidateQueries({ queryKey: ["directory"] });
      notify("User deleted");
      setConfirmDelete(null);
    },
    onError: (e) => { notify((e as Error).message, "error"); setConfirmDelete(null); },
  });

  return (
    <Stack spacing={2}>
      <Stack direction="row" spacing={2} sx={{ alignItems: "center", justifyContent: "space-between" }}>
        <TextField
          size="small"
          placeholder="Search by name or email…"
          value={userSearch}
          onChange={(e) => setUserSearch(e.target.value)}
          sx={{ minWidth: 240 }}
        />
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAdding(true)}>
          Add employee
        </Button>
      </Stack>

      {/* Add dialog */}
      <Dialog open={adding} onClose={() => setAdding(false)} fullWidth maxWidth="xs">
        <DialogTitle>Add employee</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <TextField label="E-mail" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <TextField
              label="Temporary password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              helperText="At least 8 characters. Share it with the employee."
            />
            <TextField select label="Role" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value as UserRole })}>
              <MenuItem value="Employee">Employee</MenuItem>
              <MenuItem value="Admin">Admin</MenuItem>
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAdding(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => create.mutate()} disabled={create.isPending}>
            {create.isPending ? "Adding…" : "Add"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Edit dialog */}
      <Dialog open={!!editUser} onClose={() => setEditUser(null)} fullWidth maxWidth="xs">
        <DialogTitle>Edit user</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Name" value={editUser?.name ?? ""} disabled helperText="Contact admin to change name" />
            <TextField label="E-mail" value={editUser?.email ?? ""} disabled />
            <TextField select label="Role" value={editForm.role}
              disabled={editUser?.id === me?.id}
              onChange={(e) => setEditForm({ ...editForm, role: e.target.value as UserRole })}>
              <MenuItem value="Employee">Employee</MenuItem>
              <MenuItem value="Admin">Admin</MenuItem>
            </TextField>
            <FormControlLabel
              control={
                <Switch checked={editForm.isActive}
                  disabled={editUser?.id === me?.id}
                  onChange={(e) => setEditForm({ ...editForm, isActive: e.target.checked })} />
              }
              label={editForm.isActive ? "Active" : "Deactivated"}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditUser(null)}>Cancel</Button>
          <Button variant="contained" onClick={() => saveEdit.mutate()} disabled={saveEdit.isPending}>
            {saveEdit.isPending ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={!!confirmDelete} onClose={() => setConfirmDelete(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Delete user?</DialogTitle>
        <DialogContent>
          <Typography>
            Permanently delete <strong>{confirmDelete?.name}</strong> ({confirmDelete?.email})?
            Their bookings will remain in the system.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmDelete(null)}>Cancel</Button>
          <Button color="error" variant="contained" disabled={remove.isPending}
            onClick={() => confirmDelete && remove.mutate(confirmDelete.id)}>
            {remove.isPending ? "Deleting…" : "Delete"}
          </Button>
        </DialogActions>
      </Dialog>

      <TableContainer component={Paper} variant="outlined">
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>E-mail</TableCell>
              <TableCell>Role</TableCell>
              <TableCell>Active</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filteredUsers.map((u) => (
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
                      update.mutate({ id: u.id, role: e.target.value as UserRole, isActive: u.isActive })
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
                <TableCell align="right">
                  <Tooltip title="Edit">
                    <IconButton size="small" onClick={() => openEdit(u)}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                  <Tooltip title="Delete">
                    <span>
                      <IconButton size="small" color="error" disabled={u.id === me?.id}
                        onClick={() => setConfirmDelete(u)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Stack>
  );
}

// ------------------------------------------------------------------ projects

function ProjectsTab() {
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: projects = [] } = useQuery({ queryKey: ["projects"], queryFn: api.projects });
  const { data: people = [] } = useQuery({ queryKey: ["users"], queryFn: api.users });
  const [editing, setEditing] = useState<Project | "new" | null>(null);
  const [name, setName] = useState("");
  const [members, setMembers] = useState<User[]>([]);

  const open = (p: Project | "new") => {
    setEditing(p);
    setName(p === "new" ? "" : p.name);
    setMembers(p === "new" ? [] : people.filter((u) => p.memberUserIds.includes(u.id)));
  };

  const done = (msg: string) => {
    qc.invalidateQueries({ queryKey: ["projects"] });
    notify(msg);
    setEditing(null);
  };
  const fail = (e: unknown) => notify((e as Error).message, "error");
  const save = useMutation({
    mutationFn: () => {
      const body = { name, memberUserIds: members.map((m) => m.id) };
      return editing === "new" ? api.createProject(body) : api.updateProject((editing as Project).id, body);
    },
    onSuccess: () => done("Project saved"),
    onError: fail,
  });
  const remove = useMutation({
    mutationFn: (id: number) => api.deleteProject(id),
    onSuccess: () => done("Project deleted"),
    onError: fail,
  });

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => open("new")}>
          Add project
        </Button>
      </Box>
      <TableContainer component={Paper} variant="outlined">
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Project</TableCell>
              <TableCell>Members</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {projects.map((p) => (
              <TableRow key={p.id}>
                <TableCell>{p.name}</TableCell>
                <TableCell>
                  {p.memberUserIds.length === 0
                    ? "—"
                    : people
                        .filter((u) => p.memberUserIds.includes(u.id))
                        .map((u) => u.name)
                        .join(", ")}
                </TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => open(p)}>
                    Edit
                  </Button>
                  <Button
                    size="small"
                    color="error"
                    onClick={() => window.confirm(`Delete project "${p.name}"?`) && remove.mutate(p.id)}
                  >
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Dialog open={editing !== null} onClose={() => setEditing(null)} fullWidth maxWidth="sm">
        <DialogTitle>{editing === "new" ? "Add project" : "Edit project"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Project name" value={name} onChange={(e) => setName(e.target.value)} />
            <Autocomplete
              multiple
              options={people}
              value={members}
              getOptionLabel={(u) => `${u.name} (${u.email})`}
              isOptionEqualToValue={(a, b) => a.id === b.id}
              onChange={(_, v) => setMembers(v)}
              renderInput={(params) => <TextField {...params} label="Members" />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditing(null)}>Cancel</Button>
          <Button variant="contained" onClick={() => save.mutate()} disabled={save.isPending}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
}

// ------------------------------------------------------------------ audit

function AuditTab() {
  const [take, setTake] = useState(50);
  const { data: entries, error, isLoading } = useQuery({
    queryKey: ["auditLog", take],
    queryFn: () => api.auditLog(take),
  });

  return (
    <Stack spacing={2}>
      {error && <Alert severity="error">{(error as Error).message}</Alert>}
      {isLoading && <LinearProgress />}
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Time</TableCell>
              <TableCell>User</TableCell>
              <TableCell>Action</TableCell>
              <TableCell>Entity</TableCell>
              <TableCell>Details</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {entries?.map((e) => (
              <TableRow key={e.id}>
                <TableCell sx={{ whiteSpace: "nowrap" }}>
                  {new Date(e.timestamp).toLocaleString()}
                </TableCell>
                <TableCell>{e.userName}</TableCell>
                <TableCell>
                  <Chip
                    size="small"
                    label={e.action}
                    color={
                      e.action.includes("Cancel")
                        ? "error"
                        : e.action.includes("Create")
                        ? "success"
                        : "primary"
                    }
                  />
                </TableCell>
                <TableCell>{e.entityType}</TableCell>
                <TableCell>{e.details}</TableCell>
              </TableRow>
            ))}
            {entries?.length === 0 && (
              <TableRow>
                <TableCell colSpan={5} align="center" sx={{ py: 3 }}>
                  No audit entries recorded yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {(entries?.length ?? 0) >= take && (
        <Box sx={{ display: "flex", justifyContent: "center" }}>
          <Button variant="outlined" onClick={() => setTake((t) => t + 50)}>
            Load more
          </Button>
        </Box>
      )}
    </Stack>
  );
}

// ------------------------------------------------------------------ settings

const DAY_NAMES = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

function SettingsTab() {
  const qc = useQueryClient();
  const notify = useNotify();
  const { data: rules, isLoading } = useQuery({
    queryKey: ["rules"],
    queryFn: api.rules,
  });

  const [officeStart, setOfficeStart] = useState("");
  const [officeEnd, setOfficeEnd] = useState("");
  const [slotMinutes, setSlotMinutes] = useState(15);
  const [minDuration, setMinDuration] = useState(15);
  const [maxDuration, setMaxDuration] = useState(240);
  const [maxDaysAhead, setMaxDaysAhead] = useState(30);
  const [checkInGrace, setCheckInGrace] = useState(15);
  const [workingDays, setWorkingDays] = useState<string[]>([]);
  const [autoRelease, setAutoRelease] = useState(false);
  const [purposes, setPurposes] = useState<string[]>([]);
  const [newPurpose, setNewPurpose] = useState("");

  useEffect(() => {
    if (rules) {
      setOfficeStart(rules.officeStart.substring(0, 5));
      setOfficeEnd(rules.officeEnd.substring(0, 5));
      setSlotMinutes(rules.slotMinutes);
      setMinDuration(rules.minDurationMinutes);
      setMaxDuration(rules.maxDurationMinutes);
      setMaxDaysAhead(rules.maxDaysAhead);
      setCheckInGrace(rules.checkInGraceMinutes);
      setWorkingDays(rules.workingDays);
      setAutoRelease(rules.autoReleaseEnabled);
      setPurposes(rules.purposes ?? []);
    }
  }, [rules]);

  const update = useMutation({
    mutationFn: (req: UpdateSettingsRequest) => api.updateRules(req),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["rules"] });
      notify("Settings updated successfully.", "success");
    },
    onError: (err) => notify((err as Error).message, "error"),
  });

  const handleSave = () => {
    if (!rules) return;
    if (!/^\d{2}:\d{2}$/.test(officeStart) || !/^\d{2}:\d{2}$/.test(officeEnd)) {
      notify("Office start and end times must be in HH:MM format.", "error");
      return;
    }
    if (workingDays.length === 0) {
      notify("At least one working day must be selected.", "error");
      return;
    }
    const req: UpdateSettingsRequest = {
      officeStart: `${officeStart}:00`,
      officeEnd: `${officeEnd}:00`,
      workingDays,
      slotMinutes: Number(slotMinutes),
      minDurationMinutes: Number(minDuration),
      maxDurationMinutes: Number(maxDuration),
      maxDaysAhead: Number(maxDaysAhead),
      checkInOpensMinutesBefore: rules.checkInOpensMinutesBefore,
      autoReleaseEnabled: autoRelease,
      checkInGraceMinutes: Number(checkInGrace),
      projects: rules.projects,
      purposes,
    };
    update.mutate(req);
  };

  if (isLoading) return <LinearProgress />;

  return (
    <Card sx={{ maxWidth: 650 }}>
      <CardContent>
        <Stack spacing={3}>
          <Typography variant="h6">Booking & Office Rules</Typography>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Office Start Time"
              type="time"
              value={officeStart}
              onChange={(e) => setOfficeStart(e.target.value)}
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Office End Time"
              type="time"
              value={officeEnd}
              onChange={(e) => setOfficeEnd(e.target.value)}
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>
          <Box>
            <Typography variant="body2" color="text.secondary" gutterBottom>
              Working Days
            </Typography>
            <ToggleButtonGroup
              value={workingDays}
              onChange={(_, v: string[]) => { if (v.length > 0) setWorkingDays(v); }}
              size="small"
              sx={{ flexWrap: "wrap", gap: 0.5 }}
            >
              {DAY_NAMES.map((d) => (
                <ToggleButton key={d} value={d} sx={{ px: 1.5 }}>
                  {d.slice(0, 3)}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Slot Duration (mins)"
              type="number"
              value={slotMinutes}
              onChange={(e) => setSlotMinutes(Number(e.target.value))}
              fullWidth
            />
            <TextField
              label="Min Duration (mins)"
              type="number"
              value={minDuration}
              onChange={(e) => setMinDuration(Number(e.target.value))}
              fullWidth
            />
            <TextField
              label="Max Duration (mins)"
              type="number"
              value={maxDuration}
              onChange={(e) => setMaxDuration(Number(e.target.value))}
              fullWidth
            />
          </Stack>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <TextField
              label="Max Days Ahead to Book"
              type="number"
              value={maxDaysAhead}
              onChange={(e) => setMaxDaysAhead(Number(e.target.value))}
              fullWidth
            />
            <TextField
              label="Check-in Grace Period (mins)"
              type="number"
              value={checkInGrace}
              onChange={(e) => setCheckInGrace(Number(e.target.value))}
              fullWidth
            />
          </Stack>
          <FormControlLabel
            control={
              <Switch
                checked={autoRelease}
                onChange={(e) => setAutoRelease(e.target.checked)}
              />
            }
            label="Auto-release no-show bookings"
          />
          <Alert severity="info">Projects and their members are managed in the Projects tab.</Alert>

          <Box>
            <Typography variant="subtitle2" gutterBottom>Purpose Options</Typography>
            <Stack spacing={1}>
              <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 1 }}>
                {purposes.map((p) => (
                  <Chip
                    key={p}
                    label={p}
                    onDelete={() => setPurposes(purposes.filter((x) => x !== p))}
                    size="small"
                  />
                ))}
              </Stack>
              <Stack direction="row" spacing={1}>
                <TextField
                  size="small"
                  placeholder="Add purpose…"
                  value={newPurpose}
                  onChange={(e) => setNewPurpose(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      const v = newPurpose.trim();
                      if (v && !purposes.includes(v)) setPurposes([...purposes, v]);
                      setNewPurpose("");
                    }
                  }}
                  slotProps={{ htmlInput: { maxLength: 50 } }}
                />
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={<AddIcon />}
                  onClick={() => {
                    const v = newPurpose.trim();
                    if (v && !purposes.includes(v)) setPurposes([...purposes, v]);
                    setNewPurpose("");
                  }}
                >
                  Add
                </Button>
              </Stack>
            </Stack>
          </Box>

          <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="contained"
              startIcon={<SaveIcon />}
              onClick={handleSave}
              disabled={update.isPending}
            >
              {update.isPending ? "Saving…" : "Save Settings"}
            </Button>
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );
}
