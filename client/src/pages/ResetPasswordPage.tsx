import { useState, type FormEvent } from "react";
import { Link as RouterLink, useNavigate, useSearchParams } from "react-router-dom";
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
import VpnKeyIcon from "@mui/icons-material/VpnKey";
import { api } from "../api/client";

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const emailParam = params.get("email") ?? "";
  const tokenParam = params.get("token") ?? "";

  const [email, setEmail] = useState(emailParam);
  const [token, setToken] = useState(tokenParam);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);

    if (newPassword !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    if (newPassword.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }

    setBusy(true);
    try {
      const res = await api.resetPassword(email, token, newPassword);
      setSuccess(res.message);
      setTimeout(() => navigate("/"), 2500);
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
      <Card sx={{ width: "100%", maxWidth: 420 }}>
        <CardContent sx={{ p: 4 }}>
          <Stack spacing={1} sx={{ alignItems: "center", mb: 3 }}>
            <VpnKeyIcon color="primary" sx={{ fontSize: 40 }} />
            <Typography variant="h5">Reset Password</Typography>
            <Typography variant="body2" color="text.secondary" align="center">
              Choose a new secure password for your account.
            </Typography>
          </Stack>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          {success && (
            <Alert severity="success" sx={{ mb: 2 }}>
              {success} Redirecting to sign in…
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
            <TextField
              label="Reset Token"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              required
              placeholder="Paste token from email"
            />
            <TextField
              label="New Password"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              helperText="At least 8 characters"
              autoComplete="new-password"
            />
            <TextField
              label="Confirm New Password"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
              autoComplete="new-password"
            />
            <Button type="submit" variant="contained" size="large" disabled={busy || !!success}>
              {busy ? "Resetting…" : "Set New Password"}
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
