import { useState } from "react";
import { enable2fa, verify2fa } from "../api";

export default function TwoFactorSetupPage() {
  const [qrCodeImage, setQrCodeImage] = useState("");
  const [secret, setSecret] = useState("");
  const [code, setCode] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [isLoadingQr, setIsLoadingQr] = useState(false);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isManageOpen, setIsManageOpen] = useState(false);

  const formatCode = (value) => value.replace(/\D/g, "").slice(0, 6);

  const setup = async () => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
      setError("Login first.");
      return;
    }
    setIsLoadingQr(true);
    setError("");
    try {
      const data = await enable2fa(token);
      setQrCodeImage(data.qrCodeImage);
      setSecret(data.secret);
    } catch (err) {
      setError(err.message);
    } finally {
      setIsLoadingQr(false);
    }
  };

  const verify = async () => {
    setError("");
    setMessage("");
    setIsVerifying(true);
    try {
      const token = localStorage.getItem("accessToken");
      await verify2fa(formatCode(code), null, token);
      setMessage("2FA is now enabled for your account.");
    } catch (err) {
      setError(err.message);
    } finally {
      setIsVerifying(false);
    }
  };

  return (
    <main className="container">
      <h1 className="page-title">Manage two-factor authentication</h1>
      <p className="page-subtitle">Secure your account with an authenticator app. You can manage setup here and verify before activation.</p>
      <div className="panel">
        <h2 className="section-title">Set up authenticator app</h2>
        <p className="section-help">1) Generate a QR code. 2) Scan it with your app. 3) Enter the 6-digit code to confirm.</p>

        <div className="button-row">
          <button onClick={setup} className="btn-primary" disabled={isLoadingQr}>{isLoadingQr ? "Generating..." : "Generate QR code"}</button>
          <button onClick={() => setIsManageOpen(true)} className="btn-secondary">Quick manage help</button>
        </div>

        {qrCodeImage && (
          <div className="qr-wrap">
            <img src={qrCodeImage} alt="2FA QR" className="qr" />
          </div>
        )}

        {secret && (
          <p className="section-help">
            Manual key
            <code className="inline-code">{secret}</code>
          </p>
        )}

        <label>Enter 6-digit code</label>
        <input className="code-input" value={code} onChange={(e) => setCode(formatCode(e.target.value))} maxLength={6} inputMode="numeric" autoComplete="one-time-code" />
        <button onClick={verify} className="btn-primary" disabled={isVerifying || formatCode(code).length !== 6}>{isVerifying ? "Verifying..." : "Verify and enable"}</button>
        {message && <p className="status ok">{message}</p>}
        {error && <p className="status error">{error}</p>}
      </div>

      {isManageOpen && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal">
            <h2>What you can do here</h2>
            <p className="section-help">This area is for security actions related to sign-in and account protection.</p>
            <ul className="security-list">
              <li>Enable 2FA with an authenticator app.</li>
              <li>Verify your setup before activation.</li>
              <li>Use account settings to update email or password.</li>
              <li>Return here later to reconfigure 2FA.</li>
            </ul>
            <div className="button-row">
              <button onClick={() => setIsManageOpen(false)} className="btn-primary">Close</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}
