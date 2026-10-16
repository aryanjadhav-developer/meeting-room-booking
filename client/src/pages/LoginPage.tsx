import { useState, type FormEvent } from "react";
import { Link as RouterLink } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Link,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import MeetingRoomIcon from "@mui/icons-material/MeetingRoom";
import { useAuth } from "../auth/AuthContext";

export function LoginPage() {
  const { login, register, signOutReason } = useAuth();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === "login") await login(email, password);
      else await register(name, email, password);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        bgcolor: "background.default",
        px: 2,
      }}
    >
      <Card sx={{ width: "100%", maxWidth: 400 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={1} sx={{ alignItems: "center", mb: 2 }}>
            <MeetingRoomIcon color="primary" sx={{ fontSize: 40 }} />
            <Typography variant="h5">Cabin Booking</Typography>
            <Typography variant="body2" color="text.secondary">
              Book a cabin for your meeting in seconds
            </Typography>
          </Stack>

          {signOutReason && !error && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              {signOutReason}
            </Alert>
          )}

          <Tabs
            value={mode}
            onChange={(_, v) => {
              setMode(v);
              setError(null);
            }}
            variant="fullWidth"
            sx={{ mb: 2 }}
          >
            <Tab value="login" label="Sign in" />
            <Tab value="register" label="Create account" />
          </Tabs>

          <Stack component="form" spacing={2} onSubmit={submit}>
            {mode === "register" && (
              <TextField
                label="Full name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                autoComplete="name"
              />
            )}
            <TextField
              label="Work e-mail"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
            />
            <TextField
              label="Password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete={mode === "login" ? "current-password" : "new-password"}
              helperText={mode === "register" ? "At least 8 characters" : " "}
            />
            {mode === "login" && (
              <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
                <Link component={RouterLink} to="/forgot-password" variant="body2" underline="hover">
                  Forgot password?
                </Link>
              </Box>
            )}
            {error && <Alert severity="error">{error}</Alert>}
            <Button type="submit" variant="contained" size="large" disabled={busy}>
              {busy ? "Please wait…" : mode === "login" ? "Sign in" : "Create account"}
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
