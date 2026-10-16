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
  TextField,
  Typography,
} from "@mui/material";
import LockResetIcon from "@mui/icons-material/LockReset";
import { api } from "../api/client";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setStatus(null);
    setBusy(true);
    try {
      const res = await api.forgotPassword(email);
      setStatus({ type: "success", message: res.message });
    } catch (err) {
      setStatus({ type: "error", message: (err as Error).message });
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
      <Card sx={{ width: "100%", maxWidth: 420 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={1} sx={{ alignItems: "center", mb: 3 }}>
            <LockResetIcon color="primary" sx={{ fontSize: 40 }} />
            <Typography variant="h5">Forgot Password</Typography>
            <Typography variant="body2" color="text.secondary" align="center">
              Enter your account email to receive a password reset link.
            </Typography>
          </Stack>

          {status && (
            <Alert severity={status.type} sx={{ mb: 2 }}>
              {status.message}
            </Alert>
          )}

          <Stack component="form" spacing={2} onSubmit={submit}>
            <TextField
              label="Work e-mail"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
            />
            <Button type="submit" variant="contained" size="large" disabled={busy}>
              {busy ? "Sending…" : "Send Reset Link"}
            </Button>
            <Box sx={{ textAlign: "center", mt: 1 }}>
              <Link component={RouterLink} to="/" variant="body2" underline="hover">
                Back to Sign in
              </Link>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
