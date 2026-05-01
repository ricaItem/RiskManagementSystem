import { useState } from "react";
import { enable2fa, verify2fa } from "../api";

export default function TwoFactorSetupPage() {
  const [qrCodeImage, setQrCodeImage] = useState("");
  const [secret, setSecret] = useState("");
  const [code, setCode] = useState("");
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const setup = async () => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
      setError("Login first.");
      return;
    }
    setError("");
    const data = await enable2fa(token);
    setQrCodeImage(data.qrCodeImage);
    setSecret(data.secret);
  };

  const verify = async () => {
    setError("");
    setMessage("");
    try {
      const token = localStorage.getItem("accessToken");
      await verify2fa(code, null, token);
      setMessage("2FA is now enabled for your account.");
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <main className="container">
      <h1>2FA Setup</h1>
      <div className="panel">
        <button onClick={setup}>Generate QR</button>
        {qrCodeImage && <img src={qrCodeImage} alt="2FA QR" className="qr" />}
        {secret && <p>Manual key: <code>{secret}</code></p>}
        <label>Enter 6-digit code</label>
        <input value={code} onChange={(e) => setCode(e.target.value)} maxLength={6} />
        <button onClick={verify}>Verify setup</button>
        {message && <p className="ok">{message}</p>}
        {error && <p className="error">{error}</p>}
      </div>
    </main>
  );
}
