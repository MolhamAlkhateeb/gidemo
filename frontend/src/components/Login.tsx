import { useState } from "react";
import { login, type AppConfig } from "../auth";

interface Props {
  config: AppConfig;
  onSuccess: () => void;
}

export function Login({ config, onSuccess }: Props) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [needsNewPassword, setNeedsNewPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await login(
        config.region,
        config.userPoolClientId,
        username,
        password,
        needsNewPassword ? newPassword : undefined
      );
      if (result === "NEW_PASSWORD_REQUIRED") {
        setNeedsNewPassword(true);
      } else {
        onSuccess();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-full flex items-center justify-center bg-base-200">
      <form onSubmit={submit} className="card bg-base-100 shadow-xl w-96">
        <div className="card-body">
          <h1 className="card-title justify-center text-xl">Sign in</h1>

          <label className="form-control">
            <span className="label-text">Username / email</span>
            <input
              className="input input-bordered"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              required
            />
          </label>

          <label className="form-control">
            <span className="label-text">Password</span>
            <input
              type="password"
              className="input input-bordered"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </label>

          {needsNewPassword && (
            <label className="form-control">
              <span className="label-text">Set a new password</span>
              <input
                type="password"
                className="input input-bordered"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                autoComplete="new-password"
                required
              />
            </label>
          )}

          {error && <div className="alert alert-error text-sm py-2">{error}</div>}

          <button className="btn btn-primary mt-2" disabled={busy}>
            {busy ? "Signing in…" : needsNewPassword ? "Set password & sign in" : "Sign in"}
          </button>
        </div>
      </form>
    </div>
  );
}
