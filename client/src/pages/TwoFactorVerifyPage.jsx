import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { verify2fa } from "../api";

export default function TwoFactorVerifyPage() {
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [ok, setOk] = useState("");
  const [isVerifying, setIsVerifying] = useState(false);
  const navigate = useNavigate();

  const formatCode = (value) => value.replace(/\D/g, "").slice(0, 6);

  const onVerify = async () => {
    setError("");
    setOk("");
    setIsVerifying(true);
    try {
      const twoFactorToken = sessionStorage.getItem("twoFactorToken");
      const data = await verify2fa(formatCode(code), twoFactorToken);
      localStorage.setItem("accessToken", data.accessToken);
      sessionStorage.removeItem("twoFactorToken");
      setOk("Verification complete. You are now signed in.");
      setTimeout(() => navigate("/2fa/setup"), 700);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsVerifying(false);
    }
  };

  return (
    <main className="container">
      <h1 className="page-title">Two-factor verification</h1>
      <p className="page-subtitle">Open your authenticator app and enter the current 6-digit code.</p>
      <div className="panel">
        <label>Authenticator code</label>
        <input className="code-input" value={code} onChange={(e) => setCode(formatCode(e.target.value))} maxLength={6} inputMode="numeric" autoComplete="one-time-code" />
        <button onClick={onVerify} className="btn-primary" disabled={isVerifying || formatCode(code).length !== 6}>{isVerifying ? "Verifying..." : "Verify code"}</button>
        {ok && <p className="status ok">{ok}</p>}
        {error && <p className="status error">{error}</p>}
      </div>
    </main>
  );
}
