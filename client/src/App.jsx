import { Navigate, Route, Routes } from "react-router-dom";
import LoginPage from "./pages/LoginPage";
import TwoFactorSetupPage from "./pages/TwoFactorSetupPage";
import TwoFactorVerifyPage from "./pages/TwoFactorVerifyPage";

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/2fa/setup" element={<TwoFactorSetupPage />} />
      <Route path="/2fa/verify" element={<TwoFactorVerifyPage />} />
    </Routes>
  );
}
