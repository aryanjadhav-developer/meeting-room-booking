import { createTheme } from "@mui/material/styles";

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: "media" },
  colorSchemes: {
    light: { palette: { primary: { main: "#3949ab" }, background: { default: "#f5f6fa" } } },
    dark: {
      palette: {
        primary: { main: "#8c9eff" },
        background: { default: "#121318", paper: "#1b1d24" },
      },
    },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", system-ui, -apple-system, sans-serif',
    h5: { fontWeight: 700 },
    h6: { fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600 },
  },
  components: {
    MuiCard: { defaultProps: { variant: "outlined" } },
    MuiPaper: { styleOverrides: { root: { backgroundImage: "none" } } },
  },
});
